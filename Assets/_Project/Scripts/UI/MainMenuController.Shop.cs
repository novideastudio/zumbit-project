using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Tactical Arsenal shop screen. Built from editable Unity UI elements and replaceable Resources sprites.
public partial class MainMenuController
{
    const string ShopItemsArt = "Art/Items/";
    const string ShopSkillsArt = "Art/Skills/";
    const string ShopInventoryArt = "Art/PCInventoryUI/";

    string shopCategory = "ALL";

    sealed class ShopOfferInfo
    {
        public string name, description, icon, category, itemType;
        public string stat1Label, stat1Value, stat2Label, stat2Value;
        public ItemSlot slot;
        public Rarity rarity;
        public int price;
        public float damage, health, defense;
        public AffixType? bonusAffix;
        public float bonusAffixValue;

        public ShopOfferInfo(string n, string d, string i, string cat, string type, ItemSlot sl, Rarity r,
            int p, float dmg, float hp, float def, string s1l, string s1v, string s2l, string s2v,
            AffixType? affix = null, float affixValue = 0f)
        {
            name=n; description=d; icon=i; category=cat; itemType=type; slot=sl; rarity=r; price=p;
            damage=dmg; health=hp; defense=def; stat1Label=s1l; stat1Value=s1v;
            stat2Label=s2l; stat2Value=s2v; bonusAffix=affix; bonusAffixValue=affixValue;
        }
    }

    ShopOfferInfo[] GetShopOffers()
    {
        // Store offers retain the existing prices and core bonuses. The sight and rifle receive a small,
        // fixed affix so their English stat labels reflect a real gameplay effect, not decorative text.
        return new ShopOfferInfo[]
        {
            new ShopOfferInfo("Assault Rifle", "A balanced weapon for medium-range combat.", "arma_assalto", "WEAPONS", "WEAPON",
                ItemSlot.Arma, Rarity.Raro, 1800, 6f, 0f, 0f, "DAMAGE", "+6", "FIRE RATE", "+6%", AffixType.FireRate, 0.06f),
            new ShopOfferInfo("Tactical Helmet", "Reliable protection for surviving the first encounters.", "capacete_tatico", "HELMETS", "HELMET",
                ItemSlot.Capacete, Rarity.Incomum, 600, 0f, 18f, 2.2f, "HEALTH", "+18", "DEFENSE", "+2.2"),
            new ShopOfferInfo("Reinforced Vest", "Reinforced plates reduce the risk in close combat.", "colete_pesado", "VESTS", "VEST",
                ItemSlot.Colete, Rarity.Raro, 1500, 0f, 28f, 5f, "HEALTH", "+28", "DEFENSE", "+5"),
            new ShopOfferInfo("Reinforced Pants", "Durable equipment with additional protection.", "calca_reforcada", "PANTS", "PANTS",
                ItemSlot.Calca, Rarity.Incomum, 450, 0f, 12f, 1.8f, "HEALTH", "+12", "DEFENSE", "+1.8"),
            new ShopOfferInfo("Combat Boots", "Sturdy boots for extended operations.", "botas_de_combate", "BOOTS", "BOOTS",
                ItemSlot.Botas, Rarity.Comum, 300, 0f, 7f, 1.0f, "HEALTH", "+7", "DEFENSE", "+1"),
            new ShopOfferInfo("Telescopic Sight", "Improves weapon control and effective range.", "kit_de_mira", "ACCESSORIES", "ACCESSORY",
                ItemSlot.Arma, Rarity.Raro, 2400, 7.5f, 0f, 0f, "DAMAGE", "+7.5", "RANGE", "+6%", AffixType.Range, 0.06f),
        };
    }

