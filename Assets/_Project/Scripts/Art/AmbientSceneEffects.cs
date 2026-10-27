using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Atmosfera procedural leve para o cenario de combate. Os pontos (sol, luzes, fogo, fumaca)
/// vem do SceneProfile do cenario atual (SceneBackgrounds). Adiciona chuva, fumaca, brasas,
/// tremulo de luzes e um pulso suave de halo sobre o sol pintado. All effects are recycled and
/// sorted behind gameplay characters to keep combat readable.
/// </summary>
[DisallowMultipleComponent]
public sealed class AmbientSceneEffects : MonoBehaviour
{
    [Header("Clima aleatorio")]
    [Tooltip("Sorteia o clima ao iniciar (seco / garoa / chuva forte) e pode mudar durante a missao. Cenarios internos (shopping) nunca chovem.")]
    public bool randomWeather = true;
    [Range(0f, 1f)] public float chanceDry = 0.45f;
    [Range(0f, 1f)] public float chanceLight = 0.33f;   // o resto e chuva forte (com raios)

    [Header("Vento e vida")]
    public bool enableLeaves = true;
    [Range(0, 40)] public int leafCount = 16;
    [Range(0, 8)] public int frontLeafCount = 3;
    public bool enableLightShafts = true;
    public bool enableBirds = true;

    [Header("Chuva")]
    public bool enableRain = true;
    [Range(24, 160)] public int rainCount = 120;
    [Range(0.2f, 1f)] public float rainOpacity = 0.48f;
    [Range(2f, 5f)] public float rainSpeed = 3.55f;
    [Range(-1.2f, 1.2f)] public float rainWind = -0.42f;

    [Header("Fogo (barris pintados na arte)")]
    public bool enableBarrelFire = true;
    [Range(0.1f, 1.5f)] public float fireGlowIntensity = 0.6f;
    [Tooltip("Brasas POR fogueira")]
    [Range(2, 20)] public int emberCount = 8;

    [Header("Fumaça das colunas pintadas")]
    public bool enableChimneySmoke = true;
    [Tooltip("Fiapos POR coluna de fumaça")]
    [Range(2, 10)] public int chimneySmokeCount = 4;

    [Header("Neblina e poeira")]
    public bool enableFog = true;
    [Range(0.02f, 0.3f)] public float fogOpacity = 0.05f;
    [Range(0, 40)] public int dustCount = 24;

    [Header("Atmosfera")]
    public bool animateLampGlow = true;
    public bool animateSunGlow = true;
    [Range(0.05f, 0.5f)] public float glowIntensity = 0.24f;
    [Range(0.04f, 0.20f)] public float smokeOpacity = 0.12f;

    const int BackgroundOrder = -10000;
    const int SunGlowOrder = -9996;
    const int GlowOrder = -9990;
    const int SmokeOrder = -9985;
    const int RainOrder = -9975;
    const int FogOrder = -9983;
    const int FireOrder = -9987;
    const int EmberOrder = -9978;
    const int DustOrder = -9977;

    /// <summary>Definido pelo WaveBackground logo apos AddComponent (antes do Start).</summary>
    [System.NonSerialized] public SceneProfile profile;

    readonly List<GameObject> spawnedObjects = new List<GameObject>();
    readonly List<Sprite> generatedSprites = new List<Sprite>();
    readonly List<Texture2D> generatedTextures = new List<Texture2D>();
    readonly List<Glow> glows = new List<Glow>();
    readonly List<RainDrop> rain = new List<RainDrop>();
    Bounds sceneBounds;
    Sprite glowSprite, smokeSprite, rainSprite, fogSprite;
    readonly List<FogBand> fogBands = new List<FogBand>();
    readonly List<Drifter> chimneySmoke = new List<Drifter>();
    readonly List<Drifter> embers = new List<Drifter>();
    readonly List<Drifter> dust = new List<Drifter>();

    // ---- clima / vento ----
    float rainLevel, rainTarget, weatherTimer;
    bool indoor;
    float gust;                 // 0..1, rajada atual (varia com ruido)
    sealed class Leaf
    {
        public Transform tr; public SpriteRenderer sr;
        public float phase, flutter, spin, speedMul, size, floorY;
        public bool front;
    }
    readonly List<Leaf> leaves = new List<Leaf>();
    sealed class Shaft { public SpriteRenderer sr; public float baseAlpha, phase, speed; }
    readonly List<Shaft> shafts = new List<Shaft>();
    sealed class Bird { public Transform tr; public SpriteRenderer sr; public float speed, phase, y0, bobAmp; }
    readonly List<Bird> birds = new List<Bird>();
    Sprite leafSpriteA, leafSpriteB, paperSprite, shaftSprite, birdUp, birdDown, flatSprite;
    SpriteRenderer flash;
    float nextBirdsAt, nextLightningAt, flashT = -1f, thunderAt = -1f;
    int flashPhase;

    sealed class Glow
    {
        public Transform transform;
        public SpriteRenderer renderer;
        public Vector3 baseScale;
        public float phase;
        public float intensity;
        public float pulseAmount;
        public float speed;
        public Color baseColor;
        public bool isSun;
        public bool flicker;   // true = tremulo irregular (Perlin) em vez de pulso senoidal
    }

