using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Mochila acessivel pelo mapa (parte de MainMenuController).
//
// Layout 100% ancorado: a area de conteudo acompanha a altura/largura real da janela
// (antes usava coordenadas fixas de 1080 e cortava os botoes em janelas mais baixas).
//
//  +-------------------------------------------+------------------+
//  | EQUIPAMENTO (6 slots + retratos) | ATRIBUTOS |  DETALHES DO ITEM |
//  +-------------------------------------------+  (rolagem no meio, |
//  | MOCHILA DE CAMPO (abas + ordenar + grade)  |  botoes fixos      |
//  |                                            |  na base)          |
//  +-------------------------------------------+------------------+
public partial class MainMenuController
{
    // ---------- constantes de layout ----------
    static readonly Vector2 TL = new Vector2(0f, 1f);
    static readonly Vector2 InvBL = new Vector2(0f, 0f);
    static readonly Vector2 InvTR = new Vector2(1f, 1f);

    const int BackpackCapacity = 60;
    const int InvAll = -1;
    const float InvDetailsW = 600f;   // largura do painel de detalhes (colado a direita)
    const float InvEquipW = 376f;     // largura do painel de equipamento (justo: slots + boneco em pe ~160 px)
    const float InvTopH = 450f;       // altura da linha equipamento + atributos
    const float InvGap = 12f;

    // ---------- estado da tela (persiste enquanto o menu esta aberto) ----------
    enum InvSort { Rarity, Recent, Damage, Health, Defense, Value, Upgrade }
    static readonly string[] InvSortLabels = { "RARIDADE", "MAIS RECENTES", "DANO", "VIDA", "DEFESA", "VALOR", "MELHORIA" };
    static readonly ItemSlot[] InvCategorySlots =
        { ItemSlot.Capacete, ItemSlot.Colete, ItemSlot.Calca, ItemSlot.Botas, ItemSlot.Mochila, ItemSlot.Arma };
    static readonly string[] InvCategoryNames =
        { "CAPACETES", "COLETES", "CALÇAS", "BOTAS", "MOCHILAS", "MELHORIAS" };

    // Duplo clique: ShowMapInventory recria os botoes a cada clique (zera o clickCount do EventSystem),
    // entao detectamos por item + tempo real.
    const float MapDoubleClickWindow = 0.35f;
    string lastMapClickUid;
    float lastMapClickTime = -10f;

    bool IsMapDoubleClick(string uid)
    {
        float now = Time.unscaledTime;
        bool dbl = !string.IsNullOrEmpty(uid) && uid == lastMapClickUid && now - lastMapClickTime <= MapDoubleClickWindow;
        lastMapClickUid = dbl ? null : uid;
        lastMapClickTime = now;
        return dbl;
    }

    int invCategory = InvAll;                 // aba da mochila (InvAll ou (int)ItemSlot)
    InvSort invSort = InvSort.Rarity;         // ordenacao da mochila
    string invSurvivorId;                     // sobrevivente que esta sendo editado (so UI: nao altera o esquadrao)

    // Define ancoras + offsets de uma vez (funciona tanto para esticar quanto para tamanho fixo).
    static RectTransform InvAnchor(RectTransform rt, float minX, float minY, float maxX, float maxY,
        float offMinX, float offMinY, float offMaxX, float offMaxY)
    {
        rt.anchorMin = new Vector2(minX, minY);
        rt.anchorMax = new Vector2(maxX, maxY);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(offMinX, offMinY);
        rt.offsetMax = new Vector2(offMaxX, offMaxY);
        return rt;
    }

    // ---------- tela principal ----------
    /// <param name="selectedUid">Item selecionado (na mochila ou equipado no sobrevivente atual).</param>
    /// <param name="fresh">true ao entrar pela aba MOCHILA: volta a editar o sobrevivente principal.</param>
    void ShowMapInventory(string selectedUid = null, bool fresh = false)
    {
        var save = GameSession.Save;
        if (save == null) { Show("main"); return; }
        if (save.inventory == null) save.inventory = new List<ItemInstance>();
        save.MigrateFromPreviousVersions();
        EnsureMapItemIds(save);

        if (fresh || string.IsNullOrEmpty(invSurvivorId) || !GameDatabase.Survivors.Exists(sv => sv.id == invSurvivorId))
            invSurvivorId = save.selectedSurvivor;

        var progress = save.GetProgress(invSurvivorId);
        List<ItemInstance> visibleBag = GetVisibleBagItems(save);

        // Selecao: o item pode estar equipado (slots) ou na mochila (grade filtrada).
        ItemInstance equippedSel = FindMapItem(progress.equipped, selectedUid);
        ItemInstance selectedItem = equippedSel ?? FindMapItem(save.inventory, selectedUid);
        bool selectedEquipped = equippedSel != null;
        if (selectedItem == null || (!selectedEquipped && !visibleBag.Contains(selectedItem)))
        {
            selectedItem = visibleBag.Count > 0 ? visibleBag[0] : null;
            selectedEquipped = false;
        }
        mapInventorySelectedUid = selectedItem != null ? selectedItem.uid : null;

        BuildPcInventoryScreen(save, visibleBag, selectedItem, selectedEquipped);
    }

