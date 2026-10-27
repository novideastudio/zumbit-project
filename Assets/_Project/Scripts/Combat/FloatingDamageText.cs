using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Numeros de dano em espaco de mundo, com POOL (objetos criados uma vez e reaproveitados).
///  - Fonte: a "fonte de titulos" do tema (UITheme.displayFont) em TextMeshPro; se nao houver, a padrao do TMP.
///  - Dano normal: creme com sombra, "pula" ao aparecer e sobe.
///  - CRITICO: maior, degrade amarelo -> laranja, selo "CRITICO" e leve tremor.
///  - ERROU: "MISS" cinza-azulado, pequeno, sobe rapido e some.
/// Nao usa outlineWidth/outlineColor do TMP (lancam excecao em alguns materiais no Unity 6): o contorno
/// e feito por uma copia escura deslocada atras do texto.
/// </summary>
public sealed class FloatingDamageText : MonoBehaviour
{
    const int MaxActive = 32;

    // Tamanhos em "pontos TMP" de mundo (1 ponto ~ 0,1 unidade). Ajuste aqui se quiser maior/menor.
    const float NormalSizeMin = 3.4f, NormalSizeMax = 4.8f;
    const float CritSize = 6.2f;
    const float MissSize = 3.0f;

    static readonly Color NormalColor = new Color(1f, 0.96f, 0.84f, 1f);
    static readonly Color CritTop = new Color(1f, 0.93f, 0.30f, 1f);      // amarelo
    static readonly Color CritBottom = new Color(1f, 0.46f, 0.05f, 1f);   // laranja
    static readonly Color MissColor = new Color(0.72f, 0.80f, 0.90f, 1f);
    static readonly Color ShadowColor = new Color(0.04f, 0.02f, 0.02f, 0.92f);

