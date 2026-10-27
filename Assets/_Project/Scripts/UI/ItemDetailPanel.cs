using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemDetailPanel : MonoBehaviour
{
    public Inventory inventory;

    public Image icon;
    public TMP_Text nameText;
    public TMP_Text rarityText;
    public TMP_Text statsText;
    public TMP_Text descriptionText;

    public Button equipButton;
    public TMP_Text equipButtonLabel;
    public Button sellButton;
    public TMP_Text sellLabel;

    public ItemInstance Current { get; private set; }
    bool isEquipped;

    void Awake()
    {
        if (equipButton) equipButton.onClick.AddListener(OnEquip);
        if (sellButton) sellButton.onClick.AddListener(OnSell);
        if (icon) icon.preserveAspect = true;
        Hide();
    }

    public void Show(ItemInstance item, bool equipped)
    {
        if (item == null) { Hide(); return; }
        Current = item;
        isEquipped = equipped;
        gameObject.SetActive(true);

        if (icon)
        {
            icon.sprite = ItemVisual.Icon(item);
            icon.color = Color.white;
            icon.preserveAspect = true;
        }
        if (nameText)
        {
            nameText.text = item.name;
            nameText.color = RarityColors.Get(item.rarity);
            nameText.fontStyle = FontStyles.Bold;
        }
        if (rarityText)
        {
            rarityText.text = RarityName(item.rarity) + "  |  " + SlotName(item.slot) +
                (item.upgradeLevel > 0 ? "  (+" + item.upgradeLevel + ")" : "");
            rarityText.color = RarityColors.Get(item.rarity);
        }
        if (descriptionText) descriptionText.text = UsefulDescription(item);

        ItemInstance comparison = null;
        if (!equipped && inventory && inventory.stats)
            comparison = inventory.stats.GetEquipped(item.slot);

        StringBuilder sb = new StringBuilder();
        AppendStat(sb, "DANO", item.bonusDamage, comparison != null ? comparison.bonusDamage : 0f, comparison != null);
        AppendStat(sb, "VIDA", item.bonusHealth, comparison != null ? comparison.bonusHealth : 0f, comparison != null);
        AppendStat(sb, "DEFESA", item.bonusDefense, comparison != null ? comparison.bonusDefense : 0f, comparison != null);
        if (item.HasAffixes)
            foreach (var af in item.affixes)
                if (af != null) { if (sb.Length > 0) sb.AppendLine(); sb.Append("+ ").Append(af.Describe().ToUpperInvariant()); }
        if (sb.Length == 0) sb.Append("SEM BONUS DE ATRIBUTOS");
        if (comparison != null) sb.Append("\n(variacao vs equipado)");
        if (statsText) statsText.text = sb.ToString();

        if (equipButtonLabel) equipButtonLabel.text = equipped ? "DESEQUIPAR" : "EQUIPAR";
        if (sellButton) sellButton.gameObject.SetActive(!equipped);
        if (sellLabel) sellLabel.text = "VENDER  " + item.sellValue + " OURO";
    }

    static void AppendStat(StringBuilder sb, string label, float value, float currentValue, bool compare)
    {
        if (Mathf.Abs(value) < 0.01f && (!compare || Mathf.Abs(currentValue) < 0.01f)) return;
        if (sb.Length > 0) sb.AppendLine();
        sb.Append(label).Append("  ").Append(value >= 0 ? "+" : "").Append(value.ToString("0.#"));
        if (compare)
        {
            float delta = value - currentValue;
            sb.Append("  (").Append(delta > 0 ? "+" : "").Append(delta.ToString("0.#")).Append(")");
        }
    }

    static string UsefulDescription(ItemInstance item)
    {
        if (!string.IsNullOrWhiteSpace(item.description) &&
            !item.description.ToLowerInvariant().Contains("item de exemplo")) return item.description;

        switch (Normalize(item.name))
        {
            case "kitdemira": return "Mira tatica que melhora a precisao e o dano da arma equipada.";
            case "miralendaria": return "Optica lendaria de alta precisao, com um bonus elevado de dano.";
            case "capacetetatico": return "Capacete reforcado para proteger a cabeca. Aumenta vida e defesa.";
            case "coleteleve": return "Colete de protecao leve. Oferece defesa sem sacrificar mobilidade.";
            case "coletepesado": return "Colete balistico reforcado. Maior protecao e reserva de vida.";
            case "calcareforcada": return "Calcas taticas com reforcos nos joelhos para protecao adicional.";
            case "botasdecombate": return "Botas militares robustas para longas operacoes em zona hostil.";
            case "mochilamilitar": return "Mochila de campo resistente, equipada para expedicoes prolongadas.";
            default: return "Equipamento de sobrevivencia. Confira os bonus antes de equipar ou vender.";
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

    static string RarityName(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Comum: return "COMUM";
            case Rarity.Incomum: return "INCOMUM";
            case Rarity.Raro: return "RARO";
            case Rarity.Epico: return "EPICO";
            case Rarity.Lendario: return "LENDARIO";
            default: return rarity.ToString().ToUpperInvariant();
        }
    }

    static string SlotName(ItemSlot slot)
    {
        switch (slot)
        {
            case ItemSlot.Arma: return "MELHORIA";
            case ItemSlot.Capacete: return "CAPACETE";
            case ItemSlot.Colete: return "COLETE";
            case ItemSlot.Calca: return "CALCAS";
            case ItemSlot.Botas: return "BOTAS";
            case ItemSlot.Mochila: return "MOCHILA";
            default: return slot.ToString().ToUpperInvariant();
        }
    }

    public void Hide()
    {
        Current = null;
        gameObject.SetActive(false);
    }

    void OnEquip()
    {
        if (Current == null || !inventory) return;
        if (isEquipped) inventory.Unequip(Current.slot);
        else inventory.Equip(Current);
        Hide();
    }

    void OnSell()
    {
        if (Current == null || isEquipped || !inventory) return;
        inventory.Sell(Current);
        Hide();
    }
}
