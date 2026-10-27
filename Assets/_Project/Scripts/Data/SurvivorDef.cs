using UnityEngine;

public enum UtilityType { Grenade, Precision, FieldMedic, Turret, Scavenger }

public class SurvivorDef
{
    public string id, name, role, description, utilityName, utilityDesc;
    public Color color = Color.white;
    public UtilityType utility;

    public Sprite portrait, fullBody;
    public float heroHeight = 2.6f;
    /// <summary>Ponta da arma / ponto de saida do tiro, em UV do sprite (x da esquerda, y da base; 0-1).</summary>
    public Vector2 muzzleUV = new Vector2(0.97f, 0.36f);
    /// <summary>Arma exclusiva (id do WeaponCatalog). Vazio = arma padrao da classe.</summary>
    public string weaponId = "";

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
}
