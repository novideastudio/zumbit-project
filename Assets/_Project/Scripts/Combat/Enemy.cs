using System.Collections.Generic;
using UnityEngine;

/// <summary>Modificadores de elite (so no Pesadelo).</summary>
public enum EliteMod { None, Armored, Regenerating, Explosive }

[RequireComponent(typeof(Health))]
public class Enemy : MonoBehaviour
{
    public static readonly List<Enemy> Alive = new();
    public static float HealthMult = 1f, DamageMult = 1f;
    /// <summary>Vida do CHEFE: baseBoss x (1 + 0,12 x (rec-1)) x dificuldade. Calculada pelo MissionSetup.</summary>
    public static float BossHealthMult = 1f;
    public static float SpeedMult = 1f;
    /// <summary>Elites tambem ficam furiosos aos 50% de vida (Dificil/Pesadelo). Chefes sempre ficam.</summary>
    public static bool EliteEnrage;
    public static float DamageDealt;
    /// <summary>Disparado a cada Hit que removeu vida: (inimigo, dano aplicado, dano bruto, critico). Usado pelo medidor de DPS do estande.</summary>
    public static event System.Action<Enemy, float, float, bool> Damaged;
    /// <summary>Furia da horda (HordeSpawner): multiplicadores que crescem com o tempo da missao.</summary>
    public static float FuryDamage = 1f, FurySpeed = 1f;

    // ---- Tipos de zumbi ----
    public const float TankDamageTaken = 0.8f;       // Tank recebe 20% menos dano
    public const float TankKnockResist = 0.5f;       // e resiste a empurrao/atordoamento
    public const int RunnerExtraAttackers = 2;       // Runner bate mesmo alem do limite da fila
    public const float SpitterDamageMult = 0.8f;
    public const float SpitterIntervalMult = 1.6f;

    // ---- Modificadores de elite ----
    public const float ArmoredHealthMult = 1.35f;
    public const float ArmoredDamageTaken = 0.65f;
    public const float RegenPerSecond = 0.025f;      // fracao da vida maxima por segundo
    public const float ExplosionRadius = 2.4f;
    public const float ExplosionDamageMult = 3f;     // x dano do zumbi
    public const float EnrageThreshold = 0.5f;

    // ---- Fila da horda (o heroi fica parado, os zumbis se enfileiram dos dois lados) ----
    /// <summary>Distancia X entre zumbis enfileirados.</summary>
    public static float QueueSpacing = 0.55f;
    /// <summary>Quantos zumbis de cada lado conseguem bater ao mesmo tempo (os de tras esperam a vez).</summary>
    public static int MaxAttackersPerSide = 3;

    public EnemyData data;
    /// <summary>Boneco do estande: nao anda, nao ataca, nao morre de verdade (veja TrainingDummy).</summary>
    [HideInInspector] public bool isTrainingDummy;
    public Transform target;
    public float stopDistance = 0.8f;
    [Tooltip("Definido pelo spawner antes de ativar o objeto.")] public EliteMod mod = EliteMod.None;

    public bool IsDead => dead;
    public bool Enraged => enraged;
    public bool IsBoss => data != null && data.tier == Tier.Boss;

    [Tooltip("Definido pelo spawner (ex.: comum que nasce como cuspidor). Auto = usa o EnemyData / o nome.")]
    public EnemyKind kindOverride = EnemyKind.Auto;
    EnemyKind kind = EnemyKind.Walker;
    static int spitterCounter;   // varia a distancia de cada cuspidor (sem usar GetInstanceID, obsoleto na Unity 6.6)
    bool kindReady;
    public EnemyKind Kind { get { EnsureKind(); return kind; } }

    // Classificacao por nome calculada UMA vez por nome (antes: ToLowerInvariant + varios Contains a cada spawn).
    // Preferivel: preencher EnemyData.kind explicitamente e nem chegar aqui.
    static readonly Dictionary<string, EnemyKind> kindByName = new();