    sealed class FogBand
    {
        public Transform transform;
        public SpriteRenderer renderer;
        public float speed;
        public float halfWidth;
        public float baseAlpha;
        public float phase;
    }

    sealed class Drifter
    {
        public Transform transform;
        public SpriteRenderer renderer;
        public Vector3 velocity;
        public float age;
        public float lifetime;
        public float scale;
        public float phase;
        public bool isSmoke;
        public int kind;       // 1 fumaca de coluna, 2 brasa, 3 poeira
        public Vector2 source; // ponto de origem normalizado (fumaca/brasa)
        public float baseScale;
    }

    sealed class RainDrop
    {
        public Transform transform;
        public SpriteRenderer renderer;
        public float speed;
        public float wind;
        public float alpha;
    }

    void Start()
    {
        var backgroundRenderer = GetComponent<SpriteRenderer>();
        if (backgroundRenderer == null || backgroundRenderer.sprite == null) return;
        if (profile == null) profile = SceneBackgrounds.Get(SceneBackgrounds.DefaultId);
        sceneBounds = backgroundRenderer.bounds;
        CreateTextures();
        CreateLampGlows();
        CreateSunGlow();
        if (enableChimneySmoke) CreateChimneySmoke();
        if (enableBarrelFire) CreateBarrelFire();
        if (enableFog) CreateFog();
        CreateDust();
        indoor = profile != null && profile.id == "shopping";
        CreateRain();
        ChooseWeather(true);
        if (enableLeaves) CreateLeaves();
        if (enableLightShafts) CreateLightShafts();
        CreateFlash();
        nextBirdsAt = Time.time + Random.Range(4f, 14f);
        nextLightningAt = Time.time + Random.Range(6f, 16f);
    }

    void Update()
    {
        float t = Time.time;
        for (int i = 0; i < glows.Count; i++)
        {
            Glow g = glows[i];
            if (g == null || g.renderer == null) continue;
            bool enabled = g.isSun ? animateSunGlow : animateLampGlow;
            float wave = Mathf.Sin(t * g.speed + g.phase);
            float second = Mathf.Sin(t * g.speed * 2.1f + g.phase * 1.7f);
            float strength = enabled ? 1f + g.pulseAmount * wave + g.pulseAmount * 0.28f * second : 1f;
            if (g.flicker)
            {
                // Chama / lampada com mau contato: ruido irregular, nao uma onda regular.
                float n = Mathf.PerlinNoise(g.phase * 7.3f, t * g.speed);
                float n2 = Mathf.PerlinNoise(g.phase * 3.1f + 40f, t * g.speed * 3.3f);
                strength = Mathf.Max(0.15f, 1f + g.pulseAmount * ((n * 2f - 1f) * 2.2f + (n2 * 2f - 1f) * 0.8f));
            }
            Color c = g.baseColor;
            c.a = Mathf.Clamp01(g.intensity * strength);
            g.renderer.color = c;
            float scalePulse = enabled ? 1f + g.pulseAmount * 0.18f * wave : 1f;
            g.transform.localScale = g.baseScale * scalePulse;
        }

        UpdateDrifters(chimneySmoke, t);
        UpdateDrifters(embers, t);
        UpdateDrifters(dust, t);
        UpdateFog(t);
        UpdateWeather();
        UpdateRain();
        UpdateLeaves(t);
        UpdateShafts(t);
        UpdateBirds(t);
        UpdateLightning();
    }

    void CreateTextures()
    {
        glowSprite = MakeSprite(MakeGlowTexture(64), 32f);
        smokeSprite = MakeSprite(MakeSmokeTexture(32, 28), 32f);
        rainSprite = MakeSprite(MakeRainTexture(), 80f);
        fogSprite = MakeSprite(MakeFogTexture(), 8f);
        leafSpriteA = MakeSprite(MakeLeafTexture(0), 28f);
        leafSpriteB = MakeSprite(MakeLeafTexture(1), 28f);
        paperSprite = MakeSprite(MakeLeafTexture(2), 28f);
        shaftSprite = MakeSprite(MakeShaftTexture(), 32f, new Vector2(0.5f, 1f));
        birdUp = MakeSprite(MakeBirdTexture(true), 30f);
        birdDown = MakeSprite(MakeBirdTexture(false), 30f);
        flatSprite = MakeSprite(MakeFlatTexture(), 4f);
    }

