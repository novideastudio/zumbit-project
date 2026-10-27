using UnityEngine;
public class Hero : MonoBehaviour
{
    public WeaponData weapon;
    [Tooltip("Opcional: se preenchido, o tiro sai deste Transform (ex.: um filho posicionado na ponta do cano).")]
    public Transform muzzle;
    [Tooltip("Ponta da arma em coordenadas do sprite (x da esquerda, y da base, 0-1). Vem do SurvivorAsset.")]
    public Vector2 muzzleUV = new Vector2(0.97f, 0.36f);
    [Tooltip("Acha a ponta do cano sozinho lendo o PNG (ponto mais a direita do desenho). Se a textura nao for legivel, usa muzzleUV.")]
    public bool autoMuzzle = true;
    [Tooltip("Ajuste fino (em UV do sprite) somado ao ponto automatico. Ex.: x -0.01 recua 1% da largura.")]
    public Vector2 muzzleNudgeUV;

    [HideInInspector] public float damageBonus;
    [HideInInspector] public float damageMult = 1f;
    [HideInInspector] public float fireRateMult = 1f;
    [HideInInspector] public float rangeMult = 1f;
    [HideInInspector] public float accuracyBonus = 0f;
    [HideInInspector] public float critChance = 0f;
    [HideInInspector] public float critMult = 2f;
    // Afixos dos itens equipados (somados pelo PlayerStats).
    [HideInInspector] public float affixDamage, affixCritChance, affixCritDamage, affixFireRate, affixRange, affixAccuracy;

    /// <summary>Alvo marcado pelo jogador (clique). Tem prioridade sobre o mais proximo, se estiver ao alcance.</summary>
    [HideInInspector] public Enemy focusTarget;

    public int Ammo { get; private set; }
    public int MagSize => weapon.magazineSize;
    /// <summary>Progresso da recarga atual (0..1); 1 quando nao esta recarregando.</summary>
    public float ReloadProgress => Reloading && weapon.reloadTime > 0f
        ? Mathf.Clamp01(1f - (reloadEnd - Time.time) / weapon.reloadTime) : 1f;
    public bool Reloading => Time.time < reloadEnd;
    float nextShot, reloadEnd, nextShotSound;
    // Limite de sons de disparo por segundo: MP5-SD/MAC-10 atiram 9-13x/s e roubariam as 12 vozes
    // de audio dos zumbis e explosoes. Acima de ~10/s o ouvido nao distingue os tiros mesmo.
    const float MinShotSoundInterval = 0.09f;
    SpriteRenderer sr;
    PixelCharacterMotion motion;
    Health health;   // null na torreta

    Vector3 Center => sr ? sr.bounds.center : transform.position;

    /// <summary>
    /// Ponto de saida do tiro, preso ao SPRITE do heroi: acompanha escala, espelhamento (flip),
    /// balanco e recuo, entao sempre sai na ponta da arma, qualquer que seja o tamanho do heroi.
    /// </summary>
    public Vector3 MuzzlePosition
    {
        get
        {
            if (muzzle) return muzzle.position;
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null) return transform.position + new Vector3(0.9f, 0.1f, 0f);

            Sprite sp = sr.sprite;
            Rect rect = sp.rect;
            // Posicao do ponto em pixels relativos ao pivot do sprite -> unidades locais -> mundo.
            Vector2 uv = muzzleUV;
            if (autoMuzzle && SpriteMetrics.TryGetMuzzleUV(sp, out Vector2 auto)) uv = auto + muzzleNudgeUV;
            Vector2 px = new Vector2(uv.x * rect.width - sp.pivot.x,
                                     uv.y * rect.height - sp.pivot.y);
            if (sr.flipX) px.x = -px.x;
            if (sr.flipY) px.y = -px.y;
            Vector3 local = new Vector3(px.x, px.y, 0f) / sp.pixelsPerUnit;
            return transform.TransformPoint(local);
        }
    }

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        motion = GetComponent<PixelCharacterMotion>();
        health = GetComponent<Health>();
    }
    void Start() => Ammo = weapon.magazineSize;

    void Update()
    {
        if (health != null && health.Current <= 0f) return;   // caido nao atira (modo dupla)
        if (Reloading) return;
        if (Ammo <= 0) { Ammo = weapon.magazineSize; reloadEnd = Time.time + weapon.reloadTime; return; }
        if (Time.time < nextShot) return;

        var t = FindClosest();
        if (!t) return;

        nextShot = Time.time + 1f / (weapon.shotsPerSecond * fireRateMult * (1f + affixFireRate));
        Ammo--;
        Fire(t);
    }

    Enemy FindClosest()
    {
        float r = weapon.range * rangeMult * (1f + affixRange);
        if (focusTarget != null)
        {
            if (focusTarget.IsDead) focusTarget = null;
            else if (RangeDistanceSqr(focusTarget) <= r * r + 0.0001f) return focusTarget;
        }
        Enemy best = null; float bestD = r * r;
        foreach (var e in Enemy.Alive)
        {
            // No estande, o alcance da regua e horizontal. Ignorar a diferenca entre pivots
            // do sobrevivente e do boneco permite disparar exatamente no limite (ex.: 20,5 m).
            float d = RangeDistanceSqr(e);
            if (d <= bestD) { bestD = d; best = e; }
        }
        return best;
    }

    float RangeDistanceSqr(Enemy target)
    {
        Vector3 delta = target.transform.position - transform.position;
        return target.isTrainingDummy ? delta.x * delta.x : delta.sqrMagnitude;
    }

    void Fire(Enemy t)
    {
        Vector3 origin = MuzzlePosition;
        Vector3 dest = t.AimPoint;
        float acc = Mathf.Clamp01(weapon.accuracy + accuracyBonus + affixAccuracy);
        if (Random.value > acc)
            dest += (Vector3)(Random.insideUnitCircle * Mathf.Max(0.2f, weapon.spread));

        // O bonus fixo (nivel/itens) e normalizado pela cadencia: mesmo ganho de DPS em todas as armas.
        float dmg = (weapon.damage + damageBonus * weapon.BonusScale) * damageMult * (1f + affixDamage);
        bool critical = Random.value < Mathf.Clamp01(critChance + affixCritChance);
        if (critical) dmg *= Mathf.Max(1f, critMult * (1f + affixCritDamage));

        // Projeteis vem de um pool (veja Projectile.Spawn): sem Instantiate/Destroy a cada tiro.
        var p = Projectile.Spawn(weapon.projectilePrefab, origin);
        p.Launch(dest, dmg, critical, weapon.pierceTargets, weapon.pierceFalloff, t);
        // Brief pixel-art muzzle flash, aligned with the shot direction.
        PixelCombatVFX.Muzzle(origin, (dest - origin).normalized);
        // O PixelCharacterMotion.Recoil() existia mas nunca era chamado: agora o heroi "chuta" a cada tiro.
        if (motion == null) motion = GetComponent<PixelCharacterMotion>();
        if (motion != null) motion.Recoil();
        if (Time.time >= nextShotSound)
        {
            nextShotSound = Time.time + MinShotSoundInterval;
            ZombitAudio.PlayGunshot(origin, weapon);
        }
    }

#if UNITY_EDITOR
    // Selecione o heroi durante o Play (ou na cena) para ver a saida do tiro: esfera amarela.
    void OnDrawGizmosSelected()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(MuzzlePosition, 0.08f);
    }
#endif
}