using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class SlotUI : MonoBehaviour, IPointerClickHandler
{
    public Image border;
    public Image icon;

    public ItemInstance Item { get; private set; }
    public bool IsEquipSlot;
    public ItemSlot EquipType;
    public Action<SlotUI> Clicked;
    public Action<SlotUI> DoubleClicked;

    Outline rarityOutline;
    TMP_Text upgradeBadge;

    void Awake()
    {
        if (icon)
        {
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            RectTransform rt = icon.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(6f, 6f);
            rt.offsetMax = new Vector2(-6f, -6f);
        }

        if (border)
        {
            rarityOutline = border.GetComponent<Outline>();
            if (!rarityOutline) rarityOutline = border.gameObject.AddComponent<Outline>();
            rarityOutline.effectDistance = new Vector2(1.5f, -1.5f);
            rarityOutline.useGraphicAlpha = true;
        }
        EnsureUpgradeBadge();
    }

    void EnsureUpgradeBadge()
    {
        if (upgradeBadge) return;
        GameObject go = new GameObject("UpgradeBadge", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-2f, -2f);
        rt.sizeDelta = new Vector2(27f, 15f);

        Image bg = go.GetComponent<Image>();
        bg.color = new Color(0.025f, 0.03f, 0.035f, 0.94f);
        bg.raycastTarget = false;

        RectTransform labelRect = new GameObject("Label", typeof(RectTransform)).GetComponent<RectTransform>();
        labelRect.SetParent(go.transform, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        upgradeBadge = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        upgradeBadge.text = "+1";
        upgradeBadge.fontSize = 9f;
        upgradeBadge.fontStyle = FontStyles.Bold;
        upgradeBadge.alignment = TextAlignmentOptions.Center;
        upgradeBadge.color = new Color(1f, 0.78f, 0.22f, 1f);
        upgradeBadge.raycastTarget = false;
        go.SetActive(false);
    }

    public void Set(ItemInstance item)
    {
        Item = item;
        bool hasItem = item != null;

        if (icon)
        {
            Sprite visual = null;
            if (hasItem) visual = ItemVisual.Icon(item);
            else if (IsEquipSlot && ItemVisual.HasIconForSlot(EquipType)) visual = ItemVisual.IconForSlot(EquipType);

            icon.sprite = visual;
            icon.enabled = visual != null && (hasItem || IsEquipSlot);
            icon.color = hasItem ? Color.white : new Color(0.70f, 0.76f, 0.79f, 0.34f);
        }

        if (border)
        {
            border.color = hasItem ? new Color(0.075f, 0.085f, 0.095f, 1f) : new Color(0.055f, 0.065f, 0.075f, 0.98f);
        }
        if (rarityOutline)
        {
            rarityOutline.effectColor = hasItem ? RarityColors.Get(item.rarity) :
                (IsEquipSlot ? new Color(0.40f, 0.48f, 0.52f, 0.62f) : new Color(0.20f, 0.24f, 0.27f, 0.6f));
            rarityOutline.effectDistance = hasItem ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
        }

        if (upgradeBadge)
        {
            bool showUpgrade = hasItem && item.upgradeLevel > 0;
            upgradeBadge.gameObject.SetActive(showUpgrade);
            if (showUpgrade) upgradeBadge.text = "+" + item.upgradeLevel;
        }
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (e != null && e.clickCount >= 2) DoubleClicked?.Invoke(this);
        else Clicked?.Invoke(this);
    }
}
