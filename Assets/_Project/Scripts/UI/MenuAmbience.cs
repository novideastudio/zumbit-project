using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Vida para a arte do menu principal: zoom bem lento, brilho tremulo da fogueira e brasas subindo.
/// Todos os elementos sao filhos da arte e usam ancoras NORMALIZADAS (0..1 da imagem), entao
/// acompanham a fogueira em qualquer proporcao de tela. Usa tempo nao escalado (menu pausado funciona).
/// </summary>
public sealed class MenuAmbience : MonoBehaviour
{
    // Posicao da fogueira na arte: x da esquerda, y da BASE (UI), ambos 0..1.
    public Vector2 fire = new Vector2(0.553f, 0.27f);
    [Range(0f, 0.05f)] public float breathZoom = 0.018f;
    [Range(0f, 1f)] public float glowStrength = 0.42f;
    [Range(0, 40)] public int emberCount = 18;

    sealed class Ember
    {
        public RectTransform rt; public Image img;
        public float age, life, speed, drift, phase, x0;
    }

    RectTransform art;
    RectTransform glowRt;
    Image glowImg;
    Texture2D tex;
    Sprite glowSprite;
    readonly System.Collections.Generic.List<Ember> embers = new System.Collections.Generic.List<Ember>();

    public static MenuAmbience Attach(RectTransform artRect)
    {
        var a = artRect.gameObject.AddComponent<MenuAmbience>();
        a.Build(artRect);
        return a;
    }

    void Build(RectTransform artRect)
    {
        art = artRect;
        const int n = 64;
        tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[n * n];
        float mid = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float d = Mathf.Sqrt(((x - mid) / mid) * ((x - mid) / mid) + ((y - mid) / mid) * ((y - mid) / mid));
            px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - d), 1.8f));
        }
        tex.SetPixels(px); tex.Apply(false, true);
        glowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        glowSprite.hideFlags = HideFlags.HideAndDontSave;

        glowImg = NewImage("CampfireGlow", new Color(1f, 0.55f, 0.18f, glowStrength));
        glowRt = glowImg.rectTransform;
        glowRt.anchorMin = glowRt.anchorMax = fire;
        glowRt.sizeDelta = new Vector2(900f, 620f);

        for (int i = 0; i < emberCount; i++)
        {
            var img = NewImage("Ember", Color.clear);
            var e = new Ember { rt = img.rectTransform, img = img };
            float s = Random.Range(5f, 10f);
            e.rt.sizeDelta = new Vector2(s, s);
            Respawn(e, true);
            embers.Add(e);
        }
    }

    Image NewImage(string name, Color c)
    {
        var rt = UIFactory.Node(name, art);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = glowSprite;
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    void Respawn(Ember e, bool initial)
    {
        e.life = Random.Range(1.8f, 3.6f);
        e.age = initial ? Random.Range(0f, e.life) : 0f;
        e.speed = Random.Range(0.07f, 0.14f);     // fracao da altura da arte por segundo
        e.drift = Random.Range(-0.02f, 0.02f);
        e.phase = Random.Range(0f, 6.28f);
        e.x0 = fire.x + Random.Range(-0.025f, 0.025f);
    }

    void Update()
    {
        float t = Time.unscaledTime;
        if (art != null) art.localScale = Vector3.one * (1f + breathZoom * (0.5f + 0.5f * Mathf.Sin(t * 0.12f)));

        if (glowImg != null)
        {
            float n = Mathf.PerlinNoise(t * 3.2f, 11.3f) * 0.6f + Mathf.PerlinNoise(t * 7.1f, 3.7f) * 0.4f;
            var c = glowImg.color; c.a = glowStrength * (0.65f + 0.7f * n);
            glowImg.color = c;
            float sc = 0.95f + 0.1f * n;
            glowRt.localScale = new Vector3(sc, sc, 1f);
        }

        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < embers.Count; i++)
        {
            var e = embers[i];
            e.age += dt;
            if (e.age >= e.life) Respawn(e, false);
            float k = e.age / e.life;
            float x = e.x0 + e.drift * e.age + Mathf.Sin(t * 3f + e.phase) * 0.004f;
            float y = fire.y - 0.02f + e.speed * e.age;
            e.rt.anchorMin = e.rt.anchorMax = new Vector2(x, y);
            e.rt.anchoredPosition = Vector2.zero;
            float a = Mathf.Clamp01(Mathf.Min(k * 5f, (1f - k) * 2.2f)) * (0.55f + 0.35f * Mathf.Sin(t * 14f + e.phase));
            e.img.color = new Color(1f, Mathf.Lerp(0.75f, 0.35f, k), 0.15f, a);
        }
    }

    void OnDestroy()
    {
        if (glowSprite != null) Destroy(glowSprite);
        if (tex != null) Destroy(tex);
    }
}
