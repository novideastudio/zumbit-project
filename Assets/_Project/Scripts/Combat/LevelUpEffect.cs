using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Level up "de RPG". Chamado pelo PlayerStats a cada nivel ganho:
///  1) o SPRITE do sobrevivente que subiu pulsa em dourado e volta ao normal (PixelCharacterMotion.LevelGlow);
///  2) aos pes dele: aneis de luz no chao, coluna de luz subindo e fagulhas em forma de estrela;
///  3) sobre a cabeca: "LEVEL UP!" + "NV x" que pula, sobe e some;
///  4) faixa grande no topo da tela: titulo dourado com brilho varrendo, estrelinhas, linhas e subtitulo
///     com o nome do sobrevivente e o novo nivel;
///  5) fanfarra (ZombitAudio.PlayLevelUp).
/// Tudo e criado em codigo (sem prefab/cena) e usa tempo nao escalado quando faz sentido.
/// </summary>
public sealed class LevelUpEffect : MonoBehaviour
{
    static readonly Color Gold = new Color(1f, 0.80f, 0.22f, 1f);
    static readonly Color GoldPale = new Color(1f, 0.96f, 0.62f, 1f);
    static readonly Color Orange = new Color(1f, 0.52f, 0.08f, 1f);
    static readonly Color Cyan = new Color(0.45f, 0.90f, 1f, 1f);