    static string ShopNumber(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

    static RectTransform ShopRect(Transform parent, string name, float x, float y, float width, float height)
    {
        var rt = UIFactory.Node(name, parent);
        UIFactory.Place(rt, new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(width, height));
        return rt;
    }

    static TMP_Text ShopText(Transform parent, string name, string value, float x, float y, float width, float height,
        float size, Color color, bool wrap = false, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
    {
        var text = UIFactory.Txt(parent, value, size, alignment, color, name);
        UIFactory.Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(width, height));
        text.fontStyle = FontStyles.Bold;
        text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Truncate;
        text.enableAutoSizing = true;
        text.fontSizeMax = text.fontSize;
        text.fontSizeMin = Mathf.Min(11f, text.fontSize);
        text.raycastTarget = false;
        return text;
    }

    static RectTransform ShopFrame(Transform parent, string name, float x, float y, float width, float height,
        Color fill, Color border, float borderWidth = 2f)
    {
        var frame = UIKit.Frame(parent, name, fill, border, borderWidth);
        UIFactory.Place(frame, new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(width, height));
        return frame;
    }

    static Image ShopImage(Transform parent, string name, string resourcePath, float x, float y, float width, float height,
        Color? tint = null, bool preserveAspect = true)
    {
        var image = UIFactory.Img(parent, tint ?? Color.white, name);
        image.sprite = string.IsNullOrEmpty(resourcePath) ? null : ArtLoader.Get(resourcePath);
        // Use the project's generic icon whenever an art resource is missing; replace it in Resources later.
        if (image.sprite == null) image.sprite = ArtLoader.Get("Art/PCSkillsUI/icon_generic");
        if (image.sprite == null) image.sprite = UIFactory.White;
        image.type = Image.Type.Simple;
        image.preserveAspect = preserveAspect;
        image.raycastTarget = false;
        UIFactory.Place(image.rectTransform, new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(width, height));
        return image;
    }

    Button ShopButton(Transform parent, string name, string label, float x, float y, float width, float height,
        Action action, Color fill, Color border, float fontSize)
    {
        var button = UIKit.Button(parent, label, action, UIKit.BtnStyle.Secondary, fontSize, name);
        UIFactory.Place((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(width, height));
        var image = button.GetComponent<Image>();
        image.sprite = UIKit.Chamfer;
        image.type = Image.Type.Sliced;
        image.color = fill;
        UIFactory.SetButtonColor(button, fill);
        var outline = button.GetComponent<Outline>();
        if (outline == null) outline = button.gameObject.AddComponent<Outline>();
        outline.effectColor = border;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.useGraphicAlpha = true;
        var caption = button.GetComponentInChildren<TMP_Text>(true);
        if (caption)
        {
            caption.color = new Color(0.94f, 0.94f, 0.90f);
            caption.fontStyle = FontStyles.Bold;
            caption.enableAutoSizing = true;
            caption.fontSizeMax = caption.fontSize;
            caption.fontSizeMin = Mathf.Max(11f, caption.fontSize * 0.6f);
            caption.textWrappingMode = TextWrappingModes.NoWrap;
        }
        return button;
    }

    void ShowShop()
    {
        var save = GameSession.Save;
        if (save == null) { Show("main"); return; }
        if (save.inventory == null) save.inventory = new List<ItemInstance>();

        var screen = Rebuild("shop");
        // Reference artboard is 1920x1080. CampaignPcFit keeps the composition proportional on other sizes.
        var stage = UIFactory.Node("ShopStage", screen);
        stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(0.5f, 0.5f);
        stage.anchoredPosition = Vector2.zero;
        stage.sizeDelta = new Vector2(1920f, 1080f);
        var fit = stage.gameObject.AddComponent<CampaignPcFit>();
        fit.content = stage;
        fit.viewport = screen;
        fit.Apply();

        BuildShopBackdrop(stage);
        BuildShopMerchantSide(stage);
        BuildShopHeader(stage, save);
        BuildShopTabs(stage);
        BuildShopCards(stage, save);

        Show("shop");
        fit.Apply();
    }

    void BuildShopBackdrop(RectTransform stage)
    {
        var bg = UIFactory.Img(stage, Color.white, "ShopBackgroundArt");
        bg.sprite = ArtLoader.Get("Art/Backgrounds/wave_dock");
        if (bg.sprite == null) bg.sprite = ArtLoader.Get(SceneBackgrounds.DefaultPath);
        bg.color = new Color(0.70f, 0.68f, 0.65f, 1f);
        bg.preserveAspect = false;
        bg.raycastTarget = false;
        UIFactory.Stretch(bg.rectTransform);
        bg.transform.SetAsFirstSibling();

        var shade = UIFactory.Img(stage, new Color(0.008f, 0.013f, 0.019f, 0.58f), "ShopBackgroundShade");
        UIFactory.Stretch(shade.rectTransform);
        shade.raycastTarget = false;
        shade.transform.SetSiblingIndex(1);

        ShopFrame(stage, "ShopRightGlass", 720f, 101f, 1190f, 925f,
            new Color(0.008f, 0.018f, 0.026f, 0.25f), new Color(0.22f, 0.34f, 0.40f, 0.55f), 2f);
    }

    void BuildShopMerchantSide(RectTransform stage)
    {
        // Editable poster placeholders; the vendor portrait and item thumbnails are existing project art.
        var poster = ShopFrame(stage, "MerchantPoster", 20f, 116f, 270f, 196f,
            new Color(0.20f, 0.075f, 0.035f, 0.94f), new Color(0.90f, 0.39f, 0.12f, 0.95f), 3f);
        ShopText(poster, "PosterHeadline", "BETTER WEAPONS\nHIGHER CHANCES\nTO SURVIVE TOMORROW", 12f, 9f, 246f, 175f,
            25f, new Color(0.96f, 0.70f, 0.43f), true, TextAlignmentOptions.Center).characterSpacing = 0.7f;

        // Placeholder merchant: replace Resources/Art/Characters/mainmenu_survivor.png to use custom art.
        var merchant = ShopImage(stage, "ShopMerchantPortrait", "Art/Characters/mainmenu_survivor", -4f, 180f, 626f, 790f,
            Color.white, true);
        merchant.transform.SetAsLastSibling();

        var sign = ShopFrame(stage, "MerchantSideSign", 420f, 335f, 158f, 174f,
            new Color(0.055f, 0.045f, 0.035f, 0.97f), new Color(0.88f, 0.39f, 0.15f, 1f), 3f);
        ShopText(sign, "SignText", "QUALITY GEAR\nFAIR PRICES\nSURVIVE LONGER", 8f, 10f, 142f, 151f,
            20f, new Color(0.94f, 0.82f, 0.66f), true, TextAlignmentOptions.Center);

        var table = ShopFrame(stage, "ShopFrontWorkbench", 18f, 812f, 588f, 212f,
            new Color(0.014f, 0.025f, 0.031f, 0.96f), new Color(0.39f, 0.25f, 0.13f, 0.98f), 3f);
        ShopText(table, "WorkbenchTitle", "FIELD GEAR  /  READY FOR TOMORROW", 16f, 8f, 554f, 32f,
            18f, new Color(0.90f, 0.70f, 0.41f), false, TextAlignmentOptions.Center);
        var itemKeys = new[] { "Art/Items/arma_assalto", "Art/Items/capacete_tatico", "Art/Items/colete_pesado", "Art/Items/kit_de_mira" };
        var itemNames = new[] { "WEAPONS", "HELMETS", "VESTS", "OPTICS" };
        for (int i = 0; i < itemKeys.Length; i++)
        {
            float x = 23f + i * 138f;
            var slot = ShopFrame(table, "WorkbenchSlot_" + i, x, 47f, 126f, 119f,
                new Color(0.018f, 0.034f, 0.043f, 1f), new Color(0.33f, 0.44f, 0.48f, 1f), 2f);
            ShopImage(slot, "GearIcon", itemKeys[i], 10f, 7f, 104f, 78f, Color.white, true);
            ShopText(slot, "GearLabel", itemNames[i], 4f, 85f, 118f, 24f, 14f,
                new Color(0.78f, 0.84f, 0.85f), false, TextAlignmentOptions.Center);
        }
        ShopText(table, "WorkbenchFooter", "SURVIVAL EQUIPMENT  •  TRUSTED BY THE LIVING", 12f, 174f, 560f, 25f,
            13f, new Color(0.61f, 0.67f, 0.68f), false, TextAlignmentOptions.Center);
    }

    void BuildShopHeader(RectTransform stage, SaveData save)
    {
        var header = UIFactory.Img(stage, new Color(0.006f, 0.012f, 0.017f, 0.96f), "ShopHeaderBar");
        UIFactory.Place(header.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(1920f, 104f));
        header.raycastTarget = false;
        var bottomLine = UIFactory.Img(stage, new Color(0.77f, 0.22f, 0.08f, 0.95f), "ShopHeaderAccentLine");
        UIFactory.Place(bottomLine.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -100f), new Vector2(1920f, 3f));
        bottomLine.raycastTarget = false;

        ShopButton(stage, "ShopBackArrow", "<", 22f, 18f, 78f, 70f, ReturnFromUtility,
            new Color(0.035f, 0.055f, 0.065f, 0.98f), new Color(0.68f, 0.73f, 0.74f), 32f);
        ShopImage(stage, "ShopLogo", ShopInventoryArt + "logo", 117f, 8f, 218f, 82f, Color.white, true);
        ShopText(stage, "ShopTitle", "TACTICAL ARSENAL", 351f, 10f, 650f, 48f, 37f,
            new Color(0.95f, 0.91f, 0.79f), false);
        ShopText(stage, "ShopSubtitle", "BUY EQUIPMENT AND IMPROVE YOUR CHANCES TO SURVIVE", 354f, 55f, 650f, 27f,
            17f, new Color(0.65f, 0.75f, 0.78f), false);

        var wallet = ShopFrame(stage, "ShopWallet", 1204f, 16f, 392f, 68f,
            new Color(0.014f, 0.027f, 0.036f, 0.98f), new Color(0.28f, 0.40f, 0.45f, 1f), 2f);
        ShopImage(wallet, "GoldIcon", ShopSkillsArt + "icon_coins", 14f, 15f, 38f, 38f, Color.white, true);
        ShopText(wallet, "GoldLabel", "GOLD", 57f, 12f, 60f, 22f, 16f, new Color(0.81f, 0.84f, 0.82f), false);
        ShopText(wallet, "GoldAmount", ShopNumber(save.gold), 57f, 32f, 90f, 27f, 24f, UIKit.Col.Gold, false);
        var divider = UIFactory.Img(wallet, new Color(0.40f, 0.47f, 0.49f, 0.85f), "WalletDivider");
        UIFactory.Place(divider.rectTransform, new Vector2(0f, 1f), new Vector2(175f, -12f), new Vector2(2f, 44f));
        divider.raycastTarget = false;
        ShopImage(wallet, "ScrapIcon", ShopSkillsArt + "icon_gear", 194f, 15f, 38f, 38f, Color.white, true);
        ShopText(wallet, "ScrapLabel", "SCRAP", 238f, 12f, 74f, 22f, 16f, new Color(0.81f, 0.84f, 0.82f), false);
        ShopText(wallet, "ScrapAmount", ShopNumber(save.scrap), 238f, 32f, 128f, 27f, 24f, UIKit.Col.Gold, false);

        ShopButton(stage, "ShopClose", "X   BACK", 1622f, 16f, 275f, 68f, ReturnFromUtility,
            new Color(0.48f, 0.025f, 0.035f, 1f), new Color(0.91f, 0.18f, 0.12f, 1f), 24f);
    }

    void BuildShopTabs(RectTransform stage)
    {
        string[] categories = { "ALL", "WEAPONS", "HELMETS", "VESTS", "PANTS", "BOOTS", "ACCESSORIES", "CONSUMABLES" };
        string[] iconPaths =
        {
            "", ShopSkillsArt + "icon_bullets", "Art/Items/capacete_tatico", "Art/Items/colete_pesado",
            "Art/Items/calca_reforcada", "Art/Items/botas_de_combate", ShopSkillsArt + "icon_scope", ShopSkillsArt + "icon_medcrate"
        };
        float[] widths = { 145f, 160f, 165f, 150f, 145f, 140f, 190f, 190f };
        float x = 526f;
        for (int i = 0; i < categories.Length; i++)
        {
            string category = categories[i];
            float width = widths[i];
            bool active = string.Equals(shopCategory, category, StringComparison.OrdinalIgnoreCase);
            Color fill = active ? new Color(0.45f, 0.025f, 0.035f, 0.99f) : new Color(0.008f, 0.025f, 0.037f, 0.97f);
            Color border = active ? new Color(1f, 0.24f, 0.065f, 1f) : new Color(0.28f, 0.43f, 0.49f, 1f);
            var button = ShopButton(stage, "ShopTab_" + category, "", x, 139f, width, 80f,
                () => { shopCategory = category; ShowShop(); }, fill, border, 17f);
            if (active)
            {
                var underline = UIFactory.Img(button.transform, new Color(1f, 0.47f, 0.08f, 1f), "ActiveTabUnderline");
                UIFactory.Place(underline.rectTransform, new Vector2(0f, 0f), new Vector2(5f, 0f), new Vector2(width - 10f, 4f));
                underline.raycastTarget = false;
            }
            if (i == 0)
            {
                // Four-square generic ALL icon, built from editable Unity Images.
                for (int q = 0; q < 4; q++)
                {
                    int col = q % 2, row = q / 2;
                    var square = UIFactory.Img(button.transform, new Color(1f, 0.55f, 0.08f, 1f), "AllIconSquare_" + q);
                    UIFactory.Place(square.rectTransform, new Vector2(0f, 1f), new Vector2(15f + col * 18f, -20f - row * 18f), new Vector2(13f, 13f));
                    square.raycastTarget = false;
                }
            }
            else
            {
                var icon = ShopImage(button.transform, "TabIcon", iconPaths[i], 12f, 20f, 36f, 38f, Color.white, true);
                icon.raycastTarget = false;
            }
            ShopText(button.transform, "TabLabel", category, i == 0 ? 47f : 54f, 0f,
                width - (i == 0 ? 51f : 58f), 80f, i == 6 || i == 7 ? 14.5f : 16f,
                active ? new Color(1f, 0.94f, 0.83f) : new Color(0.84f, 0.88f, 0.89f), false, TextAlignmentOptions.Center);
            x += width + 8f;
        }
    }

    void BuildShopCards(RectTransform stage, SaveData save)
    {
        var offers = GetShopOffers();
        int shown = 0;
        for (int i = 0; i < offers.Length; i++)
        {
            var offer = offers[i];
            if (shopCategory != "ALL" && !string.Equals(shopCategory, offer.category, StringComparison.OrdinalIgnoreCase)) continue;
            int col = shown % 3;
            int row = shown / 3;
            float x = 740f + col * 382f;
            float y = 237f + row * 362f;
            BuildShopCard(stage, save, offer, i, x, y);
            shown++;
        }

        if (shown == 0)
        {
            var empty = ShopFrame(stage, "ShopEmptyState", 740f, 255f, 1125f, 650f,
                new Color(0.008f, 0.022f, 0.032f, 0.94f), new Color(0.26f, 0.41f, 0.48f, 0.9f), 2f);
            ShopImage(empty, "EmptyStateIcon", ShopSkillsArt + "icon_medcrate", 482f, 118f, 150f, 150f,
                new Color(0.65f, 0.76f, 0.78f), true);
            ShopText(empty, "EmptyStateTitle", "NO ITEMS AVAILABLE", 110f, 305f, 905f, 60f, 36f,
                new Color(0.94f, 0.91f, 0.82f), false, TextAlignmentOptions.Center);
            ShopText(empty, "EmptyStateSubtitle", "NEW EQUIPMENT WILL BE ADDED IN A FUTURE UPDATE.", 100f, 372f, 925f, 42f,
                20f, new Color(0.62f, 0.73f, 0.76f), true, TextAlignmentOptions.Center);
        }
    }

    void BuildShopCard(RectTransform stage, SaveData save, ShopOfferInfo offer, int offerIndex, float x, float y)
    {
        Color rarityColor = RarityColors.Get(offer.rarity);
        bool canAfford = save.gold >= offer.price;
        Color fill = offer.rarity == Rarity.Incomum
            ? new Color(0.006f, 0.073f, 0.044f, 0.98f)
            : offer.rarity == Rarity.Comum
                ? new Color(0.025f, 0.035f, 0.043f, 0.98f)
                : new Color(0.006f, 0.026f, 0.044f, 0.98f);
        Color border = offer.rarity == Rarity.Comum ? new Color(0.55f, 0.64f, 0.68f, 1f) : rarityColor;
        var card = ShopFrame(stage, "ShopOffer_" + offerIndex, x, y, 368f, 348f, fill, border, 3f);

        var topLine = UIFactory.Img(card, rarityColor, "RarityAccentLine");
        UIFactory.Place(topLine.rectTransform, new Vector2(0f, 1f), new Vector2(5f, -3f), new Vector2(354f, 4f));
        topLine.raycastTarget = false;

        var iconFrame = ShopFrame(card, "IconFrame", 15f, 22f, 102f, 105f,
            new Color(0.008f, 0.018f, 0.026f, 0.98f), rarityColor, 2f);
        var icon = ShopImage(iconFrame, "OfferIcon", ShopItemsArt + offer.icon, 7f, 7f, 88f, 91f, Color.white, true);
        if (icon.sprite == UIFactory.White)
        {
            icon.color = new Color(0.22f, 0.29f, 0.32f, 1f);
            ShopText(iconFrame, "GenericIconLabel", offer.itemType.Substring(0, Mathf.Min(3, offer.itemType.Length)),
                8f, 29f, 86f, 42f, 22f, Color.white, false, TextAlignmentOptions.Center);
        }

        ShopText(card, "OfferName", offer.name, 129f, 19f, 221f, 34f, 23f,
            new Color(0.94f, 0.94f, 0.90f), false);
        var rarityChip = UIKit.Chip(card, ShopRarityLabel(offer.rarity) + "  ·  " + offer.itemType,
            rarityColor, 216f, "RarityChip");
        UIFactory.Place(rarityChip, new Vector2(0f, 1f), new Vector2(129f, -57f), new Vector2(216f, 29f));
        var rarityText = rarityChip.GetComponentInChildren<TMP_Text>(true);
        if (rarityText)
        {
            rarityText.fontSize = 13f; rarityText.fontSizeMax = 13f; rarityText.fontSizeMin = 10f;
            rarityText.enableAutoSizing = true; rarityText.textWrappingMode = TextWrappingModes.NoWrap;
        }

        ShopText(card, "OfferDescription", offer.description, 129f, 92f, 220f, 76f, 16f,
            new Color(0.76f, 0.83f, 0.86f), true);

        Color statColor = offer.rarity == Rarity.Comum ? new Color(0.79f, 0.85f, 0.87f) :
            offer.rarity == Rarity.Incomum ? new Color(0.17f, 0.94f, 0.62f) : new Color(0.20f, 0.77f, 1f);
        BuildShopStatRow(card, "StatRowPrimary", offer.stat1Label, offer.stat1Value, 183f, statColor);
        BuildShopStatRow(card, "StatRowSecondary", offer.stat2Label, offer.stat2Value, 219f, statColor);

        ShopImage(card, "PriceCoin", ShopSkillsArt + "icon_coins", 19f, 286f, 34f, 34f, Color.white, true);
        ShopText(card, "OfferPrice", ShopNumber(offer.price), 58f, 279f, 130f, 47f, 27f,
            canAfford ? UIKit.Col.Gold : new Color(1f, 0.33f, 0.27f), false);
        Color buttonFill = offer.rarity == Rarity.Incomum ? new Color(0.015f, 0.37f, 0.22f, 1f) :
            offer.rarity == Rarity.Comum ? new Color(0.15f, 0.21f, 0.27f, 1f) : new Color(0.01f, 0.28f, 0.55f, 1f);
        var buy = ShopButton(card, "BuyOfferButton", "BUY", 207f, 278f, 143f, 48f,
            () => BuyShopOffer(offerIndex), buttonFill, rarityColor, 20f);
        if (!canAfford) buy.GetComponent<Image>().color = new Color(buttonFill.r * 0.60f, buttonFill.g * 0.60f, buttonFill.b * 0.60f, 1f);
    }

    void BuildShopStatRow(Transform card, string name, string label, string value, float y, Color accent)
    {
        var row = ShopFrame(card, name, 15f, y, 337f, 30f,
            new Color(0.016f, 0.044f, 0.058f, 0.99f), new Color(0.11f, 0.22f, 0.28f, 0.98f), 1f);
        ShopText(row, "StatLabel", label, 11f, 0f, 215f, 29f, 15f, accent, false);
        ShopText(row, "StatValue", value, 227f, 0f, 99f, 29f, 16f,
            new Color(1f, 0.77f, 0.28f), false, TextAlignmentOptions.MidlineRight);
    }

    static string ShopRarityLabel(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Comum: return "COMMON";
            case Rarity.Incomum: return "UNCOMMON";
            case Rarity.Raro: return "RARE";
            case Rarity.Epico: return "EPIC";
            case Rarity.Lendario: return "LEGENDARY";
            default: return rarity.ToString().ToUpperInvariant();
        }
    }

