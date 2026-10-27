using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Loja (parte de MainMenuController).
public partial class MainMenuController
{
    sealed class ShopOfferInfo
    {
        public string name, description, icon;
        public ItemSlot slot;
        public Rarity rarity;
        public int price;
        public float damage, health, defense;
        public ShopOfferInfo(string n, string d, string i, ItemSlot sl, Rarity r, int p, float dmg, float hp, float def)
        { name=n; description=d; icon=i; slot=sl; rarity=r; price=p; damage=dmg; health=hp; defense=def; }
    }

    ShopOfferInfo[] GetShopOffers()
    {
        return new ShopOfferInfo[]
        {
            // Preços recalibrados (Balance): a loja vende só até Raro e sem afixos, sempre pior que um drop equivalente.
            // Épico/Lendário só vêm de missões. Venda de volta = preço / 3.
            new ShopOfferInfo("Fuzil de Assalto", "Arma equilibrada para combater hordas a média distância.", "arma_assalto", ItemSlot.Arma, Rarity.Raro, 1800, 6f, 0f, 0f),
            new ShopOfferInfo("Capacete Tático", "Proteção fiável para sobreviver aos primeiros confrontos.", "capacete_tatico", ItemSlot.Capacete, Rarity.Incomum, 600, 0f, 18f, 2.2f),
            new ShopOfferInfo("Colete Reforçado", "Placas adicionais para reduzir o risco em combate próximo.", "colete_pesado", ItemSlot.Colete, Rarity.Raro, 1500, 0f, 28f, 5f),
            new ShopOfferInfo("Calças Reforçadas", "Equipamento resistente com proteção adicional.", "calca_reforcada", ItemSlot.Calca, Rarity.Incomum, 450, 0f, 12f, 1.8f),
            new ShopOfferInfo("Botas de Combate", "Botas robustas para operações prolongadas.", "botas_de_combate", ItemSlot.Botas, Rarity.Comum, 300, 0f, 7f, 1.0f),
            new ShopOfferInfo("Mira Telescópica", "Melhora o controlo e a capacidade ofensiva da arma.", "kit_de_mira", ItemSlot.Arma, Rarity.Raro, 2400, 7.5f, 0f, 0f),
        };
    }

