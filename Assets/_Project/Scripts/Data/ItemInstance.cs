using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ItemInstance
{
    public string uid;
    public string name;
    public string description;
    public ItemSlot slot;
    public Rarity rarity;
    public float bonusDamage, bonusHealth, bonusDefense;
    public int sellValue;
    // Campo aditivo: saves antigos recebem nível 0 quando forem desserializados.
    public int upgradeLevel;
    // Adicionados na versao 5: afixos aleatorios + marca de item exclusivo do Pesadelo.
    // Saves antigos nao tem estes campos: ficam vazios/falsos.
    public List<ItemAffix> affixes = new List<ItemAffix>();
    public bool nightmare;

    public bool HasAffixes => affixes != null && affixes.Count > 0;
}

public static class ItemFactory
{
    static void Range(Rarity r, out float min, out float max, out float extraChance) => Balance.ItemRange(r, out min, out max, out extraChance);

    static float R1(float v) => Mathf.Round(v * 10f) / 10f;

    public static ItemInstance Create(ItemData t, bool nightmareTier = false)
    {
        Range(t.rarity, out float min, out float max, out float extraChance);
        float m = UnityEngine.Random.Range(min, max);

        var it = new ItemInstance
        {
            uid = Guid.NewGuid().ToString("N"),
            name = t.displayName,
            description = t.description,
            slot = t.slot,
            rarity = t.rarity,
            bonusDamage = R1(t.bonusDamage * m),
            bonusHealth = Mathf.Round(t.bonusHealth * m),
            bonusDefense = R1(t.bonusDefense * m),
            sellValue = Mathf.Max(1, Mathf.RoundToInt(t.sellValue * m)),
        };

        if (UnityEngine.Random.value < extraChance)
        {
            float tier = ((int)t.rarity + 1) * UnityEngine.Random.Range(0.8f, 1.2f);
            switch (UnityEngine.Random.Range(0, 3))
            {
                case 0: it.bonusDamage = R1(it.bonusDamage + 2f * tier); break;
                case 1: it.bonusHealth = Mathf.Round(it.bonusHealth + 10f * tier); break;
                default: it.bonusDefense = R1(it.bonusDefense + 1f * tier); break;
            }
            it.sellValue = Mathf.RoundToInt(it.sellValue * 1.25f);
        }

        // Afixos: assinatura (itens exclusivos) + aleatorios por raridade/slot.
        it.nightmare = t.nightmareOnly;
        AffixType? reserved = null;
        if (t.hasSignature)
        {
            reserved = t.signatureAffix;
            it.affixes.Add(new ItemAffix { type = t.signatureAffix, value = t.signatureValue });
        }
        it.affixes.AddRange(AffixRoller.Roll(t.slot, t.rarity, nightmareTier, reserved));
        if (it.affixes.Count > 0)
            it.sellValue = Mathf.RoundToInt(it.sellValue * (1f + 0.2f * it.affixes.Count));
        // Venda: fracao do valor por raridade (Balance.SellMult).
        it.sellValue = Mathf.Max(1, Mathf.RoundToInt(it.sellValue * Balance.SellMult(t.rarity)));
        return it;
    }
}

public static class ItemVisual
{
    // Icons por item-base. Itens adicionados no futuro recorrem ao icone generico do respetivo slot.
    static string ItemKey(string itemName)
    {
        switch (Normalize(itemName))
        {
            case "botasdecombate": return "botas_de_combate";
            case "rifle":
            case "fuzil":
            case "fuzildeassalto":
            case "armaassalto": return "arma_assalto";
            case "calcareforcada":
            case "calcasreforcadas": return "calca_reforcada";
            case "capacetetatico": return "capacete_tatico";
            case "coleteleve": return "colete_leve";
            case "coletepesado":
            case "coletereforcado": return "colete_pesado";
            case "kitdemira":
            case "miraoptica": return "kit_de_mira";
            case "miralendaria": return "mira_lendaria";
            case "mochilamilitar": return "mochila_militar";
            default: return null;
        }
    }

    static string SlotKey(ItemSlot slot)
    {
        switch (slot)
        {
            case ItemSlot.Arma: return "arma_assalto";
            case ItemSlot.Capacete: return "capacete_tatico";
            case ItemSlot.Colete: return "colete_leve";
            case ItemSlot.Calca: return "calca_reforcada";
            case ItemSlot.Botas: return "botas_de_combate";
            case ItemSlot.Mochila: return "mochila_militar";
            default: return "";
        }
    }

    static string Normalize(string value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant()
            .Replace("á", "a").Replace("à", "a").Replace("ã", "a").Replace("â", "a")
            .Replace("é", "e").Replace("ê", "e").Replace("í", "i")
            .Replace("ó", "o").Replace("ô", "o").Replace("õ", "o").Replace("ú", "u").Replace("ç", "c")
            .Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty);
    }

    static Sprite LoadIcon(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        return ArtLoader.Get("Art/Items/" + key);
    }

    public static Sprite Icon(ItemInstance item)
    {
        if (item == null) return UIFactory.White;
        Sprite specific = LoadIcon(ItemKey(item.name));
        if (specific != null) return specific;
        Sprite slot = IconForSlotAsset(item.slot);
        return slot != null ? slot : UIFactory.White;
    }

    public static Sprite IconForSlot(ItemSlot slot)
    {
        Sprite sprite = IconForSlotAsset(slot);
        return sprite != null ? sprite : UIFactory.White;
    }

    public static bool HasIconForSlot(ItemSlot slot) => IconForSlotAsset(slot) != null;

    static Sprite IconForSlotAsset(ItemSlot slot) => LoadIcon(SlotKey(slot));

    public static Color Tint(ItemInstance item)
    {
        if (item == null) return Color.white;
        if (Icon(item) != UIFactory.White) return Color.white;
        switch (item.slot)
        {
            case ItemSlot.Arma:     return new Color(0.85f, 0.85f, 0.90f);
            case ItemSlot.Capacete: return new Color(0.50f, 0.65f, 0.50f);
            case ItemSlot.Colete:   return new Color(0.45f, 0.55f, 0.85f);
            case ItemSlot.Calca:    return new Color(0.60f, 0.50f, 0.38f);
            case ItemSlot.Botas:    return new Color(0.45f, 0.35f, 0.25f);
            default:                return new Color(0.55f, 0.62f, 0.40f);
        }
    }
}