    void EnsureKind()
    {
        if (kindReady) return;
        kindReady = true;
        kind = ResolveKind();
        if (kind == EnemyKind.Tank) damageTakenMult *= TankDamageTaken;
        if (kind == EnemyKind.Spitter)
        {
            // Para a distancia propria (cada um um pouco diferente) e fica fora da fila dos corpo a corpo.
            float range = data != null ? Mathf.Clamp(data.attackRange, 3f, 5.2f) : 4.8f;
            slotDistance = range + (spitterCounter++ & 3) * 0.2f;
            queueRank = 0;
        }
    }

    EnemyKind ResolveKind()
    {
        if (kindOverride != EnemyKind.Auto) return kindOverride;
        if (data == null || IsBoss) return EnemyKind.Walker;
        if (data.kind != EnemyKind.Auto) return data.kind;
        string key = data.displayName ?? string.Empty;
        if (!kindByName.TryGetValue(key, out var k))
        {
            k = ClassifyByName(key.ToLowerInvariant());
            kindByName[key] = k;
        }
        return k;
    }

    static EnemyKind ClassifyByName(string n)
    {
        if (n.Contains("vomitador") || n.Contains("tóxica") || n.Contains("atirador") || n.Contains("sniper") ||
            n.Contains("lança") || n.Contains("granadeiro")) return EnemyKind.Spitter;
        if (n.Contains("cão") || n.Contains("corredor") || n.Contains("motoqueiro")) return EnemyKind.Runner;
        if (n.Contains("gordo") || n.Contains("brutamonte") || n.Contains("demolidor") || n.Contains("blindado") ||
            n.Contains("swat") || n.Contains("experimento militar")) return EnemyKind.Tank;
        return EnemyKind.Walker;
    }

    // Estado dinamico
    bool enraged;
    float speedBoost = 1f, damageBoost = 1f, intervalMult = 1f;
    float stunUntil;
    float damageTakenMult = 1f;

    Health hp;
    SpriteRenderer sr;
    PixelCharacterMotion motion;
    YSort ySort;
    Health targetHealth;
    float nextAttack;
    bool dead;
    bool registered;

    // Dados da fila, recalculados 1x por frame em RebuildQueue().
    float distToTarget;
    float slotDistance;
    int queueRank;

    // Ponto de mira em cache (campo gerenciado): projeteis/granadas leem isso sem chamar sr.bounds.
    Vector3 aimPoint;

    static readonly List<Enemy> leftQueue = new();
    static readonly List<Enemy> rightQueue = new();
    // Vaga/posicao POR ALVO: na dupla/trio, zumbis que perseguem sobreviventes diferentes nao dividem a mesma fila.
    static readonly Dictionary<Transform, float> prevSlotByTarget = new();
    static readonly Dictionary<Transform, int> rankByTarget = new();
    static readonly List<Health> explosionVictims = new();
    static int queueFrame = -1;
    static readonly System.Comparison<Enemy> ByDistance = (a, b) => a.distToTarget.CompareTo(b.distToTarget);

    public Vector3 AimPoint => aimPoint;

    void Awake()
    {
        hp = GetComponent<Health>();
        sr = GetComponent<SpriteRenderer>();
        motion = GetComponent<PixelCharacterMotion>();
        ySort = GetComponent<YSort>();
        if (hp != null) { hp.Died += OnDied; hp.Changed += OnHealthChanged; }
        RefreshAim();
    }

    void OnEnable()
    {
        if (!registered) { Alive.Add(this); registered = true; }
        if (hp == null) hp = GetComponent<Health>();
        damageTakenMult = mod == EliteMod.Armored ? ArmoredDamageTaken : 1f;
        if (data != null && hp != null)
        {
            // Chefe usa a formula propria por local (antes: 1200 fixo x multiplicador generico).
            float mult = IsBoss ? BossHealthMult : HealthMult;
            if (mod == EliteMod.Armored) mult *= ArmoredHealthMult;
            if (isTrainingDummy) mult = 1f;   // vida do boneco e exatamente a do preset
            hp.Init(data.maxHealth * mult);
        }
        RefreshAim();
    }

