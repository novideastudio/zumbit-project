using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Design system do ZomBit Survival: cores, escala tipografica e componentes reutilizaveis.
/// Todas as telas devem usar estes componentes para manter o visual padronizado.
/// Visual "moderno" (conceito anexo): carvao profundo, vermelho sangue, dourado, ciano tatico,
/// cantos arredondados suaves, bordas finas e contraste alto.
/// </summary>
public static class UIKit
{
    // ---------- tokens ----------
    public static class Col
    {
        public static readonly Color Bg        = Hex(0x0D, 0x11, 0x15);
        public static readonly Color Header    = Hex(0x15, 0x1B, 0x22, 250);
        public static readonly Color Surface   = Hex(0x1B, 0x22, 0x2B, 245);
        public static readonly Color SurfaceHi = Hex(0x26, 0x30, 0x3C);
        public static readonly Color Border    = Hex(0x33, 0x3E, 0x4B);
        public static readonly Color BorderHi  = Hex(0x5C, 0x6E, 0x80);
        public static readonly Color Text      = Hex(0xEE, 0xF2, 0xF5);
        public static readonly Color TextDim   = Hex(0x9A, 0xA6, 0xB2);
        public static readonly Color Accent    = Hex(0xD8, 0x24, 0x2B);   // vermelho sangue
        public static readonly Color AccentDk  = Hex(0x7A, 0x14, 0x18);
        public static readonly Color Danger    = Hex(0xB0, 0x1E, 0x28);
        public static readonly Color Gold      = Hex(0xFF, 0xC2, 0x4B);
        public static readonly Color Success   = Hex(0x48, 0xDE, 0x7C);
        public static readonly Color SuccessDk = Hex(0x1E, 0x60, 0x40);
        public static readonly Color Cyan      = Hex(0x3F, 0xC8, 0xE8);   // energia / XP

        static Color Hex(int r, int g, int b, int a = 255) => new Color32((byte)r, (byte)g, (byte)b, (byte)a);
    }

    // Tamanhos minimos legiveis para referencia 1920x1080.
    public static class Size
    {
        public const float Caption = 15f;
        public const float Body = 18f;
        public const float Label = 20f;
        public const float Title = 28f;
        public const float Display = 44f;
    }

    // ---------- sprites procedurais (9-slice) ----------
    // "Chamfer" mantem o nome historico (usado em todo o projeto), mas agora gera um
    // retangulo ARREDONDADO com bordas suavizadas (anti-alias), como no conceito visual.
    static Sprite chamfer;
    public static Sprite Chamfer
    {
        get
        {
            if (chamfer == null)
            {
                const int N = 32, radius = 8, border = 10;
                chamfer = BuildRounded(N, radius, border);
            }
            return chamfer;
        }
    }

