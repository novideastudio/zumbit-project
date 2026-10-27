using System.Collections.Generic;
using UnityEngine;

public enum Tier { Common, Elite, Boss }

public class EnemyEntry
{
    public string name;
    public Tier tier;
    public Sprite sprite;
    public EnemyData data;
}

public class Roster
{
    public List<EnemyEntry> commons = new List<EnemyEntry>();
    public List<EnemyEntry> elites = new List<EnemyEntry>();
    public EnemyEntry boss;
    public bool HasArt => commons.Count > 0;
}

public static class EnemyRoster
{
    public const float UnitsPerPixel = 0.0075f;
    public static readonly string[] Themes = { "escola", "delegacia", "hospital", "base", "centro" };

    static readonly Dictionary<string, string[]> commonNames = new Dictionary<string, string[]>
    {
        { "escola",    new[] { "Estudante", "Professor", "Limpeza", "Esportista" } },
        { "delegacia", new[] { "Policial", "Detento", "Agente SWAT", "Cão Infectado" } },
        { "hospital",  new[] { "Paciente", "Enfermeira", "Médico", "Camareiro" } },
        { "base",      new[] { "Soldado", "Sniper", "Engenheiro", "Cão Militar" } },
        { "centro",    new[] { "Civil", "Motorista", "Trabalhador", "Motoqueiro" } },
    };

    static readonly Dictionary<string, string[]> eliteNames = new Dictionary<string, string[]>
    {
        { "escola",    new[] { "Líder Estudante", "Professora Mutante", "Zumbi Gordo da Cantina", "Corredor Rápido" } },
        { "delegacia", new[] { "Capitão", "Detento Blindado", "Cão Mutante", "Atirador de Elite" } },
        { "hospital",  new[] { "Paciente Mutante", "Doutor Experimento", "Enfermeira Tóxica", "Brutamonte" } },
        { "base",      new[] { "Soldado Blindado", "Lança-Chamas", "Granadeiro", "Experimento Militar" } },
        { "centro",    new[] { "Demolidor", "Corredor Fúria", "Infectado Espinhoso", "Vomitador" } },
    };

    static readonly Dictionary<string, string> bossNames = new Dictionary<string, string>
    {
        { "escola", "Diretor Mutante" },
        { "delegacia", "Comandante Corrompido" },
        { "hospital", "Paciente Zero" },
        { "base", "Experimento Alfa" },
        { "centro", "Titã da Infecção" },
    };

    // usa assets em Resources/Config/Enemies/{tema}; se nao houver, usa os padroes
    public static Roster Get(string theme)
    {
        if (string.IsNullOrEmpty(theme)) return new Roster();

        // Um roster novo = entradas novas: os zumbis guardados no pool do roster anterior nao servem mais.
        EnemyFactory.ClearPools();

        var assets = Resources.LoadAll<EnemyData>("Config/Enemies/" + theme);
        if (assets.Length > 0)
        {
            System.Array.Sort(assets, (a, b) => string.CompareOrdinal(a.name, b.name));
            var r = new Roster();
            foreach (var d in assets)
            {
                if (d.sprite == null) continue;
                var e = new EnemyEntry { name = d.displayName, tier = d.tier, sprite = d.sprite, data = d };
                if (d.tier == Tier.Common) r.commons.Add(e);
                else if (d.tier == Tier.Elite) r.elites.Add(e);
                else r.boss = e;
            }
            if (r.HasArt) return r;
        }
        return BuildDefault(theme);
    }

    public static Roster BuildDefault(string theme)
    {
        var r = new Roster();
        if (string.IsNullOrEmpty(theme) || !commonNames.ContainsKey(theme)) return r;

        var c = commonNames[theme];
        for (int i = 0; i < c.Length; i++)
        {
            var sp = ArtLoader.Get($"Art/Zombies/{theme}/common_{i + 1}");
            if (sp != null) r.commons.Add(Entry(c[i], Tier.Common, theme, sp));
        }
        var e = eliteNames[theme];
        for (int i = 0; i < e.Length; i++)
        {
            var sp = ArtLoader.Get($"Art/Zombies/{theme}/elite_{i + 1}");
            if (sp != null) r.elites.Add(Entry(e[i], Tier.Elite, theme, sp));
        }
        var bsp = ArtLoader.Get($"Art/Zombies/{theme}/boss");
        if (bsp != null) r.boss = Entry(bossNames[theme], Tier.Boss, theme, bsp);
        return r;
    }

