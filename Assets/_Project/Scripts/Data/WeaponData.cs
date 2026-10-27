using UnityEngine;
[CreateAssetMenu(menuName = "Zombit/Weapon")]
public class WeaponData : ScriptableObject
{
    /// <summary>
    /// Cadencia de referencia (a da M4A1). Bonus de dano FIXO (nivel, itens) e normalizado por ela:
    /// cada arma recebe o mesmo ganho de DPS por ponto de bonus, nao importa se atira 1 ou 13 vezes por segundo.
    /// Sem isso, uma MAC-10 (13 tiros/s) ganharia 3x mais com um "+10 de dano" do que a M4A1.
    /// </summary>
    public const float ReferenceFireRate = 4f;

    public string displayName;
    [Tooltip("Categoria exibida na interface (ex.: FUZIL DE ASSALTO).")]
    public string weaponClass = "";
    public float damage = 10f;
    public float shotsPerSecond = 3f;
    [Range(0f, 1f)] public float accuracy = 0.8f;
    public int magazineSize = 30;
    public float reloadTime = 1.5f;
    public float range = 8f;
    [Tooltip("Desvio do tiro quando erra (unidades). Armas imprecisas espalham mais.")]
    public float spread = 0.8f;
    [Tooltip("Escala de tom do disparo: < 1 = tiro grave/pesado, > 1 = tiro seco/abafado.")]
    public float soundPitch = 1f;
    [Tooltip("Quantos inimigos EXTRA o projetil atravessa depois do primeiro (0 = nenhum).")]
    public int pierceTargets = 0;
    [Tooltip("Dano multiplicado a cada inimigo atravessado (0,5 = metade no 2o, um quarto no 3o).")]
    [Range(0f, 1f)] public float pierceFalloff = 0.5f;
    public GameObject projectilePrefab;

    /// <summary>Quanto de cada ponto de bonus fixo de dano entra em CADA tiro (1 na M4A1).</summary>
    public float BonusScale => ReferenceFireRate / Mathf.Max(0.1f, shotsPerSecond);

    /// <summary>
    /// Dano "equivalente M4A1" (mesmo DPS, cadencia de referencia). Usado por habilidades de area
    /// (granada, onda de choque, torreta) para que nao fiquem fortes demais com a SCAR-20 nem fracas com a MAC-10.
    /// </summary>
    public float ReferenceDamage => damage * Mathf.Max(0.1f, shotsPerSecond) / ReferenceFireRate;

    /// <summary>DPS sustentado (considerando recarga), sem bonus. Util para balanceamento e UI.</summary>
    public float SustainedDps
    {
        get
        {
            float sps = Mathf.Max(0.1f, shotsPerSecond);
            int mag = Mathf.Max(1, magazineSize);
            return damage * mag / (mag / sps + Mathf.Max(0f, reloadTime));
        }
    }
}
