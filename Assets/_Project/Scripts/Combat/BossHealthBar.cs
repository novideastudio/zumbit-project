using UnityEngine;

/// <summary>
/// Barra de vida em espaco de mundo para chefes.
///
/// CORRECAO: antes a barra era FILHA do chefe. Como o Enemy espelha scale.x ao virar para o heroi,
/// a barra herdava o espelhamento e esvaziava para o lado errado. Agora ela e um objeto independente
/// (sem pai) que segue o chefe em LateUpdate usando a escala ABSOLUTA, entao o flip nao a afeta.
/// O tamanho e a altura continuam proporcionais ao chefe, como antes.
/// </summary>
public sealed class BossHealthBar : MonoBehaviour
{
    // Medidas em "unidades locais do chefe" (iguais as do layout antigo).
    const float OffsetY = 1.05f;
    const float BarWidth = 1.25f;
    const float BarHeight = 0.105f;
    const float FillWidth = 1.22f;
    const float FillHeight = 0.065f;

    static readonly Color Back = new Color(0.07f, 0.025f, 0.03f, 0.95f);
    static readonly Color Normal = new Color(0.82f, 0.08f, 0.12f);
    static readonly Color Low = new Color(1f, 0.05f, 0.04f);

    Transform root, bg, fill;
    SpriteRenderer bgRenderer, fillRenderer;
    Health health;
    float ratio = 1f;

    void Awake()
    {
        health = GetComponent<Health>();

        root = new GameObject("BossHealthBar_" + name).transform; // sem pai: nao herda scale/flip
        bg = NewQuad("Background", Back, 23000, out bgRenderer);
        fill = NewQuad("Fill", Normal, 23001, out fillRenderer);

        if (health != null) health.Changed += Refresh;
    }

    Transform NewQuad(string quadName, Color color, int order, out SpriteRenderer renderer)
    {
        var go = new GameObject(quadName);
        go.transform.SetParent(root, false);
        renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = UIFactory.White;
        renderer.color = color;
        renderer.sortingOrder = order;
        return go.transform;
    }

    void Start()
    {
        if (health != null) Refresh(health.Current, health.max);
        Layout();
    }

    void OnDestroy()
    {
        if (health != null) health.Changed -= Refresh;
        if (root != null) Destroy(root.gameObject);
    }

    void OnDisable() { if (root != null) root.gameObject.SetActive(false); }
    void OnEnable() { if (root != null) root.gameObject.SetActive(true); }

    void Refresh(float current, float max)
    {
        ratio = max <= 0f ? 0f : Mathf.Clamp01(current / max);
        if (fillRenderer != null) fillRenderer.color = ratio < 0.25f ? Low : Normal;
        Layout();
    }

    void LateUpdate() => Layout();

    void Layout()
    {
        if (root == null) return;

        // lossyScale absoluto: ignora o sinal negativo do flip horizontal.
        Vector3 s = transform.lossyScale;
        float sx = Mathf.Abs(s.x), sy = Mathf.Abs(s.y);

        // Posicao do mundo: acima do chefe, com a altura proporcional a escala vertical.
        root.position = transform.position + new Vector3(0f, OffsetY * sy, 0f);
        root.rotation = Quaternion.identity;

        float bgW = BarWidth * sx, bgH = BarHeight * sy;
        bg.localScale = new Vector3(bgW, bgH, 1f);
        bg.localPosition = Vector3.zero;

        // A barra esvazia sempre da direita para a esquerda (borda esquerda fixa).
        float fullW = FillWidth * sx;
        float w = fullW * ratio;
        fill.localScale = new Vector3(Mathf.Max(0.0001f, w), FillHeight * sy, 1f);
        fill.localPosition = new Vector3(-fullW * 0.5f + w * 0.5f, 0f, -0.01f);
    }
}