    Texture2D NewTexture(int width, int height, FilterMode filter)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "Zombit_Ambient_RuntimeTexture";
        texture.filterMode = filter;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.hideFlags = HideFlags.DontSave;
        generatedTextures.Add(texture);
        return texture;
    }

    Sprite MakeSprite(Texture2D texture, float ppu, Vector2? pivot = null)
    {
        var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                                   pivot ?? new Vector2(0.5f, 0.5f), ppu);
        sprite.name = "Zombit_Ambient_RuntimeSprite";
        sprite.hideFlags = HideFlags.DontSave;
        generatedSprites.Add(sprite);
        return sprite;
    }

    Texture2D MakeGlowTexture(int size)
    {
        var tex = NewTexture(size, size, FilterMode.Bilinear);
        var pixels = new Color[size * size];
        float mid = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x - mid) / mid;
            float dy = (y - mid) / mid;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.Pow(Mathf.Clamp01(1f - d), 1.7f);
            pixels[y * size + x] = new Color(1f, 0.73f, 0.39f, a);
        }
        tex.SetPixels(pixels);
        tex.Apply(false, true);
        return tex;
    }

    Texture2D MakeSmokeTexture(int width, int height)
    {
        var tex = NewTexture(width, height, FilterMode.Bilinear);
        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float nx = x / (float)(width - 1);
            float ny = y / (float)(height - 1);
            float a = 0f;
            a = Mathf.Max(a, Blob(nx, ny, 0.30f, 0.51f, 0.24f, 0.33f));
            a = Mathf.Max(a, Blob(nx, ny, 0.51f, 0.41f, 0.31f, 0.39f));
            a = Mathf.Max(a, Blob(nx, ny, 0.73f, 0.53f, 0.22f, 0.32f));
            a = Mathf.Max(a, Blob(nx, ny, 0.48f, 0.72f, 0.22f, 0.20f));
            float edgeFade = Mathf.Clamp01(Mathf.Min(nx, 1f - nx) * 6f + 0.15f);
            a *= edgeFade * 0.72f;
            pixels[y * width + x] = new Color(0.63f, 0.66f, 0.70f, a);
        }
        tex.SetPixels(pixels);
        tex.Apply(false, true);
        return tex;
    }

    // Small pixel-art rain streak texture used by CreateRain().
    // Kept in the same runtime texture lifecycle as the other ambient sprites.
    Texture2D MakeRainTexture()
    {
        const int width = 4, height = 16;
        var tex = NewTexture(width, height, FilterMode.Point);
        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float a = y < 2 || y > 13 ? 0.22f : (y > 5 && y < 11 ? 0.78f : 0.55f);
            pixels[y * width + 1] = new Color(0.67f, 0.80f, 0.91f, a);
            if (y > 2 && y < 13)
                pixels[y * width + 2] = new Color(0.82f, 0.90f, 0.96f, a * 0.48f);
        }
        tex.SetPixels(pixels);
        tex.Apply(false, true);
        return tex;
    }

    // Faixa de neblina: elipse larga e suave.
    Texture2D MakeFogTexture()
    {
        const int w = 64, h = 16;
        var tex = NewTexture(w, h, FilterMode.Bilinear);
        var pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float dx = (x - (w - 1) * 0.5f) / (w * 0.5f);
            float dy = (y - (h - 1) * 0.5f) / (h * 0.5f);
            float a = Mathf.Pow(Mathf.Clamp01(1f - dx * dx), 1.4f) * Mathf.Pow(Mathf.Clamp01(1f - dy * dy), 1.2f);
            pixels[y * w + x] = new Color(0.52f, 0.30f, 0.24f, a);   // neblina quente e escura (antes quase branca: deixava o cenario lavado)
        }
        tex.SetPixels(pixels);
        tex.Apply(false, true);
        return tex;
    }

    static float Blob(float x, float y, float cx, float cy, float rx, float ry)
    {
        float dx = (x - cx) / rx;
        float dy = (y - cy) / ry;
        return Mathf.Clamp01(1f - (dx * dx + dy * dy)) * 0.82f;
    }

    void CreateLampGlows()
    {
        var lamps = profile.lamps;
        for (int i = 0; i < lamps.Length; i++)
        {
            var l = lamps[i];
            AddGlow(l.x, l.y, l.size, l.alpha, i * 1.37f,
                new Color(1f, 0.66f, 0.30f, 1f), l.flicker ? 0.10f : 0.15f, 1.6f + (i % 4) * 0.5f, false, l.flicker);
        }
    }

    void CreateSunGlow()
    {
        // O sol ja esta pintado na arte; este halo so da uma pulsacao suave de luz.
        AddGlow(profile.sun.x, profile.sun.y, profile.sunSize * 1.12f, 0.13f, 0.48f,
            new Color(1f, 0.56f, 0.20f, 1f), 0.045f, 0.78f, true);
    }

    void AddGlow(float x, float yFromTop, float size, float alpha, float phase,
                 Color color, float pulseAmount, float speed, bool isSun, bool flicker = false)
    {
        var go = new GameObject(isSun ? "AmbientSunHalo" : "AmbientLampGlow");
        spawnedObjects.Add(go);
        go.transform.position = WorldAt(x, yFromTop, -0.03f);
        go.transform.localScale = new Vector3(size, size, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = glowSprite;
        sr.sortingOrder = isSun ? SunGlowOrder : GlowOrder;
        float scaledAlpha = alpha * (glowIntensity / 0.24f);
        sr.color = new Color(color.r, color.g, color.b, scaledAlpha);
        glows.Add(new Glow
        {
            transform = go.transform, renderer = sr, baseScale = go.transform.localScale,
            phase = phase, intensity = scaledAlpha, pulseAmount = pulseAmount, speed = speed,
            baseColor = color, isSun = isSun, flicker = flicker
        });
    }

    Drifter MakeDrifter(string objectName, Sprite sprite, int sortingOrder, bool isSmoke)
    {
        var go = new GameObject(objectName);
        spawnedObjects.Add(go);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = sortingOrder;
        sr.color = Color.white;
        return new Drifter { transform = go.transform, renderer = sr, isSmoke = isSmoke };
    }

    void ResetDrifter(Drifter d, bool initial)
    {
        switch (d.kind)
        {
            case 1: ResetChimneySmoke(d, initial); return;
            case 2: ResetEmber(d, initial); return;
            default: ResetDust(d, initial); return;
        }
    }

    void ResetChimneySmoke(Drifter d, bool initial)
    {
        d.lifetime = Random.Range(6.5f, 10f);
        d.scale = Random.Range(0.55f, 0.85f);
        // Sobe e e empurrada para a esquerda pelo vento da chuva.
        d.velocity = new Vector3(Random.Range(-0.10f, -0.03f), Random.Range(0.12f, 0.20f), 0f);
        d.renderer.color = new Color(0.45f, 0.45f, 0.50f, 0f);
        d.transform.localScale = Vector3.one * d.scale;
        d.transform.position = WorldAt(d.source.x + Random.Range(-0.004f, 0.004f), d.source.y, 0f);
        d.age = initial ? Random.Range(0f, d.lifetime) : 0f;
        d.phase = Random.Range(0f, 6.28f);
    }

    void ResetEmber(Drifter d, bool initial)
    {
        d.lifetime = Random.Range(1.2f, 2.6f);
        d.scale = Random.Range(0.05f, 0.10f);
        d.velocity = new Vector3(Random.Range(-0.18f, 0.12f), Random.Range(0.45f, 0.95f), 0f);
        d.renderer.color = new Color(1f, Random.Range(0.45f, 0.75f), 0.15f, 0f);
        d.transform.localScale = Vector3.one * d.scale;
        d.transform.position = WorldAt(d.source.x + Random.Range(-0.008f, 0.008f), d.source.y, -0.01f);
        d.age = initial ? Random.Range(0f, d.lifetime) : 0f;
        d.phase = Random.Range(0f, 6.28f);
    }

    void ResetDust(Drifter d, bool initial)
    {
        d.lifetime = Random.Range(7f, 14f);
        d.scale = Random.Range(0.03f, 0.07f);
        d.velocity = new Vector3(Random.Range(-0.25f, -0.05f), Random.Range(-0.03f, 0.06f), 0f);
        d.renderer.color = new Color(1f, 0.82f, 0.62f, 0f);
        d.transform.localScale = Vector3.one * d.scale;
        d.transform.position = WorldAt(Random.Range(0.02f, 0.98f), Random.Range(0.25f, 0.70f), 0f);
        d.age = initial ? Random.Range(0f, d.lifetime) : 0f;
        d.phase = Random.Range(0f, 6.28f);
    }

    void UpdateDrifters(List<Drifter> list, float time)
    {
        for (int i = 0; i < list.Count; i++)
        {
            Drifter d = list[i];
            if (d == null || d.transform == null || d.renderer == null) continue;
            d.age += Time.deltaTime;
            if (d.age >= d.lifetime)
            {
                ResetDrifter(d, false);
                continue;
            }
            d.transform.position += d.velocity * Time.deltaTime;
            float life01 = d.age / d.lifetime;
            float fade = Mathf.Clamp01(Mathf.Min(life01 * 3.8f, (1f - life01) * 2.8f));
            if (d.isSmoke)
            {
                Color c = d.renderer.color;
                c.a = smokeOpacity * fade;
                d.renderer.color = c;
                float size = d.scale * (0.78f + life01 * 0.62f);
                d.transform.localScale = Vector3.one * size;
                // Leve balanco lateral: a fumaca serpenteia em vez de subir em linha reta.
                if (d.kind == 1)
                    d.transform.position += new Vector3(Mathf.Sin(time * 0.9f + d.phase) * 0.05f * Time.deltaTime, 0f, 0f);
            }
            else if (d.kind == 2)
            {
                // Brasa: brilha forte, tremula e encolhe ate apagar.
                Color c = d.renderer.color;
                c.a = (0.55f + 0.35f * Mathf.Sin(time * 17f + d.phase)) * fade;
                d.renderer.color = c;
                d.transform.localScale = Vector3.one * d.scale * (1f - life01 * 0.6f);
                d.transform.position += new Vector3(Mathf.Sin(time * 5f + d.phase) * 0.25f * Time.deltaTime, 0f, 0f);
            }
            else if (d.kind == 3)
            {
                Color c = d.renderer.color;
                c.a = 0.30f * fade;
                d.renderer.color = c;
            }
            else
            {
                Color c = d.renderer.color;
                c.a = (0.45f + 0.28f * Mathf.Sin(time * 11f + d.phase)) * fade;
                d.renderer.color = c;
            }
        }
    }

    // ------------------------------------------------------------------------------------
    // Fogo: os barris/fogueiras ja estao PINTADOS na arte. Aqui entram so a luz laranja
    // tremula (Perlin) e as brasas subindo, em cada ponto de profile.fires.
    // ------------------------------------------------------------------------------------
    void CreateBarrelFire()
    {
        var fires = profile.fires;
        for (int f = 0; f < fires.Length; f++)
        {
            Vector2 p = fires[f];
            AddGlow(p.x, p.y - 0.02f, 0.95f, fireGlowIntensity * 0.50f, 5.2f + f,
                new Color(1f, 0.52f, 0.14f, 1f), 0.16f, 4.2f, false, true);
            AddGlow(p.x, p.y + 0.05f, 1.6f, fireGlowIntensity * 0.22f, 7.7f + f,
                new Color(1f, 0.42f, 0.10f, 1f), 0.20f, 3.4f, false, true);

            for (int i = 0; i < emberCount; i++)
            {
                Drifter d = MakeDrifter("AmbientEmber", glowSprite, EmberOrder, false);
                d.kind = 2;
                d.source = p;
                embers.Add(d);
                ResetDrifter(d, true);
            }
        }
    }

    void CreateChimneySmoke()
    {
        var sources = profile.smokes;
        for (int s = 0; s < sources.Length; s++)
            for (int i = 0; i < chimneySmokeCount; i++)
            {
                Drifter d = MakeDrifter("AmbientChimneySmoke", smokeSprite, SmokeOrder, true);
                d.kind = 1;
                d.source = sources[s];
                chimneySmoke.Add(d);
                ResetDrifter(d, true);
            }
    }

    // ------------------------------------------------------------------------------------
    // Neblina baixa: faixas largas que deslizam devagar rente ao chao (atras dos personagens).
    // ------------------------------------------------------------------------------------
    void CreateFog()
    {
        float g = profile.groundRow;
        float[] heights = { g - 0.15f, g - 0.08f, g - 0.01f };
        float[] speeds = { 0.16f, -0.11f, 0.07f };
        float[] scales = { 2.4f, 2.9f, 2.1f };
        for (int i = 0; i < heights.Length; i++)
        {
            var go = new GameObject("AmbientFogBand");
            spawnedObjects.Add(go);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = fogSprite;
            sr.sortingOrder = FogOrder;
            go.transform.localScale = new Vector3(scales[i], scales[i] * 0.8f, 1f);
            float halfWidth = fogSprite.bounds.size.x * scales[i] * 0.5f;
            float x = Random.Range(sceneBounds.min.x, sceneBounds.max.x);
            Vector3 pos = WorldAt(0.5f, heights[i], 0f);
            pos.x = x;
            go.transform.position = pos;
            var band = new FogBand
            {
                transform = go.transform, renderer = sr, speed = speeds[i], halfWidth = halfWidth,
                baseAlpha = fogOpacity * Random.Range(0.7f, 1f), phase = Random.Range(0f, 6.28f)
            };
            sr.color = new Color(1f, 1f, 1f, band.baseAlpha);
            fogBands.Add(band);
        }
    }

    void UpdateFog(float t)
    {
        for (int i = 0; i < fogBands.Count; i++)
        {
            FogBand b = fogBands[i];
            if (b == null || b.transform == null) continue;
            Vector3 p = b.transform.position;
            p.x += b.speed * Time.deltaTime;
            float min = sceneBounds.min.x - b.halfWidth;
            float max = sceneBounds.max.x + b.halfWidth;
            if (p.x > max) p.x = min;
            else if (p.x < min) p.x = max;
            b.transform.position = p;
            Color c = b.renderer.color;
            c.a = b.baseAlpha * (0.75f + 0.25f * Mathf.Sin(t * 0.35f + b.phase));
            b.renderer.color = c;
        }
    }

    void CreateDust()
    {
        for (int i = 0; i < dustCount; i++)
        {
            Drifter d = MakeDrifter("AmbientDust", glowSprite, DustOrder, false);
            d.kind = 3;
            dust.Add(d);
            ResetDrifter(d, true);
        }
    }

    void CreateRain()
    {
        for (int i = 0; i < rainCount; i++)
        {
            var go = new GameObject("AmbientRainDrop");
            spawnedObjects.Add(go);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = rainSprite;
            sr.sortingOrder = RainOrder;
            RainDrop drop = new RainDrop { transform = go.transform, renderer = sr };
            rain.Add(drop);
            ResetRain(drop, true);
        }
    }

    void ResetRain(RainDrop drop, bool initial)
    {
        float x = Random.Range(sceneBounds.min.x - 0.35f, sceneBounds.max.x + 0.35f);
        float y = initial
            ? Random.Range(sceneBounds.min.y, sceneBounds.max.y)
            : sceneBounds.max.y + Random.Range(0.05f, 1.10f);
        drop.transform.position = new Vector3(x, y, transform.position.z);
        drop.transform.rotation = Quaternion.Euler(0f, 0f, -8f);
        float s = Random.Range(0.82f, 1.24f);
        drop.transform.localScale = new Vector3(s, s, 1f);
        drop.speed = rainSpeed * Random.Range(0.82f, 1.18f);
        drop.wind = rainWind * Random.Range(0.72f, 1.24f);
        drop.alpha = Random.Range(0.36f, 0.70f) * rainOpacity / 0.48f;
        drop.renderer.color = new Color(0.75f, 0.85f, 0.94f, Mathf.Clamp01(drop.alpha * Mathf.Lerp(0.55f, 1f, rainLevel)));
    }

    void UpdateRain()
    {
        // So as primeiras N gotas existem: N cresce com a intensidade (garoa -> temporal).
        int activeDrops = Mathf.CeilToInt(rain.Count * rainLevel);
        float gustWind = 0.7f + 0.9f * gust;
        for (int i = 0; i < rain.Count; i++)
        {
            RainDrop d = rain[i];
            if (d == null || d.transform == null) continue;
            bool on = i < activeDrops;
            if (d.renderer.enabled != on) { d.renderer.enabled = on; if (on) ResetRain(d, true); }
            if (!on) continue;
            Vector3 p = d.transform.position;
            p.x += d.wind * gustWind * Time.deltaTime;
            p.y -= d.speed * (0.85f + 0.3f * rainLevel) * Time.deltaTime;
            d.transform.position = p;
            if (p.y < sceneBounds.min.y - 0.25f || p.x < sceneBounds.min.x - 0.65f || p.x > sceneBounds.max.x + 0.65f)
                ResetRain(d, false);
        }
    }

    // ------------------------------------------------------------------------------------
    // Clima: seco / garoa / chuva forte, sorteado ao iniciar e re-sorteado de tempos em tempos.
    // ------------------------------------------------------------------------------------
    void ChooseWeather(bool initial)
    {
        weatherTimer = Random.Range(60f, 130f);
        if (indoor || !enableRain) { rainTarget = rainLevel = 0f; return; }
        if (!randomWeather) { rainTarget = 1f; if (initial) rainLevel = 0.6f; return; }
        float r = Random.value;
        rainTarget = r < chanceDry ? 0f : (r < chanceDry + chanceLight ? 0.45f : 1f);
        if (initial) rainLevel = rainTarget;   // a missao ja comeca chovendo (ou seca)
    }

    void UpdateWeather()
    {
        gust = Mathf.PerlinNoise(Time.time * 0.12f, 5.1f);
        gust = gust * gust;
        weatherTimer -= Time.deltaTime;
        if (weatherTimer <= 0f) ChooseWeather(false);
        rainLevel = Mathf.MoveTowards(rainLevel, rainTarget, Time.deltaTime * 0.12f);
        ZombitAudio.SetRain(rainLevel);
    }

    // ------------------------------------------------------------------------------------
    // Folhas / papeis levados pelo vento: tombam, rodam e rolam pelo chao nas rajadas.
    // ------------------------------------------------------------------------------------
    Texture2D MakeLeafTexture(int kind)
    {
        int w = kind == 2 ? 6 : 8, h = kind == 2 ? 5 : (kind == 1 ? 8 : 5);
        var tex = NewTexture(w, h, FilterMode.Point);
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float nx = (x + 0.5f) / w * 2f - 1f, ny = (y + 0.5f) / h * 2f - 1f;
            bool on; float v = 1f;
            if (kind == 0) { on = nx * nx + ny * ny * 2.6f < 0.95f; if (y == h / 2) v = 0.72f; }         // folha oval com nervura
            else if (kind == 1) { on = Mathf.Abs(nx) + Mathf.Abs(ny) < 0.98f; if (x == w / 2 || x == w / 2 - 1) v = 0.78f; } // folha de losango
            else { on = true; v = (x + y) % 3 == 0 ? 0.88f : 1f; }                                       // papel
            px[y * w + x] = on ? new Color(v, v, v, 1f) : Color.clear;
        }
        tex.SetPixels(px); tex.Apply(false, true);
        return tex;
    }

    Texture2D MakeShaftTexture()
    {
        const int w = 32, h = 128;
        var tex = NewTexture(w, h, FilterMode.Bilinear);
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float u = (x + 0.5f) / w * 2f - 1f;
            float v = (y + 0.5f) / h;           // 0 = base (longe do sol), 1 = topo (no sol)
            float a = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(u)), 1.6f) * Mathf.Pow(v, 0.9f);
            a *= Mathf.Clamp01((1f - v) * 0.0f + 1f);
            px[y * w + x] = new Color(1f, 0.85f, 0.6f, a);
        }
        tex.SetPixels(px); tex.Apply(false, true);
        return tex;
    }

    Texture2D MakeBirdTexture(bool wingsUp)
    {
        const int w = 11, h = 6;
        var tex = NewTexture(w, h, FilterMode.Point);
        var px = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = Color.clear;
        for (int x = 0; x < w; x++)
        {
            float d = Mathf.Abs(x - 5f);
            int y = wingsUp ? 2 + Mathf.RoundToInt(d * 0.6f) : 3 - Mathf.RoundToInt(d * 0.5f);
            y = Mathf.Clamp(y, 0, h - 1);
            px[y * w + x] = Color.white;
            if (d < 2) px[Mathf.Clamp(y - 1, 0, h - 1) * w + x] = Color.white;   // corpo
        }
        tex.SetPixels(px); tex.Apply(false, true);
        return tex;
    }

    Texture2D MakeFlatTexture()
    {
        var tex = NewTexture(4, 4, FilterMode.Point);
        var px = new Color[16];
        for (int i = 0; i < 16; i++) px[i] = Color.white;
        tex.SetPixels(px); tex.Apply(false, true);
        return tex;
    }

    Color[] LeafPalette(out float paperChance)
    {
        string id = profile != null ? profile.id : "";
        paperChance = 0.12f;
        if (id == "shopping") { paperChance = 1f; return new[] { new Color(0.92f, 0.9f, 0.82f), new Color(0.78f, 0.78f, 0.74f) }; }
        if (id == "hospital" || id == "rpd" || id == "metro" || id == "city") paperChance = 0.45f;
        if (id == "dock" || id == "military") return new[] { new Color(0.55f, 0.42f, 0.2f), new Color(0.42f, 0.34f, 0.2f), new Color(0.62f, 0.5f, 0.28f), new Color(0.36f, 0.4f, 0.22f) };
        return new[] { new Color(0.88f, 0.46f, 0.12f), new Color(0.72f, 0.26f, 0.08f), new Color(0.92f, 0.68f, 0.2f), new Color(0.56f, 0.4f, 0.1f), new Color(0.45f, 0.5f, 0.15f) };
    }

    void CreateLeaves()
    {
        var palette = LeafPalette(out float paperChance);
        float floor = WorldAt(0.5f, Mathf.Min(0.97f, profile.groundRow + 0.09f), 0f).y;
        int total = leafCount + frontLeafCount;
        for (int i = 0; i < total; i++)
        {
            bool front = i >= leafCount;
            var go = new GameObject(front ? "AmbientLeafFront" : "AmbientLeaf");
            spawnedObjects.Add(go);
            var sr = go.AddComponent<SpriteRenderer>();
            bool paper = Random.value < paperChance;
            sr.sprite = paper ? paperSprite : (Random.value < 0.5f ? leafSpriteA : leafSpriteB);
            Color c = palette[Random.Range(0, palette.Length)];
            sr.color = new Color(c.r, c.g, c.b, front ? 0.9f : 0.85f);
            sr.sortingOrder = front ? 15000 : DustOrder - 1;
            var lf = new Leaf
            {
                tr = go.transform, sr = sr, front = front,
                phase = Random.Range(0f, 6.28f), flutter = Random.Range(2.5f, 5.5f), spin = Random.Range(-260f, 260f),
                speedMul = Random.Range(0.7f, 1.4f), size = (front ? Random.Range(1.5f, 2.1f) : Random.Range(0.9f, 1.5f)) * (paper ? 0.9f : 1f),
                floorY = floor + Random.Range(-0.35f, 0.15f) - (front ? 0.5f : 0f)
            };
            go.transform.localScale = Vector3.one * lf.size;
            leaves.Add(lf);
            RespawnLeaf(lf, true);
        }
    }

    void RespawnLeaf(Leaf l, bool initial)
    {
        float x = initial ? Random.Range(sceneBounds.min.x, sceneBounds.max.x) : sceneBounds.max.x + Random.Range(0.3f, 3.5f);
        float yTop = WorldAt(0.5f, Mathf.Max(0.15f, profile.groundRow - 0.30f), 0f).y;
        float y = Random.Range(l.floorY + 0.2f, yTop);
        l.tr.position = new Vector3(x, y, transform.position.z);
        l.phase = Random.Range(0f, 6.28f);
    }

    void UpdateLeaves(float t)
    {
        if (leaves.Count == 0) return;
        float dt = Time.deltaTime;
        float wind = 0.45f + 2.3f * gust;
        for (int i = 0; i < leaves.Count; i++)
        {
            var l = leaves[i];
            if (l.tr == null) continue;
            Vector3 p = l.tr.position;
            float vx = -wind * l.speedMul;
            float vy = Mathf.Sin(t * l.flutter + l.phase) * 0.5f - 0.16f + gust * 0.55f;
            p.x += vx * dt;
            p.y += vy * dt;
            bool onGround = p.y <= l.floorY;
            if (onGround) { p.y = l.floorY; l.tr.position = p; }
            else l.tr.position = p;
            float spinMul = onGround ? 0.25f : (0.5f + gust * 1.6f);
            l.tr.Rotate(0f, 0f, l.spin * spinMul * dt);
            // "tombo" 3D falso: achata o eixo Y como se a folha virasse
            float flip = 0.35f + 0.65f * Mathf.Abs(Mathf.Cos(t * l.flutter * 0.7f + l.phase));
            l.tr.localScale = new Vector3(l.size, l.size * (onGround ? 0.6f : flip), 1f);
            if (p.x < sceneBounds.min.x - 0.8f || p.y > sceneBounds.max.y) RespawnLeaf(l, false);
        }
    }

    // ------------------------------------------------------------------------------------
    // Feixes de luz saindo do sol/janela (respiram devagar, somem com chuva forte).
    // ------------------------------------------------------------------------------------
    void CreateLightShafts()
    {
        int n = 5;
        float len = sceneBounds.size.y * 0.85f;
        bool warm = !indoor;
        for (int i = 0; i < n; i++)
        {
            var go = new GameObject("AmbientLightShaft");
            spawnedObjects.Add(go);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = shaftSprite;
            sr.sortingOrder = SmokeOrder - 1;
            Vector3 origin = WorldAt(profile.sun.x, profile.sun.y, 0.01f);
            go.transform.position = origin + new Vector3((i - 2) * 0.35f, 0f, 0f);
            float ang = (i - 2) * 11f + Random.Range(-4f, 4f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, ang);
            float wUnits = Random.Range(1.2f, 2.8f);
            go.transform.localScale = new Vector3(wUnits / 1f, len / 4f, 1f);   // sprite: 1 unidade de largura x 4 de altura
            var sh = new Shaft { sr = sr, baseAlpha = Random.Range(0.05f, 0.10f) * (warm ? 1f : 0.7f), phase = Random.Range(0f, 6.28f), speed = Random.Range(0.25f, 0.5f) };
            sr.color = new Color(1f, warm ? 0.72f : 0.9f, warm ? 0.38f : 0.7f, sh.baseAlpha);
            shafts.Add(sh);
        }
    }

    void UpdateShafts(float t)
    {
        float dim = 1f - 0.65f * rainLevel;
        for (int i = 0; i < shafts.Count; i++)
        {
            var s = shafts[i];
            if (s.sr == null) continue;
            Color c = s.sr.color;
            c.a = s.baseAlpha * dim * (0.65f + 0.35f * Mathf.Sin(t * s.speed + s.phase));
            s.sr.color = c;
        }
    }

    // ------------------------------------------------------------------------------------
    // Passaros: bando atravessa o ceu de tempos em tempos (so ao ar livre e sem temporal).
    // ------------------------------------------------------------------------------------
    void UpdateBirds(float t)
    {
        if (enableBirds && !indoor && rainLevel < 0.6f && Time.time >= nextBirdsAt)
        {
            nextBirdsAt = Time.time + Random.Range(18f, 45f);
            int n = Random.Range(2, 5);
            float dir = Random.value < 0.5f ? 1f : -1f;
            float y0 = WorldAt(0.5f, Random.Range(0.08f, 0.30f), 0f).y;
            float x0 = dir > 0f ? sceneBounds.min.x - 1f : sceneBounds.max.x + 1f;
            float speed = Random.Range(1.6f, 2.6f);
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("AmbientBird");
                spawnedObjects.Add(go);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = birdUp;
                sr.sortingOrder = DustOrder - 2;
                sr.color = new Color(0.05f, 0.03f, 0.05f, 0.9f);
                go.transform.position = new Vector3(x0 - dir * i * Random.Range(0.6f, 1.1f), y0 + Random.Range(-0.35f, 0.35f), transform.position.z);
                go.transform.localScale = new Vector3(dir, 1f, 1f);
                birds.Add(new Bird { tr = go.transform, sr = sr, speed = dir * speed * Random.Range(0.9f, 1.1f), phase = Random.Range(0f, 6.28f), y0 = go.transform.position.y, bobAmp = Random.Range(0.08f, 0.2f) });
            }
        }
        for (int i = birds.Count - 1; i >= 0; i--)
        {
            var b = birds[i];
            if (b.tr == null) { birds.RemoveAt(i); continue; }
            Vector3 p = b.tr.position;
            p.x += b.speed * Time.deltaTime;
            p.y = b.y0 + Mathf.Sin(t * 1.3f + b.phase) * b.bobAmp;
            b.tr.position = p;
            b.sr.sprite = Mathf.Sin(t * 11f + b.phase) > 0f ? birdUp : birdDown;
            if (p.x < sceneBounds.min.x - 2f || p.x > sceneBounds.max.x + 2f)
            {
                Destroy(b.tr.gameObject);
                birds.RemoveAt(i);
            }
        }
    }

    // ------------------------------------------------------------------------------------
    // Raios (so no temporal): clarao duplo na cena e trovao com atraso.
    // ------------------------------------------------------------------------------------
    void CreateFlash()
    {
        var go = new GameObject("AmbientLightningFlash");
        spawnedObjects.Add(go);
        flash = go.AddComponent<SpriteRenderer>();
        flash.sprite = flatSprite;
        flash.sortingOrder = 25000;
        go.transform.position = new Vector3(sceneBounds.center.x, sceneBounds.center.y, transform.position.z - 1f);
        go.transform.localScale = new Vector3(sceneBounds.size.x, sceneBounds.size.y, 1f);
        flash.color = new Color(0.85f, 0.9f, 1f, 0f);
    }

    void UpdateLightning()
    {
        if (flash == null) return;
        if (rainLevel > 0.85f && flashT < 0f && Time.time >= nextLightningAt)
        {
            nextLightningAt = Time.time + Random.Range(9f, 24f);
            flashT = 0f; flashPhase = 0;
            thunderAt = Time.time + Random.Range(0.5f, 1.8f);
        }
        float a = 0f;
        if (flashT >= 0f)
        {
            flashT += Time.deltaTime;
            // clarao forte, pausa curta, segundo clarao menor
            if (flashT < 0.07f) a = 0.55f * (1f - flashT / 0.07f);
            else if (flashT > 0.14f && flashT < 0.30f) a = 0.38f * (1f - (flashT - 0.14f) / 0.16f);
            else if (flashT >= 0.30f) flashT = -1f;
        }
        var c = flash.color; c.a = a; flash.color = c;
        if (thunderAt > 0f && Time.time >= thunderAt) { thunderAt = -1f; ZombitAudio.PlayThunder(); }
    }

    Vector3 WorldAt(float xNormalized, float yFromTopNormalized, float zOffset)
    {
        return new Vector3(
            Mathf.Lerp(sceneBounds.min.x, sceneBounds.max.x, xNormalized),
            sceneBounds.max.y - sceneBounds.size.y * yFromTopNormalized,
            transform.position.z + zOffset);
    }

    void OnDestroy()
    {
        ZombitAudio.SetRain(0f);
        for (int i = 0; i < spawnedObjects.Count; i++)
            if (spawnedObjects[i] != null) Destroy(spawnedObjects[i]);
        for (int i = 0; i < generatedSprites.Count; i++)
            if (generatedSprites[i] != null) Destroy(generatedSprites[i]);
        for (int i = 0; i < generatedTextures.Count; i++)
            if (generatedTextures[i] != null) Destroy(generatedTextures[i]);
    }
}