    void OnHealthChanged(float current, float max)
    {
        if (enraged || dead || max <= 0f || current <= 0f) return;
        if (!(IsBoss || (EliteEnrage && data != null && data.tier == Tier.Elite))) return;
        if (current / max <= EnrageThreshold) Enrage();
    }

    /// <summary>Aos 50% de vida: mais rapido, bate mais forte e mais depressa.</summary>
    void Enrage()
    {
        enraged = true;
        bool boss = IsBoss;
        speedBoost = boss ? 1.35f : 1.25f;
        damageBoost = boss ? 1.3f : 1.2f;
        intervalMult = boss ? 0.75f : 0.85f;
        if (motion == null) motion = GetComponent<PixelCharacterMotion>();
        if (motion != null) motion.tint = new Color(1f, 0.52f, 0.46f);
        Vector3 feet = sr != null ? new Vector3(transform.position.x, sr.bounds.min.y, transform.position.z) : transform.position;
        PixelCombatVFX.Explosion(feet, boss ? 2.2f : 1.2f, true);
    }

    /// <summary>Empurra o zumbi para longe do alvo e o atordoa. Chefes resistem a metade.</summary>
    public void Knockback(float distance, float stunSeconds)
    {
        if (dead || isTrainingDummy) return;
        float resist = IsBoss ? 0.4f : (Kind == EnemyKind.Tank ? TankKnockResist : 1f);
        if (target != null)
        {
            float away = target.position.x < transform.position.x ? 1f : -1f;
            Vector3 p = transform.position;
            p.x += away * distance * resist;
            transform.position = p;
        }
        stunUntil = Mathf.Max(stunUntil, Time.time + stunSeconds * (IsBoss ? 0.5f : (Kind == EnemyKind.Tank ? TankKnockResist : 1f)));
    }

    void OnDisable()
    {
        if (registered) { Alive.Remove(this); registered = false; }
    }

    void RefreshAim()
    {
        if (sr == null || sr.sprite == null) { aimPoint = transform.position; return; }

        if (isTrainingDummy)
        {
            // Hero.Fire e Projectile usam AimPoint; apontar para aqui faz os tiros convergirem
            // para a cabeca do boneco e tambem valida o impacto nesse mesmo ponto.
            var metrics = SpriteMetrics.Get(sr.sprite);
            float scaleY = Mathf.Abs(transform.lossyScale.y);
            float visibleHeight = metrics.heightPx / sr.sprite.pixelsPerUnit * scaleY;
            float visibleBottom = sr.bounds.min.y + metrics.bottomPx / sr.sprite.pixelsPerUnit * scaleY;
            aimPoint = new Vector3(sr.bounds.center.x,
                visibleBottom + visibleHeight * TrainingDummy.HeadAimFraction, transform.position.z);
            return;
        }

        // Mesmo resultado de sr.bounds.center para um sprite sem rotacao, sem custo nativo por consulta.
        Vector3 s = transform.localScale;
        Vector3 c = sr.sprite.bounds.center;
        aimPoint = transform.position + new Vector3(c.x * s.x, c.y * Mathf.Abs(s.y), 0f);
    }

