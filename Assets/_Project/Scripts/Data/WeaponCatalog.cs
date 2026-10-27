using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Armas exclusivas de cada sobrevivente, definidas em codigo (mesmo padrao do ItemCatalog).
/// A identidade de combate de cada classe agora vem da ARMA; os multiplicadores do sobrevivente
/// (damageMult, fireRateMult, rangeMult) ficaram em 1 e servem apenas para ajuste fino.
///
/// DPS sustentado (dano x pente / (pente / cadencia + recarga)), sem bonus e com acerto perfeito:
///   M4A1    ~40   equilibrada
///   SCAR-20 ~38   (+ criticos do Sniper ~52) tiros muito fortes, lentos, longo alcance, perfura 2 extras
///   MP5-SD  ~39   rajadas rapidas de dano baixo
///   M249    ~45   pente de 60 tiros, recarga longa, pouco precisa (tiro errado causa menos dano)
///   MAC-10  ~33   a mais rapida e a mais fraca (o Explorer compensa com loot)
/// Com o dano reduzido dos tiros errados (Projectile.MissDamageMult = 0,6) o DPS ESPERADO fica em ~37 nas quatro primeiras e ~29 na MAC-10.
/// </summary>
public static class WeaponCatalog
{
    public const string M4A1 = "m4a1", Scar20 = "scar20", Mp5sd = "mp5sd", M249 = "m249", Mac10 = "mac10";

    public struct Profile
    {
        public string id, displayName, weaponClass;
        public float damage, shotsPerSecond, accuracy, reloadTime, range, spread, soundPitch;
        public int magazineSize;
        public int pierceTargets;       // inimigos EXTRA atravessados (0 = nenhum)
        public float pierceFalloff;     // multiplicador de dano a cada inimigo atravessado
    }

    static Dictionary<string, Profile> profiles;
    static readonly Dictionary<string, WeaponData> previews = new Dictionary<string, WeaponData>();

    static void Build()
    {
        profiles = new Dictionary<string, Profile>();
        Add(new Profile { id = M4A1, displayName = "M4A1", weaponClass = "FUZIL DE ASSALTO",
            damage = 12f, shotsPerSecond = 4f, accuracy = 0.80f, magazineSize = 30, reloadTime = 1.5f, range = 14.5f,
            spread = 0.8f, soundPitch = 1f });
        Add(new Profile { id = Scar20, displayName = "SCAR-20", weaponClass = "FUZIL DE PRECISÃO",
            damage = 40f, shotsPerSecond = 1.1f, accuracy = 0.92f, magazineSize = 15, reloadTime = 2.2f, range = 20.5f,
            spread = 0.5f, soundPitch = 0.78f, pierceTargets = 2, pierceFalloff = 0.5f });
        Add(new Profile { id = Mp5sd, displayName = "MP5-SD", weaponClass = "SUBMETRALHADORA SILENCIADA",
            damage = 6f, shotsPerSecond = 9f, accuracy = 0.82f, magazineSize = 40, reloadTime = 1.6f, range = 10f,
            spread = 0.8f, soundPitch = 1.25f });
        Add(new Profile { id = M249, displayName = "M249", weaponClass = "METRALHADORA LEVE",
            damage = 12.3f, shotsPerSecond = 4.5f, accuracy = 0.55f, magazineSize = 60, reloadTime = 3.2f, range = 11f,
            spread = 1.25f, soundPitch = 0.9f });
        Add(new Profile { id = Mac10, displayName = "MAC-10", weaponClass = "PISTOLA-METRALHADORA",
            damage = 3.75f, shotsPerSecond = 13f, accuracy = 0.65f, magazineSize = 60, reloadTime = 2.1f, range = 8.5f,
            spread = 1.05f, soundPitch = 1.15f });
    }

    static void Add(Profile p) => profiles[p.id] = p;

    /// <summary>Arma padrao de cada sobrevivente (quando o SurvivorAsset nao define weaponId).</summary>
    public static string DefaultIdFor(string survivorId)
    {
        switch (survivorId)
        {
            case "sniper": return Scar20;
            case "medic": return Mp5sd;
            case "engineer": return M249;
            case "explorer": return Mac10;
            default: return M4A1;
        }
    }

    public static string IdFor(SurvivorDef s)
    {
        if (profiles == null) Build();
        if (s == null) return M4A1;
        if (!string.IsNullOrEmpty(s.weaponId) && profiles.ContainsKey(s.weaponId)) return s.weaponId;
        return DefaultIdFor(s.id);
    }

    public static Profile Get(string id)
    {
        if (profiles == null) Build();
        return id != null && profiles.TryGetValue(id, out var p) ? p : profiles[M4A1];
    }

    public static Profile ForSurvivor(SurvivorDef s) => Get(IdFor(s));

    public static void ApplyTo(Profile p, WeaponData w)
    {
        w.name = p.displayName;
        w.displayName = p.displayName; w.weaponClass = p.weaponClass;
        w.damage = p.damage; w.shotsPerSecond = p.shotsPerSecond; w.accuracy = p.accuracy;
        w.magazineSize = p.magazineSize; w.reloadTime = p.reloadTime; w.range = p.range;
        w.spread = p.spread; w.soundPitch = p.soundPitch;
        // Antes a perfuracao vinha da arma-molde da cena (todas herdavam o mesmo valor). Agora e por arma.
        w.pierceTargets = p.pierceTargets;
        w.pierceFalloff = p.pierceTargets > 0 ? p.pierceFalloff : 0.5f;
    }

    /// <summary>
    /// Cria a arma da missao copiando o template da cena (para manter o prefab do projetil)
    /// e aplicando o perfil da arma do sobrevivente. Quem chama e dono da instancia (Destroy no fim).
    /// </summary>
    public static WeaponData CreateRuntime(SurvivorDef s, WeaponData template)
    {
        var w = template != null ? Object.Instantiate(template) : ScriptableObject.CreateInstance<WeaponData>();
        w.hideFlags = HideFlags.DontSave;
        ApplyTo(ForSurvivor(s), w);
        return w;
    }

    /// <summary>Instancia somente-leitura para menus (cacheada, nao use em combate).</summary>
    public static WeaponData Preview(SurvivorDef s)
    {
        string id = IdFor(s);
        if (previews.TryGetValue(id, out var w) && w != null) return w;
        w = ScriptableObject.CreateInstance<WeaponData>();
        w.hideFlags = HideFlags.HideAndDontSave;
        ApplyTo(Get(id), w);
        previews[id] = w;
        return w;
    }

    /// <summary>Maiores valores do catalogo (para normalizar as barras de atributos da UI).</summary>
    public static void Max(out float damage, out float fireRate, out int magazine, out float range)
    {
        if (profiles == null) Build();
        damage = fireRate = range = 0f; magazine = 0;
        foreach (var p in profiles.Values)
        {
            damage = Mathf.Max(damage, p.damage);
            fireRate = Mathf.Max(fireRate, p.shotsPerSecond);
            magazine = Mathf.Max(magazine, p.magazineSize);
            range = Mathf.Max(range, p.range);
        }
    }
}