    static EnemyEntry Entry(string n, Tier t, string theme, Sprite sp)
    {
        return new EnemyEntry { name = n, tier = t, sprite = sp, data = MakeData(n, t, theme, sp) };
    }

    static EnemyData MakeData(string n, Tier t, string theme, Sprite sp)
    {
        float hp = 1f, spd = 1f, dmg = 1f;
        string l = n.ToLower();
        // Tipo de comportamento definido AQUI, de forma explicita (mesma prioridade do Enemy: cuspidor > corredor > tanque).
        EnemyKind kind = EnemyKind.Walker;
        if (l.Contains("vomitador") || l.Contains("tóxica") || l.Contains("atirador") || l.Contains("sniper") ||
            l.Contains("lança") || l.Contains("granadeiro")) kind = EnemyKind.Spitter;
        else if (l.Contains("cão") || l.Contains("corredor") || l.Contains("motoqueiro")) kind = EnemyKind.Runner;
        else if (l.Contains("gordo") || l.Contains("brutamonte") || l.Contains("demolidor") ||
                 l.Contains("blindado") || l.Contains("swat") || l.Contains("experimento militar")) kind = EnemyKind.Tank;

        if (l.Contains("cão") || l.Contains("corredor") || l.Contains("motoqueiro")) { hp = 0.7f; spd = 1.9f; }
        else if (l.Contains("gordo") || l.Contains("brutamonte") || l.Contains("demolidor") ||
                 l.Contains("blindado") || l.Contains("swat") || l.Contains("experimento militar"))
        { hp = 1.8f; spd = 0.7f; dmg = 1.3f; }

        var d = ScriptableObject.CreateInstance<EnemyData>();
        d.displayName = n; d.tier = t; d.theme = theme; d.sprite = sp; d.sizeMult = 1f;
        d.kind = t == Tier.Boss ? EnemyKind.Walker : kind;
        switch (t)
        {
            case Tier.Common:
                d.maxHealth = 30f * hp; d.moveSpeed = 1.2f * spd; d.damage = 5f * dmg; d.attackInterval = 1f;
                d.xp = 25; d.gold = 2; d.dropChance = 0.2f; break;
            case Tier.Elite:
                d.maxHealth = 100f * hp; d.moveSpeed = 1.1f * spd; d.damage = 9f * dmg; d.attackInterval = 0.9f;
                d.xp = 100; d.gold = 10; d.dropChance = 0.5f; break;
            default:
                d.maxHealth = 1200f; d.moveSpeed = 0.8f; d.damage = 22f; d.attackInterval = 1.3f;
                d.xp = 500; d.gold = 100; d.dropChance = 1f; break;
        }
        return d;
    }
}

public static class EnemyFactory
{
    // Alturas-alvo em unidades de mundo (o heroi tem ~2). Todo zumbi de um tier fica com a MESMA altura
    // visivel, nao importa o tamanho em pixels do PNG (antes a escala era fixa por pixel e a arte da escola,
    // com mais pixels, ficava ~40% maior que as outras).
    // Valores padrao em GameTuning (Resources/Config/GameTuning.asset), ajustaveis no Inspector.

    // ---- Pool de zumbis: comuns e elites voltam para ca em vez de serem destruidos (chefes nao). ----
    static readonly Dictionary<EnemyEntry, Stack<Enemy>> pools = new Dictionary<EnemyEntry, Stack<Enemy>>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => pools.Clear();

    /// <summary>Destroi os zumbis parados no pool. Chamado quando um roster novo e carregado (nova missao).</summary>
    public static void ClearPools()
    {
        foreach (var kv in pools)
            while (kv.Value.Count > 0)
            {
                var en = kv.Value.Pop();
                if (en != null) Object.Destroy(en.gameObject);
            }
        pools.Clear();
    }

    /// <summary>Devolve um zumbi morto ao pool (chamado pelo proprio Enemy depois do tempo do corpo).</summary>
    public static void Release(Enemy en)
    {
        if (en == null) return;
        var entry = en.poolEntry;
        en.gameObject.SetActive(false);
        if (entry == null) { Object.Destroy(en.gameObject); return; }
        if (!pools.TryGetValue(entry, out var stack)) pools[entry] = stack = new Stack<Enemy>(16);
        stack.Push(en);
    }

    static Enemy TakeFromPool(EnemyEntry e)
    {
        if (!pools.TryGetValue(e, out var stack)) return null;
        while (stack.Count > 0)
        {
            var en = stack.Pop();
            if (en != null) return en;   // ignora os destruidos por troca de cena
        }
        return null;
    }

