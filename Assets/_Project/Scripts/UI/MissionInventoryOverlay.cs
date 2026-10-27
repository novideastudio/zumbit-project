using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Mochila de dentro da wave ([I] ou botao MOCHILA do HUD).
///
/// Usa EXATAMENTE o mesmo layout, constantes e componentes (UIKit/UIFactory) da tela INVENTARIO TATICO do mapa
/// (MainMenuController.Inventory), so que ligada ao Inventory/PlayerStats da missao em vez do SaveData:
///   cabecalho | EQUIPAMENTO + ATRIBUTOS | MOCHILA DE CAMPO (abas + grade rolavel) | DETALHES DO ITEM (coluna direita)
///
/// Todo texto e de linha unica com truncamento/auto-ajuste, entao nada vaza para fora das molduras.
/// A missao NAO pausa enquanto a mochila esta aberta.
/// </summary>
public sealed class MissionInventoryOverlay : MonoBehaviour
{
    static MissionInventoryOverlay instance;
    static int closedFrame = -1;

    /// <summary>True enquanto aberta (e no quadro em que fechou, para o ESC nao abrir a pausa junto).</summary>
    public static bool IsOpen => instance != null || closedFrame == Time.frameCount;

    public static void Toggle()
    {
        if (instance != null) { instance.Close(); return; }
        // Nao abre por cima da pausa, de outro painel ou da tela de resultado.
        if (Time.timeScale == 0f) return;
        var flow = FindFirstObjectByType<MissionFlow>();
        if (flow != null && flow.IsFinished) return;
        var inv = FindFirstObjectByType<Inventory>();
        if (inv == null) return;
        instance = new GameObject("MissionInventoryOverlay").AddComponent<MissionInventoryOverlay>();
        instance.Open(inv);
    }

    // ---------- constantes de layout (iguais as do mapa) ----------
    static readonly Vector2 TL = new Vector2(0f, 1f);
    static readonly Vector2 InvBL = new Vector2(0f, 0f);
    static readonly Vector2 InvTR = new Vector2(1f, 1f);
    const int InvAll = -1;
    const float InvDetailsW = 600f;
    const float InvEquipW = 376f;   // justo: slots + boneco em pé (~160 px)
    const float InvTopH = 450f;
    const float InvGap = 12f;

    enum InvSort { Rarity, Recent, Damage, Health, Defense, Value, Upgrade }
    static readonly string[] InvSortLabels = { "RARIDADE", "MAIS RECENTES", "DANO", "VIDA", "DEFESA", "VALOR", "MELHORIA" };
    static readonly ItemSlot[] InvCategorySlots =
        { ItemSlot.Capacete, ItemSlot.Colete, ItemSlot.Calca, ItemSlot.Botas, ItemSlot.Mochila, ItemSlot.Arma };
    static readonly string[] InvCategoryNames =
        { "CAPACETES", "COLETES", "CALÇAS", "BOTAS", "MOCHILAS", "MELHORIAS" };

    // ---------- duplo clique ----------
    // O 1o clique reconstroi a UI (Rebuild) e recria os botoes, entao o clickCount do EventSystem
    // zera. Por isso o duplo clique e detectado aqui, por item + tempo real (o jogo esta pausado).
    const float DoubleClickWindow = 0.35f;
    ItemInstance lastClickItem;
    float lastClickTime = -10f;

    bool IsDoubleClick(ItemInstance item)
    {
        float now = Time.unscaledTime;
        bool dbl = item != null && item == lastClickItem && now - lastClickTime <= DoubleClickWindow;
        lastClickItem = dbl ? null : item;   // depois do duplo, o proximo clique recomeca a contagem
        lastClickTime = now;
        return dbl;
    }

    // ---------- estado ----------
    Inventory inv;
    PlayerStats stats;
    Canvas canvas;
    RectTransform screen;
    TMP_Text toast;
    Coroutine toastCo;

    ItemInstance selected;
    int category = InvAll;
    InvSort sort = InvSort.Rarity;