    // ---------- mochila: filtro por categoria + ordenacao ----------
    List<ItemInstance> GetVisibleBagItems(SaveData save)
    {
        var list = new List<ItemInstance>();
        var order = new Dictionary<ItemInstance, int>();
        for (int n = 0; n < save.inventory.Count; n++)
        {
            var it = save.inventory[n];
            if (it == null) continue;
            order[it] = n;
            if (invCategory != InvAll && (int)it.slot != invCategory) continue;
            list.Add(it);
        }

        IOrderedEnumerable<ItemInstance> q;
        switch (invSort)
        {
            case InvSort.Recent:
                q = list.OrderByDescending(x => order[x]); break;
            case InvSort.Damage:
                q = list.OrderByDescending(x => x.bonusDamage).ThenByDescending(x => (int)x.rarity); break;
            case InvSort.Health:
                q = list.OrderByDescending(x => x.bonusHealth).ThenByDescending(x => (int)x.rarity); break;
            case InvSort.Defense:
                q = list.OrderByDescending(x => x.bonusDefense).ThenByDescending(x => (int)x.rarity); break;
            case InvSort.Value:
                q = list.OrderByDescending(x => x.sellValue).ThenByDescending(x => (int)x.rarity); break;
            case InvSort.Upgrade:
                q = list.OrderByDescending(x => x.upgradeLevel).ThenByDescending(x => (int)x.rarity); break;
            default:
                q = list.OrderByDescending(x => (int)x.rarity).ThenByDescending(x => x.upgradeLevel)
                        .ThenByDescending(x => x.bonusDamage + x.bonusHealth * 0.1f + x.bonusDefense); break;
        }
        // Desempate estavel: ordem original na mochila (mais antigo primeiro, exceto em "mais recentes").
        return invSort == InvSort.Recent ? q.ToList() : q.ThenBy(x => order[x]).ToList();
    }

    void BuildMapBackpackPanel(RectTransform panel, SaveData save, List<ItemInstance> visible, string selectedUid)
    {
        // Cabecalho: contador + botao de ordenar (canto direito).
        var sortBtn = UIKit.Button(panel, "ORDENAR: " + InvSortLabels[(int)invSort], () =>
        {
            invSort = (InvSort)(((int)invSort + 1) % InvSortLabels.Length);
            ShowMapInventory(mapInventorySelectedUid);
        }, UIKit.BtnStyle.Secondary, 16, "InventorySortButton");
        UIFactory.Place((RectTransform)sortBtn.transform, InvTR, new Vector2(-14f, -9f), new Vector2(310f, 38f));

        var count = UIFactory.Txt(panel, save.inventory.Count + " / " + BackpackCapacity + "  ITENS", UIKit.Size.Caption,
            TextAlignmentOptions.MidlineRight, UIKit.Col.TextDim, "InventoryPanelCount");
        count.textWrappingMode = TextWrappingModes.NoWrap;
        UIFactory.Place(count.rectTransform, InvTR, new Vector2(-(14f + 310f + 12f), -9f), new Vector2(200f, 38f));

        // Abas por tipo de drop (se dividem a largura disponivel).
        int total = save.inventory.Count(it => it != null);
        var tabs = UIFactory.Node("InventoryCategoryTabs", panel);
        InvAnchor(tabs, 0f, 1f, 1f, 1f, 14f, -104f, -14f, -62f);
        var hl = UIFactory.HLayout(tabs, 6f, TextAnchor.MiddleCenter);
        hl.childForceExpandWidth = true;
        hl.childForceExpandHeight = true;
        AddInvCategoryTab(tabs, InvAll, "TODOS", total);
        for (int c = 0; c < InvCategorySlots.Length; c++)
        {
            ItemSlot slot = InvCategorySlots[c];
            int n = save.inventory.Count(it => it != null && it.slot == slot);
            AddInvCategoryTab(tabs, (int)slot, InvCategoryNames[c], n);
        }

        // Grade com rolagem. As colunas se ajustam a largura (GridLayoutGroup flexivel).
        var viewport = UIFactory.Node("MapItemsViewport", panel);
        InvAnchor(viewport, 0f, 0f, 1f, 1f, 14f, 14f, -14f, -112f);
        var maskImage = viewport.gameObject.AddComponent<Image>();
        maskImage.color = new Color(0f, 0f, 0f, 0.001f);
        maskImage.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();

        var gridRoot = UIFactory.Node("MapItemsContent", viewport);
        gridRoot.anchorMin = new Vector2(0f, 1f);
        gridRoot.anchorMax = new Vector2(1f, 1f);
        gridRoot.pivot = new Vector2(0.5f, 1f);
        gridRoot.anchoredPosition = Vector2.zero;
        gridRoot.sizeDelta = Vector2.zero;
        var grid = gridRoot.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(74f, 74f);
        grid.spacing = new Vector2(6f, 6f);
        grid.padding = new RectOffset(6, 6, 6, 6);
        grid.constraint = GridLayoutGroup.Constraint.Flexible;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;
        var fitter = gridRoot.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = gridRoot;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 34f;
        scroll.inertia = true;

        for (int i = 0; i < visible.Count; i++)
        {
            ItemInstance item = visible[i];
            string uid = item.uid;
            CreateItemTile(gridRoot, item, null, uid == selectedUid, () =>
            {
                // Duplo clique num item da mochila: equipa direto.
                if (IsMapDoubleClick(uid)) { EquipMapItem(uid); return; }
                ShowMapInventory(uid);
            }, "Item_" + uid);
        }
        // Casas vazias so na aba TODOS (mostra a capacidade total da mochila).
        if (invCategory == InvAll)
            for (int e = visible.Count; e < BackpackCapacity; e++) CreateEmptyCell(gridRoot, e);

        if (visible.Count == 0 && invCategory != InvAll)
        {
            var msg = UIFactory.Txt(viewport, "NENHUM ITEM NESTA CATEGORIA", UIKit.Size.Label,
                TextAlignmentOptions.Center, UIKit.Col.TextDim, "InventoryEmptyMessage");
            msg.textWrappingMode = TextWrappingModes.NoWrap;
            InvAnchor(msg.rectTransform, 0.1f, 0.35f, 0.9f, 0.65f, 0f, 0f, 0f, 0f);
        }
    }