    static int activeCount;
    static readonly Stack<FloatingDamageText> pool = new Stack<FloatingDamageText>(MaxActive);
    static readonly string[] numberCache = new string[1000];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        pool.Clear();
        activeCount = 0;
    }

    enum Kind { Normal, Crit, Miss }

    TextMeshPro label, shadow;
    float age, duration;
    Vector3 velocity;
    Kind kind;
    float baseSize;
    Color faceColor;
    float shakeSeed;
    Vector3 basePos;
    bool inUse;
    // Ultimo alpha escrito nos TMP: cada escrita de cor marca a malha como "suja" (2 TMP por numero, ate 32 numeros).
    float lastAlpha = 1f;

    // ------------------------------------------------------------------ API
    public static void Show(Vector3 worldPosition, float damage, bool critical = false)
    {
        if (damage <= 0f || activeCount >= MaxActive) return;
        var p = Acquire();
        if (p != null) p.Activate(worldPosition, damage, critical ? Kind.Crit : Kind.Normal);
    }

    /// <summary>"MISS" sobre o alvo (tiro que errou). Tem prioridade menor: some se a tela ja esta cheia de numeros.</summary>
    public static void ShowMiss(Vector3 worldPosition)
    {
        if (activeCount >= MaxActive - 8) return;
        var p = Acquire();
        if (p != null) p.Activate(worldPosition, 0f, Kind.Miss);
    }

    static FloatingDamageText Acquire()
    {
        FloatingDamageText popup = null;
        while (pool.Count > 0)
        {
            popup = pool.Pop();
            if (popup != null) break;   // pode ter sido destruido numa troca de cena
            popup = null;
        }
        if (popup != null) return popup;

        var go = new GameObject("FloatingDamageText");
        popup = go.AddComponent<FloatingDamageText>();
        if (!popup.Build()) { Destroy(go); return null; }
        return popup;
    }

    static string NumberText(float damage)
    {
        int n = Mathf.Max(0, Mathf.RoundToInt(damage));
        if (n >= numberCache.Length) return n.ToString();
        return numberCache[n] ?? (numberCache[n] = n.ToString());
    }

    static TMP_FontAsset PickFont()
    {
        var theme = UITheme.Current;
        if (theme != null && theme.displayFont != null) return theme.displayFont;
        if (theme != null && theme.font != null) return theme.font;
        return TMP_Settings.defaultFontAsset;
    }

    // ------------------------------------------------------------------ construcao
    TextMeshPro MakeText(string name, Transform parent, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshPro>();
        var font = PickFont();
        if (font != null) t.font = font;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.richText = true;
        t.raycastTarget = false;
        t.rectTransform.sizeDelta = new Vector2(12f, 4f);
        t.sortingOrder = order;
        return t;
    }

    bool Build()
    {
        if (PickFont() == null) return false;
        label = MakeText("Face", transform, 32001);
        shadow = MakeText("Shadow", transform, 32000);
        shadow.rectTransform.localPosition = new Vector3(0.045f, -0.06f, 0.01f);
        gameObject.SetActive(false);
        return true;
    }

    // ------------------------------------------------------------------ ativacao
    void Activate(Vector3 worldPosition, float damage, Kind k)
    {
        kind = k;
        age = 0f;
        lastAlpha = 1f;
        shakeSeed = Random.value * 100f;

        string txt;
        if (k == Kind.Miss)
        {
            txt = "MISS";
            baseSize = MissSize;
            duration = 0.62f;
            faceColor = MissColor;
            velocity = new Vector3(Random.Range(-0.45f, 0.45f), Random.Range(1.1f, 1.45f), 0f);
            label.fontStyle = FontStyles.Italic | FontStyles.Bold;
        }
        else if (k == Kind.Crit)
        {
            txt = "<size=48%><cspace=0.12em>CRÍTICO</cspace></size>\n" + NumberText(damage) + "<size=70%>!</size>";
            baseSize = CritSize;
            duration = 0.95f;
            faceColor = Color.white;   // o degrade usa as cores do vertice
            velocity = new Vector3(Random.Range(-0.18f, 0.18f), Random.Range(0.95f, 1.2f), 0f);
            label.fontStyle = FontStyles.Bold;
        }
        else
        {
            txt = NumberText(damage);
            baseSize = Mathf.Lerp(NormalSizeMin, NormalSizeMax, Mathf.InverseLerp(4f, 140f, damage));
            duration = 0.72f;
            faceColor = NormalColor;
            velocity = new Vector3(Random.Range(-0.28f, 0.28f), Random.Range(0.85f, 1.1f), 0f);
            label.fontStyle = FontStyles.Bold;
        }

        label.text = txt;
        label.fontSize = baseSize;
        label.color = faceColor;
        label.enableVertexGradient = k == Kind.Crit;
        if (k == Kind.Crit) label.colorGradient = new VertexGradient(CritTop, CritTop, CritBottom, CritBottom);

        shadow.fontStyle = label.fontStyle;
        shadow.text = StripTags(txt);
        shadow.fontSize = baseSize;
        shadow.color = ShadowColor;
        shadow.enableVertexGradient = false;
        shadow.rectTransform.localPosition = new Vector3(0.05f * (k == Kind.Crit ? 1.4f : 1f), -0.065f * (k == Kind.Crit ? 1.4f : 1f), 0.01f);

        basePos = worldPosition + new Vector3(Random.Range(-0.18f, 0.18f), k == Kind.Miss ? 0.25f : 0.55f, -0.2f);
        transform.position = basePos;
        transform.localScale = Vector3.one * 0.01f;

        if (!inUse) { inUse = true; activeCount++; }
        gameObject.SetActive(true);
    }

    // A sombra usa o mesmo texto sem as tags de tamanho/espacamento (so troca o tamanho do selo igual).
    static string StripTags(string s) => s;

    void Release()
    {
        if (inUse) { inUse = false; activeCount = Mathf.Max(0, activeCount - 1); }
        gameObject.SetActive(false);
        pool.Push(this);
    }

    // ------------------------------------------------------------------ animacao
    void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / duration);

        // Movimento: sobe desacelerando.
        velocity.y = Mathf.Lerp(velocity.y, velocity.y * 0.25f, Time.deltaTime * 2.4f);
        basePos += velocity * Time.deltaTime;
        Vector3 p = basePos;
        if (kind == Kind.Crit)
        {
            // Tremor curto no inicio do critico.
            float shake = Mathf.Clamp01(1f - age / 0.22f) * 0.07f;
            p.x += (Mathf.PerlinNoise(shakeSeed, age * 60f) - 0.5f) * 2f * shake;
            p.y += (Mathf.PerlinNoise(shakeSeed + 9f, age * 60f) - 0.5f) * 2f * shake;
        }
        transform.position = p;

        // Escala: "pop" com overshoot, assenta e encolhe no fim.
        float popDur = kind == Kind.Crit ? 0.16f : 0.10f;
        float pop = age < popDur ? Pop(age / popDur, kind == Kind.Crit ? 1.45f : 1.25f) : 1f;
        float fadeScale = Mathf.Lerp(1f, kind == Kind.Miss ? 0.85f : 0.72f, Mathf.SmoothStep(0.55f, 1f, t));
        transform.localScale = Vector3.one * (pop * fadeScale);

        // Cores so sao reescritas quando o alpha realmente muda (na primeira metade da vida ele fica em 1).
        float alpha = 1f - Mathf.SmoothStep(0.5f, 1f, t);
        if (Mathf.Abs(alpha - lastAlpha) > 0.004f)
        {
            lastAlpha = alpha;
            var fc = label.color; fc.a = alpha; label.color = fc;
            var sc = ShadowColor; sc.a = ShadowColor.a * alpha; shadow.color = sc;
            if (kind == Kind.Crit)
            {
                label.colorGradient = new VertexGradient(
                    new Color(CritTop.r, CritTop.g, CritTop.b, alpha), new Color(CritTop.r, CritTop.g, CritTop.b, alpha),
                    new Color(CritBottom.r, CritBottom.g, CritBottom.b, alpha), new Color(CritBottom.r, CritBottom.g, CritBottom.b, alpha));
            }
        }

        if (age >= duration) Release();
    }

    // 0 -> overshoot -> 1 (back ease-out)
    static float Pop(float x, float overshoot)
    {
        x = Mathf.Clamp01(x);
        float c1 = overshoot, c3 = c1 + 1f;
        float y = 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        return Mathf.Max(0.01f, y);
    }

    void OnDestroy()
    {
        if (inUse) { inUse = false; activeCount = Mathf.Max(0, activeCount - 1); }
    }
}
