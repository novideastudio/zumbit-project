using UnityEngine;

public class YSort : MonoBehaviour
{
    /// <summary>Desempate para personagens na mesma altura (menor = desenhado atras).</summary>
    [HideInInspector] public int sortBias;

    [Header("Sombra de contato (ancora o personagem no chao)")]
    public bool contactShadow = true;
    [Range(0f, 1f)] public float shadowOpacity = 0.42f;
    [Range(0.3f, 1.2f)] public float shadowWidth = 0.80f;    // fracao da largura do sprite
    [Range(0.1f, 0.5f)] public float shadowAspect = 0.26f;   // altura / largura da elipse

    SpriteRenderer sr;
    float spriteMinY;   // base do sprite em unidades locais (relativa ao pivot)
    Sprite cachedSprite;

    SpriteRenderer shadow;
    static Sprite shadowSprite;
    static Transform shadowRoot;

    void Awake() => sr = GetComponent<SpriteRenderer>();

    void OnEnable() { if (shadow != null) shadow.gameObject.SetActive(true); }
    void OnDisable() { if (shadow != null) shadow.gameObject.SetActive(false); }
    void OnDestroy() { if (shadow != null) Destroy(shadow.gameObject); }

    void LateUpdate()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;

        // O bounds do sprite so muda quando o sprite muda; evita sr.bounds (chamada nativa) por quadro.
        if (sr.sprite != cachedSprite)
        {
            cachedSprite = sr.sprite;
            spriteMinY = cachedSprite.bounds.min.y;
        }

        // Sort by the sprite's feet/bottom edge, not its pivot or center.
        float feetY = transform.position.y + spriteMinY * Mathf.Abs(transform.localScale.y);
        sr.sortingOrder = Mathf.RoundToInt(-feetY * 100f) + sortBias;

        UpdateShadow(feetY);
    }

    // Elipse escura nos pes. Fica logo ATRAS do personagem (order - 1), entao quem esta na frente cobre a sombra de quem esta atras.
    void UpdateShadow(float feetY)
    {
        if (!contactShadow)
        {
            if (shadow != null) shadow.enabled = false;
            return;
        }
        if (shadow == null) CreateShadow();
        if (shadow == null) return;

        float sx = Mathf.Abs(transform.lossyScale.x);
        var b = cachedSprite.bounds;
        float width = Mathf.Max(0.25f, b.size.x * sx * shadowWidth);
        float height = width * shadowAspect;
        float cx = transform.position.x + b.center.x * sx * (sr.flipX ? -1f : 1f);

        var t = shadow.transform;
        t.position = new Vector3(cx, feetY + height * 0.12f, transform.position.z);
        t.localScale = new Vector3(width, height, 1f);
        shadow.enabled = sr.enabled;
        shadow.sortingLayerID = sr.sortingLayerID;
        shadow.sortingOrder = sr.sortingOrder - 1;
        shadow.color = new Color(0f, 0f, 0f, shadowOpacity * sr.color.a);   // some junto com o personagem (morte/fade)
    }

    void CreateShadow()
    {
        if (shadowSprite == null) shadowSprite = MakeShadowSprite();
        if (shadowRoot == null) shadowRoot = new GameObject("ContactShadows").transform;
        var go = new GameObject("ContactShadow");
        go.transform.SetParent(shadowRoot, false);
        shadow = go.AddComponent<SpriteRenderer>();
        shadow.sprite = shadowSprite;
    }

    static Sprite MakeShadowSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f) / size * 2f - 1f;
            float dy = (y + 0.5f) / size * 2f - 1f;
            float r2 = Mathf.Clamp01(dx * dx + dy * dy);
            float a = Mathf.Pow(1f - r2, 1.6f);   // centro denso, borda suave
            px[y * size + x] = new Color(1f, 1f, 1f, a);
        }
        tex.SetPixels(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);   // 1 x 1 unidade
    }
}