    /// <summary>
    /// Aligns the rendered bottom/feet to a world-space street baseline.
    /// Call after the sprite, scale and SpriteRenderer have been assigned.
    /// </summary>
    public void AlignFeetToGround(float groundY)
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;
        Vector3 p = transform.position;
        p.y += groundY - sr.bounds.min.y;
        transform.position = p;
        RefreshAim();
    }

    // Applies player damage and records only damage actually removed from enemy HP.
    public void Hit(float dmg, bool critical = false)
    {
        if (hp == null) hp = GetComponent<Health>();
        if (hp == null || hp.Current <= 0f) return;

        float before = hp.Current;
        hp.Take(dmg * damageTakenMult);
        float applied = Mathf.Max(0f, before - hp.Current);
        DamageDealt += applied;
        if (applied > 0f)
        {
            FloatingDamageText.Show(AimPoint, applied, critical);
            Damaged?.Invoke(this, applied, dmg, critical);
            if (hp.Current > 0f) PixelCombatVFX.Hit(AimPoint, applied);
        }
    }

    // ------------------------------------------------------------------------------------
    // Fila: ordena os inimigos por distancia ao heroi (cada lado separado) e entrega a cada um
    // uma "vaga" (slotDistance). Antes todos corriam para o MESMO X e ficavam empilhados num
    // unico ponto: todos batiam ao mesmo tempo e o YSort empatava (sprites piscando).
    // ------------------------------------------------------------------------------------
    static void RebuildQueue()
    {
        if (queueFrame == Time.frameCount) return;
        queueFrame = Time.frameCount;

        leftQueue.Clear();
        rightQueue.Clear();
        for (int i = 0; i < Alive.Count; i++)
        {
            Enemy e = Alive[i];
            if (e == null || e.dead || e.target == null) continue;
            e.EnsureKind();
            if (e.kind == EnemyKind.Spitter) continue;   // cuspidor atira de longe: nao ocupa vaga na fila
            float dx = e.target.position.x - e.transform.position.x;
            e.distToTarget = Mathf.Abs(dx);
            if (dx < 0f) rightQueue.Add(e); // inimigo a direita do heroi
            else leftQueue.Add(e);
        }
        AssignSlots(leftQueue);
        AssignSlots(rightQueue);
    }

    static void AssignSlots(List<Enemy> list)
    {
        list.Sort(ByDistance);
        prevSlotByTarget.Clear();
        rankByTarget.Clear();
        for (int i = 0; i < list.Count; i++)
        {
            Enemy e = list[i];
            Transform tg = e.target;
            float previous = prevSlotByTarget.TryGetValue(tg, out var pv) ? pv : float.NegativeInfinity;
            int rank = rankByTarget.TryGetValue(tg, out var rk) ? rk : 0;
            // Respeita o stopDistance proprio (chefes ficam mais longe) e o espaco do da frente (do MESMO alvo).
            float slot = Mathf.Max(e.stopDistance, previous + QueueSpacing);
            e.slotDistance = slot;
            e.queueRank = rank;
            prevSlotByTarget[tg] = slot;
            rankByTarget[tg] = rank + 1;
        }
    }

    bool TargetDown()
    {
        if (targetHealth == null) targetHealth = target.GetComponent<Health>();
        return targetHealth != null && targetHealth.Current <= 0f;
    }

    void Update()
    {
        if (dead) return;
        RefreshAim();
        if (isTrainingDummy) return;   // boneco parado: sem fila, sem ataque
        EnsureKind();
        // Modo dupla: se o alvo caiu, passa a perseguir o sobrevivente vivo mais a frente.
        if (HeroSquad.Count > 0 && (target == null || TargetDown()))
        {
            var next = HeroSquad.FrontTarget();
            if (next != null && next != target) { target = next; targetHealth = null; }
        }
        if (target == null || data == null)
        {
            if (motion != null) motion.SetMoving(false);
            return;
        }

        RebuildQueue();

        if (mod == EliteMod.Regenerating && hp != null) hp.Heal(hp.max * RegenPerSecond * Time.deltaTime);

        // Atordoado (empurrao): fica parado e nao ataca.
        if (Time.time < stunUntil)
        {
            if (motion != null) motion.SetMoving(false);
            return;
        }

        // This is a side-view street fight: enemies must never chase the hero vertically.
        float deltaX = target.position.x - transform.position.x;
        float side = deltaX < 0f ? -1f : 1f;
        float stopX = target.position.x - side * slotDistance;

        Vector3 p = transform.position;
        float remaining = Mathf.Abs(stopX - p.x);
        if (remaining > 0.02f)
        {
            p.x = Mathf.MoveTowards(p.x, stopX, Mathf.Max(0f, data.moveSpeed) * SpeedMult * speedBoost * FurySpeed * Time.deltaTime);
            transform.position = p; // preserve lane Y

            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (deltaX < 0f ? -1f : 1f);
            transform.localScale = scale;
            if (motion != null) motion.SetMoving(true);
        }
        else
        {
            if (motion != null) motion.SetMoving(false);
        }

        // So bate quem esta na frente da fila e ja encostou na sua vaga.
        bool spitter = kind == EnemyKind.Spitter;
        int limit = MaxAttackersPerSide + (kind == EnemyKind.Runner ? RunnerExtraAttackers : 0);
        bool inReach = (spitter || queueRank < limit) && Mathf.Abs(deltaX) <= slotDistance + 0.12f;
        if (inReach && Time.time >= nextAttack)
        {
            float interval = data.attackInterval * intervalMult * (spitter ? SpitterIntervalMult : 1f);
            nextAttack = Time.time + Mathf.Max(0.05f, interval);
            if (targetHealth == null) targetHealth = target.GetComponent<Health>();
            if (targetHealth != null)
            {
                float dmg = data.damage * DamageMult * damageBoost * FuryDamage * (spitter ? SpitterDamageMult : 1f);
                targetHealth.Take(dmg);
                if (spitter) PixelCombatVFX.Hit(target.position + Vector3.up * 1f, 6f);   // cuspe acertou
            }
        }

        // Quem esta mais perto do heroi e desenhado na frente (desempate do YSort).
        if (ySort != null) ySort.sortBias = -Mathf.Min(queueRank, 60);
    }

    void OnDied()
    {
        if (isTrainingDummy) return;   // o TrainingDummy renasce sozinho; nao conta abate nem solta loot
        if (dead) return;
        dead = true;

        // Sai da lista imediatamente: projeteis deixam de acertar o cadaver e a wave termina na hora.
        if (registered) { Alive.Remove(this); registered = false; }
        if (motion != null) motion.SetMoving(false);

        bool boss = data != null && data.tier == Tier.Boss;
        Vector3 feet = sr != null
            ? new Vector3(transform.position.x, sr.bounds.min.y, transform.position.z)
            : transform.position;
        GameEvents.EnemyKilled?.Invoke(this);
        if (mod == EliteMod.Explosive) ExplodeOnDeath(feet);
        PixelCombatVFX.Death(feet, boss);
        ZombitAudio.PlayZombieDeath(transform.position, boss);

        // PixelCharacterMotion anima a queda/fade em ~0.28 s; o objeto some logo depois.
        Destroy(gameObject, 0.35f);
    }

    /// <summary>Explode ao morrer: fere TODOS os sobreviventes proximos (dupla/trio inclusive). Matar de longe evita o dano.</summary>
    void ExplodeOnDeath(Vector3 feet)
    {
        PixelCombatVFX.Explosion(feet, ExplosionRadius);
        if (data == null) return;
        float dmg = data.damage * DamageMult * ExplosionDamageMult;
        float x = transform.position.x;

        // Coleta primeiro e so depois aplica o dano: Take pode derrubar alguem e mexer na lista do esquadrao.
        explosionVictims.Clear();
        if (target != null)
        {
            if (targetHealth == null) targetHealth = target.GetComponent<Health>();
            if (targetHealth != null && Mathf.Abs(target.position.x - x) <= ExplosionRadius) explosionVictims.Add(targetHealth);
        }
        foreach (var m in HeroSquad.Members)
        {
            if (m.health == null || !m.Alive || explosionVictims.Contains(m.health)) continue;
            if (Mathf.Abs(m.health.transform.position.x - x) <= ExplosionRadius) explosionVictims.Add(m.health);
        }
        for (int i = 0; i < explosionVictims.Count; i++) explosionVictims[i].Take(dmg);
        explosionVictims.Clear();
    }

    void OnDestroy()
    {
        if (hp != null) { hp.Died -= OnDied; hp.Changed -= OnHealthChanged; }
    }
}