    public static float TargetHeight(EnemyEntry e)
    {
        var t = GameTuning.Current;
        float h = e.data.targetHeight;   // altura propria do asset do zumbi (0 = usa a do tier)
        if (h <= 0f)
            h = e.tier == Tier.Boss ? t.bossHeight : e.tier == Tier.Elite ? t.eliteHeight : t.commonHeight;
        return h * Mathf.Max(0.1f, e.data.sizeMult) * Mathf.Max(0.1f, t.zombieSizeMultiplier);
    }

    /// <summary>Escala do transform para que a parte visivel do sprite tenha exatamente TargetHeight.</summary>
    public static float ComputeScale(EnemyEntry e)
    {
        return TargetHeight(e) / SpriteMetrics.WorldHeight(e.sprite);
    }

    public static Color ModTint(EliteMod mod)
    {
        switch (mod)
        {
            case EliteMod.Armored: return new Color(0.62f, 0.74f, 1f);
            case EliteMod.Regenerating: return new Color(0.58f, 1f, 0.62f);
            case EliteMod.Explosive: return new Color(1f, 0.66f, 0.28f);
            default: return Color.white;
        }
    }

    /// <summary>X de nascimento fora da tela, a esquerda (zumbis de flanco).</summary>
    public static float LeftSpawnX(Transform hero)
    {
        float fallback = (hero != null ? hero.position.x : -6.5f) - 6f;
        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return fallback;
        float left = cam.transform.position.x - cam.orthographicSize * cam.aspect - 1.6f;
        return Mathf.Min(left, fallback);
    }

    public static EliteMod RandomMod()
    {
        switch (Random.Range(0, 3))
        {
            case 0: return EliteMod.Armored;
            case 1: return EliteMod.Regenerating;
            default: return EliteMod.Explosive;
        }
    }

    public static Enemy Spawn(EnemyEntry e, Vector3 pos, Transform target, EliteMod mod = EliteMod.None)
    {
        float s = ComputeScale(e);
        bool poolable = e.tier != Tier.Boss;   // chefe tem barra de vida propria e aparece uma vez: nao entra no pool

        // ---- reaproveita um zumbi do pool ----
        var pooled = poolable ? TakeFromPool(e) : null;
        if (pooled != null)
        {
            var pgo = pooled.gameObject;   // esta desativado
            pgo.transform.position = pos;
            pgo.transform.localScale = new Vector3(s, s, 1f);
            pooled.ResetForReuse();
            pooled.target = target;
            pooled.mod = mod;
            pooled.stopDistance = 1.1f;
            var pmotion = pgo.GetComponent<PixelCharacterMotion>();
            pmotion.tint = ModTint(mod);
            pmotion.ResetForReuse();
            pgo.SetActive(true);   // OnEnable: registra na lista, recalcula vida e dano recebido
            AlignSpriteFeetToGround(pgo.transform, pos.y);
            return pooled;
        }

        // ---- cria um novo ----
        var go = new GameObject(e.name);
        go.SetActive(false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(s, s, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = e.sprite;
        go.AddComponent<Health>();
        var en = go.AddComponent<Enemy>();
        en.data = e.data;
        en.target = target;
        en.mod = mod;
        en.stopDistance = e.tier == Tier.Boss ? 1.8f : 1.1f;
        if (poolable) en.poolEntry = e;
        go.AddComponent<YSort>();
        var motion = go.AddComponent<PixelCharacterMotion>();
        motion.isEnemy = true;
        motion.tint = ModTint(mod);
        if (e.tier == Tier.Boss) go.AddComponent<BossHealthBar>();
        go.SetActive(true);
        // Align the actual rendered bottom of this individual PNG to the road baseline.
        // Keep this in the factory, not as a required Enemy method, so older Enemy
        // script revisions cannot break compilation of the factory/spawner.
        AlignSpriteFeetToGround(go.transform, pos.y);
        return en;
    }

    static void AlignSpriteFeetToGround(Transform character, float groundY)
    {
        if (character == null) return;
        var renderer = character.GetComponent<SpriteRenderer>();
        if (renderer == null || renderer.sprite == null) return;
        // Usa a base VISIVEL do sprite (alguns PNGs tem margem transparente embaixo).
        float feetY = renderer.bounds.min.y;
        var m = SpriteMetrics.Get(renderer.sprite);
        feetY += m.bottomPx / renderer.sprite.pixelsPerUnit * Mathf.Abs(character.localScale.y);
        Vector3 position = character.position;
        position.y += groundY - feetY;
        character.position = position;
    }
}
