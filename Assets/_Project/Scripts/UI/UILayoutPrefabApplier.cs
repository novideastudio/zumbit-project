using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Applies an editable layout prefab to a screen that is still assembled by the existing
/// gameplay/UI code. Objects are matched by hierarchy and name, so dynamic callbacks and
/// save-dependent values remain owned by their original systems.
/// </summary>
public static class UILayoutPrefabApplier
{
    public const string ResourcesFolder = "UI/Screens/Layout_";

    /// <summary>Used by the Editor baker so a re-bake always captures the original code layout.</summary>
    public static bool SuppressApply { get; set; }

    public static bool HasLayout(string key)
    {
        // O mapa foi reformulado no runtime com cartoes ilustrados e âncoras responsivas.
        // O Layout_map.prefab antigo permanece no projeto como referencia, mas nao controla essa tela.
        if (key == "map") return false;
        return !SuppressApply && !string.IsNullOrEmpty(key) && UITheme.Current.useEditorUI &&
               Resources.Load<GameObject>(ResourcesFolder + key) != null;
    }

    public static bool Apply(RectTransform runtimeScreen, string key)
    {
        if (SuppressApply || runtimeScreen == null || string.IsNullOrEmpty(key) || !UITheme.Current.useEditorUI)
            return false;

        GameObject prefab = Resources.Load<GameObject>(ResourcesFolder + key);
        if (prefab == null) return false;

        // Layout_map antigo (feito antes do hub sem cabecalho): nao aplicar, senao as ancoras velhas
        // desmontam a tela. Para voltar a editar pelo prefab: apague Layout_map.prefab e rode
        // ZomBit > UI > 3 - Gerar layouts editaveis (so gera os que faltam).
        if (key == "map" && EditorUiBinding.FindDeep(prefab.transform, "HubProfile") == null)
        {
            Debug.LogWarning("[ZomBit UI] Layout_map desatualizado (hub novo). Usando o layout do codigo. Apague Layout_map.prefab e rode ZomBit > UI > 3 para regerar.");
            return false;
        }

        ApplyNode(prefab.transform, runtimeScreen, true, key);
        return true;
    }

    // No layout do mapa, estas pecas repetidas vem de prefabs-modelo proprios (Node_Location, Tile_Hub, Cell_Equip).
    // As copias "fotografadas" dentro do Layout_map NAO podem sobrescrever o modelo, senao editar o modelo nao tem efeito.
    static bool OwnedByTemplate(string key, string childName)
    {
        if (key != "map") return false;
        return childName.StartsWith("Node_") || childName.StartsWith("Enemy_") || childName.StartsWith("EquipSlot_");
    }

    static void ApplyNode(Transform source, Transform target, bool isRoot, string key)
    {
        if (source == null || target == null) return;

        var flags = source.GetComponent<UIPrefabLayoutOverride>();
        if (flags != null)
        {
            // A raiz do layout e so o Canvas de preview do Editor (escala/ancoras/tamanho 0): nunca copiar para a tela real.
            if (!isRoot) ApplyRectTransform(source as RectTransform, target as RectTransform, flags);
            ApplyImage(source.GetComponent<Image>(), target.GetComponent<Image>(), flags);
            ApplyText(source.GetComponent<TMP_Text>(), target.GetComponent<TMP_Text>(), flags);
            ApplyButton(source.GetComponent<Button>(), target.GetComponent<Button>(), flags);
            if (flags.overrideLayoutComponents) ApplyLayoutComponents(source, target);
        }

        // Match children by name and occurrence within the same parent. This supports repeated
        // generic names such as "Label" without accidentally binding to a label in another card.
        for (int i = 0; i < source.childCount; i++)
        {
            Transform sourceChild = source.GetChild(i);
            if (OwnedByTemplate(key, sourceChild.name)) continue;
            int occurrence = 0;
            for (int j = 0; j < i; j++)
                if (source.GetChild(j).name == sourceChild.name) occurrence++;

            Transform targetChild = FindChildOccurrence(target, sourceChild.name, occurrence);
            if (targetChild != null) ApplyNode(sourceChild, targetChild, false, key);
        }
    }