    void AddInvCategoryTab(RectTransform tabs, int category, string label, int count)
    {
        var tab = UIKit.Tab(tabs, label + "  " + count, category == invCategory, () =>
        {
            invCategory = category;
            ShowMapInventory(mapInventorySelectedUid);
        }, "InvCat_" + label);
        var le = UIFactory.Elem(tab.gameObject, 60f, 40f);
        le.flexibleWidth = 1f;
    }

    void CreateEmptyCell(Transform parent, int index)
    {
        var cell = UIFactory.Img(parent, new Color(0.03f, 0.05f, 0.065f, 0.9f), "Empty_" + index);
        UIKit.ApplyChamfer(cell, cell.color);
        cell.raycastTarget = false;
    }

    /// <summary>Casa de item (mochila e slots de equipamento): borda por raridade, ícone e nível de melhoria.</summary>
    Button CreateItemTile(Transform parent, ItemInstance item, Sprite emptyIcon, bool selected,
        System.Action onClick, string name)
    {
        Color rc = item != null ? RarityColors.Get(item.rarity) : UIKit.Col.Border;
        Color border = selected ? UIKit.Col.Gold
            : item != null ? Color.Lerp(rc, UIKit.Col.Border, 0.3f) : UIKit.Col.Border;
        border.a = 1f;
        var b = UIKit.Button(parent, "", onClick, UIKit.BtnStyle.Secondary, 10, name);
        UIFactory.SetButtonColor(b, border);
        var lbl = b.GetComponentInChildren<TMP_Text>(true);
        if (lbl) lbl.text = "";

        Color fillColor = selected ? new Color(0.20f, 0.11f, 0.06f, 1f)
            : item != null ? Color.Lerp(new Color(0.045f, 0.07f, 0.085f, 1f), rc, 0.14f)
            : new Color(0.03f, 0.05f, 0.065f, 1f);
        var fill = UIFactory.Img(b.transform, fillColor, "Fill");
        UIKit.ApplyChamfer(fill, fillColor);
        UIFactory.Inset(UIFactory.Stretch(fill.rectTransform), 2f, 2f);
        fill.raycastTarget = false;
        fill.transform.SetAsFirstSibling();

        var icon = UIFactory.Img(b.transform, Color.white, "Icon");
        UIFactory.Stretch(icon.rectTransform, 0.1f, 0.1f, 0.9f, 0.9f);
        icon.sprite = item != null ? ItemVisual.Icon(item) : emptyIcon;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.color = item != null ? ItemVisual.Tint(item) : new Color(0.56f, 0.65f, 0.58f, 0.35f);
        icon.enabled = icon.sprite != null;

        if (item != null)
        {
            var bar = UIFactory.Img(b.transform, rc, "RarityBar");
            var br = bar.rectTransform;
            br.anchorMin = new Vector2(0f, 0f); br.anchorMax = new Vector2(1f, 0f); br.pivot = new Vector2(0.5f, 0f);
            br.offsetMin = new Vector2(8f, 3f); br.offsetMax = new Vector2(-8f, 6f);
            bar.raycastTarget = false;
            if (item.upgradeLevel > 0)
            {
                var up = UIFactory.Txt(b.transform, "+" + item.upgradeLevel, UIKit.Size.Caption,
                    TextAlignmentOptions.TopRight, UIKit.Col.Gold, "Upgrade");
                up.fontStyle = FontStyles.Bold;
                up.textWrappingMode = TextWrappingModes.NoWrap;
                UIFactory.Stretch(up.rectTransform, 0f, 0.55f, 0.94f, 0.98f);
            }
        }
        return b;
    }

    ItemInstance GetEquippedMapItem(SaveData save, ItemSlot slot)
    {
        if (save == null) return null;
        var progress = save.GetProgress(invSurvivorId);
        if (progress.equipped == null) return null;
        for (int n = 0; n < progress.equipped.Count; n++)
            if (progress.equipped[n] != null && progress.equipped[n].slot == slot) return progress.equipped[n];
        return null;
    }

    // ---------- equipamento: 3 linhas x 2 colunas + manequim pequeno + retratos ----------
    //   CAPACETE | COLETE
    //   MOCHILA  | CALÇAS
    //   MELHORIA | BOTAS
    void BuildMapEquipmentPanel(RectTransform panel, SaveData save, string selectedUid)
    {
        BuildPcEquipmentPanel(panel, save, selectedUid);
    }

