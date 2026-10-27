using UnityEngine;

[CreateAssetMenu(menuName = "Zombit/Survivor", fileName = "Survivor")]
public class SurvivorAsset : ScriptableObject
{
    [Header("Identidade")]
    [Tooltip("Nao mude depois de criado (usado nos saves)")] public string id = "novo";
    public int order;
    public string displayName = "Novo";
    public string role = "";
    [TextArea] public string description = "";
    public string utilityName = "";
    [TextArea] public string utilityDesc = "";
    public UtilityType utility;
    public Color color = Color.white;

    [Header("Arte")]
    public Sprite portrait;
    public Sprite fullBody;
    [Tooltip("Altura do heroi no jogo, em unidades")] public float heroHeight = 2.6f;
    [Tooltip("Onde o tiro sai, em coordenadas do sprite: x da esquerda (0-1), y da base (0-1). " +
             "Selecione o heroi em Play Mode para ver o ponto (esfera amarela) e ajuste ate ficar na ponta do cano.")]
    public Vector2 muzzleUV = new Vector2(0.97f, 0.36f);

    [Header("Arma")]
    [Tooltip("Id da arma exclusiva no WeaponCatalog: m4a1, scar20, mp5sd, m249, mac10. Vazio = padrao da classe.")]
    public string weaponId = "";

    [Header("Atributos (multiplicadores)")]
    public float healthMult = 1f;
    public float damageMult = 1f;
    public float fireRateMult = 1f;
    public float rangeMult = 1f;
    public float accuracyBonus = 0f;
    public float critChance = 0f;
    public float critMult = 2f;
    public float xpMult = 1f;
    public float goldMult = 1f;
    public float dropMult = 1f;

    public SurvivorDef ToDef()
    {
        return new SurvivorDef
        {
            id = id, name = displayName, role = role, description = description,
            utilityName = utilityName, utilityDesc = utilityDesc, utility = utility, color = color,
            portrait = portrait, fullBody = fullBody, heroHeight = heroHeight, muzzleUV = muzzleUV, weaponId = weaponId,
            healthMult = healthMult, damageMult = damageMult, fireRateMult = fireRateMult, rangeMult = rangeMult,
            accuracyBonus = accuracyBonus, critChance = critChance, critMult = critMult,
            xpMult = xpMult, goldMult = goldMult, dropMult = dropMult
        };
    }

    public static SurvivorAsset FromDef(SurvivorDef d, int order)
    {
        var a = CreateInstance<SurvivorAsset>();
        a.id = d.id; a.order = order; a.displayName = d.name; a.role = d.role; a.description = d.description;
        a.utilityName = d.utilityName; a.utilityDesc = d.utilityDesc; a.utility = d.utility; a.color = d.color; a.weaponId = d.weaponId;
        a.healthMult = d.healthMult; a.damageMult = d.damageMult; a.fireRateMult = d.fireRateMult; a.rangeMult = d.rangeMult;
        a.accuracyBonus = d.accuracyBonus; a.critChance = d.critChance; a.critMult = d.critMult;
        a.xpMult = d.xpMult; a.goldMult = d.goldMult; a.dropMult = d.dropMult;
        return a;
    }
}