    static Transform FindChildOccurrence(Transform parent, string name, int occurrence)
    {
        if (parent == null) return null;
        int found = 0;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name != name) continue;
            if (found == occurrence) return child;
            found++;
        }
        return null;
    }

    static void ApplyRectTransform(RectTransform source, RectTransform target, UIPrefabLayoutOverride flags)
    {
        if (source == null || target == null) return;
        if (flags.overrideAnchorsAndPosition)
        {
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.anchoredPosition3D = source.anchoredPosition3D;
        }
        if (flags.overrideSize) target.sizeDelta = source.sizeDelta;
        if (flags.overridePivot) target.pivot = source.pivot;
        if (flags.overrideScaleAndRotation)
        {
            target.localScale = source.localScale;
            target.localRotation = source.localRotation;
        }
    }

    static void ApplyImage(Image source, Image target, UIPrefabLayoutOverride flags)
    {
        if (source == null || target == null) return;
        // If the runtime builder selected a different sprite, this is likely a character/item/status
        // image. Keep the game-selected asset instead of freezing the preview asset from the prefab.
        bool spriteStillAtBakedValue = target.sprite == flags.bakedImageSprite;
        if (flags.overrideImageSprite && spriteStillAtBakedValue)
        {
            target.sprite = source.sprite;
            target.type = source.type;
            target.preserveAspect = source.preserveAspect;
            target.fillCenter = source.fillCenter;
            target.fillMethod = source.fillMethod;
            target.fillOrigin = source.fillOrigin;
            target.fillAmount = source.fillAmount;
            target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
            target.useSpriteMesh = source.useSpriteMesh;
        }
        if (flags.overrideImageColor && target.color == flags.bakedImageColor) target.color = source.color;
    }

    static void ApplyText(TMP_Text source, TMP_Text target, UIPrefabLayoutOverride flags)
    {
        if (source == null || target == null) return;
        // Text written by the runtime builder usually represents save/game data. Preserve it when
        // it differs from the original bake, but allow edited prefab text for fixed labels.
        if (flags.overrideTextContent && string.Equals(target.text, flags.bakedTextContent, System.StringComparison.Ordinal))
            target.text = source.text;
        if (flags.overrideTextColor && target.color == flags.bakedTextColor) target.color = source.color;
        if (!flags.overrideTextStyle) return;

        if (source.font != null) target.font = source.font;
        target.fontSize = source.fontSize;
        target.fontStyle = source.fontStyle;
        target.alignment = source.alignment;
        target.enableAutoSizing = source.enableAutoSizing;
        target.fontSizeMin = source.fontSizeMin;
        target.fontSizeMax = source.fontSizeMax;
        target.textWrappingMode = source.textWrappingMode;
        target.overflowMode = source.overflowMode;
        target.margin = source.margin;
        target.characterSpacing = source.characterSpacing;
        target.wordSpacing = source.wordSpacing;
        target.lineSpacing = source.lineSpacing;
        target.paragraphSpacing = source.paragraphSpacing;
        target.richText = source.richText;
        target.extraPadding = source.extraPadding;
        target.raycastTarget = source.raycastTarget;
        target.maskable = source.maskable;
    }

    static void ApplyButton(Button source, Button target, UIPrefabLayoutOverride flags)
    {
        if (source == null || target == null) return;
        if (flags.overrideButtonColors && ColorsMatch(target.colors, flags.bakedButtonColors)) target.colors = source.colors;
        if (flags.overrideButtonTransition)
        {
            target.transition = source.transition;
            target.spriteState = source.spriteState;
            target.navigation = source.navigation;
            if (source.animationTriggers != null && target.animationTriggers != null)
            {
                target.animationTriggers.normalTrigger = source.animationTriggers.normalTrigger;
                target.animationTriggers.highlightedTrigger = source.animationTriggers.highlightedTrigger;
                target.animationTriggers.pressedTrigger = source.animationTriggers.pressedTrigger;
                target.animationTriggers.selectedTrigger = source.animationTriggers.selectedTrigger;
                target.animationTriggers.disabledTrigger = source.animationTriggers.disabledTrigger;
            }
        }
    }

    static bool ColorsMatch(ColorBlock a, ColorBlock b)
    {
        return a.normalColor == b.normalColor && a.highlightedColor == b.highlightedColor &&
               a.pressedColor == b.pressedColor && a.selectedColor == b.selectedColor &&
               a.disabledColor == b.disabledColor && Mathf.Approximately(a.colorMultiplier, b.colorMultiplier) &&
               Mathf.Approximately(a.fadeDuration, b.fadeDuration);
    }

    static void ApplyLayoutComponents(Transform source, Transform target)
    {
        var sourceElement = source.GetComponent<LayoutElement>();
        if (sourceElement != null)
        {
            var element = target.GetComponent<LayoutElement>();
            if (element == null) element = target.gameObject.AddComponent<LayoutElement>();
            element.ignoreLayout = sourceElement.ignoreLayout;
            element.minWidth = sourceElement.minWidth;
            element.minHeight = sourceElement.minHeight;
            element.preferredWidth = sourceElement.preferredWidth;
            element.preferredHeight = sourceElement.preferredHeight;
            element.flexibleWidth = sourceElement.flexibleWidth;
            element.flexibleHeight = sourceElement.flexibleHeight;
            element.layoutPriority = sourceElement.layoutPriority;
        }

        var sourceHorizontal = source.GetComponent<HorizontalLayoutGroup>();
        var targetHorizontal = target.GetComponent<HorizontalLayoutGroup>();
        if (sourceHorizontal != null)
        {
            if (targetHorizontal == null && target.GetComponent<VerticalLayoutGroup>() == null && target.GetComponent<GridLayoutGroup>() == null)
                targetHorizontal = target.gameObject.AddComponent<HorizontalLayoutGroup>();
            if (targetHorizontal != null) CopyHorizontalOrVertical(sourceHorizontal, targetHorizontal);
        }

        var sourceVertical = source.GetComponent<VerticalLayoutGroup>();
        var targetVertical = target.GetComponent<VerticalLayoutGroup>();
        if (sourceVertical != null)
        {
            if (targetVertical == null && target.GetComponent<HorizontalLayoutGroup>() == null && target.GetComponent<GridLayoutGroup>() == null)
                targetVertical = target.gameObject.AddComponent<VerticalLayoutGroup>();
            if (targetVertical != null) CopyHorizontalOrVertical(sourceVertical, targetVertical);
        }

        var sourceGrid = source.GetComponent<GridLayoutGroup>();
        var targetGrid = target.GetComponent<GridLayoutGroup>();
        if (sourceGrid != null)
        {
            if (targetGrid == null && target.GetComponent<HorizontalLayoutGroup>() == null && target.GetComponent<VerticalLayoutGroup>() == null)
                targetGrid = target.gameObject.AddComponent<GridLayoutGroup>();
            if (targetGrid != null)
            {
                CopyLayoutGroup(sourceGrid, targetGrid);
                targetGrid.startCorner = sourceGrid.startCorner;
                targetGrid.startAxis = sourceGrid.startAxis;
                targetGrid.cellSize = sourceGrid.cellSize;
                targetGrid.spacing = sourceGrid.spacing;
                targetGrid.constraint = sourceGrid.constraint;
                targetGrid.constraintCount = sourceGrid.constraintCount;
            }
        }

        var sourceFitter = source.GetComponent<ContentSizeFitter>();
        if (sourceFitter != null)
        {
            var fitter = target.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = target.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = sourceFitter.horizontalFit;
            fitter.verticalFit = sourceFitter.verticalFit;
        }

        var sourceAspect = source.GetComponent<AspectRatioFitter>();
        if (sourceAspect != null)
        {
            var aspect = target.GetComponent<AspectRatioFitter>();
            if (aspect == null) aspect = target.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = sourceAspect.aspectMode;
            aspect.aspectRatio = sourceAspect.aspectRatio;
        }

        var sourceCanvasGroup = source.GetComponent<CanvasGroup>();
        if (sourceCanvasGroup != null)
        {
            var group = target.GetComponent<CanvasGroup>();
            if (group == null) group = target.gameObject.AddComponent<CanvasGroup>();
            group.alpha = sourceCanvasGroup.alpha;
            group.interactable = sourceCanvasGroup.interactable;
            group.blocksRaycasts = sourceCanvasGroup.blocksRaycasts;
            group.ignoreParentGroups = sourceCanvasGroup.ignoreParentGroups;
        }
    }

    static void CopyHorizontalOrVertical(HorizontalOrVerticalLayoutGroup source, HorizontalOrVerticalLayoutGroup target)
    {
        CopyLayoutGroup(source, target);
        target.spacing = source.spacing;
        target.childControlWidth = source.childControlWidth;
        target.childControlHeight = source.childControlHeight;
        target.childForceExpandWidth = source.childForceExpandWidth;
        target.childForceExpandHeight = source.childForceExpandHeight;
        target.reverseArrangement = source.reverseArrangement;
    }

    static void CopyLayoutGroup(LayoutGroup source, LayoutGroup target)
    {
        target.padding = new RectOffset(source.padding.left, source.padding.right, source.padding.top, source.padding.bottom);
        target.childAlignment = source.childAlignment;
    }
}