    void CreateMapEquipmentSlot(RectTransform parent, SaveData save, ItemSlot slot, string label,
        float x, float labelY, float size, string selectedUid)
    {
        var caption = UIFactory.Txt(parent, label, UIKit.Size.Caption - 1f, TextAlignmentOptions.Center,
            UIKit.Col.TextDim, "EquipLabel_" + slot);
        caption.textWrappingMode = TextWrappingModes.NoWrap;
        caption.overflowMode = TextOverflowModes.Overflow;
        UIFactory.Place(caption.rectTransform, TL, new Vector2(x - 14f, -labelY), new Vector2(size + 28f, 20f));

        ItemInstance item = GetEquippedMapItem(save, slot);
        bool selected = item != null && item.uid == selectedUid;
        var b = CreateItemTile(parent, item, ArtLoader.Get("Art/Items/" + slot.ToString().ToLowerInvariant()), selected, () =>
        {
            if (item == null)
            {
                // Slot vazio: abre a aba da mochila com os itens desse tipo para equipar.
                invCategory = (int)slot;
                ShowMapInventory(null);
                return;
            }
            // Duplo clique num item equipado: desequipa.
            if (IsMapDoubleClick(item.uid)) { UnequipMapItem(item.uid); return; }
            ShowMapInventory(item.uid);
        }, "EquipSlot_" + slot);
        UIFactory.Place((RectTransform)b.transform, TL, new Vector2(x, -(labelY + 19f)), new Vector2(size, size));
    }

    void BuildSurvivorChip(RectTransform strip, SurvivorDef def, SaveData save, float width)
    {
        bool active = def.id == invSurvivorId;
        string id = def.id;
        var b = UIKit.Button(strip, "", () =>
        {
            if (id == invSurvivorId) return;
            invSurvivorId = id;
            ShowMapInventory(null);
        }, UIKit.BtnStyle.Secondary, 12, "Survivor_" + id);
        UIFactory.Elem(b.gameObject, width, 78f);
        UIFactory.SetButtonColor(b, active ? UIKit.Col.Accent : UIKit.Col.Border);
        var lbl = b.GetComponentInChildren<TMP_Text>(true);
        if (lbl) lbl.text = "";

        Color fillColor = active ? new Color(0.20f, 0.09f, 0.10f, 1f) : new Color(0.045f, 0.07f, 0.085f, 1f);
        var fill = UIFactory.Img(b.transform, fillColor, "Fill");
        UIKit.ApplyChamfer(fill, fillColor);
        UIFactory.Inset(UIFactory.Stretch(fill.rectTransform), 2f, 2f);
        fill.raycastTarget = false;
        fill.transform.SetAsFirstSibling();

        var portrait = UIFactory.Img(b.transform, Color.white, "Portrait");
        UIFactory.Stretch(portrait.rectTransform, 0.10f, 0.32f, 0.90f, 0.95f);
        portrait.sprite = SurvivorPortraitSprite(def);
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;
        portrait.color = portrait.sprite != null ? Color.white : def.color;

        var nameText = UIFactory.Txt(b.transform, def.name.ToUpperInvariant(), 11f, TextAlignmentOptions.Center,
            active ? UIKit.Col.Text : UIKit.Col.TextDim, "Name");
        nameText.fontStyle = FontStyles.Bold;
        nameText.textWrappingMode = TextWrappingModes.NoWrap;
        nameText.enableAutoSizing = true;
        nameText.fontSizeMax = nameText.fontSize;
        nameText.fontSizeMin = 8f;
        UIFactory.Stretch(nameText.rectTransform, 0.02f, 0.02f, 0.98f, 0.30f);

        var lvText = UIFactory.Txt(b.transform, "LV." + SurvivorLevel(save, id), 11f, TextAlignmentOptions.TopRight,
            UIKit.Col.Gold, "Level");
        lvText.fontStyle = FontStyles.Bold;
        lvText.textWrappingMode = TextWrappingModes.NoWrap;
        lvText.enableAutoSizing = true;
        lvText.fontSizeMax = lvText.fontSize;
        lvText.fontSizeMin = 8f;
        UIFactory.Stretch(lvText.rectTransform, 0.40f, 0.76f, 0.96f, 0.98f);
    }

    static int SurvivorLevel(SaveData save, string id)
    {
        // Leitura sem criar entrada de progresso para quem ainda nao jogou.
        if (save.progress != null)
            foreach (var p in save.progress)
                if (p != null && p.id == id) return Mathf.Max(1, p.level);
        return 1;
    }

    static Sprite SurvivorPortraitSprite(SurvivorDef def)
    {
        if (def == null) return null;
        Sprite sp = def.portrait;
        if (sp == null) sp = ArtLoader.Get("Art/Characters/" + def.id + "_portrait");
        if (sp == null) sp = ArtLoader.Get("Art/Characters/" + def.id + "_card");
        return sp;
    }

    // ---------- atributos ----------
    void StatBlock(RectTransform grid, string label, string value, string sub, Color color, int col, int row)
    {
        var f = UIKit.Frame(grid, "Stat_" + label, new Color(0.05f, 0.08f, 0.10f, 1f),
            Color.Lerp(color, UIKit.Col.Border, 0.6f), 1.5f);
        InvAnchor(f, col * 0.5f, 1f - (row + 1) * 0.5f, (col + 1) * 0.5f, 1f - row * 0.5f, 6f, 6f, -6f, -6f);

        var l = UIFactory.Txt(f, label, UIKit.Size.Caption, TextAlignmentOptions.MidlineLeft, UIKit.Col.TextDim, "Label");
        l.characterSpacing = 1.5f;
        l.textWrappingMode = TextWrappingModes.NoWrap;
        UIFactory.Stretch(l.rectTransform, 0.06f, 0.64f, 0.94f, 0.95f);

        var v = UIFactory.Txt(f, value, 38, TextAlignmentOptions.MidlineLeft, color, "Value");
        v.fontStyle = FontStyles.Bold;
        v.textWrappingMode = TextWrappingModes.NoWrap;
        UIFactory.Stretch(v.rectTransform, 0.06f, 0.27f, 0.94f, 0.66f);

        if (!string.IsNullOrEmpty(sub))
        {
            var sb = UIFactory.Txt(f, sub, UIKit.Size.Caption - 1f, TextAlignmentOptions.MidlineLeft, UIKit.Col.TextDim, "Sub");
            sb.textWrappingMode = TextWrappingModes.NoWrap;
            UIFactory.Stretch(sb.rectTransform, 0.06f, 0.04f, 0.94f, 0.28f);
        }
    }

