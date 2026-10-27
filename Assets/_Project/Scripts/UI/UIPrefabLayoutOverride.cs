using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Editor-authored flags attached to layout-prefab objects.
/// The game keeps generating dynamic content and gameplay state in code, then applies only
/// the visual/layout properties explicitly allowed here from the matching prefab object.
/// </summary>
[DisallowMultipleComponent]
public sealed class UIPrefabLayoutOverride : MonoBehaviour
{
    [Header("Rect Transform")]
    [Tooltip("Apply anchor and position values from the layout prefab. Disable for data-positioned elements such as map markers.")]
    public bool overrideAnchorsAndPosition = true;
    public bool overrideSize = true;
    public bool overridePivot = true;
    public bool overrideScaleAndRotation = true;

    [Header("TextMeshPro")]
    [Tooltip("Allows fixed labels to be edited in the prefab. At runtime, values that differ from the baked text are treated as dynamic game data and are preserved.")]
    public bool overrideTextContent = true;
    public bool overrideTextStyle = true;
    public bool overrideTextColor = true;
    [HideInInspector] public string bakedTextContent;
    [HideInInspector] public Color bakedTextColor = Color.white;

    [Header("Image")]
    [Tooltip("Allows changing a fixed image in the prefab. If gameplay supplies a different sprite from the baked one, the runtime sprite is preserved.")]
    public bool overrideImageSprite;
    [Tooltip("Allows editing an image tint. Runtime state-driven colors that differ from the baked color are preserved.")]
    public bool overrideImageColor;
    [HideInInspector] public Sprite bakedImageSprite;
    [HideInInspector] public Color bakedImageColor = Color.white;

    [Header("Button")]
    [Tooltip("Allows editing button colors when their runtime color block still matches the baked values.")]
    public bool overrideButtonColors;
    [HideInInspector] public ColorBlock bakedButtonColors;
    public bool overrideButtonTransition = true;

    [Header("Layout Components")]
    [Tooltip("Apply LayoutElement, LayoutGroup, ContentSizeFitter, AspectRatioFitter and CanvasGroup settings from the prefab.")]
    public bool overrideLayoutComponents = true;
}
