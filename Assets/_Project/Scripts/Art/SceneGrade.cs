using UnityEngine;

/// <summary>
/// Acabamento de cor do combate: vinheta escura nas bordas + leve tom quente por cima de tudo (cenario, sobreviventes e zumbis).
/// Funciona como um "color grading" barato: o cenario pintado e os sprites deixam de parecer duas imagens coladas.
/// Fica filho da camera, acompanha o tremor de dano e se ajusta a qualquer proporcao de tela.
/// </summary>
[DisallowMultipleComponent]
public sealed class SceneGrade : MonoBehaviour
{
    public bool enableGrade = true;
    [Range(0f, 1f)] public float vignette = 0.55f;
    [Range(0f, 0.2f)] public float warmTint = 0.05f;
    public Color tint = new Color(0.30f, 0.10f, 0.04f, 1f);

    const int Order = 8000;   // acima de personagens/projeteis, abaixo do HUD (Canvas overlay)

    Camera cam;
    Transform vignetteT, tintT;

    void Start()
    {
        cam = Camera.main;
        if (!enableGrade || cam == null) { enabled = false; return; }

        vignetteT = MakeLayer("SceneGradeVignette", MakeVignetteSprite(), Color.white);
        tintT = MakeLayer("SceneGradeTint", MakeWhiteSprite(), new Color(tint.r, tint.g, tint.b, warmTint));
    }

    Transform MakeLayer(string name, Sprite sprite, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(cam.transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = Order;
        return go.transform;
    }

    void LateUpdate()
    {
        if (cam == null || vignetteT == null) return;
        float h = cam.orthographicSize * 2f;
        float w = h * cam.aspect;
        var s = new Vector3(w * 1.03f, h * 1.03f, 1f);
        vignetteT.localScale = s;
        tintT.localScale = s;
    }

    Sprite MakeVignetteSprite()
    {
        const int n = 128;
        var tex = NewTex(n);
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float nx = (x + 0.5f) / n * 2f - 1f;
            float ny = (y + 0.5f) / n * 2f - 1f;
            float r = Mathf.Clamp01(Mathf.Sqrt(nx * nx + ny * ny) / 1.4142f);
            float v = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.40f, 1f, r));
            px[y * n + x] = new Color(0.03f, 0.01f, 0.01f, v * vignette * 0.8f);
        }
        tex.SetPixels(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);   // 1 x 1 unidade
    }

    Sprite MakeWhiteSprite()
    {
        var tex = NewTex(4);
        var px = new Color[16];
        for (int i = 0; i < px.Length; i++) px[i] = Color.white;
        tex.SetPixels(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
    }

    static Texture2D NewTex(int n) => new Texture2D(n, n, TextureFormat.RGBA32, false)
    {
        filterMode = FilterMode.Bilinear,
        wrapMode = TextureWrapMode.Clamp,
        hideFlags = HideFlags.HideAndDontSave
    };
}