    static LevelUpEffect instance;
    static Sprite glowSprite, ringSprite, starSprite, pillarSprite, streakSprite;
    static Material fxMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
        glowSprite = ringSprite = starSprite = pillarSprite = streakSprite = null;
        fxMaterial = null;
    }

    public static void Play(PlayerStats stats, int level)
    {
        if (stats == null) return;
        if (instance == null)
        {
            var go = new GameObject("LevelUpEffect_Runtime");
            instance = go.AddComponent<LevelUpEffect>();
        }
        instance.Run(stats, level);
    }

    // ------------------------------------------------------------------ particulas de mundo
    sealed class P
    {
        public Transform tr;
        public SpriteRenderer sr;
        public int mode;                 // 0 fagulha, 1 anel, 2 coluna, 3 brilho
        public float age, life;          // age < 0 = ainda esperando (delay)
        public Vector3 vel;
        public float s0, s1, sway, phase;
        public Color color;
        public float flat = 1f;          // achatamento vertical (aneis no chao)
        public Vector3 basePos;
    }

    readonly List<P> active = new List<P>(96);
    readonly Stack<P> free = new Stack<P>(96);

    void Update()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var p = active[i];
            if (p.tr == null) { active.RemoveAt(i); continue; }
            p.age += Time.deltaTime;
            if (p.age < 0f) { if (p.sr.enabled) p.sr.enabled = false; continue; }
            if (!p.sr.enabled) p.sr.enabled = true;
            float t = Mathf.Clamp01(p.age / p.life);
            Color c = p.color;
            switch (p.mode)
            {
                case 0:   // fagulha: sobe balancando, pisca e encolhe
                {
                    p.basePos += p.vel * Time.deltaTime;
                    Vector3 pos = p.basePos + new Vector3(Mathf.Sin(p.age * 5f + p.phase) * p.sway, 0f, 0f);
                    p.tr.position = pos;
                    float tw = 0.65f + 0.35f * Mathf.Sin(p.age * 18f + p.phase * 3f);
                    c.a = Mathf.Min(Mathf.SmoothStep(0f, 0.12f, t), 1f - Mathf.SmoothStep(0.55f, 1f, t)) * tw;
                    float s = Mathf.Lerp(p.s0, p.s1, t);
                    p.tr.localScale = new Vector3(s, s, 1f);
                    p.tr.Rotate(0f, 0f, 90f * Time.deltaTime);
                    break;
                }
                case 1:   // anel que se abre no chao
                {
                    float e = 1f - (1f - t) * (1f - t) * (1f - t);
                    float s = Mathf.Lerp(p.s0, p.s1, e);
                    p.tr.localScale = new Vector3(s, s * p.flat, 1f);
                    c.a = (1f - t) * (1f - t) * p.color.a;
                    break;
                }
                case 2:   // coluna de luz: sobe rapido, afina e apaga
                {
                    float rise = 1f - (1f - Mathf.Clamp01(t * 3.2f)) * (1f - Mathf.Clamp01(t * 3.2f));
                    float w = Mathf.Lerp(p.s0, p.s0 * 0.35f, Mathf.SmoothStep(0.2f, 1f, t));
                    p.tr.localScale = new Vector3(w, p.s1 * rise, 1f);
                    c.a = Mathf.Min(Mathf.SmoothStep(0f, 0.08f, t), 1f - Mathf.SmoothStep(0.35f, 1f, t)) * p.color.a;
                    break;
                }
                default:  // brilho: nasce forte e se dissolve
                {
                    float s = Mathf.Lerp(p.s0, p.s1, 1f - (1f - t) * (1f - t));
                    p.tr.localScale = new Vector3(s, s, 1f);
                    c.a = (1f - t) * p.color.a;
                    break;
                }
            }
            p.sr.color = c;
            if (p.age >= p.life) Recycle(i);
        }
    }

    void Recycle(int index)
    {
        var p = active[index];
        active.RemoveAt(index);
        if (p.tr != null) { p.sr.enabled = false; p.tr.gameObject.SetActive(false); free.Push(p); }
    }

    P NewParticle(Sprite sprite, int order, Vector3 pos, Color color, float life, int mode, float delay)
    {
        P p = null;
        while (free.Count > 0) { var f = free.Pop(); if (f.tr != null) { p = f; break; } }
        if (p == null)
        {
            var go = new GameObject("LevelUpFX");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            if (fxMaterial == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                if (sh != null) fxMaterial = new Material(sh) { name = "Zombit_LevelUpFX_Material" };
            }
            if (fxMaterial != null) sr.sharedMaterial = fxMaterial;
            p = new P { tr = go.transform, sr = sr };
        }
        p.tr.gameObject.SetActive(true);
        p.tr.position = pos;
        p.tr.rotation = Quaternion.identity;
        p.sr.sprite = sprite;
        p.sr.sortingOrder = order;
        p.sr.color = new Color(color.r, color.g, color.b, 0f);
        p.sr.enabled = delay <= 0f;
        p.color = color;
        p.life = life;
        p.mode = mode;
        p.age = -delay;
        p.flat = 1f;
        p.vel = Vector3.zero;
        p.sway = 0f;
        p.phase = Random.value * 6.28f;
        p.basePos = pos;
        active.Add(p);
        return p;
    }

    // ------------------------------------------------------------------ execucao
    void Run(PlayerStats stats, int level)
    {
        string who = stats.Survivor != null && !string.IsNullOrEmpty(stats.Survivor.name)
            ? stats.Survivor.name.ToUpperInvariant() : "SOBREVIVENTE";

        // 1) sprite do sobrevivente pulsa em dourado e volta ao normal
        var motion = stats.GetComponent<PixelCharacterMotion>();
        if (motion != null) motion.LevelGlow(Gold, 1.3f);

        // 2) efeitos de mundo
        var sr = stats.GetComponent<SpriteRenderer>();
        Vector3 center = sr != null ? sr.bounds.center : stats.transform.position;
        float feetY = sr != null ? sr.bounds.min.y : stats.transform.position.y;
        float headY = sr != null ? sr.bounds.max.y : stats.transform.position.y + 2f;
        float z = stats.transform.position.z;
        Vector3 feet = new Vector3(center.x, feetY + 0.05f, z - 0.1f);
        WorldBurst(feet, headY);

        // 3) texto sobre a cabeca
        var lbl = new GameObject("LevelUpFloat").AddComponent<FloatLabel>();
        lbl.Begin(new Vector3(center.x, headY + 0.35f, z - 0.3f), level);

        // 4) faixa de tela + 5) som
        ShowBanner(who, level);
        ZombitAudio.PlayLevelUp();
    }

    void WorldBurst(Vector3 feet, float headY)
    {
        EnsureSprites();
        const int Over = 21050;
        float h = Mathf.Max(1.5f, headY - feet.y);

        // brilho quente envolvendo o personagem (lava a cor do sprite com luz dourada)
        var glow = NewParticle(glowSprite, Over, feet + new Vector3(0f, h * 0.5f, 0f), new Color(1f, 0.85f, 0.35f, 0.55f), 0.9f, 3, 0f);
        glow.s0 = h * 1.1f; glow.s1 = h * 2.2f;

        // aneis de luz no chao (2, defasados)
        for (int i = 0; i < 2; i++)
        {
            var ring = NewParticle(ringSprite, Over - 1, feet, i == 0 ? GoldPale : Orange, 0.85f, 1, i * 0.16f);
            ring.s0 = 0.5f; ring.s1 = 3.6f - i * 0.8f; ring.flat = 0.34f;
            ring.color.a = 0.95f;
        }

        // coluna de luz
        var pillar = NewParticle(pillarSprite, Over - 2, feet, new Color(1f, 0.86f, 0.40f, 0.62f), 1.05f, 2, 0f);
        pillar.s0 = 6.2f;            // largura (pixels->unidades pelo ppu do sprite)
        pillar.s1 = (h * 2.4f) / 2f; // altura em escala (sprite tem 2 unidades)

        // fagulhas em estrela subindo
        for (int i = 0; i < 20; i++)
        {
            Vector3 pos = feet + new Vector3(Random.Range(-0.7f, 0.7f), Random.Range(0f, 0.25f), 0f);
            Color col = i % 3 == 0 ? Color.white : (i % 3 == 1 ? Gold : Cyan);
            var s = NewParticle(starSprite, Over + 1, pos, col, Random.Range(0.9f, 1.5f), 0, Random.Range(0f, 0.45f));
            s.vel = new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(1.4f, 3.0f), 0f);
            s.sway = Random.Range(0.08f, 0.22f);
            s.s0 = Random.Range(0.22f, 0.42f); s.s1 = 0.05f;
        }
    }

    // ------------------------------------------------------------------ texto sobre a cabeca
    sealed class FloatLabel : MonoBehaviour
    {
        TextMeshPro face, shadow;
        float age;
        const float Duration = 1.6f;
        Vector3 basePos;

        static TMP_FontAsset Font()
        {
            var th = UITheme.Current;
            if (th != null && th.displayFont != null) return th.displayFont;
            if (th != null && th.font != null) return th.font;
            return TMP_Settings.defaultFontAsset;
        }

        TextMeshPro Make(string n, int order)
        {
            var g = new GameObject(n);
            g.transform.SetParent(transform, false);
            var t = g.AddComponent<TextMeshPro>();
            var f = Font();
            if (f != null) t.font = f;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.richText = true;
            t.fontStyle = FontStyles.Bold;
            t.rectTransform.sizeDelta = new Vector2(14f, 5f);
            t.sortingOrder = order;
            t.fontSize = 6.4f;
            return t;
        }

        public void Begin(Vector3 pos, int level)
        {
            basePos = pos;
            transform.position = pos;
            if (Font() == null) { Destroy(gameObject); return; }
            string txt = "LEVEL UP!\n<size=62%>NV " + level + "</size>";
            face = Make("Face", 32011);
            shadow = Make("Shadow", 32010);
            face.text = txt;
            face.enableVertexGradient = true;
            face.colorGradient = new VertexGradient(GoldPale, GoldPale, Orange, Orange);
            face.color = Color.white;
            shadow.text = txt;
            shadow.color = new Color(0.1f, 0.03f, 0f, 0.9f);
            shadow.rectTransform.localPosition = new Vector3(0.06f, -0.08f, 0.01f);
            transform.localScale = Vector3.one * 0.01f;
        }

        void Update()
        {
            if (face == null) { Destroy(gameObject); return; }
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / Duration);
            float pop = age < 0.28f ? Back(age / 0.28f, 1.9f) : 1f;
            float end = Mathf.Lerp(1f, 0.8f, Mathf.SmoothStep(0.7f, 1f, t));
            transform.localScale = Vector3.one * (pop * end);
            transform.position = basePos + new Vector3(0f, Mathf.Lerp(0f, 1.1f, 1f - (1f - t) * (1f - t)), 0f);
            float a = 1f - Mathf.SmoothStep(0.65f, 1f, t);
            face.colorGradient = new VertexGradient(
                new Color(GoldPale.r, GoldPale.g, GoldPale.b, a), new Color(GoldPale.r, GoldPale.g, GoldPale.b, a),
                new Color(Orange.r, Orange.g, Orange.b, a), new Color(Orange.r, Orange.g, Orange.b, a));
            face.alpha = a;
            shadow.color = new Color(0.1f, 0.03f, 0f, 0.9f * a);
            if (age >= Duration) Destroy(gameObject);
        }

        static float Back(float x, float k)
        {
            x = Mathf.Clamp01(x);
            float c3 = k + 1f;
            return Mathf.Max(0.01f, 1f + c3 * Mathf.Pow(x - 1f, 3f) + k * Mathf.Pow(x - 1f, 2f));
        }
    }

    // ------------------------------------------------------------------ faixa na tela
    Canvas canvas;
    RectTransform bannerRoot, streak, shineBar, titleRt, subRt, glowRt, lineL, lineR;
    CanvasGroup bannerGroup;
    TMP_Text title, titleShadow, subtitle;
    readonly List<Image> sparks = new List<Image>();
    Coroutine bannerRoutine;

    void BuildBanner()
    {
        canvas = UIFactory.MakeCanvas("LevelUpBannerCanvas", 70);
        canvas.transform.SetParent(transform, false);
        var gr = canvas.GetComponent<GraphicRaycaster>();
        if (gr != null) gr.enabled = false;   // nunca bloqueia cliques

        bannerRoot = UIFactory.Node("Banner", canvas.transform);
        UIFactory.Place(bannerRoot, new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(1100f, 260f));
        bannerGroup = bannerRoot.gameObject.AddComponent<CanvasGroup>();
        bannerGroup.blocksRaycasts = false;
        bannerGroup.interactable = false;

        // brilho redondo atras do titulo
        var glowImg = UIFactory.Img(bannerRoot, new Color(1f, 0.70f, 0.20f, 0.38f), "Glow");
        glowImg.sprite = GetGlowSprite();
        glowImg.raycastTarget = false;
        glowRt = glowImg.rectTransform;
        UIFactory.Place(glowRt, new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(900f, 280f));

        // faixa horizontal de luz
        var streakImg = UIFactory.Img(bannerRoot, new Color(1f, 0.78f, 0.28f, 0.55f), "Streak");
        streakImg.sprite = GetStreakSprite();
        streakImg.raycastTarget = false;
        streak = streakImg.rectTransform;
        UIFactory.Place(streak, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(1100f, 70f));

        // titulo (sombra + face) dentro de um recorte para o brilho "varrer" so o texto
        var clip = UIFactory.Node("TitleClip", bannerRoot);
        UIFactory.Place(clip, new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(820f, 130f));
        clip.gameObject.AddComponent<RectMask2D>();

        titleShadow = UIFactory.Txt(clip, "LEVEL UP!", 104f, TextAlignmentOptions.Center, new Color(0.18f, 0.05f, 0f, 0.9f), "TitleShadow");
        titleShadow.fontStyle = FontStyles.Bold;
        titleShadow.textWrappingMode = TextWrappingModes.NoWrap;
        UIFactory.Place(titleShadow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(5f, -6f), new Vector2(820f, 130f));

        title = UIFactory.Txt(clip, "LEVEL UP!", 104f, TextAlignmentOptions.Center, Color.white, "Title");
        title.fontStyle = FontStyles.Bold;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.enableVertexGradient = true;
        title.colorGradient = new VertexGradient(GoldPale, GoldPale, Orange, Orange);
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 130f));
        titleRt = clip;

        var shine = UIFactory.Img(clip, new Color(1f, 1f, 1f, 0.0f), "Shine");
        shine.sprite = GetStreakSprite();
        shine.raycastTarget = false;
        shineBar = shine.rectTransform;
        UIFactory.Place(shineBar, new Vector2(0.5f, 0.5f), new Vector2(-600f, 0f), new Vector2(130f, 170f));
        shineBar.localRotation = Quaternion.Euler(0f, 0f, -18f);

        // subtitulo + linhas decorativas
        subtitle = UIFactory.Txt(bannerRoot, "", 34f, TextAlignmentOptions.Center, Cyan, "Subtitle");
        subtitle.fontStyle = FontStyles.Bold;
        subtitle.textWrappingMode = TextWrappingModes.NoWrap;
        subtitle.characterSpacing = 8f;
        subtitle.richText = true;
        subRt = subtitle.rectTransform;
        UIFactory.Place(subRt, new Vector2(0.5f, 0.5f), new Vector2(0f, -62f), new Vector2(900f, 48f));

        lineL = MakeLine("LineL", -1f);
        lineR = MakeLine("LineR", 1f);

        // estrelinhas que estouram em volta
        for (int i = 0; i < 16; i++)
        {
            var im = UIFactory.Img(bannerRoot, GoldPale, "Spark");
            im.sprite = UIKit.Star;
            im.raycastTarget = false;
            im.rectTransform.sizeDelta = new Vector2(24f, 24f);
            im.gameObject.SetActive(false);
            sparks.Add(im);
        }
        bannerRoot.gameObject.SetActive(false);
    }

    RectTransform MakeLine(string name, float side)
    {
        var img = UIFactory.Img(bannerRoot, new Color(1f, 0.78f, 0.3f, 0.9f), name);
        img.sprite = GetStreakSprite();
        img.raycastTarget = false;
        var rt = img.rectTransform;
        UIFactory.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(side * 330f, -62f), new Vector2(240f, 4f));
        return rt;
    }

    void ShowBanner(string who, int level)
    {
        if (canvas == null) BuildBanner();
        string hex = ColorUtility.ToHtmlStringRGB(Gold);
        subtitle.text = $"{who}   <color=#{hex}>·</color>   NÍVEL <color=#{hex}>{level}</color>";
        if (bannerRoutine != null) StopCoroutine(bannerRoutine);
        bannerRoutine = StartCoroutine(BannerRoutine());
    }

    IEnumerator BannerRoutine()
    {
        bannerRoot.gameObject.SetActive(true);
        bannerRoot.SetAsLastSibling();
        const float inT = 0.38f, hold = 1.9f, outT = 0.6f;
        float total = inT + hold + outT;
        // estrelinhas: posicao/velocidade iniciais
        var vel = new Vector2[sparks.Count];
        var pos = new Vector2[sparks.Count];
        var delay = new float[sparks.Count];
        for (int i = 0; i < sparks.Count; i++)
        {
            pos[i] = new Vector2(Random.Range(-380f, 380f), Random.Range(-10f, 40f));
            vel[i] = new Vector2(Random.Range(-90f, 90f), Random.Range(60f, 200f));
            delay[i] = Random.Range(0.1f, 0.9f);
            sparks[i].gameObject.SetActive(false);
        }

        float t = 0f;
        while (t < total)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / inT);
            float outK = Mathf.Clamp01((t - inT - hold) / outT);

            float pop = k < 1f ? Back(k, 1.6f) : 1f;
            bannerRoot.localScale = Vector3.one * (Mathf.Lerp(0.3f, 1f, pop) * Mathf.Lerp(1f, 0.92f, outK));
            bannerGroup.alpha = Mathf.Clamp01(k * 2.2f) * (1f - outK * outK);
            bannerRoot.anchoredPosition = new Vector2(0f, -190f + outK * 36f);

            // letras abrem de um espacamento largo para o normal
            float spacing = Mathf.Lerp(70f, 6f, 1f - (1f - k) * (1f - k) * (1f - k));
            title.characterSpacing = spacing; titleShadow.characterSpacing = spacing;

            // faixa de luz abre do centro; pulsa de leve
            float breathe = 1f + 0.06f * Mathf.Sin(t * 5f);
            streak.localScale = new Vector3(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.35f)) * breathe, 1f, 1f);
            glowRt.localScale = Vector3.one * (0.85f + 0.15f * Mathf.Sin(t * 4f)) * Mathf.Lerp(0.6f, 1f, k);
            lineL.localScale = lineR.localScale = new Vector3(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.15f) / 0.4f)), 1f, 1f);

            // brilho varrendo o titulo (uma passada, depois outra mais discreta)
            float sweep = Mathf.Clamp01((t - 0.22f) / 0.75f);
            float sweep2 = Mathf.Clamp01((t - 1.35f) / 0.7f);
            float sx = sweep > 0f && sweep < 1f ? Mathf.Lerp(-520f, 520f, sweep) : (sweep2 > 0f && sweep2 < 1f ? Mathf.Lerp(-520f, 520f, sweep2) : 9999f);
            shineBar.anchoredPosition = new Vector2(sx, 0f);
            var shineImg = shineBar.GetComponent<Image>();
            shineImg.color = new Color(1f, 1f, 0.92f, sweep2 > 0f && sweep2 < 1f ? 0.45f : 0.7f);

            // estrelinhas
            for (int i = 0; i < sparks.Count; i++)
            {
                float lt = t - delay[i];
                var im = sparks[i];
                if (lt < 0f || lt > 1.1f) { if (im.gameObject.activeSelf) im.gameObject.SetActive(false); continue; }
                if (!im.gameObject.activeSelf) im.gameObject.SetActive(true);
                Vector2 p = pos[i] + vel[i] * lt + new Vector2(0f, -60f * lt * lt);
                im.rectTransform.anchoredPosition = p + new Vector2(0f, 10f);
                float a = Mathf.Sin(Mathf.Clamp01(lt / 1.1f) * Mathf.PI);
                float s = Mathf.Lerp(1.1f, 0.3f, lt / 1.1f);
                im.rectTransform.localScale = new Vector3(s, s, 1f);
                im.rectTransform.localRotation = Quaternion.Euler(0f, 0f, lt * 140f);
                im.color = new Color(i % 2 == 0 ? 1f : 0.7f, 0.95f, i % 2 == 0 ? 0.65f : 1f, a);
            }
            yield return null;
        }
        bannerRoot.gameObject.SetActive(false);
        bannerRoutine = null;
    }

    static float Back(float x, float k)
    {
        x = Mathf.Clamp01(x);
        float c3 = k + 1f;
        return Mathf.Max(0.01f, 1f + c3 * Mathf.Pow(x - 1f, 3f) + k * Mathf.Pow(x - 1f, 2f));
    }

    // ------------------------------------------------------------------ sprites gerados em codigo
    static void EnsureSprites()
    {
        GetGlowSprite();
        GetStreakSprite();
        if (ringSprite == null) ringSprite = MakeRing();
        if (starSprite == null) starSprite = MakeStar();
        if (pillarSprite == null) pillarSprite = MakePillar();
    }

    static Texture2D NewTex(int w, int h)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };
        return t;
    }

    static Sprite GetGlowSprite()
    {
        if (glowSprite != null) return glowSprite;
        const int n = 64;
        var tex = NewTex(n, n);
        var px = new Color32[n * n];
        float mid = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x - mid) / mid, dy = (y - mid) / mid;
                float a = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)), 1.8f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply(false, true);
        glowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 64f);
        glowSprite.hideFlags = HideFlags.HideAndDontSave;
        return glowSprite;
    }

    static Sprite GetStreakSprite()
    {
        if (streakSprite != null) return streakSprite;
        const int w = 128, h = 16;
        var tex = NewTex(w, h);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h * 2f - 1f;
                float a = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(u)), 0.9f) * Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(v)), 1.4f);
                px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply(false, true);
        streakSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        streakSprite.hideFlags = HideFlags.HideAndDontSave;
        return streakSprite;
    }

    static Sprite MakeRing()
    {
        const int n = 64;
        var tex = NewTex(n, n);
        var px = new Color32[n * n];
        float mid = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - mid) * (x - mid) + (y - mid) * (y - mid)) / mid;
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.86f) / 0.12f);
                a = a * a * (3f - 2f * a);
                a = Mathf.Max(a, Mathf.Clamp01(1f - d) * 0.12f);   // leve preenchimento interno
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply(false, true);
        var sp = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 64f);
        sp.hideFlags = HideFlags.HideAndDontSave;
        return sp;
    }

    static Sprite MakeStar()
    {
        const int n = 32;
        var tex = NewTex(n, n);
        var px = new Color32[n * n];
        float mid = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float nx = Mathf.Abs((x - mid) / mid), ny = Mathf.Abs((y - mid) / mid);
                float a = Mathf.Clamp01(1f - (Mathf.Sqrt(nx) + Mathf.Sqrt(ny)));   // estrela de 4 pontas (astroide)
                a = Mathf.Clamp01(a * 1.8f);
                float core = Mathf.Clamp01(1f - Mathf.Sqrt(nx * nx + ny * ny) * 2.2f);
                a = Mathf.Max(a, core);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply(false, true);
        var sp = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 64f);
        sp.hideFlags = HideFlags.HideAndDontSave;
        return sp;
    }

    // Coluna de luz: base larga e brilhante, topo some; bordas suaves. Pivot na base. 16x128 px @64ppu = 0,25 x 2 unidades.
    static Sprite MakePillar()
    {
        const int w = 16, h = 128;
        var tex = NewTex(w, h);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f;
                float v = (y + 0.5f) / h;
                float a = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(u)), 1.3f) * Mathf.Pow(1f - v, 1.4f);
                px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
            }
        tex.SetPixels32(px); tex.Apply(false, true);
        var sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 64f);
        sp.hideFlags = HideFlags.HideAndDontSave;
        return sp;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