    void ShowShop()
    {
        var save = GameSession.Save;
        if (save == null) { Show("main"); return; }
        if (save.inventory == null) save.inventory = new List<ItemInstance>();
        var s = Rebuild("shop");
        CreateUtilityBackdrop(s, "ShopBackdrop");
        CreateUtilityHeader(s, "ARSENAL TÁTICO", "OURO  " + save.gold.ToString("N0"), NavTab.Shop);

        MapText(s, "EQUIPAMENTO DISPONÍVEL", UIKit.Size.Label, TextAlignmentOptions.MidlineLeft,
            UIKit.Col.Text, 44f, 118f, 520f, 30f, "ShopSectionTitle").fontStyle = FontStyles.Bold;
        MapText(s, "Compre equipamento com o ouro recolhido nas missões. Os itens entram diretamente na mochila.",
            UIKit.Size.Caption, TextAlignmentOptions.MidlineLeft, UIKit.Col.TextDim, 44f, 150f, 1450f, 26f, "ShopSubtitle");

        ShopOfferInfo[] offers = GetShopOffers();
        for (int i = 0; i < offers.Length; i++)
        {
            ShopOfferInfo offer = offers[i];
            int col = i % 3, row = i / 3;
            float x = 44f + col * 615f;
            float y = 194f + row * 384f;
            Color rarityColor = RarityColors.Get(offer.rarity);
            bool canAfford = save.gold >= offer.price;
            Color borderColor = Color.Lerp(rarityColor, UIKit.Col.Border, 0.35f);
            var card = UIKit.Frame(s, "ShopOffer_" + i, new Color(0.035f, 0.055f, 0.068f, 0.98f), borderColor, 2f);
            UIFactory.Place(card, new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(580f, 360f));
            var band = UIFactory.Img(card, rarityColor, "RarityBand");
            UIFactory.Place(band.rectTransform, new Vector2(0f, 1f), new Vector2(12f, -2f), new Vector2(556f, 5f));
            band.raycastTarget = false;

            var iconFrame = UIKit.Frame(card, "IconFrame", new Color(0.02f, 0.035f, 0.045f, 1f), borderColor, 2f);
            UIFactory.Place(iconFrame, new Vector2(0f, 1f), new Vector2(22f, -30f), new Vector2(118f, 118f));
            var icon = UIFactory.Img(iconFrame, Color.white, "OfferIcon");
            icon.sprite = ArtLoader.Get("Art/Items/" + offer.icon);
            if (icon.sprite == null) icon.sprite = ArtLoader.Get("Art/Items/" + offer.slot.ToString().ToLowerInvariant());
            icon.preserveAspect = true; icon.raycastTarget = false;
            UIFactory.Inset(UIFactory.Stretch(icon.rectTransform), 12f, 12f);

            var name = MapText(card, offer.name, 27, TextAlignmentOptions.MidlineLeft, UIKit.Col.Text,
                158f, 34f, 396f, 38f, "OfferName"); name.fontStyle = FontStyles.Bold;
            var rarityChip = UIKit.Chip(card, RarityMapLabel(offer.rarity).ToUpperInvariant() + "  ·  " + offer.slot.ToString().ToUpperInvariant(),
                rarityColor, 230f, "RarityChip");
            UIFactory.Place(rarityChip, new Vector2(0f, 1f), new Vector2(158f, -84f), new Vector2(230f, 36f));
            var desc = MapText(card, offer.description, UIKit.Size.Body, TextAlignmentOptions.TopLeft,
                new Color(0.78f, 0.85f, 0.88f), 22f, 160f, 536f, 66f, "OfferDescription");
            desc.textWrappingMode = TextWrappingModes.Normal;

            var statRow = UIFactory.Node("StatChips", card);
            UIFactory.Place(statRow, new Vector2(0f, 1f), new Vector2(22f, -232f), new Vector2(536f, 36f));
            UIFactory.HLayout(statRow, 8f, TextAnchor.MiddleLeft);
            if (offer.damage != 0) UIKit.Chip(statRow, "DANO  +" + offer.damage.ToString("0.#"), new Color(1f, 0.55f, 0.25f), 150f, "ChipDamage");
            if (offer.health != 0) UIKit.Chip(statRow, "VIDA  +" + offer.health.ToString("0.#"), UIKit.Col.Success, 150f, "ChipHealth");
            if (offer.defense != 0) UIKit.Chip(statRow, "DEFESA  +" + offer.defense.ToString("0.#"), new Color(0.40f, 0.70f, 1f), 170f, "ChipDefense");

            var coin = UIFactory.Node("PriceIcon", card);
            UIFactory.Place(coin, new Vector2(0f, 1f), new Vector2(24f, -296f), new Vector2(26f, 26f));
            UIKit.Diamond(coin, UIKit.Col.Gold, 26f, "Diamond");
            MapText(card, offer.price.ToString("N0") + " OURO", 26, TextAlignmentOptions.MidlineLeft,
                canAfford ? UIKit.Col.Gold : new Color(1f, 0.38f, 0.32f), 60f, 288f, 270f, 42f, "OfferPrice").fontStyle = FontStyles.Bold;
            int offerIndex = i;
            var buy = UIKit.Button(card, "COMPRAR", () => BuyShopOffer(offerIndex),
                canAfford ? UIKit.BtnStyle.Primary : UIKit.BtnStyle.Secondary, 20, "BuyOfferButton");
            UIFactory.Place((RectTransform)buy.transform, new Vector2(0f, 1f), new Vector2(350f, -286f), new Vector2(204f, 52f));
        }
        Show("shop");
    }

    void BuyShopOffer(int index)
    {
        var save = GameSession.Save;
        if (save == null) return;
        if (save.inventory == null) save.inventory = new List<ItemInstance>();
        if (save.inventory.Count >= 60) { Toast("Mochila cheia. Libere espaço antes de comprar."); return; }
        ShopOfferInfo[] offers = GetShopOffers();
        if (index < 0 || index >= offers.Length) return;
        ShopOfferInfo offer = offers[index];
        if (save.gold < offer.price) { Toast("Ouro insuficiente para comprar este item."); return; }
        var item = new ItemInstance
        {
            uid = System.Guid.NewGuid().ToString("N"),
            name = offer.name,
            description = offer.description,
            slot = offer.slot,
            rarity = offer.rarity,
            bonusDamage = offer.damage,
            bonusHealth = offer.health,
            bonusDefense = offer.defense,
            sellValue = Mathf.Max(1, offer.price / 3),
            upgradeLevel = 0
        };
        save.gold -= offer.price;
        save.inventory.Add(item);
        try { if (!GameSession.DebugRun) SaveSystem.Save(save); }
        catch (System.Exception e) { Debug.LogWarning("Zombit: falha ao salvar compra: " + e.Message); }
        ZombitAudio.PlayUI();
        Toast("Comprado: " + offer.name);
        ShowShop();
    }
}