    void BuildMapStatsPanel(RectTransform panel, SaveData save)
    {
        SurvivorDef survivor = GameDatabase.GetSurvivor(invSurvivorId);
        SurvivorProgress progress = save.GetProgress(invSurvivorId);
        string nick = GetPlayerNickname(save);

        // Identidade de quem esta sendo editado (muda ao clicar nos retratos).
        var nameText = MapText(panel, (survivor != null ? survivor.name : invSurvivorId).ToUpperInvariant(), 30,
            TextAlignmentOptions.MidlineLeft, UIKit.Col.Gold, 24f, 62f, 420f, 40f, "ProfileName");
        nameText.fontStyle = FontStyles.Bold;
        nameText.textWrappingMode = TextWrappingModes.NoWrap;
        string roleLine = survivor != null && !string.IsNullOrEmpty(survivor.role) ? survivor.role.ToUpperInvariant() : "";
        var cls = MapText(panel, (roleLine.Length > 0 ? roleLine + "  ·  " : "") + "JOGADOR  " + nick.ToUpperInvariant(),
            UIKit.Size.Caption, TextAlignmentOptions.MidlineLeft, UIKit.Col.TextDim, 24f, 102f, 560f, 24f, "ProfileClass");
        cls.textWrappingMode = TextWrappingModes.NoWrap;

        var lv = UIFactory.Txt(panel, "LV. " + progress.level, UIKit.Size.Title, TextAlignmentOptions.MidlineRight,
            UIKit.Col.Text, "ProfileLevel");
        lv.fontStyle = FontStyles.Bold;
        lv.textWrappingMode = TextWrappingModes.NoWrap;
        UIFactory.Place(lv.rectTransform, InvTR, new Vector2(-24f, -62f), new Vector2(160f, 40f));

        int xpNext = Mathf.Max(1, PlayerStats.XpForLevel(progress.level));
        var xpTxt = MapText(panel, "XP  " + progress.xp.ToString("N0") + " / " + xpNext.ToString("N0"), UIKit.Size.Caption,
            TextAlignmentOptions.MidlineLeft, UIKit.Col.TextDim, 24f, 130f, 400f, 22f, "ProfileXp");
        xpTxt.textWrappingMode = TextWrappingModes.NoWrap;
        var xpFill = UIKit.Bar(panel, UIKit.Col.Cyan, (float)progress.xp / xpNext, "ProfileXpBar");
        InvAnchor((RectTransform)xpFill.transform.parent, 0f, 1f, 1f, 1f, 24f, -166f, -24f, -156f);

        float itemHealth = 0f, itemDamage = 0f, itemDefense = 0f;
        if (progress.equipped != null)
            foreach (var item in progress.equipped)
                if (item != null) { itemHealth += item.bonusHealth; itemDamage += item.bonusDamage; itemDefense += item.bonusDefense; }
        float classHealth = survivor != null ? survivor.healthMult : 1f;
        float hp = (100f + 20f * Mathf.Max(0, progress.level - 1)) * classHealth + itemHealth;
        float damage = 2f * Mathf.Max(0, progress.level - 1) + itemDamage;
        float defense = Mathf.Max(0, progress.level - 1) + itemDefense;

        var grid = UIFactory.Node("StatsGrid", panel);
        InvAnchor(grid, 0f, 0f, 1f, 1f, 18f, 12f, -18f, -180f);
        const string signed = "+0.#;-0.#;0";
        StatBlock(grid, "VIDA", Mathf.RoundToInt(hp).ToString(), "ITENS  " + itemHealth.ToString(signed), UIKit.Col.Success, 0, 0);
        StatBlock(grid, "DANO", damage.ToString("0.#"), "ITENS  " + itemDamage.ToString(signed), new Color(1f, 0.55f, 0.25f), 1, 0);
        StatBlock(grid, "DEFESA", defense.ToString("0.#"), "ITENS  " + itemDefense.ToString(signed), new Color(0.40f, 0.70f, 1f), 0, 1);
        StatBlock(grid, "PONTOS DE HABILIDADE", progress.skillPoints.ToString(), "PARA GASTAR NA ÁRVORE", UIKit.Col.Gold, 1, 1);
    }

    void EnsureMapItemIds(SaveData save)
    {
        bool changed = false;
        if (save.inventory != null)
            foreach (var item in save.inventory)
                if (item != null && string.IsNullOrEmpty(item.uid)) { item.uid = System.Guid.NewGuid().ToString("N"); changed = true; }
        if (save.progress != null)
            foreach (var progress in save.progress)
                if (progress != null && progress.equipped != null)
                    foreach (var item in progress.equipped)
                        if (item != null && string.IsNullOrEmpty(item.uid)) { item.uid = System.Guid.NewGuid().ToString("N"); changed = true; }
        if (changed && !GameSession.DebugRun) SaveSystem.Save(save);
    }

    static ItemInstance FindMapItem(List<ItemInstance> items, string uid)
    {
        if (items == null || string.IsNullOrEmpty(uid)) return null;
        for (int n = 0; n < items.Count; n++)
            if (items[n] != null && items[n].uid == uid) return items[n];
        return null;
    }

