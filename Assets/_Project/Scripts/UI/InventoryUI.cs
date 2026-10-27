using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    public Inventory inventory;
    public PlayerStats stats;

    [Header("Grade")]
    public Transform gridParent;
    public SlotUI slotPrefab;

    [Header("Slots de equipamento")]
    public SlotUI[] equipSlots;

    [Header("Textos")]
    public TMP_Text goldText;
    public TMP_Text capacityText;

    [Header("Detalhes")]
    public ItemDetailPanel detail;

    readonly List<SlotUI> gridSlots = new List<SlotUI>();
    readonly List<ItemInstance> visibleItems = new List<ItemInstance>();
    readonly List<Button> filterButtons = new List<Button>();
    readonly List<TMP_Text> filterLabels = new List<TMP_Text>();
    readonly List<ItemSlot?> filterValues = new List<ItemSlot?>();

    enum SortMode { Rarity, Name, Type }
    SortMode sortMode = SortMode.Rarity;
    ItemSlot? activeFilter;
    Rarity? activeRarity;
    string searchQuery = string.Empty;
    TMP_InputField searchField;
    Button rarityButton;
    TMP_Text rarityLabel;
    Button sortButton;
    TMP_Text sortLabel;
    float nextFooterRefresh;

    static readonly Color SlotBackground = new Color(0.055f, 0.070f, 0.082f, 0.98f);
    static readonly Color FilterBackground = new Color(0.10f, 0.13f, 0.16f, 0.98f);
    static readonly Color FilterSelected = new Color(0.847f, 0.141f, 0.169f, 1f);

    void Awake()
    {
        if (!inventory) inventory = FindFirstObjectByType<Inventory>();
        if (!stats && inventory) stats = inventory.stats;
        if (!stats) stats = FindFirstObjectByType<PlayerStats>();

        if (!inventory || !gridParent || !slotPrefab)
        {
            Debug.LogWarning("Zombit InventoryUI: referências incompletas. Confirme Inventory, Grid Parent e Slot Prefab no Inspector.", this);
            return;
        }

        for (int i = 0; i < inventory.capacity; i++)
        {
            SlotUI s = Instantiate(slotPrefab, gridParent);
            s.Clicked = OnSlotClicked;
            s.DoubleClicked = OnSlotDoubleClicked;
            gridSlots.Add(s);
        }

        if (equipSlots != null)
        {
            foreach (SlotUI e in equipSlots)
            {
                if (!e) continue;
                e.IsEquipSlot = true;
                e.Clicked = OnSlotClicked;
                e.DoubleClicked = OnSlotDoubleClicked;
            }
        }

        ConfigureGridLayout();
        BuildFilterToolbar();
        BuildSearchBox();
        UpdateFooter();
    }

    void ConfigureGridLayout()
    {
        if (gridParent is RectTransform gridRect)
        {
            // Cria espaço para os filtros, sem reduzir o número total de espaços da mochila.
            Vector2 pos = gridRect.anchoredPosition;
            pos.y = -146f;
            gridRect.anchoredPosition = pos;
            Vector2 size = gridRect.sizeDelta;
            size.y = 392f;
            gridRect.sizeDelta = size;
        }

        GridLayoutGroup grid = gridParent.GetComponent<GridLayoutGroup>();
        if (grid)
        {
            grid.cellSize = new Vector2(58f, 58f);
            grid.spacing = new Vector2(6f, 6f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 10;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
        }

        if (capacityText)
        {
            capacityText.fontSize = Mathf.Min(capacityText.fontSize, 16f);
            RectTransform r = capacityText.rectTransform;
            Vector2 size = r.sizeDelta;
            size.x = Mathf.Max(size.x, 420f);
            r.sizeDelta = size;
        }
        if (goldText) goldText.fontSize = Mathf.Min(goldText.fontSize, 19f);
    }

    void BuildFilterToolbar()
    {
        RectTransform panel = transform as RectTransform;
        if (!panel || transform.Find("InventoryFilterToolbar")) return;

        RectTransform toolbar = UIFactory.Node("InventoryFilterToolbar", transform);
        toolbar.anchorMin = toolbar.anchorMax = toolbar.pivot = new Vector2(0f, 1f);
        toolbar.anchoredPosition = new Vector2(20f, -108f);
        toolbar.sizeDelta = new Vector2(694f, 28f);

        HorizontalLayoutGroup layout = toolbar.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 4f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        AddFilterButton(toolbar, "TODOS", null, 58f);
        AddFilterButton(toolbar, "MELHORIAS", ItemSlot.Arma, 86f);
        AddFilterButton(toolbar, "CAPACETE", ItemSlot.Capacete, 78f);
        AddFilterButton(toolbar, "COLETE", ItemSlot.Colete, 62f);
        AddFilterButton(toolbar, "CALCAS", ItemSlot.Calca, 58f);
        AddFilterButton(toolbar, "BOTAS", ItemSlot.Botas, 50f);
        AddFilterButton(toolbar, "MOCHILA", ItemSlot.Mochila, 70f);

        CreateRarityButton(toolbar);
        CreateSortButton(toolbar);
        UpdateFilterStyles();
    }

    void AddFilterButton(RectTransform parent, string label, ItemSlot? slot, float width)
    {
        GameObject go = new GameObject("Filter_" + label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = UIKit.Chamfer;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        image.raycastTarget = true;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        SetButtonPalette(button, FilterBackground);
        LayoutElement element = go.GetComponent<LayoutElement>();
        element.preferredWidth = width;
        element.minWidth = width;
        element.preferredHeight = 27f;

        RectTransform labelRect = UIFactory.Node("Label", go.transform);
        UIFactory.Stretch(labelRect, 0f, 0f, 1f, 1f);
        TMP_Text text = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 11f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 8f;
        text.fontSizeMax = 11f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.82f, 0.86f, 0.88f, 1f);
        text.raycastTarget = false;

        ItemSlot? capturedSlot = slot;
        button.onClick.AddListener(() =>
        {
            activeFilter = capturedSlot;
            UpdateFilterStyles();
            Refresh();
        });
        filterButtons.Add(button);
        filterLabels.Add(text);
        filterValues.Add(slot);
    }

    void BuildSearchBox()
    {
        RectTransform panel = transform as RectTransform;
        if (!panel) return;

        Transform existing = transform.Find("InventorySearchBox");
        GameObject go;
        Image background;
        bool createdNow = false;
        if (existing)
        {
            go = existing.gameObject;
            background = go.GetComponent<Image>();
            searchField = go.GetComponent<TMP_InputField>();
        }
        else
        {
            // Build while inactive so TMP_InputField does not initialize before its text/placeholder references exist.
            go = new GameObject("InventorySearchBox", typeof(RectTransform), typeof(Image));
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            background = go.GetComponent<Image>();
            searchField = go.AddComponent<TMP_InputField>();
            createdNow = true;
        }

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(20f, -74f);
        rt.sizeDelta = new Vector2(300f, 26f);

        background.sprite = UIKit.Chamfer;
        background.type = Image.Type.Sliced;
        background.color = new Color(0.045f, 0.06f, 0.07f, 0.98f);
        background.raycastTarget = true;

        Transform viewportT = go.transform.Find("Text Area");
        RectTransform viewport;
        if (!viewportT)
        {
            viewport = UIFactory.Node("Text Area", go.transform);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(10f, 2f);
            viewport.offsetMax = new Vector2(-10f, -2f);
            viewport.gameObject.AddComponent<RectMask2D>();
        }
        else viewport = viewportT as RectTransform;

        Transform textT = viewport.Find("Text");
        TextMeshProUGUI inputText;
        if (!textT)
        {
            RectTransform textRT = UIFactory.Node("Text", viewport);
            UIFactory.Stretch(textRT);
            inputText = textRT.gameObject.AddComponent<TextMeshProUGUI>();
        }
        else inputText = textT.GetComponent<TextMeshProUGUI>();
        inputText.fontSize = 13f;
        inputText.color = new Color(0.93f, 0.95f, 0.96f, 1f);
        inputText.alignment = TextAlignmentOptions.MidlineLeft;
        inputText.enableAutoSizing = true;
        inputText.fontSizeMin = 10f;
        inputText.fontSizeMax = 13f;
        inputText.raycastTarget = false;

        Transform placeholderT = viewport.Find("Placeholder");
        TextMeshProUGUI placeholder;
        if (!placeholderT)
        {
            RectTransform phRT = UIFactory.Node("Placeholder", viewport);
            UIFactory.Stretch(phRT);
            placeholder = phRT.gameObject.AddComponent<TextMeshProUGUI>();
        }
        else placeholder = placeholderT.GetComponent<TextMeshProUGUI>();
        placeholder.text = "BUSCAR ITEM POR NOME...";
        placeholder.fontSize = 12f;
        placeholder.color = new Color(0.56f, 0.61f, 0.64f, 1f);
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        placeholder.fontStyle = FontStyles.Italic;
        placeholder.raycastTarget = false;

        searchField.targetGraphic = background;
        searchField.textViewport = viewport;
        searchField.textComponent = inputText;
        searchField.placeholder = placeholder;
        searchField.lineType = TMP_InputField.LineType.SingleLine;
        searchField.characterLimit = 40;
        searchField.restoreOriginalTextOnEscape = true;
        searchField.SetTextWithoutNotify(searchQuery);
        searchField.onValueChanged.RemoveListener(OnSearchChanged);
        searchField.onValueChanged.AddListener(OnSearchChanged);
        if (createdNow) go.SetActive(true);
    }

    void OnSearchChanged(string value)
    {
        searchQuery = (value ?? string.Empty).Trim();
        Refresh();
    }

    void CreateRarityButton(RectTransform parent)
    {
        GameObject go = new GameObject("RarityFilterButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = UIKit.Chamfer;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        image.raycastTarget = true;

        rarityButton = go.GetComponent<Button>();
        rarityButton.targetGraphic = image;
        SetButtonPalette(rarityButton, FilterBackground);
        LayoutElement element = go.GetComponent<LayoutElement>();
        element.preferredWidth = 96f;
        element.minWidth = 96f;
        element.preferredHeight = 27f;

        RectTransform labelRect = UIFactory.Node("Label", go.transform);
        UIFactory.Stretch(labelRect, 0f, 0f, 1f, 1f);
        rarityLabel = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        rarityLabel.text = "RARIDADE: TODAS";
        rarityLabel.fontSize = 10f;
        rarityLabel.enableAutoSizing = true;
        rarityLabel.fontSizeMin = 8f;
        rarityLabel.fontSizeMax = 10f;
        rarityLabel.alignment = TextAlignmentOptions.Center;
        rarityLabel.color = new Color(0.82f, 0.86f, 0.88f, 1f);
        rarityLabel.raycastTarget = false;
        rarityButton.onClick.AddListener(CycleRarityFilter);
    }

    void CycleRarityFilter()
    {
        // Ordem: Todas -> Comum -> Incomum -> Raro -> Épico -> Lendário -> Todas.
        int current = activeRarity.HasValue ? (int)activeRarity.Value + 1 : 0;
        current = (current + 1) % 6;
        activeRarity = current == 0 ? (Rarity?)null : (Rarity)(current - 1);

        if (rarityLabel)
        {
            rarityLabel.text = activeRarity.HasValue
                ? "RARIDADE: " + RarityLabel(activeRarity.Value)
                : "RARIDADE: TODAS";
            rarityLabel.color = activeRarity.HasValue ? RarityColors.Get(activeRarity.Value) : Color.white;
        }
        Refresh();
    }

    static string RarityLabel(Rarity rarity)
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

    void CreateSortButton(RectTransform parent)
    {
        GameObject go = new GameObject("SortInventoryButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = UIKit.Chamfer;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        SetButtonPalette(button, new Color(0.149f, 0.188f, 0.235f, 1f));
        LayoutElement element = go.GetComponent<LayoutElement>();
        element.preferredWidth = 126f;
        element.minWidth = 126f;
        element.preferredHeight = 27f;

        RectTransform labelRect = UIFactory.Node("Label", go.transform);
        UIFactory.Stretch(labelRect, 0f, 0f, 1f, 1f);
        sortLabel = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        sortLabel.text = "ORDEM: RARIDADE";
        sortLabel.fontSize = 10f;
        sortLabel.enableAutoSizing = true;
        sortLabel.fontSizeMin = 8f;
        sortLabel.fontSizeMax = 10f;
        sortLabel.alignment = TextAlignmentOptions.Center;
        sortLabel.color = Color.white;
        sortLabel.raycastTarget = false;

        button.onClick.AddListener(CycleSortMode);
        sortButton = button;
    }

    static void SetButtonPalette(Button button, Color normal)
    {
        ColorBlock cb = button.colors;
        cb.normalColor = normal;
        cb.highlightedColor = Color.Lerp(normal, Color.white, 0.15f);
        cb.pressedColor = Color.Lerp(normal, Color.black, 0.24f);
        cb.selectedColor = normal;
        cb.disabledColor = normal;
        cb.fadeDuration = 0.08f;
        button.colors = cb;
    }

    void UpdateFilterStyles()
    {
        for (int i = 0; i < filterButtons.Count; i++)
        {
            bool selected = filterValues[i] == activeFilter;
            Color bg = selected ? FilterSelected : FilterBackground;
            Image image = filterButtons[i].targetGraphic as Image;
            if (image) image.color = Color.white;
            SetButtonPalette(filterButtons[i], bg);
            filterLabels[i].color = selected ? Color.white : new Color(0.82f, 0.86f, 0.88f, 1f);
            filterLabels[i].fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
        }
    }

    void CycleSortMode()
    {
        sortMode = (SortMode)(((int)sortMode + 1) % 3);
        if (sortLabel)
        {
            switch (sortMode)
            {
                case SortMode.Rarity: sortLabel.text = "ORDEM: RARIDADE"; break;
                case SortMode.Name: sortLabel.text = "ORDEM: NOME A-Z"; break;
                default: sortLabel.text = "ORDEM: TIPO"; break;
            }
        }
        Refresh();
    }

    void OnEnable()
    {
        if (inventory) inventory.Changed += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        if (inventory) inventory.Changed -= Refresh;
    }

    void Update()
    {
        // Ouro muda a cada abate; atualiza só o rodapé em intervalo curto sem reconstruir a grade.
        if (Time.unscaledTime >= nextFooterRefresh)
        {
            nextFooterRefresh = Time.unscaledTime + 0.25f;
            UpdateFooter();
        }
    }

    void Refresh()
    {
        if (!inventory) return;

        visibleItems.Clear();
        IReadOnlyList<ItemInstance> items = inventory.Items;
        for (int i = 0; i < items.Count; i++)
        {
            ItemInstance item = items[i];
            if (item == null) continue;
            if (activeFilter.HasValue && item.slot != activeFilter.Value) continue;
            if (activeRarity.HasValue && item.rarity != activeRarity.Value) continue;
            if (!MatchesSearch(item, searchQuery)) continue;
            visibleItems.Add(item);
        }

        SortVisibleItems();
        for (int i = 0; i < gridSlots.Count; i++)
            gridSlots[i].Set(i < visibleItems.Count ? visibleItems[i] : null);

        if (equipSlots != null)
        {
            foreach (SlotUI e in equipSlots)
            {
                if (!e) continue;
                e.Set(stats ? stats.GetEquipped(e.EquipType) : null);
            }
        }

        UpdateFooter();
        if (detail && detail.Current != null)
        {
            ItemInstance current = detail.Current;
            bool stillOwned = inventory.Contains(current) || (stats && stats.GetEquipped(current.slot) == current);
            bool matchesFilter = (!activeFilter.HasValue || current.slot == activeFilter.Value) &&
                                 (!activeRarity.HasValue || current.rarity == activeRarity.Value) &&
                                 MatchesSearch(current, searchQuery);
            if (!stillOwned || !matchesFilter) detail.Hide();
        }
    }

    static bool MatchesSearch(ItemInstance item, string query)
    {
        if (item == null || string.IsNullOrWhiteSpace(query)) return true;
        return (!string.IsNullOrEmpty(item.name) && item.name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0) ||
               (!string.IsNullOrEmpty(item.description) && item.description.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0);
    }

    void SortVisibleItems()
    {
        visibleItems.Sort((a, b) =>
        {
            switch (sortMode)
            {
                case SortMode.Name:
                    return string.Compare(a.name, b.name, StringComparison.CurrentCultureIgnoreCase);
                case SortMode.Type:
                    int type = a.slot.CompareTo(b.slot);
                    if (type != 0) return type;
                    int typeRarity = ((int)b.rarity).CompareTo((int)a.rarity);
                    return typeRarity != 0 ? typeRarity : string.Compare(a.name, b.name, StringComparison.CurrentCultureIgnoreCase);
                default:
                    int rarity = ((int)b.rarity).CompareTo((int)a.rarity);
                    if (rarity != 0) return rarity;
                    int slot = a.slot.CompareTo(b.slot);
                    return slot != 0 ? slot : string.Compare(a.name, b.name, StringComparison.CurrentCultureIgnoreCase);
            }
        });
    }

    void UpdateFooter()
    {
        if (inventory && goldText) goldText.text = "OURO  " + inventory.Gold.ToString("N0");
        if (inventory && capacityText)
            capacityText.text = $"MOCHILA  {inventory.Items.Count}/{inventory.capacity}  |  {visibleItems.Count} VISIVEIS  |  BUSCA + RARIDADE  |  DUPLO CLIQUE EQUIPA";
        if (capacityText) capacityText.fontSize = 12f;
    }

    void OnSlotClicked(SlotUI slot)
    {
        if (slot == null || slot.Item == null) { if (detail) detail.Hide(); return; }
        if (detail) detail.Show(slot.Item, slot.IsEquipSlot);
    }

    void OnSlotDoubleClicked(SlotUI slot)
    {
        if (slot == null || slot.Item == null || !inventory) return;
        if (slot.IsEquipSlot) inventory.Unequip(slot.EquipType);
        else inventory.Equip(slot.Item);
        if (detail) detail.Hide();
    }
}