    /// <summary>Circulo cheio (badges, nos de habilidade, retratos).</summary>
    static Sprite circle;
    public static Sprite Circle
    {
        get
        {
            if (circle == null)
            {
                const int N = 64;
                var t = new Texture2D(N, N, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[N * N];
                float c = (N - 1) * 0.5f, r = c;
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) - r;
                        float a = Mathf.Clamp01(0.5f - d);
                        px[y * N + x] = new Color(255, 255, 255, (byte)(a * 255f));
                    }
                t.SetPixels32(px);
                t.Apply(false, true);
                t.hideFlags = HideFlags.HideAndDontSave;
                circle = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(N * 0.5f, N * 0.5f, N * 0.5f, N * 0.5f));
                circle.hideFlags = HideFlags.HideAndDontSave;
            }
            return circle;
        }
    }

    /// <summary>Estrela de 5 pontas (avaliacao de missao).</summary>
    static Sprite star;
    public static Sprite Star
    {
        get
        {
            if (star == null)
            {
                const int N = 64;
                var t = new Texture2D(N, N, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                // Vertices da estrela (5 pontas, raio externo/interno) em coordenadas 0..1.
                Vector2[] outer = new Vector2[5], inner = new Vector2[5];
                for (int i = 0; i < 5; i++)
                {
                    float aOut = Mathf.PI * 0.5f + i * (2f * Mathf.PI / 5f);
                    float aIn = aOut + Mathf.PI / 5f;
                    outer[i] = new Vector2(0.5f + 0.48f * Mathf.Cos(aOut), 0.5f + 0.48f * Mathf.Sin(aOut));
                    inner[i] = new Vector2(0.5f + 0.20f * Mathf.Cos(aIn), 0.5f + 0.20f * Mathf.Sin(aIn));
                }
                bool Inside(float x, float y)
                {
                    // point-in-polygon sobre o contorno alternado ponta/interno
                    int n = 10, cross = 0;
                    for (int i = 0, j = n - 1; i < n; j = i++)
                    {
                        Vector2 a = (i % 2 == 0) ? outer[i / 2] : inner[i / 2];
                        Vector2 b = (j % 2 == 0) ? outer[j / 2] : inner[j / 2];
                        if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x) cross++;
                    }
                    return (cross & 1) == 1;
                }
                var px = new Color32[N * N];
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                        px[y * N + x] = Inside((x + 0.5f) / N, (y + 0.5f) / N)
                            ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                t.SetPixels32(px);
                t.Apply(false, true);
                t.hideFlags = HideFlags.HideAndDontSave;
                star = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
                star.hideFlags = HideFlags.HideAndDontSave;
            }
            return star;
        }
    }

    // Retangulo arredondado com campo de distancia (bordas suavizadas) e 9-slice.
    static Sprite BuildRounded(int n, int radius, int border)
    {
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        float half = n * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // SDF de retangulo arredondado centrado
                float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                float ax = Mathf.Max(qx, 0f), ay = Mathf.Max(qy, 0f);
                float d = Mathf.Sqrt(ax * ax + ay * ay) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                float a = Mathf.Clamp01(0.5f - d);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        t.SetPixels32(px);
        t.Apply(false, true);
        t.hideFlags = HideFlags.HideAndDontSave;
        var s = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        s.hideFlags = HideFlags.HideAndDontSave;
        return s;
    }

    public static void ResetCache()
    {
        chamfer = null;
        circle = null;
        star = null;
    }

    public static void ApplyChamfer(Image img, Color c)
    {
        img.sprite = Chamfer;
        img.type = Image.Type.Sliced;
        img.color = c;
    }

    /// <summary>Aplica o circulo procedural a uma imagem existente.</summary>
    public static void ApplyCircle(Image img, Color c)
    {
        img.sprite = Circle;
        img.type = Image.Type.Simple;
        img.color = c;
    }

    // ---------- containers ----------
    /// <summary>Moldura arredondada: borda + preenchimento (sem Outline). Filhos entram no root.</summary>
    public static RectTransform Frame(Transform parent, string name, Color fill, Color border, float borderWidth = 2f)
    {
        var root = UIFactory.Node(name, parent);
        var b = UIFactory.Img(root, border, "Border");
        ApplyChamfer(b, border);
        UIFactory.Stretch(b.rectTransform);
        b.raycastTarget = false;
        var f = UIFactory.Img(root, fill, "Fill");
        ApplyChamfer(f, fill);
        UIFactory.Inset(UIFactory.Stretch(f.rectTransform), borderWidth, borderWidth);
        f.raycastTarget = false;
        return root;
    }

    public static void SetFrameColors(RectTransform frame, Color fill, Color border)
    {
        if (!frame) return;
        var b = frame.Find("Border"); var f = frame.Find("Fill");
        if (b) b.GetComponent<Image>().color = border;
        if (f) f.GetComponent<Image>().color = fill;
    }

    /// <summary>Painel padrao com titulo opcional, tick de destaque e divisor.</summary>
    public static RectTransform Panel(Transform parent, string name, string title, Color? accent = null)
    {
        var root = Frame(parent, name, Col.Surface, Col.Border, 2f);
        if (string.IsNullOrEmpty(title)) return root;
        Color acc = accent ?? Col.Accent;

        var tick = UIFactory.Img(root, acc, "TitleTick");
        UIFactory.Place(tick.rectTransform, new Vector2(0f, 1f), new Vector2(14f, -16f), new Vector2(5f, 28f));
        tick.raycastTarget = false;

        var t = UIFactory.Txt(root, title.ToUpperInvariant(), Size.Label, TextAlignmentOptions.MidlineLeft, Col.Text, "PanelTitle");
        t.fontStyle = FontStyles.Bold;
        t.characterSpacing = 2f;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        var rt = t.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(30f, -52f); rt.offsetMax = new Vector2(-16f, -8f);

        var line = UIFactory.Img(root, Col.Border, "TitleDivider");
        var lr = line.rectTransform;
        lr.anchorMin = new Vector2(0f, 1f); lr.anchorMax = new Vector2(1f, 1f); lr.pivot = new Vector2(0.5f, 1f);
        lr.offsetMin = new Vector2(14f, -55f); lr.offsetMax = new Vector2(-14f, -54f);
        line.raycastTarget = false;
        return root;
    }

    // ---------- botoes ----------
    public enum BtnStyle { Primary, Secondary, Danger, Success, Ghost }

    static Color StyleColor(BtnStyle s)
    {
        switch (s)
        {
            case BtnStyle.Primary: return Col.Accent;
            case BtnStyle.Danger: return Col.Danger;
            case BtnStyle.Success: return Col.SuccessDk;
            case BtnStyle.Ghost: return new Color(1f, 1f, 1f, 0.05f);
            default: return Col.SurfaceHi;
        }
    }

    public static Button Button(Transform parent, string label, Action onClick,
        BtnStyle style = BtnStyle.Secondary, float fontSize = Size.Label, string name = "Button")
    {
        var b = UIFactory.Btn(parent, label, fontSize, onClick, StyleColor(style), name);
        var img = b.GetComponent<Image>();
        img.sprite = Chamfer; img.type = Image.Type.Sliced;
        var t = b.GetComponentInChildren<TMP_Text>(true);
        if (t)
        {
            t.fontStyle = FontStyles.Bold;
            t.characterSpacing = 1.5f;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.enableAutoSizing = true;
            t.fontSizeMax = t.fontSize;
            t.fontSizeMin = Mathf.Max(12f, t.fontSize * 0.6f);
            t.margin = new Vector4(10f, 2f, 10f, 2f);
        }
        return b;
    }

    /// <summary>Aba de navegacao: ativa = vermelho escuro + sublinhado dourado.</summary>
    public static Button Tab(Transform parent, string label, bool active, Action onClick, string name)
    {
        var b = Button(parent, label, onClick, active ? BtnStyle.Primary : BtnStyle.Secondary, Size.Body, name);
        var c = active ? Col.AccentDk : Col.SurfaceHi;
        UIFactory.SetButtonColor(b, c);
        if (active)
        {
            var ul = UIFactory.Img(b.transform, Col.Gold, "ActiveUnderline");
            var r = ul.rectTransform;
            r.anchorMin = new Vector2(0f, 0f); r.anchorMax = new Vector2(1f, 0f); r.pivot = new Vector2(0.5f, 0f);
            r.offsetMin = new Vector2(6f, 0f); r.offsetMax = new Vector2(-6f, 4f);
            ul.raycastTarget = false;
        }
        return b;
    }

    // ---------- pecas pequenas ----------
    /// <summary>Losango dourado (substitui o glifo ◆ que a fonte padrao nao possui).</summary>
    public static Image Diamond(Transform parent, Color c, float size, string name = "Diamond")
    {
        var img = UIFactory.Img(parent, c, name);
        var rt = img.rectTransform;
        rt.sizeDelta = new Vector2(size * 0.72f, size * 0.72f);
        rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
        img.raycastTarget = false;
        return img;
    }

    /// <summary>Etiqueta (chip) com texto, usada para atributos e raridade.</summary>
    public static RectTransform Chip(Transform parent, string text, Color color, float width = 130f, string name = "Chip")
    {
        var root = Frame(parent, name, new Color(color.r * 0.22f, color.g * 0.22f, color.b * 0.22f, 1f),
            new Color(color.r * 0.8f, color.g * 0.8f, color.b * 0.8f, 1f), 1.5f);
        UIFactory.Elem(root.gameObject, width, 36f);
        var t = UIFactory.Txt(root, text, Size.Caption, TextAlignmentOptions.Center, color, "Label");
        t.fontStyle = FontStyles.Bold;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        UIFactory.Stretch(t.rectTransform);
        return root;
    }

    /// <summary>Barra de progresso (vida, XP, atributos). Retorna a imagem de preenchimento.</summary>
    public static Image Bar(Transform parent, Color fillColor, float value01, string name = "Bar")
    {
        var bg = UIFactory.Img(parent, new Color(0.03f, 0.05f, 0.06f, 1f), name);
        bg.raycastTarget = false;
        var f = UIFactory.Img(bg.transform, fillColor, "Fill");
        UIFactory.Stretch(f.rectTransform);
        f.type = Image.Type.Filled;
        f.fillMethod = Image.FillMethod.Horizontal;
        f.fillOrigin = (int)Image.OriginHorizontal.Left;
        f.fillAmount = Mathf.Clamp01(value01);
        f.raycastTarget = false;
        return f;
    }

    /// <summary>Vinheta (escurece bordas) para dar profundidade aos fundos.</summary>
    public static void Vignette(RectTransform screen, float strength = 0.55f)
    {
        var top = UIFactory.Img(screen, new Color(0f, 0f, 0f, strength), "VignetteTop");
        UIFactory.Stretch(top.rectTransform, 0f, 0.75f, 1f, 1f);
        top.raycastTarget = false;
        var bot = UIFactory.Img(screen, new Color(0f, 0f, 0f, strength), "VignetteBottom");
        UIFactory.Stretch(bot.rectTransform, 0f, 0f, 1f, 0.2f);
        bot.raycastTarget = false;
    }
}