    static string ItemDescriptionOrDefault(ItemInstance item)
    {
        string d = item.description;
        if (string.IsNullOrWhiteSpace(d) || d.Trim().StartsWith("Item de exemplo"))
            return "Equipamento tático de " + item.slot.ToString().ToLowerInvariant() + ".";
        return d;
    }

    // ---------- detalhes do item ----------
    // Estrutura vertical do painel (nada fica cortado, qualquer que seja a altura da janela):
    //   topo fixo   : icone, nome, raridade
    //   meio        : descricao + atributos + afixos (area com rolagem, so se faltar espaco)
    //   base fixa   : valor de venda, MELHORAR, EQUIPAR/VENDER ou DESEQUIPAR (ancorados embaixo)
    void DetailStatRow(RectTransform parent, string label, float value, float compare, bool showDelta,
        string format, float y, float cw)
    {
        var sep = UIFactory.Img(parent, UIKit.Col.Border, "RowSeparator");
        UIFactory.Place(sep.rectTransform, TL, new Vector2(0f, -(y + 44f)), new Vector2(cw, 1f));
        sep.raycastTarget = false;
        var l = MapText(parent, label, UIKit.Size.Label, TextAlignmentOptions.MidlineLeft, UIKit.Col.TextDim,
            0f, y, 200f, 44f, "StatLabel_" + label);
        l.characterSpacing = 1.5f;
        l.textWrappingMode = TextWrappingModes.NoWrap;
        var v = MapText(parent, value.ToString(format), 26, TextAlignmentOptions.MidlineRight, UIKit.Col.Text,
            190f, y, 140f, 44f, "StatValue_" + label);
        v.fontStyle = FontStyles.Bold;
        v.textWrappingMode = TextWrappingModes.NoWrap;
        if (showDelta)
        {
            float delta = value - compare;
            if (Mathf.Abs(delta) > 0.001f)
            {
                var d = MapText(parent, "(" + delta.ToString(format) + ")", UIKit.Size.Body, TextAlignmentOptions.MidlineLeft,
                    delta > 0f ? UIKit.Col.Success : new Color(1f, 0.38f, 0.32f), 346f, y, cw - 346f, 44f, "StatDelta_" + label);
                d.fontStyle = FontStyles.Bold;
                d.textWrappingMode = TextWrappingModes.NoWrap;
            }
        }
    }

