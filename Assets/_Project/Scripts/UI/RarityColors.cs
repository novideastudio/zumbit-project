using UnityEngine;
public static class RarityColors
{
    public static Color Get(Rarity r) => r switch
    {
        Rarity.Comum    => new Color(0.75f, 0.75f, 0.75f),
        Rarity.Incomum  => new Color(0.30f, 0.85f, 0.35f),
        Rarity.Raro     => new Color(0.30f, 0.60f, 1.00f),
        Rarity.Epico    => new Color(0.70f, 0.35f, 0.95f),
        Rarity.Lendario => new Color(1.00f, 0.65f, 0.10f),
        _ => Color.white
    };
}