    void BuyShopOffer(int index)
    {
        var save = GameSession.Save;
        if (save == null) return;
        if (save.inventory == null) save.inventory = new List<ItemInstance>();
        if (save.inventory.Count >= 60) { Toast("Backpack is full. Free up space before buying."); return; }
        ShopOfferInfo[] offers = GetShopOffers();
        if (index < 0 || index >= offers.Length) return;
        ShopOfferInfo offer = offers[index];
        if (save.gold < offer.price) { Toast("Not enough gold to buy this item."); return; }

        var item = new ItemInstance
        {
            uid = Guid.NewGuid().ToString("N"),
            name = offer.name,
            description = offer.description,
            slot = offer.slot,
            rarity = offer.rarity,
            bonusDamage = offer.damage,
            bonusHealth = offer.health,
            bonusDefense = offer.defense,
            sellValue = Mathf.Max(1, offer.price / 3),
            upgradeLevel = 0,
            affixes = new List<ItemAffix>()
        };
        if (offer.bonusAffix.HasValue && offer.bonusAffixValue > 0f)
            item.affixes.Add(new ItemAffix { type = offer.bonusAffix.Value, value = offer.bonusAffixValue });

        save.gold -= offer.price;
        save.inventory.Add(item);
        try { if (!GameSession.DebugRun) SaveSystem.Save(save); }
        catch (Exception e) { Debug.LogWarning("Zombit: failed to save shop purchase: " + e.Message); }
        ZombitAudio.PlayUI();
        Toast("Purchased: " + offer.name);
        ShowShop();
    }
}