    void BuildMapItemDetails(RectTransform panel, ItemInstance item, bool equipped, SaveData save)
    {
        const float pad = 28f;
        float cw = InvDetailsW - pad * 2f;

        if (item == null)
        {
            var et = UIFactory.Txt(panel, "SELECIONE UM ITEM", UIKit.Size.Title, TextAlignmentOptions.Center,
                UIKit.Col.TextDim, "DetailsEmptyTitle");
            et.fontStyle = FontStyles.Bold;
            InvAnchor(et.rectTransform, 0.08f, 0.50f, 0.92f, 0.58f, 0f, 0f, 0f, 0f);
            var eb = UIFactory.Txt(panel, "Escolha um item na mochila ou nos slots para ver atributos, comparar com o equipado e agir.",
                UIKit.Size.Body, TextAlignmentOptions.Top, UIKit.Col.TextDim, "DetailsEmpty");
            eb.textWrappingMode = TextWrappingModes.Normal;
            InvAnchor(eb.rectTransform, 0.12f, 0.38f, 0.88f, 0.49f, 0f, 0f, 0f, 0f);
            return;
        }

        string uid = item.uid;
        Color rc = RarityColors.Get(item.rarity);

        // ---- topo fixo ----
        var iconFrame = UIKit.Frame(panel, "SelectedItemFrame", new Color(0.035f, 0.055f, 0.068f, 1f),
            Color.Lerp(rc, UIKit.Col.Border, 0.2f), 2.5f);
        UIFactory.Place(iconFrame, TL, new Vector2(pad, -70f), new Vector2(120f, 120f));
        var icon = UIFactory.Img(iconFrame, Color.white, "SelectedItemIcon");
        icon.sprite = ItemVisual.Icon(item);
        icon.color = ItemVisual.Tint(item);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        UIFactory.Inset(UIFactory.Stretch(icon.rectTransform), 12f, 12f);

        float textX = pad + 120f + 16f;
        var title = MapText(panel, item.name, 30, TextAlignmentOptions.MidlineLeft, rc, textX, 72f,
            InvDetailsW - textX - pad, 48f, "SelectedItemName");
        title.fontStyle = FontStyles.Bold;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.overflowMode = TextOverflowModes.Truncate;
        var chip = UIKit.Chip(panel, RarityMapLabel(item.rarity) + "  ·  " + item.slot.ToString().ToUpperInvariant() +
            (item.upgradeLevel > 0 ? "  +" + item.upgradeLevel : ""), rc, 330f, "SelectedItemRarity");
        UIFactory.Place(chip, TL, new Vector2(textX, -126f), new Vector2(Mathf.Min(330f, InvDetailsW - textX - pad), 36f));

        // ---- meio: area com rolagem entre o topo e a base fixa ----
        var viewport = UIFactory.Node("DetailsViewport", panel);
        InvAnchor(viewport, 0f, 0f, 1f, 1f, pad, 190f, -pad, -202f);
        var vpImg = viewport.gameObject.AddComponent<Image>();
        vpImg.color = new Color(0f, 0f, 0f, 0.001f);
        vpImg.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();

        var body = UIFactory.Node("DetailsBody", viewport);
        body.anchorMin = new Vector2(0f, 1f);
        body.anchorMax = new Vector2(1f, 1f);
        body.pivot = new Vector2(0.5f, 1f);
        body.anchoredPosition = Vector2.zero;
        body.sizeDelta = new Vector2(0f, 300f);
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = body;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        var desc = MapText(body, ItemDescriptionOrDefault(item), UIKit.Size.Body, TextAlignmentOptions.TopLeft,
            new Color(0.80f, 0.86f, 0.88f), 0f, 0f, cw, 60f, "SelectedItemDescription");
        desc.textWrappingMode = TextWrappingModes.Normal;
        float descH = Mathf.Clamp(Mathf.Ceil(desc.GetPreferredValues(desc.text, cw, 0f).y) + 4f, 26f, 140f);
        desc.rectTransform.sizeDelta = new Vector2(cw, descH);

        float y = descH + 14f;
        var header = MapText(body, equipped ? "ATRIBUTOS" : "ATRIBUTOS  ·  COMPARADO COM O EQUIPADO",
            UIKit.Size.Caption, TextAlignmentOptions.MidlineLeft, UIKit.Col.Gold, 0f, y, cw, 26f, "StatsHeader");
        header.characterSpacing = 1.5f;
        header.textWrappingMode = TextWrappingModes.NoWrap;
        y += 32f;

        ItemInstance cmp = equipped ? null : GetEquippedMapItem(save, item.slot);
        bool showDelta = !equipped;
        const string fmt = "+0.#;-0.#;0";
        bool anyRow = false;
        if (item.bonusDamage != 0 || (cmp != null && cmp.bonusDamage != 0))
        { DetailStatRow(body, "DANO", item.bonusDamage, cmp != null ? cmp.bonusDamage : 0f, showDelta, fmt, y, cw); y += 48f; anyRow = true; }
        if (item.bonusHealth != 0 || (cmp != null && cmp.bonusHealth != 0))
        { DetailStatRow(body, "VIDA", item.bonusHealth, cmp != null ? cmp.bonusHealth : 0f, showDelta, fmt, y, cw); y += 48f; anyRow = true; }
        if (item.bonusDefense != 0 || (cmp != null && cmp.bonusDefense != 0))
        { DetailStatRow(body, "DEFESA", item.bonusDefense, cmp != null ? cmp.bonusDefense : 0f, showDelta, fmt, y, cw); y += 48f; anyRow = true; }
        if (!anyRow && !item.HasAffixes)
        {
            MapText(body, "Sem bônus de atributos.", UIKit.Size.Body, TextAlignmentOptions.MidlineLeft,
                UIKit.Col.TextDim, 0f, y, 400f, 40f, "NoStats");
            y += 48f;
        }
        // Afixos (aleatorios e assinatura de itens exclusivos do Pesadelo)
        if (item.HasAffixes)
        {
            for (int ai = 0; ai < item.affixes.Count; ai++)
            {
                var af = item.affixes[ai];
                if (af == null) continue;
                var line = MapText(body, "◆  " + af.Describe(), UIKit.Size.Label, TextAlignmentOptions.MidlineLeft,
                    new Color(0.55f, 0.85f, 1f), 0f, y, cw, 30f, "Affix_" + ai);
                line.textWrappingMode = TextWrappingModes.NoWrap;
                y += 30f;
            }
            if (item.nightmare)
            {
                MapText(body, "EXCLUSIVO DO PESADELO", UIKit.Size.Caption, TextAlignmentOptions.MidlineLeft,
                    new Color(0.95f, 0.30f, 0.30f), 0f, y + 2f, 400f, 24f, "NightmareTag");
                y += 28f;
            }
            anyRow = true;
        }
        if (!equipped && cmp == null && anyRow)
        {
            MapText(body, "Nenhum item equipado neste slot.", UIKit.Size.Caption, TextAlignmentOptions.MidlineLeft,
                UIKit.Col.TextDim, 0f, y + 4f, 500f, 26f, "NoCompareNote");
            y += 32f;
        }
        body.sizeDelta = new Vector2(0f, y + 8f);

        // ---- base fixa (ancorada embaixo: nunca e cortada) ----
        var coin = UIFactory.Node("SellIcon", panel);
        UIFactory.Place(coin, InvBL, new Vector2(pad + 2f, 152f), new Vector2(24f, 24f));
        UIKit.Diamond(coin, UIKit.Col.Gold, 24f, "Diamond");
        var sell = UIFactory.Txt(panel, "VALOR DE VENDA   " + Mathf.Max(1, item.sellValue) + " OURO", UIKit.Size.Label,
            TextAlignmentOptions.MidlineLeft, UIKit.Col.Gold, "SellValue");
        sell.fontStyle = FontStyles.Bold;
        sell.textWrappingMode = TextWrappingModes.NoWrap;
        UIFactory.Place(sell.rectTransform, InvBL, new Vector2(pad + 38f, 144f), new Vector2(cw - 38f, 40f));

        // Oficina: melhora o item com SUCATA (+8% nos atributos por nivel; maximo depende da raridade).
        int upCost = ItemUpgrade.Cost(item), upGold = ItemUpgrade.GoldCost(item);
        bool maxed = item.upgradeLevel >= ItemUpgrade.MaxFor(item);
        string upLabel = maxed ? "NÍVEL MÁXIMO  +" + item.upgradeLevel
            : "MELHORAR  +" + item.upgradeLevel + " → +" + (item.upgradeLevel + 1) + "   ·   " + upCost + " SUCATA + " + upGold + " OURO";
        var up = UIKit.Button(panel, upLabel, () => UpgradeMapItem(uid), UIKit.BtnStyle.Primary, 22, "UpgradeMapItem");
        UIFactory.Place((RectTransform)up.transform, InvBL, new Vector2(pad, 84f), new Vector2(cw, 52f));
        up.interactable = !maxed && save.scrap >= upCost && save.gold >= upGold;

        if (equipped)
        {
            var un = UIKit.Button(panel, "DESEQUIPAR", () => UnequipMapItem(uid), UIKit.BtnStyle.Secondary, 24, "UnequipMapItem");
            UIFactory.Place((RectTransform)un.transform, InvBL, new Vector2(pad, 16f), new Vector2(cw, 60f));
        }
        else
        {
            float half = (cw - 12f) * 0.5f;
            var eq = UIKit.Button(panel, "EQUIPAR", () => EquipMapItem(uid), UIKit.BtnStyle.Success, 24, "EquipMapItem");
            UIFactory.Place((RectTransform)eq.transform, InvBL, new Vector2(pad, 16f), new Vector2(half, 60f));
            var sl = UIKit.Button(panel, "VENDER", () => SellMapItem(uid), UIKit.BtnStyle.Danger, 24, "SellMapItem");
            UIFactory.Place((RectTransform)sl.transform, InvBL, new Vector2(pad + half + 12f, 16f), new Vector2(half, 60f));
        }
    }

