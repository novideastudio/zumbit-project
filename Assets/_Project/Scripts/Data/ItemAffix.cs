using System;
using System.Collections.Generic;
using UnityEngine;

public enum AffixType { Damage, CritChance, CritDamage, FireRate, Range, Accuracy, XpGain, GoldFind, Regen }

[Serializable]
public class ItemAffix
{
    public AffixType type;
    public float value; // fracao (0.05 = +5%). Regen = fracao da vida maxima por segundo.

    public static string Label(AffixType t)
    {
        switch (t)
        {
            case AffixType.Damage: return "Dano causado";
            case AffixType.CritChance: return "Chance de crítico";
            case AffixType.CritDamage: return "Dano crítico";
            case AffixType.FireRate: return "Cadência de tiro";
            case AffixType.Range: return "Alcance";
            case AffixType.Accuracy: return "Precisão";
            case AffixType.XpGain: return "XP recebido";
            case AffixType.GoldFind: return "Ouro recebido";
            default: return "Regeneração de vida";
        }
    }

    public string Describe()
    {
        float pct = Mathf.Round(value * 1000f) / 10f;
        string suffix = type == AffixType.Regen ? "% /s" : "%";
        return Label(type) + " +" + pct.ToString("0.#") + suffix;
    }
}

/// <summary>Soma dos afixos dos itens equipados.</summary>
public struct AffixBonuses
{
    public float damage, critChance, critDamage, fireRate, range, accuracy, xp, gold, regen;

    public static AffixBonuses Sum(IEnumerable<ItemInstance> items)
    {
        var s = new AffixBonuses();
        if (items == null) return s;
        foreach (var it in items)
        {
            if (it == null || it.affixes == null) continue;
            foreach (var a in it.affixes)
            {
                if (a == null) continue;
                switch (a.type)
                {
                    case AffixType.Damage: s.damage += a.value; break;
                    case AffixType.CritChance: s.critChance += a.value; break;
                    case AffixType.CritDamage: s.critDamage += a.value; break;
                    case AffixType.FireRate: s.fireRate += a.value; break;
                    case AffixType.Range: s.range += a.value; break;
                    case AffixType.Accuracy: s.accuracy += a.value; break;
                    case AffixType.XpGain: s.xp += a.value; break;
                    case AffixType.GoldFind: s.gold += a.value; break;
                    case AffixType.Regen: s.regen += a.value; break;
                }
            }
        }
        return s;
    }
}

public static class AffixRoller
{
    // Valor MAXIMO (item Lendario) de cada afixo. Raridades menores usam uma fracao disso.
    static float Max(AffixType t)
    {
        switch (t)
        {
            case AffixType.Damage: return 0.08f;
            case AffixType.CritChance: return 0.06f;
            case AffixType.CritDamage: return 0.25f;
            case AffixType.FireRate: return 0.10f;
            case AffixType.Range: return 0.10f;
            case AffixType.Accuracy: return 0.08f;
            case AffixType.XpGain: return 0.15f;
            case AffixType.GoldFind: return 0.20f;
            default: return 0.008f;
        }
    }

    static readonly float[] RarityScale = { 0.35f, 0.5f, 0.7f, 0.85f, 1f };

    static AffixType[] PoolFor(ItemSlot slot)
    {
        switch (slot)
        {
            case ItemSlot.Arma: return new[] { AffixType.Damage, AffixType.CritChance, AffixType.CritDamage, AffixType.FireRate, AffixType.Range, AffixType.Accuracy };
            case ItemSlot.Capacete: return new[] { AffixType.Accuracy, AffixType.CritChance, AffixType.XpGain, AffixType.Regen };
            case ItemSlot.Colete: return new[] { AffixType.Regen, AffixType.Damage, AffixType.XpGain };
            case ItemSlot.Calca: return new[] { AffixType.GoldFind, AffixType.Regen, AffixType.Range };
            case ItemSlot.Botas: return new[] { AffixType.FireRate, AffixType.GoldFind, AffixType.Accuracy };
            default: return new[] { AffixType.GoldFind, AffixType.XpGain, AffixType.Regen };
        }
    }

    public static float RollValue(AffixType t, Rarity r, bool nightmare)
    {
        float v = Max(t) * RarityScale[Mathf.Clamp((int)r, 0, 4)] * UnityEngine.Random.Range(0.75f, 1f);
        if (nightmare) v *= 1.15f;
        return Mathf.Round(v * 1000f) / 1000f;
    }

    public static int Count(Rarity r, bool nightmare)
    {
        int n;
        switch (r)
        {
            case Rarity.Comum: n = 0; break;
            case Rarity.Incomum: n = UnityEngine.Random.value < 0.25f ? 1 : 0; break;
            case Rarity.Raro: n = 1; break;
            case Rarity.Epico: n = 2; break;
            default: n = 3; break;
        }
        if (nightmare && r >= Rarity.Raro) n++;
        return Mathf.Min(n, 4);
    }

    /// <param name="reserved">Tipo ja usado (afixo assinatura), para nao duplicar.</param>
    public static List<ItemAffix> Roll(ItemSlot slot, Rarity rarity, bool nightmare, AffixType? reserved = null)
    {
        var result = new List<ItemAffix>();
        var pool = new List<AffixType>(PoolFor(slot));
        if (reserved.HasValue) pool.Remove(reserved.Value);
        int n = Mathf.Min(Count(rarity, nightmare), pool.Count);
        for (int i = 0; i < n; i++)
        {
            int k = UnityEngine.Random.Range(0, pool.Count);
            var t = pool[k];
            pool.RemoveAt(k);
            result.Add(new ItemAffix { type = t, value = RollValue(t, rarity, nightmare) });
        }
        return result;
    }
}
