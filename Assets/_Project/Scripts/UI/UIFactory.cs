using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class UIFactory
{
    public static UITheme Theme => UITheme.Current;

    public static Color Dark => Theme.backgroundColor;
    public static Color PanelColor => Theme.panelColor;
    public static Color Red => Theme.accentColor;
    public static Color Gold => Theme.goldColor;
    public static Color Gray => Theme.grayColor;

    static Sprite white;
    public static Sprite White
    {
        get
        {
            if (white == null)
            {
                var t = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var px = new Color[16];
                for (int i = 0; i < 16; i++) px[i] = Color.white;
                t.SetPixels(px); t.Apply();
                t.hideFlags = HideFlags.HideAndDontSave;
                white = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                white.hideFlags = HideFlags.HideAndDontSave;
            }
            return white;
        }
    }

    public static void ResetCache() { white = null; UIKit.ResetCache(); }

    static bool Same(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) < 0.003f && Mathf.Abs(a.g - b.g) < 0.003f &&
        Mathf.Abs(a.b - b.b) < 0.003f && Mathf.Abs(a.a - b.a) < 0.003f;

    static void ApplySprite(Image img, Sprite sp)
    {
        img.sprite = sp;
        img.type = sp.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
    }

    public static Canvas MakeCanvas(string name, int order)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = order;
        var s = go.AddComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920, 1080);
        s.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();
        return c;
    }

    public static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }

    public static RectTransform Node(string n, Transform p)
    {
        var go = new GameObject(n, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(p, false);
        return rt;
    }

    public static RectTransform Stretch(RectTransform rt, float minX = 0, float minY = 0, float maxX = 1, float maxY = 1)
    {
        rt.anchorMin = new Vector2(minX, minY);
        rt.anchorMax = new Vector2(maxX, maxY);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    public static RectTransform Inset(RectTransform rt, float x, float y)
    {
        rt.offsetMin = new Vector2(x, y);
        rt.offsetMax = new Vector2(-x, -y);
        return rt;
    }

    public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    public static Image Img(Transform p, Color c, string n = "Image")
    {
        var rt = Node(n, p);
        var i = rt.gameObject.AddComponent<Image>();
        i.sprite = White;
        i.color = c;

        var th = Theme;
        if (n == "Background" && th.menuBackground != null)
        {
            i.sprite = th.menuBackground;
            i.color = Color.white;
            i.preserveAspect = false;

            var fit = i.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = th.menuBackground.rect.width / th.menuBackground.rect.height;
        }
        else if (th.panelSprite != null && Same(c, th.panelColor))
        {
            ApplySprite(i, th.panelSprite);
            i.color = th.panelTint;
        }
        return i;
    }

    public static TMP_Text Txt(Transform p, string text, float size,
        TextAlignmentOptions al = TextAlignmentOptions.Center, Color? color = null, string n = "Text")
    {
        var th = Theme;
        var rt = Node(n, p);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (size >= 20f && th.displayFont != null) t.font = th.displayFont;
        else if (th.font != null) t.font = th.font;
        t.text = text;
        float scaled = size * (size >= 60f ? th.titleScale : th.fontScale);
        t.fontSize = th.minFontSize > 0f ? Mathf.Max(scaled, th.minFontSize) : scaled;
        t.alignment = al;
        t.color = color ?? Color.white;
        t.raycastTarget = false;
        // Unity 6.6 / current TextMeshPro can throw NullReferenceException when
        // outlineWidth/outlineColor setters target a TMP material without outline properties.
        // Keep text readable through the configured font weight and panel contrast instead.
        return t;
    }

    public static void SetButtonColor(Button b, Color c)
    {
        var cb = b.colors;
        cb.normalColor = c;
        cb.highlightedColor = Color.Lerp(c, Color.white, 0.2f);
        cb.pressedColor = Color.Lerp(c, Color.black, 0.3f);
        cb.selectedColor = c;
        cb.disabledColor = new Color(0.14f, 0.14f, 0.14f, 0.85f);
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.08f;
        b.colors = cb;
    }

    public static Button Btn(Transform p, string label, float fontSize, Action onClick,
        Color? color = null, string n = "Button")
    {
        var th = Theme;
        var img = Img(p, Color.white, n);
        if (th.buttonSprite != null) ApplySprite(img, th.buttonSprite);
        else
        {
            // Botões padrão agora usam o retângulo arredondado do UIKit (visual moderno).
            img.sprite = UIKit.Chamfer;
            img.type = Image.Type.Sliced;
        }

        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        img.gameObject.AddComponent<UIButtonFx>(); // hover/press suave (tempo nao escalado)
        Color baseColor = color ?? (th.buttonSprite != null ? th.buttonTint : th.buttonColor);
        SetButtonColor(b, baseColor);

        var t = Txt(img.transform, label, fontSize, TextAlignmentOptions.Center, null, "Label");
        Stretch(t.rectTransform);
        if (onClick != null) b.onClick.AddListener(() => { ZombitAudio.PlayUI(); onClick(); });
        return b;
    }

    public static LayoutElement Elem(GameObject g, float w, float h)
    {
        var le = g.GetComponent<LayoutElement>();
        if (le == null) le = g.AddComponent<LayoutElement>();
        le.preferredWidth = w;
        le.preferredHeight = h;
        return le;
    }

    public static VerticalLayoutGroup VLayout(RectTransform rt, float spacing, TextAnchor align = TextAnchor.MiddleCenter)
    {
        var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = spacing;
        v.childAlignment = align;
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = false; v.childForceExpandHeight = false;
        return v;
    }

    public static HorizontalLayoutGroup HLayout(RectTransform rt, float spacing, TextAnchor align = TextAnchor.MiddleCenter)
    {
        var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.childAlignment = align;
        h.childControlWidth = true; h.childControlHeight = true;
        h.childForceExpandWidth = false; h.childForceExpandHeight = false;
        return h;
    }
}