    // ---------- acoes ----------
    void UpgradeMapItem(string uid)
    {
        var save = GameSession.Save;
        if (save == null) return;
        ItemInstance item = null;
        if (save.inventory != null)
            foreach (var it in save.inventory) if (it != null && it.uid == uid) { item = it; break; }
        if (item == null && save.progress != null)
            foreach (var p in save.progress)
            {
                if (p == null || p.equipped == null) continue;
                foreach (var it in p.equipped) if (it != null && it.uid == uid) { item = it; break; }
                if (item != null) break;
            }
        if (item == null) return;
        if (!ItemUpgrade.TryUpgrade(save, item, out string msg)) { Toast(msg); return; }
        if (!GameSession.DebugRun) SaveSystem.Save(save);
        Toast(msg);
        ShowMapInventory(item.uid);
    }

    // Equipa no sobrevivente que esta sendo editado. O item sai da mochila; o que estava no slot volta para ela.
    void EquipMapItem(string uid)
    {
        var save = GameSession.Save;
        if (save == null || save.inventory == null) return;
        var progress = save.GetProgress(invSurvivorId);
        ItemInstance selectedItem = null;
        for (int n = 0; n < save.inventory.Count; n++)
            if (save.inventory[n] != null && save.inventory[n].uid == uid) { selectedItem = save.inventory[n]; break; }
        if (selectedItem == null) return;
        if (progress.equipped == null) progress.equipped = new List<ItemInstance>();

        ItemInstance old = null;
        for (int n = 0; n < progress.equipped.Count; n++)
            if (progress.equipped[n] != null && progress.equipped[n].slot == selectedItem.slot) { old = progress.equipped[n]; break; }
        save.inventory.Remove(selectedItem);
        if (old != null) { progress.equipped.Remove(old); save.inventory.Add(old); }
        progress.equipped.Add(selectedItem);
        if (!GameSession.DebugRun) SaveSystem.Save(save);
        Toast("Equipado: " + selectedItem.name);
        ShowMapInventory(selectedItem.uid);
    }

    // Tira do sobrevivente editado e devolve para a mochila (abre a aba do tipo para o item aparecer).
    void UnequipMapItem(string uid)
    {
        var save = GameSession.Save;
        if (save == null || save.inventory == null) return;
        if (save.inventory.Count >= BackpackCapacity) { Toast("Mochila cheia. Venda ou equipe algum item primeiro."); return; }
        var progress = save.GetProgress(invSurvivorId);
        if (progress.equipped == null) return;
        ItemInstance item = null;
        for (int n = 0; n < progress.equipped.Count; n++)
            if (progress.equipped[n] != null && progress.equipped[n].uid == uid) { item = progress.equipped[n]; break; }
        if (item == null) return;
        progress.equipped.Remove(item);
        save.inventory.Add(item);
        if (invCategory != InvAll && invCategory != (int)item.slot) invCategory = (int)item.slot;
        if (!GameSession.DebugRun) SaveSystem.Save(save);
        Toast("Desequipado: " + item.name);
        ShowMapInventory(item.uid);
    }

    void SellMapItem(string uid)
    {
        var save = GameSession.Save;
        if (save == null || save.inventory == null) return;
        ItemInstance item = null;
        for (int n = 0; n < save.inventory.Count; n++)
            if (save.inventory[n] != null && save.inventory[n].uid == uid) { item = save.inventory[n]; break; }
        if (item == null) return;
        save.inventory.Remove(item);
        int value = Mathf.Max(1, item.sellValue);
        save.gold += value;
        if (!GameSession.DebugRun) SaveSystem.Save(save);
        Toast("Vendido por " + value + " de ouro.");
        ShowMapInventory(null);
    }

    static string RarityMapLabel(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Comum: return "COMUM";
            case Rarity.Incomum: return "INCOMUM";
            case Rarity.Raro: return "RARO";
            case Rarity.Epico: return "ÉPICO";
            case Rarity.Lendario: return "LENDÁRIO";
            default: return rarity.ToString().ToUpperInvariant();
        }
    }
}