    void Open(Inventory inventory)
    {
        inv = inventory;
        stats = inv.stats ? inv.stats : FindFirstObjectByType<PlayerStats>();
        canvas = UIFactory.MakeCanvas("MissionInventoryCanvas", 105);
        canvas.transform.SetParent(transform, false);

        toast = UIFactory.Txt(canvas.transform, "", 30, TextAlignmentOptions.Center, UIFactory.Gold, "Toast");
        toast.textWrappingMode = TextWrappingModes.NoWrap;
        UIFactory.Place(toast.rectTransform, new Vector2(0.5f, 0.9f), new Vector2(0, -10), new Vector2(1200, 44));
        toast.gameObject.SetActive(false);

        Rebuild();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) Close();   // [I] e tratado pelo PanelToggle
    }

    void Close()
    {
        if (instance == this) { instance = null; closedFrame = Time.frameCount; }
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // ---------- util ----------
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

    /// <summary>Texto de linha unica: nunca quebra nem passa da caixa (reduz a fonte e, por fim, corta).</summary>
    static TMP_Text FitText(Transform parent, string value, float size, TextAlignmentOptions alignment,
        Color color, string name, float minSize = 9f)
    {
        var t = UIFactory.Txt(parent, value, size, alignment, color, name);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Truncate;
        t.enableAutoSizing = true;
        t.fontSizeMax = t.fontSize;
        t.fontSizeMin = Mathf.Min(minSize, t.fontSize);
        t.raycastTarget = false;
        return t;
    }

    static TMP_Text MapText(Transform parent, string value, float size, TextAlignmentOptions alignment,
        Color color, float x, float y, float width, float height, string name)
    {
        var text = FitText(parent, value, size, alignment, color, name);
        UIFactory.Place(text.rectTransform, TL, new Vector2(x, -y), new Vector2(width, height));
        return text;
    }

    void Toast(string msg)
    {
        if (!toast) return;
        toast.text = msg;
        toast.gameObject.SetActive(true);
        toast.transform.SetAsLastSibling();
        if (toastCo != null) StopCoroutine(toastCo);
        toastCo = StartCoroutine(HideToast());
    }

    IEnumerator HideToast()
    {
        yield return new WaitForSecondsRealtime(2.2f);
        if (toast) toast.gameObject.SetActive(false);
    }

    SaveData Save => GameSession.Save;
    ItemInstance Equipped(ItemSlot slot) => stats ? stats.GetEquipped(slot) : null;
    bool IsEquipped(ItemInstance it) => it != null && stats && stats.GetEquipped(it.slot) == it;

    // ---------- tela ----------
    void Rebuild()
    {
        if (screen) { screen.gameObject.SetActive(false); Destroy(screen.gameObject); }
        screen = UIFactory.Node("MissionInventory", canvas.transform);
        UIFactory.Stretch(screen);

        List<ItemInstance> visibleBag = GetVisibleBagItems();

        // Selecao: item equipado ou da mochila (grade filtrada). Sem selecao valida -> primeiro da lista.
        if (selected != null && !IsEquipped(selected) && !visibleBag.Contains(selected)) selected = null;
        if (selected == null && visibleBag.Count > 0) selected = visibleBag[0];
        bool selectedEquipped = IsEquipped(selected);

        BuildBackdrop();
        BuildHeader();

        var content = UIFactory.Node("InventoryContent", screen);
        InvAnchor(content, 0f, 0f, 1f, 0.915f, 20f, 14f, -20f, -12f);

        var detailsPanel = UIKit.Panel(content, "MapItemDetailsPanel", "Detalhes do item");
        InvAnchor(detailsPanel, 1f, 0f, 1f, 1f, -InvDetailsW, 0f, 0f, 0f);

        var left = UIFactory.Node("InventoryLeft", content);
        InvAnchor(left, 0f, 0f, 1f, 1f, 0f, 0f, -(InvDetailsW + InvGap), 0f);

        var topRow = UIFactory.Node("InventoryTopRow", left);
        InvAnchor(topRow, 0f, 1f, 1f, 1f, 0f, -InvTopH, 0f, 0f);

        var equipmentPanel = UIKit.Panel(topRow, "MapEquipmentPanel", "Equipamento do sobrevivente");
        InvAnchor(equipmentPanel, 0f, 0f, 0f, 1f, 0f, 0f, InvEquipW, 0f);

        var statsPanel = UIKit.Panel(topRow, "MapStatsPanel", "Atributos do sobrevivente");
        InvAnchor(statsPanel, 0f, 0f, 1f, 1f, InvEquipW + InvGap, 0f, 0f, 0f);

        var backpackPanel = UIKit.Panel(left, "MapBackpackPanel", "Mochila de campo");
        InvAnchor(backpackPanel, 0f, 0f, 1f, 1f, 0f, 0f, 0f, -(InvTopH + InvGap));

        BuildEquipmentPanel(equipmentPanel);
        BuildStatsPanel(statsPanel);
        BuildBackpackPanel(backpackPanel, visibleBag);
        BuildItemDetails(detailsPanel, selected, selectedEquipped);

        if (toast) toast.transform.SetAsLastSibling();
    }

    void BuildBackdrop()
    {
        var art = UIFactory.Img(screen, Color.white, "MissionInventoryBackdropArt");
        art.sprite = ArtLoader.Get(SceneBackgrounds.DefaultPath);
        art.preserveAspect = false;
        art.color = new Color(0.62f, 0.69f, 0.73f, 0.28f);
        UIFactory.Stretch(art.rectTransform);
        art.raycastTarget = false;
        // Bloqueia cliques na missao por tras da tela.
        var backdrop = UIFactory.Img(screen, new Color(0.007f, 0.013f, 0.019f, 0.92f), "MissionInventoryBackdrop");
        UIFactory.Stretch(backdrop.rectTransform);
        backdrop.raycastTarget = true;
    }

    void BuildHeader()
    {
        var header = UIFactory.Img(screen, UIKit.Col.Header, "NavHeader");
        UIFactory.Stretch(header.rectTransform, 0f, 0.915f, 1f, 1f);
        header.raycastTarget = false;

        var line = UIFactory.Img(header.transform, UIKit.Col.Accent, "NavHeaderLine");
        var lr = line.rectTransform;
        lr.anchorMin = new Vector2(0f, 0f); lr.anchorMax = new Vector2(1f, 0f); lr.pivot = new Vector2(0.5f, 0f);
        lr.offsetMin = Vector2.zero; lr.offsetMax = new Vector2(0f, 3f);
        line.raycastTarget = false;

        // Titulo (esquerda)
        var left = UIFactory.Node("NavLeft", header.transform);
        UIFactory.Stretch(left, 0f, 0f, 0.40f, 1f);
        left.offsetMin = new Vector2(28f, 8f); left.offsetMax = new Vector2(0f, -8f);
        var tick = UIFactory.Img(left, UIKit.Col.Accent, "TitleTick");
        UIFactory.Place(tick.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(6f, 44f));
        tick.raycastTarget = false;
        var t = FitText(left, "INVENTÁRIO TÁTICO", UIKit.Size.Title, TextAlignmentOptions.MidlineLeft, UIKit.Col.Text, "UtilityTitle", 16f);
        t.fontStyle = FontStyles.Bold;
        t.characterSpacing = 1.5f;
        UIFactory.Stretch(t.rectTransform);
        t.rectTransform.offsetMin = new Vector2(20f, 0f);

        // Ouro + sucata (centro/direita) e botao de fechar
        var right = UIFactory.Node("NavRight", header.transform);
        UIFactory.Stretch(right, 0.40f, 0f, 1f, 1f);
        right.offsetMin = new Vector2(0f, 14f); right.offsetMax = new Vector2(-24f, -14f);
        var h = UIFactory.HLayout(right, 10f, TextAnchor.MiddleRight);
        h.childForceExpandWidth = false; h.childForceExpandHeight = true;

        int scrap = Save != null ? Save.scrap : 0;
        var pill = UIKit.Frame(right, "InfoPill", new Color(0.06f, 0.09f, 0.11f, 1f), UIKit.Col.Border, 1.5f);
        UIFactory.Elem(pill.gameObject, 380f, 56f);
        var it = FitText(pill, "OURO  " + inv.Gold.ToString("N0") + "    ·    SUCATA  " + scrap.ToString("N0"),
            UIKit.Size.Label, TextAlignmentOptions.Center, UIKit.Col.Gold, "UtilityInfo", 12f);
        it.fontStyle = FontStyles.Bold;
        UIFactory.Stretch(it.rectTransform);
        it.rectTransform.offsetMin = new Vector2(10f, 0f);
        it.rectTransform.offsetMax = new Vector2(-10f, 0f);

        var close = UIKit.Button(right, "FECHAR  [I]", Close, UIKit.BtnStyle.Danger, UIKit.Size.Body, "CloseInventory");
        UIFactory.Elem(close.gameObject, 190f, 60f);
    }

    // ---------- mochila ----------
    List<ItemInstance> GetVisibleBagItems()
    {
        var list = new List<ItemInstance>();
        var order = new Dictionary<ItemInstance, int>();
        var items = inv.Items;
        for (int n = 0; n < items.Count; n++)
        {
            var it = items[n];
            if (it == null) continue;
            order[it] = n;
            if (category != InvAll && (int)it.slot != category) continue;
            list.Add(it);
        }

        IOrderedEnumerable<ItemInstance> q;
        switch (sort)
        {
            case InvSort.Recent: q = list.OrderByDescending(x => order[x]); break;
            case InvSort.Damage: q = list.OrderByDescending(x => x.bonusDamage).ThenByDescending(x => (int)x.rarity); break;
            case InvSort.Health: q = list.OrderByDescending(x => x.bonusHealth).ThenByDescending(x => (int)x.rarity); break;
            case InvSort.Defense: q = list.OrderByDescending(x => x.bonusDefense).ThenByDescending(x => (int)x.rarity); break;
            case InvSort.Value: q = list.OrderByDescending(x => x.sellValue).ThenByDescending(x => (int)x.rarity); break;
            case InvSort.Upgrade: q = list.OrderByDescending(x => x.upgradeLevel).ThenByDescending(x => (int)x.rarity); break;
            default:
                q = list.OrderByDescending(x => (int)x.rarity).ThenByDescending(x => x.upgradeLevel)
                        .ThenByDescending(x => x.bonusDamage + x.bonusHealth * 0.1f + x.bonusDefense); break;
        }
        return sort == InvSort.Recent ? q.ToList() : q.ThenBy(x => order[x]).ToList();
    }

    void BuildBackpackPanel(RectTransform panel, List<ItemInstance> visible)
    {
        int capacity = Mathf.Max(1, inv.capacity);

        var sortBtn = UIKit.Button(panel, "ORDENAR: " + InvSortLabels[(int)sort], () =>
        {
            sort = (InvSort)(((int)sort + 1) % InvSortLabels.Length);
            Rebuild();
        }, UIKit.BtnStyle.Secondary, 16, "InventorySortButton");
        UIFactory.Place((RectTransform)sortBtn.transform, InvTR, new Vector2(-14f, -9f), new Vector2(310f, 38f));

        var count = FitText(panel, inv.Items.Count + " / " + capacity + "  ITENS", UIKit.Size.Caption,
            TextAlignmentOptions.MidlineRight, UIKit.Col.TextDim, "InventoryPanelCount", 10f);
        UIFactory.Place(count.rectTransform, InvTR, new Vector2(-(14f + 310f + 12f), -9f), new Vector2(200f, 38f));

        int total = inv.Items.Count(it => it != null);
        var tabs = UIFactory.Node("InventoryCategoryTabs", panel);
        InvAnchor(tabs, 0f, 1f, 1f, 1f, 14f, -104f, -14f, -62f);
        var hl = UIFactory.HLayout(tabs, 6f, TextAnchor.MiddleCenter);
        hl.childForceExpandWidth = true;
        hl.childForceExpandHeight = true;
        AddCategoryTab(tabs, InvAll, "TODOS", total);
        for (int c = 0; c < InvCategorySlots.Length; c++)
        {
            ItemSlot slot = InvCategorySlots[c];
            int n = inv.Items.Count(it => it != null && it.slot == slot);
            AddCategoryTab(tabs, (int)slot, InvCategoryNames[c], n);
        }

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
            CreateItemTile(gridRoot, item, null, item == selected, () =>
            {
                // Duplo clique num item da mochila: equipa direto.
                if (IsDoubleClick(item)) { EquipItem(item); return; }
                selected = item;
                Rebuild();
            }, "Item_" + i);
        }
        if (category == InvAll)
            for (int e = visible.Count; e < capacity; e++) CreateEmptyCell(gridRoot, e);

        if (visible.Count == 0 && category != InvAll)
        {
            var msg = FitText(viewport, "NENHUM ITEM NESTA CATEGORIA", UIKit.Size.Label,
                TextAlignmentOptions.Center, UIKit.Col.TextDim, "InventoryEmptyMessage");
            InvAnchor(msg.rectTransform, 0.1f, 0.35f, 0.9f, 0.65f, 0f, 0f, 0f, 0f);
        }
    }

    void AddCategoryTab(RectTransform tabs, int cat, string label, int count)
    {
        var tab = UIKit.Tab(tabs, label + "  " + count, cat == category, () =>
        {
            category = cat;
            Rebuild();
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

    /// <summary>Casa de item (mochila e slots de equipamento): borda por raridade, icone e nivel de melhoria.</summary>
    Button CreateItemTile(Transform parent, ItemInstance item, Sprite emptyIcon, bool isSelected,
        System.Action onClick, string name)
    {
        Color rc = item != null ? RarityColors.Get(item.rarity) : UIKit.Col.Border;
        Color border = isSelected ? UIKit.Col.Accent
            : item != null ? Color.Lerp(rc, UIKit.Col.Border, 0.3f) : UIKit.Col.Border;
        border.a = 1f;
        var b = UIKit.Button(parent, "", onClick, UIKit.BtnStyle.Secondary, 10, name);
        UIFactory.SetButtonColor(b, border);
        var lbl = b.GetComponentInChildren<TMP_Text>(true);
        if (lbl) lbl.text = "";

        Color fillColor = isSelected ? new Color(0.20f, 0.11f, 0.06f, 1f)
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
                var up = FitText(b.transform, "+" + item.upgradeLevel, UIKit.Size.Caption,
                    TextAlignmentOptions.TopRight, UIKit.Col.Gold, "Upgrade", 8f);
                up.fontStyle = FontStyles.Bold;
                UIFactory.Stretch(up.rectTransform, 0f, 0.55f, 0.94f, 0.98f);
            }
        }
        return b;
    }

    // ---------- equipamento ----------
    //   CAPACETE | COLETE
    //   MOCHILA  | CALÇAS
    //   MELHORIA | BOTAS
    void BuildEquipmentPanel(RectTransform panel)
    {
        const float slotSize = 72f;
        const float margin = 24f;
        float rightX = InvEquipW - margin - slotSize;
        float mannequinX = margin + slotSize + 12f;
        float mannequinW = InvEquipW - mannequinX * 2f;
        SurvivorDef def = Def();

        var glow = UIFactory.Img(panel, new Color(0.06f, 0.10f, 0.12f, 1f), "MannequinBackdrop");
        UIKit.ApplyChamfer(glow, glow.color);
        UIFactory.Place(glow.rectTransform, TL, new Vector2(mannequinX, -62f), new Vector2(mannequinW, 288f));
        glow.raycastTarget = false;

        var mannequinNode = UIFactory.Node("EquipmentMannequin", panel);
        UIFactory.Place(mannequinNode, TL, new Vector2(mannequinX + 6f, -68f), new Vector2(mannequinW - 12f, 276f));
        Image mannequin = mannequinNode.gameObject.AddComponent<Image>();
        mannequin.sprite = InventoryMannequin.Get(def);
        if (mannequin.sprite == null) mannequin.sprite = PortraitSprite(def);
        mannequin.preserveAspect = true;
        mannequin.raycastTarget = false;
        mannequin.color = mannequin.sprite != null ? Color.white : new Color(0.55f, 0.61f, 0.64f, 1f);

        float row0 = 62f, row1 = 160f, row2 = 258f;
        CreateEquipmentSlot(panel, ItemSlot.Capacete, "CAPACETE", margin, row0, slotSize);
        CreateEquipmentSlot(panel, ItemSlot.Colete, "COLETE", rightX, row0, slotSize);
        CreateEquipmentSlot(panel, ItemSlot.Mochila, "MOCHILA", margin, row1, slotSize);
        CreateEquipmentSlot(panel, ItemSlot.Calca, "CALÇAS", rightX, row1, slotSize);
        CreateEquipmentSlot(panel, ItemSlot.Arma, "MELHORIA", margin, row2, slotSize);
        CreateEquipmentSlot(panel, ItemSlot.Botas, "BOTAS", rightX, row2, slotSize);

        var sep = UIFactory.Img(panel, UIKit.Col.Border, "SurvivorStripSeparator");
        InvAnchor(sep.rectTransform, 0f, 0f, 1f, 0f, 14f, 90f, -14f, 91f);
        sep.raycastTarget = false;

        var strip = UIFactory.Node("SurvivorStrip", panel);
        InvAnchor(strip, 0f, 0f, 1f, 0f, 14f, 6f, -14f, 84f);
        var h = UIFactory.HLayout(strip, 8f, TextAnchor.MiddleCenter);
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        // Um retrato por sobrevivente do esquadrao: clique para editar o equipamento dele.
        var members = new List<PlayerStats>();
        for (int i = 0; i < HeroSquad.Members.Count; i++)
            if (HeroSquad.Members[i].stats) members.Add(HeroSquad.Members[i].stats);
        if (members.Count == 0 && stats) members.Add(stats);
        int n = Mathf.Max(1, members.Count);
        float chipW = Mathf.Floor(Mathf.Min(84f, ((InvEquipW - 28f) - 8f * (n - 1)) / n));
        foreach (var m in members) BuildSurvivorChip(strip, m, chipW);
    }

    void CreateEquipmentSlot(RectTransform parent, ItemSlot slot, string label, float x, float labelY, float size)
    {
        // Rotulo com a largura do slot + folga, em linha unica: nunca vaza para fora do painel.
        var caption = FitText(parent, label, UIKit.Size.Caption - 1f, TextAlignmentOptions.Center,
            UIKit.Col.TextDim, "EquipLabel_" + slot, 8f);
        UIFactory.Place(caption.rectTransform, TL, new Vector2(x - 14f, -labelY), new Vector2(size + 28f, 20f));

        ItemInstance item = Equipped(slot);
        bool isSel = item != null && item == selected;
        var b = CreateItemTile(parent, item, ArtLoader.Get("Art/Items/" + slot.ToString().ToLowerInvariant()), isSel, () =>
        {
            if (item == null)
            {
                // Slot vazio: abre a aba da mochila com os itens desse tipo para equipar.
                category = (int)slot;
                selected = null;
                Rebuild();
                return;
            }
            // Duplo clique num item equipado: desequipa.
            if (IsDoubleClick(item)) { UnequipItem(item); return; }
            selected = item;
            Rebuild();
        }, "EquipSlot_" + slot);
        UIFactory.Place((RectTransform)b.transform, TL, new Vector2(x, -(labelY + 19f)), new Vector2(size, size));
    }

    static SurvivorDef DefOf(PlayerStats ps)
    {
        SurvivorDef def = ps ? ps.Survivor : null;
        if (def == null && ps)
        {
            var m = HeroSquad.Find(ps);
            if (m != null) def = m.def;
        }
        if (def == null && ps == HeroSquad.Primary) def = GameSession.Survivor;
        return def;
    }

    SurvivorDef Def()
    {
        SurvivorDef def = DefOf(stats);
        if (def == null) def = GameSession.Survivor;
        return def;
    }

    void BuildSurvivorChip(RectTransform strip, PlayerStats member, float width)
    {
        SurvivorDef def = DefOf(member);
        if (def == null && member == stats) def = Def();
        bool active = member == stats;
        var b = UIKit.Button(strip, "", () =>
        {
            if (member == stats) return;
            stats = member;
            selected = null;
            Rebuild();
        }, UIKit.BtnStyle.Secondary, 12, "Survivor_" + (def != null ? def.id : "main"));
        UIFactory.Elem(b.gameObject, width, 78f);
        UIFactory.SetButtonColor(b, active ? UIKit.Col.Accent : UIKit.Col.Border);
        var lbl = b.GetComponentInChildren<TMP_Text>(true);
        if (lbl) lbl.text = "";

        var fillColor = active ? new Color(0.20f, 0.09f, 0.10f, 1f) : new Color(0.045f, 0.07f, 0.085f, 1f);
        var fill = UIFactory.Img(b.transform, fillColor, "Fill");
        UIKit.ApplyChamfer(fill, fillColor);
        UIFactory.Inset(UIFactory.Stretch(fill.rectTransform), 2f, 2f);
        fill.raycastTarget = false;
        fill.transform.SetAsFirstSibling();

        var portrait = UIFactory.Img(b.transform, Color.white, "Portrait");
        UIFactory.Stretch(portrait.rectTransform, 0.10f, 0.32f, 0.90f, 0.95f);
        portrait.sprite = PortraitSprite(def);
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;
        portrait.color = portrait.sprite != null ? Color.white : (def != null ? def.color : Color.gray);

        var nameText = FitText(b.transform, (def != null ? def.name : "Sobrevivente").ToUpperInvariant(), 11f,
            TextAlignmentOptions.Center, active ? UIKit.Col.Text : UIKit.Col.TextDim, "Name", 8f);
        nameText.fontStyle = FontStyles.Bold;
        UIFactory.Stretch(nameText.rectTransform, 0.02f, 0.02f, 0.98f, 0.30f);

        var lvText = FitText(b.transform, "LV." + (member ? member.Level : 1), 11f, TextAlignmentOptions.TopRight,
            UIKit.Col.Gold, "Level", 8f);
        lvText.fontStyle = FontStyles.Bold;
        UIFactory.Stretch(lvText.rectTransform, 0.40f, 0.76f, 0.96f, 0.98f);
    }

    static Sprite PortraitSprite(SurvivorDef def)
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

        var l = FitText(f, label, UIKit.Size.Caption, TextAlignmentOptions.MidlineLeft, UIKit.Col.TextDim, "Label", 8f);
        l.characterSpacing = 1.5f;
        UIFactory.Stretch(l.rectTransform, 0.06f, 0.64f, 0.94f, 0.95f);

        var v = FitText(f, value, 38, TextAlignmentOptions.MidlineLeft, color, "Value", 16f);
        v.fontStyle = FontStyles.Bold;
        UIFactory.Stretch(v.rectTransform, 0.06f, 0.27f, 0.94f, 0.66f);

        if (!string.IsNullOrEmpty(sub))
        {
            var sb = FitText(f, sub, UIKit.Size.Caption - 1f, TextAlignmentOptions.MidlineLeft, UIKit.Col.TextDim, "Sub", 8f);
            UIFactory.Stretch(sb.rectTransform, 0.06f, 0.04f, 0.94f, 0.28f);
        }
    }

    void BuildStatsPanel(RectTransform panel)
    {
        SurvivorDef survivor = Def();
        string nick = Save != null && !string.IsNullOrWhiteSpace(Save.playerNickname)
            ? Save.playerNickname.Trim() : "SOBREVIVENTE";
        int level = stats ? stats.Level : 1;
        int xp = stats ? stats.Xp : 0;
        int xpNext = stats ? Mathf.Max(1, stats.XpToNext) : 1;

        var nameText = MapText(panel, (survivor != null ? survivor.name : "SOBREVIVENTE").ToUpperInvariant(), 30,
            TextAlignmentOptions.MidlineLeft, UIKit.Col.Gold, 24f, 62f, 420f, 40f, "ProfileName");
        nameText.fontStyle = FontStyles.Bold;
        string roleLine = survivor != null && !string.IsNullOrEmpty(survivor.role) ? survivor.role.ToUpperInvariant() : "";
        MapText(panel, (roleLine.Length > 0 ? roleLine + "  ·  " : "") + "JOGADOR  " + nick.ToUpperInvariant(),
            UIKit.Size.Caption, TextAlignmentOptions.MidlineLeft, UIKit.Col.TextDim, 24f, 102f, 560f, 24f, "ProfileClass");

        var lv = FitText(panel, "LV. " + level, UIKit.Size.Title, TextAlignmentOptions.MidlineRight, UIKit.Col.Text, "ProfileLevel", 14f);
        lv.fontStyle = FontStyles.Bold;
        UIFactory.Place(lv.rectTransform, InvTR, new Vector2(-24f, -62f), new Vector2(160f, 40f));

        MapText(panel, "XP  " + xp.ToString("N0") + " / " + xpNext.ToString("N0"), UIKit.Size.Caption,
            TextAlignmentOptions.MidlineLeft, UIKit.Col.TextDim, 24f, 130f, 400f, 22f, "ProfileXp");
        var xpFill = UIKit.Bar(panel, UIKit.Col.Cyan, (float)xp / xpNext, "ProfileXpBar");
        InvAnchor((RectTransform)xpFill.transform.parent, 0f, 1f, 1f, 1f, 24f, -166f, -24f, -156f);

        // Valores reais da missao (ja incluem nivel, equipamento, arvore de habilidades e arma).
        Hero hero = stats ? stats.GetComponent<Hero>() : null;
        Health health = stats ? stats.GetComponent<Health>() : null;
        float maxHp = health ? Mathf.Max(1f, health.max) : (stats ? stats.TotalHealth : 100f);
        float curHp = health ? health.Current : maxHp;
        float baseWeaponDamage = hero && hero.weapon ? hero.weapon.damage : 0f;
        float bonusScale = hero && hero.weapon ? hero.weapon.BonusScale : 1f;
        float damage = Mathf.Max(0f, baseWeaponDamage + (stats ? stats.TotalDamage : 0f) * bonusScale);
        float defense = stats ? stats.TotalDefense : 0f;
        float cadence = hero && hero.weapon ? hero.weapon.shotsPerSecond * Mathf.Max(0.05f, hero.fireRateMult) : 0f;
        float accuracy = hero && hero.weapon ? Mathf.Clamp01(hero.weapon.accuracy + hero.accuracyBonus) : 0f;
        float crit = hero ? Mathf.Clamp01(hero.critChance) : 0f;

        float itemHealth = 0f, itemDamage = 0f, itemDefense = 0f;
        if (stats)
            foreach (var item in stats.EquippedList())
                if (item != null) { itemHealth += item.bonusHealth; itemDamage += item.bonusDamage; itemDefense += item.bonusDefense; }

        var grid = UIFactory.Node("StatsGrid", panel);
        InvAnchor(grid, 0f, 0f, 1f, 1f, 18f, 12f, -18f, -180f);
        const string signed = "+0.#;-0.#;0";
        StatBlock(grid, "VIDA", Mathf.CeilToInt(curHp) + " / " + Mathf.CeilToInt(maxHp),
            "ITENS  " + itemHealth.ToString(signed), UIKit.Col.Success, 0, 0);
        StatBlock(grid, "DANO", damage.ToString("0.#"), "ITENS  " + itemDamage.ToString(signed),
            new Color(1f, 0.55f, 0.25f), 1, 0);
        StatBlock(grid, "DEFESA", defense.ToString("0.#"), "ITENS  " + itemDefense.ToString(signed),
            new Color(0.40f, 0.70f, 1f), 0, 1);
        StatBlock(grid, "CADÊNCIA", cadence.ToString("0.0") + "/s",
            "PRECISÃO  " + Mathf.RoundToInt(accuracy * 100f) + "%   ·   CRÍTICO  " + Mathf.RoundToInt(crit * 100f) + "%",
            UIKit.Col.Gold, 1, 1);
    }

    // ---------- detalhes do item ----------
    static string ItemDescriptionOrDefault(ItemInstance item)
    {
        string d = item.description;
        if (string.IsNullOrWhiteSpace(d) || d.Trim().StartsWith("Item de exemplo"))
            return "Equipamento tático de " + item.slot.ToString().ToLowerInvariant() + ".";
        return d;
    }

    static string RarityLabel(Rarity rarity)
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

    void DetailStatRow(RectTransform parent, string label, float value, float compare, bool showDelta,
        string format, float y, float cw)
    {
        var sep = UIFactory.Img(parent, UIKit.Col.Border, "RowSeparator");
        UIFactory.Place(sep.rectTransform, TL, new Vector2(0f, -(y + 44f)), new Vector2(cw, 1f));
        sep.raycastTarget = false;
        var l = MapText(parent, label, UIKit.Size.Label, TextAlignmentOptions.MidlineLeft, UIKit.Col.TextDim,
            0f, y, 200f, 44f, "StatLabel_" + label);
        l.characterSpacing = 1.5f;
        var v = MapText(parent, value.ToString(format), 26, TextAlignmentOptions.MidlineRight, UIKit.Col.Text,
            190f, y, 140f, 44f, "StatValue_" + label);
        v.fontStyle = FontStyles.Bold;
        if (showDelta)
        {
            float delta = value - compare;
            if (Mathf.Abs(delta) > 0.001f)
            {
                var d = MapText(parent, "(" + delta.ToString(format) + ")", UIKit.Size.Body, TextAlignmentOptions.MidlineLeft,
                    delta > 0f ? UIKit.Col.Success : new Color(1f, 0.38f, 0.32f), 346f, y, cw - 346f, 44f, "StatDelta_" + label);
                d.fontStyle = FontStyles.Bold;
            }
        }
    }

    void BuildItemDetails(RectTransform panel, ItemInstance item, bool equipped)
    {
        const float pad = 28f;
        float cw = InvDetailsW - pad * 2f;

        if (item == null)
        {
            var et = FitText(panel, "SELECIONE UM ITEM", UIKit.Size.Title, TextAlignmentOptions.Center,
                UIKit.Col.TextDim, "DetailsEmptyTitle", 16f);
            et.fontStyle = FontStyles.Bold;
            InvAnchor(et.rectTransform, 0.08f, 0.50f, 0.92f, 0.58f, 0f, 0f, 0f, 0f);
            var eb = UIFactory.Txt(panel, "Escolha um item na mochila ou nos slots para ver atributos, comparar com o equipado e agir.",
                UIKit.Size.Body, TextAlignmentOptions.Top, UIKit.Col.TextDim, "DetailsEmpty");
            eb.textWrappingMode = TextWrappingModes.Normal;
            eb.raycastTarget = false;
            InvAnchor(eb.rectTransform, 0.12f, 0.38f, 0.88f, 0.49f, 0f, 0f, 0f, 0f);
            return;
        }

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
        var chip = UIKit.Chip(panel, RarityLabel(item.rarity) + "  ·  " + item.slot.ToString().ToUpperInvariant() +
            (item.upgradeLevel > 0 ? "  +" + item.upgradeLevel : ""), rc, 330f, "SelectedItemRarity");
        UIFactory.Place(chip, TL, new Vector2(textX, -126f), new Vector2(Mathf.Min(330f, InvDetailsW - textX - pad), 36f));
        var chipLabel = chip.GetComponentInChildren<TMP_Text>(true);
        if (chipLabel)
        {
            chipLabel.enableAutoSizing = true;
            chipLabel.fontSizeMax = chipLabel.fontSize;
            chipLabel.fontSizeMin = 8f;
            chipLabel.overflowMode = TextOverflowModes.Truncate;
        }

        // ---- meio: area com rolagem ----
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

        var desc = UIFactory.Txt(body, ItemDescriptionOrDefault(item), UIKit.Size.Body, TextAlignmentOptions.TopLeft,
            new Color(0.80f, 0.86f, 0.88f), "SelectedItemDescription");
        desc.textWrappingMode = TextWrappingModes.Normal;
        desc.overflowMode = TextOverflowModes.Truncate;
        desc.raycastTarget = false;
        UIFactory.Place(desc.rectTransform, TL, Vector2.zero, new Vector2(cw, 60f));
        float descH = Mathf.Clamp(Mathf.Ceil(desc.GetPreferredValues(desc.text, cw, 0f).y) + 4f, 26f, 140f);
        desc.rectTransform.sizeDelta = new Vector2(cw, descH);

        float y = descH + 14f;
        var header = MapText(body, equipped ? "ATRIBUTOS" : "ATRIBUTOS  ·  COMPARADO COM O EQUIPADO",
            UIKit.Size.Caption, TextAlignmentOptions.MidlineLeft, UIKit.Col.Gold, 0f, y, cw, 26f, "StatsHeader");
        header.characterSpacing = 1.5f;
        y += 32f;

        ItemInstance cmp = equipped ? null : Equipped(item.slot);
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
        if (item.HasAffixes)
        {
            for (int ai = 0; ai < item.affixes.Count; ai++)
            {
                var af = item.affixes[ai];
                if (af == null) continue;
                MapText(body, "◆  " + af.Describe(), UIKit.Size.Label, TextAlignmentOptions.MidlineLeft,
                    new Color(0.55f, 0.85f, 1f), 0f, y, cw, 30f, "Affix_" + ai);
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

        // ---- base fixa ----
        var coin = UIFactory.Node("SellIcon", panel);
        UIFactory.Place(coin, InvBL, new Vector2(pad + 2f, 152f), new Vector2(24f, 24f));
        UIKit.Diamond(coin, UIKit.Col.Gold, 24f, "Diamond");
        var sell = FitText(panel, "VALOR DE VENDA   " + Mathf.Max(1, item.sellValue) + " OURO", UIKit.Size.Label,
            TextAlignmentOptions.MidlineLeft, UIKit.Col.Gold, "SellValue", 11f);
        sell.fontStyle = FontStyles.Bold;
        UIFactory.Place(sell.rectTransform, InvBL, new Vector2(pad + 38f, 144f), new Vector2(cw - 38f, 40f));

        int upCost = ItemUpgrade.Cost(item), upGold = ItemUpgrade.GoldCost(item);
        bool maxed = item.upgradeLevel >= ItemUpgrade.MaxFor(item);
        string upLabel = maxed ? "NÍVEL MÁXIMO  +" + item.upgradeLevel
            : "MELHORAR  +" + item.upgradeLevel + " → +" + (item.upgradeLevel + 1) + "   ·   " + upCost + " SUCATA + " + upGold + " OURO";
        var up = UIKit.Button(panel, upLabel, () => UpgradeItem(item), UIKit.BtnStyle.Primary, 22, "UpgradeMapItem");
        UIFactory.Place((RectTransform)up.transform, InvBL, new Vector2(pad, 84f), new Vector2(cw, 52f));
        up.interactable = !maxed && Save != null && Save.scrap >= upCost && Save.gold >= upGold;

        if (equipped)
        {
            var un = UIKit.Button(panel, "DESEQUIPAR", () => UnequipItem(item), UIKit.BtnStyle.Secondary, 24, "UnequipMapItem");
            UIFactory.Place((RectTransform)un.transform, InvBL, new Vector2(pad, 16f), new Vector2(cw, 60f));
        }
        else
        {
            float half = (cw - 12f) * 0.5f;
            var eq = UIKit.Button(panel, "EQUIPAR", () => EquipItem(item), UIKit.BtnStyle.Success, 24, "EquipMapItem");
            UIFactory.Place((RectTransform)eq.transform, InvBL, new Vector2(pad, 16f), new Vector2(half, 60f));
            var sl = UIKit.Button(panel, "VENDER", () => SellItem(item), UIKit.BtnStyle.Danger, 24, "SellMapItem");
            UIFactory.Place((RectTransform)sl.transform, InvBL, new Vector2(pad + half + 12f, 16f), new Vector2(half, 60f));
        }
    }

    // ---------- acoes (passam pelo Inventory/PlayerStats da missao, que cuidam do salvamento) ----------
    void UpgradeItem(ItemInstance item)
    {
        if (item == null) return;
        if (Save == null) { Toast("Melhoria indisponível nesta partida."); return; }
        bool wasEquipped = IsEquipped(item);
        if (!ItemUpgrade.TryUpgrade(Save, item, out string msg)) { Toast(msg); return; }
        if (wasEquipped && stats) stats.Equip(item);   // recalcula vida/dano/defesa com o item melhorado
        inv.MarkChanged();                             // agenda o salvamento (inclui a sucata gasta)
        Toast(msg);
        selected = item;
        Rebuild();
    }

    void EquipItem(ItemInstance item)
    {
        if (item == null || !inv.Contains(item)) return;
        inv.Equip(item, stats);
        Toast("Equipado: " + item.name);
        selected = item;
        Rebuild();
    }

    void UnequipItem(ItemInstance item)
    {
        if (item == null) return;
        if (inv.Items.Count >= inv.capacity) { Toast("Mochila cheia. Venda ou equipe algum item primeiro."); return; }
        inv.Unequip(item.slot, stats);
        if (category != InvAll && category != (int)item.slot) category = (int)item.slot;
        Toast("Desequipado: " + item.name);
        selected = item;
        Rebuild();
    }

    void SellItem(ItemInstance item)
    {
        if (item == null || !inv.Contains(item)) return;
        int value = Mathf.Max(1, item.sellValue);
        inv.Sell(item);
        Toast("Vendido por " + value + " de ouro.");
        selected = null;
        Rebuild();
    }
}
