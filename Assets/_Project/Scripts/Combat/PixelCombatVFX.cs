using System.Collections.Generic;
using UnityEngine;

/// <summary>Lightweight pixel-art combat effects. No external textures or packages required.</summary>
public sealed class PixelCombatVFX : MonoBehaviour
{
    sealed class Fx
    {
        public Transform transform;
        public SpriteRenderer renderer;
        public Vector3 velocity;
        public float age, life, spin;
        public Vector3 baseScale;
        public bool bloodPool;
        // Particulas "custom" (explosao/onda de choque): gradiente de 3 cores, gravidade, arrasto e crescimento.
        public bool custom, align;
        public Color c0, c1, c2;
        public float grow = 1f, gravity, drag = 3.2f, alphaPow = 1f, startAlpha = 1f, flat = 1f;
    }

    static PixelCombatVFX instance;
    static Sprite bloodSprite, sparkSprite, flameSprite, poolSprite, blobSprite, glowSprite, ringSprite;
    static Material fxMaterial;
    readonly List<Fx> active = new List<Fx>(96);
    // Efeitos desativados prontos para reuso (antes cada particula era um GameObject novo + Destroy).
    readonly Stack<Fx> free = new Stack<Fx>(128);
    const int MaxEffects = 220;
    const int MaxPools = 26;

    static PixelCombatVFX Instance
    {
        get
        {
            if (instance) return instance;
            var go = new GameObject("PixelCombatVFX_Runtime");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<PixelCombatVFX>();
            return instance;
        }
    }

    static Sprite MakePixelSprite(int kind)
    {
        int w = kind == 3 ? 24 : 8;
        int h = kind == 3 ? 18 : 8;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.name = "Zombit_PixelFX_" + kind;
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float nx = (x - (w - 1) * 0.5f) / (w * 0.5f);
            float ny = (y - (h - 1) * 0.5f) / (h * 0.5f);
            bool on = false;
            if (kind == 0) on = (x == 3 || x == 4 || y == 3 || y == 4) && Random.value > 0.22f;
            else if (kind == 1) on = Mathf.Abs(nx) + Mathf.Abs(ny) < 1.05f && Random.value > 0.12f;
            else if (kind == 2) on = (Mathf.Abs(nx) < 0.8f && Mathf.Abs(ny) < 0.8f) && Random.value > 0.18f;
            else
            {
                float n = Mathf.PerlinNoise(x * 0.41f + 1.7f, y * 0.37f + 4.1f);
                on = nx * nx * 0.85f + ny * ny * 1.4f < 0.8f + n * 0.25f && Random.value > 0.13f;
            }
            if (on) pixels[y * w + x] = Color.white;
        }
        tex.SetPixels(pixels); tex.Apply(false, true);
        var sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), kind == 3 ? 20f : 8f);
        sp.name = tex.name + "_Sprite";
        return sp;
    }

    static Sprite SpriteFor(int kind)
    {
        if (kind == 0 && !bloodSprite) bloodSprite = MakePixelSprite(0);
        if (kind == 1 && !sparkSprite) sparkSprite = MakePixelSprite(1);
        if (kind == 2 && !flameSprite) flameSprite = MakePixelSprite(2);
        if (kind == 3 && !poolSprite) poolSprite = MakePixelSprite(3);
        return kind == 0 ? bloodSprite : kind == 1 ? sparkSprite : kind == 2 ? flameSprite : poolSprite;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    Fx Create(Sprite sprite, Vector3 pos, Color color, Vector3 scale, float life, Vector3 velocity, int sorting)
    {
        if (active.Count >= MaxEffects)
        {
            int oldest = -1; float age = -1f;
            for (int i = 0; i < active.Count; i++)
                if (!active[i].bloodPool && active[i].age > age) { age = active[i].age; oldest = i; }
            if (oldest >= 0) RemoveAt(oldest);
            else return null;
        }

        // Reaproveita um efeito desativado; so cria GameObject quando o pool esta vazio.
        Fx fx = null;
        while (free.Count > 0)
        {
            Fx candidate = free.Pop();
            if (candidate.transform && candidate.renderer) { fx = candidate; break; } // ignora os destruidos por troca de cena
        }

        if (fx == null)
        {
            var go = new GameObject("PixelFX");
            var sr = go.AddComponent<SpriteRenderer>();
            // Explicit URP-compatible sprite shader prevents magenta fallback squares
            // on installations where the project's default sprite material is missing.
            if (fxMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader != null) fxMaterial = new Material(shader) { name = "Zombit_PixelFX_Material" };
            }
            if (fxMaterial != null) sr.sharedMaterial = fxMaterial;
            fx = new Fx { transform = go.transform, renderer = sr };
        }

        Transform t = fx.transform;
        t.position = pos;
        t.rotation = Quaternion.identity;
        t.localScale = scale;
        fx.renderer.sprite = sprite;
        fx.renderer.color = color;
        fx.renderer.sortingOrder = sorting;
        t.gameObject.SetActive(true);

        fx.baseScale = scale;
        fx.life = life;
        fx.velocity = velocity;
        fx.spin = Random.Range(-160f, 160f);
        fx.age = 0f;
        fx.bloodPool = false;
        fx.custom = false; fx.align = false;
        fx.grow = 1f; fx.gravity = 0f; fx.drag = 3.2f; fx.alphaPow = 1f; fx.startAlpha = 1f; fx.flat = 1f;
        active.Add(fx);
        return fx;
    }

    void RemoveAt(int i)
    {
        var fx = active[i];
        active.RemoveAt(i);
        if (fx.transform)
        {
            fx.transform.gameObject.SetActive(false);
            free.Push(fx);
        }
    }

    void Update()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var fx = active[i];
            if (!fx.transform || !fx.renderer) { active.RemoveAt(i); continue; }
            fx.age += Time.deltaTime;
            float t = Mathf.Clamp01(fx.age / Mathf.Max(0.01f, fx.life));
            if (fx.bloodPool)
            {
                var c = fx.renderer.color; c.a = Mathf.Lerp(0.75f, 0f, t); fx.renderer.color = c;
            }
            else if (fx.custom)
            {
                fx.velocity.y -= fx.gravity * Time.deltaTime;
                fx.velocity *= Mathf.Max(0f, 1f - fx.drag * Time.deltaTime);
                fx.transform.position += fx.velocity * Time.deltaTime;
                if (fx.align && fx.velocity.sqrMagnitude > 0.04f) fx.transform.right = fx.velocity.normalized;
                else if (!fx.align) fx.transform.Rotate(0f, 0f, fx.spin * Time.deltaTime);
                float grow = Mathf.Lerp(1f, fx.grow, 1f - (1f - t) * (1f - t));
                fx.transform.localScale = new Vector3(fx.baseScale.x * grow, fx.baseScale.y * grow * fx.flat, 1f);
                Color col = t < 0.4f ? Color.Lerp(fx.c0, fx.c1, t / 0.4f) : Color.Lerp(fx.c1, fx.c2, (t - 0.4f) / 0.6f);
                col.a *= fx.startAlpha * Mathf.Pow(1f - t, fx.alphaPow);
                fx.renderer.color = col;
            }
            else
            {
                fx.transform.position += fx.velocity * Time.deltaTime;
                fx.velocity = Vector3.Lerp(fx.velocity, Vector3.zero, Time.deltaTime * 3.2f);
                fx.transform.Rotate(0f, 0f, fx.spin * Time.deltaTime);
                var c = fx.renderer.color; c.a = 1f - t; fx.renderer.color = c;
                fx.transform.localScale = fx.baseScale * Mathf.Lerp(1f, 0.35f, t);
            }
            if (fx.age >= fx.life) RemoveAt(i);
        }
    }

    public static void Muzzle(Vector3 pos, Vector3 direction)
    {
        var self = Instance;
        var fx = self.Create(SpriteFor(2), pos, new Color(1f, 0.73f, 0.18f), new Vector3(0.32f, 0.22f, 1f), 0.09f, Vector3.zero, 22000);
        if (fx != null) fx.transform.right = direction.sqrMagnitude > 0.01f ? direction : Vector3.right;
        for (int i = 0; i < 2; i++) self.Create(SpriteFor(1), pos, new Color(1f, 0.9f, 0.55f), Vector3.one * Random.Range(0.08f, 0.13f), 0.12f, (Vector3)(Random.insideUnitCircle * 1.8f), 22001);
    }

    public static void Impact(Vector3 pos, bool critical = false)
    {
        var self = Instance;
        Color c = critical ? new Color(1f, 0.75f, 0.2f) : new Color(1f, 0.85f, 0.55f);
        for (int i = 0; i < (critical ? 7 : 4); i++)
            self.Create(SpriteFor(1), pos, c, Vector3.one * Random.Range(0.07f, 0.14f), Random.Range(0.12f, 0.24f), (Vector3)(Random.insideUnitCircle.normalized * Random.Range(0.7f, 2.2f)), 21000);
    }

    public static void Hit(Vector3 pos, float amount)
    {
        var self = Instance;
        int count = Mathf.Clamp(Mathf.RoundToInt(amount / 12f) + 2, 2, 7);
        for (int i = 0; i < count; i++)
        {
            var col = Random.value > 0.35f ? new Color(0.78f, 0.06f, 0.08f) : new Color(0.48f, 0.02f, 0.04f);
            self.Create(SpriteFor(0), pos + (Vector3)(Random.insideUnitCircle * 0.12f), col, Vector3.one * Random.Range(0.08f, 0.16f), Random.Range(0.18f, 0.34f), (Vector3)(Random.insideUnitCircle.normalized * Random.Range(0.6f, 2.4f)), 20500);
        }
    }

    public static void Death(Vector3 pos, bool boss)
    {
        var self = Instance;
        CreatePool(pos, boss ? 1.45f : Random.Range(0.65f, 0.95f));
        int count = boss ? 16 : 8;
        for (int i = 0; i < count; i++)
        {
            Color col = Random.value > 0.4f ? new Color(0.78f, 0.04f, 0.07f) : new Color(0.43f, 0.02f, 0.035f);
            self.Create(SpriteFor(0), pos + (Vector3)(Random.insideUnitCircle * 0.22f), col, Vector3.one * Random.Range(0.1f, boss ? 0.22f : 0.16f), Random.Range(0.35f, 0.7f), (Vector3)(Random.insideUnitCircle.normalized * Random.Range(0.35f, 1.6f)), 20400);
        }
    }

    public static void CreatePool(Vector3 pos, float size = 0.8f)
    {
        var self = Instance;
        int poolCount = 0;
        for (int i = 0; i < self.active.Count; i++) if (self.active[i].bloodPool) poolCount++;
        if (poolCount >= MaxPools)
        {
            for (int i = 0; i < self.active.Count; i++) if (self.active[i].bloodPool) { self.RemoveAt(i); break; }
        }
        // pos is the character's foot point, not its center/aim point.
        // Place the pool directly under the feet and keep it behind the character sprite.
        var fx = self.Create(SpriteFor(3), new Vector3(pos.x, pos.y + 0.015f, pos.z + 0.02f), new Color(0.42f, 0.015f, 0.03f, 0.8f), new Vector3(size, size * 0.42f, 1f), 24f, Vector3.zero, Mathf.RoundToInt(-pos.y * 100f) - 5);
        if (fx != null) fx.bloodPool = true;
    }

    // ------------------------------------------------------------------------------------
    // Sprites das explosoes (gerados uma vez)
    // ------------------------------------------------------------------------------------
    // Bolha pixelada com borda irregular e leve sombreamento (centro claro): tingida vira fogo ou fumaca volumosos.
    static Sprite BlobSprite()
    {
        if (blobSprite != null) return blobSprite;
        const int n = 28;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var px = new Color32[n * n];
        float mid = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float dx = (x - mid) / mid, dy = (y - mid) / mid;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float nz = Mathf.PerlinNoise(x * 0.33f + 3.1f, y * 0.33f + 7.7f);
            float edge = 0.78f + nz * 0.34f;
            if (d > edge) { px[y * n + x] = new Color32(255, 255, 255, 0); continue; }
            float shade = Mathf.Clamp01(1f - d * 0.42f + (nz - 0.5f) * 0.25f);
            // dithering em 2 tons nas bordas internas: leitura de pixel art
            if (d > 0.55f && ((x + y) & 1) == 0) shade *= 0.88f;
            byte v = (byte)(shade * 255f);
            px[y * n + x] = new Color32(v, v, v, 255);
        }
        tex.SetPixels32(px); tex.Apply(false, true);
        blobSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);   // 1 unidade de lado
        blobSprite.hideFlags = HideFlags.HideAndDontSave;
        return blobSprite;
    }

    static Sprite GlowSprite()
    {
        if (glowSprite != null) return glowSprite;
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var px = new Color32[n * n];
        float mid = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float d = Mathf.Sqrt((x - mid) * (x - mid) + (y - mid) * (y - mid)) / mid;
            float a = Mathf.Pow(Mathf.Clamp01(1f - d), 1.6f);
            px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        tex.SetPixels32(px); tex.Apply(false, true);
        glowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        glowSprite.hideFlags = HideFlags.HideAndDontSave;
        return glowSprite;
    }

    static Sprite RingSprite()
    {
        if (ringSprite != null) return ringSprite;
        const int n = 96;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var px = new Color32[n * n];
        float mid = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float d = Mathf.Sqrt((x - mid) * (x - mid) + (y - mid) * (y - mid)) / mid;
            float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.88f) / 0.10f);   // anel fino e nitido
            a = a * a * (3f - 2f * a);
            a = Mathf.Max(a, Mathf.Clamp01(1f - d) * 0.10f);               // leve preenchimento (frente de pressao)
            px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        tex.SetPixels32(px); tex.Apply(false, true);
        ringSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        ringSprite.hideFlags = HideFlags.HideAndDontSave;
        return ringSprite;
    }

    // Particula "custom": gradiente c0 -> c1 -> c2 ao longo da vida, crescimento, gravidade (negativa = flutua) e arrasto.
    Fx Burst(Sprite sp, Vector3 pos, Color c0, Color c1, Color c2, Vector3 size, float grow, float life, Vector3 vel,
             int sorting, float drag = 3f, float gravity = 0f, float alphaPow = 1f, float flat = 1f, float spin = 0f,
             bool align = false, float startAlpha = 1f)
    {
        var fx = Create(sp, pos, c0, size, life, vel, sorting);
        if (fx == null) return null;
        fx.custom = true; fx.align = align;
        fx.c0 = c0; fx.c1 = c1; fx.c2 = c2;
        fx.grow = grow; fx.drag = drag; fx.gravity = gravity; fx.alphaPow = alphaPow;
        fx.flat = flat; fx.spin = spin; fx.startAlpha = startAlpha;
        return fx;
    }

    /// <summary>Marca escura de queimado no chao (some devagar).</summary>
    static void CreateScorch(Vector3 pos, float size)
    {
        var self = Instance;
        var fx = self.Create(SpriteFor(3), new Vector3(pos.x, pos.y + 0.02f, pos.z + 0.03f), new Color(0.03f, 0.025f, 0.02f, 0.85f),
            new Vector3(size, size * 0.42f, 1f), 14f, Vector3.zero, Mathf.RoundToInt(-pos.y * 100f) - 6);
        if (fx != null) fx.bloodPool = true;
    }

    /// <summary>
    /// Explosao de granada. Camadas: clarao + brilho de luz, bola de fogo (bolhas pixeladas branco->laranja->vermelho->brasa),
    /// anel de choque achatado no chao, onda de poeira, faiscas com gravidade, estilhacos, coluna de fumaca e marca de queimado.
    /// Tambem toca o som e treme a camera.
    /// </summary>
    public static void Explosion(Vector3 pos, float radius, bool light = false)
    {
        var self = Instance;
        ZombitAudio.PlayExplosion(pos);
        if (!light) HeroDamageFeedback.Shake(0.55f);
        float r = Mathf.Max(0.9f, radius);
        Vector3 c = new Vector3(pos.x, pos.y + 0.25f, pos.z - 0.05f);
        var blob = BlobSprite(); var glow = GlowSprite(); var ring = RingSprite();

        // 1) clarao + brilho ambiente (ilumina a cena por um instante)
        self.Burst(glow, c, new Color(1f, 1f, 0.9f, 1f), new Color(1f, 0.75f, 0.30f, 0.9f), new Color(1f, 0.38f, 0.08f, 0f),
            Vector3.one * r * 3.0f, 1.25f, 0.20f, Vector3.zero, 21600, 0f, 0f, 1.4f);
        self.Burst(glow, c, new Color(1f, 0.62f, 0.2f, 0.55f), new Color(1f, 0.42f, 0.1f, 0.35f), new Color(0.6f, 0.15f, 0.04f, 0f),
            Vector3.one * r * 4.6f, 1.2f, 0.7f, Vector3.zero, 21490, 0f, 0f, 1.2f);

        // 2) nucleo branco-quente
        self.Burst(blob, c, new Color(1f, 1f, 0.92f), new Color(1f, 0.88f, 0.45f), new Color(1f, 0.55f, 0.12f),
            Vector3.one * r * 0.95f, 1.5f, 0.24f, Vector3.zero, 21590, 0f, 0f, 1.2f, 1f, 0f);

        // 3) bola de fogo: bolhas que sobem como cogumelo e esfriam ate virar brasa escura
        for (int i = 0; i < 18; i++)
        {
            Vector2 o = Random.insideUnitCircle * r * 0.38f;
            Vector3 p = c + new Vector3(o.x, Mathf.Abs(o.y) * 0.8f, 0f);
            float size = r * Random.Range(0.45f, 0.85f);
            Vector3 v = new Vector3(o.x * 2.4f, 0.8f + Mathf.Abs(o.y) * 2.6f + Random.Range(0f, 1.3f), 0f);
            self.Burst(blob, p, new Color(1f, 0.95f, 0.60f), new Color(1f, 0.48f, 0.08f), new Color(0.26f, 0.06f, 0.03f),
                Vector3.one * size, 1.7f, Random.Range(0.45f, 0.85f), v, 21520 + (i % 4), 2.4f, -0.9f, 0.85f, 1f, Random.Range(-60f, 60f));
        }

        // 4) anel de choque no chao (achatado) + anel de poeira mais lento
        self.Burst(ring, new Vector3(pos.x, pos.y + 0.06f, pos.z), new Color(1f, 0.95f, 0.70f, 0.95f), new Color(1f, 0.55f, 0.18f, 0.7f), new Color(0.55f, 0.22f, 0.08f, 0f),
            Vector3.one * r * 0.5f, 5.2f, 0.38f, Vector3.zero, 21510, 0f, 0f, 1.3f, 0.36f);
        self.Burst(ring, new Vector3(pos.x, pos.y + 0.03f, pos.z), new Color(0.85f, 0.72f, 0.55f, 0.5f), new Color(0.7f, 0.58f, 0.42f, 0.3f), new Color(0.5f, 0.42f, 0.32f, 0f),
            Vector3.one * r * 0.6f, 6.0f, 0.6f, Vector3.zero, 21465, 0f, 0f, 1.1f, 0.30f);

        // 5) poeira levantada rente ao chao, espalhando para os lados
        for (int i = 0; i < 10; i++)
        {
            float dir = i % 2 == 0 ? 1f : -1f;
            Vector3 v = new Vector3(dir * Random.Range(2.5f, 6.5f), Random.Range(0.1f, 0.5f), 0f);
            self.Burst(blob, new Vector3(pos.x + Random.Range(-0.3f, 0.3f), pos.y + 0.12f, pos.z), new Color(0.66f, 0.54f, 0.38f), new Color(0.5f, 0.42f, 0.32f), new Color(0.32f, 0.28f, 0.22f),
                Vector3.one * r * Random.Range(0.28f, 0.45f), 2.3f, Random.Range(0.55f, 0.95f), v, 21468, 3.2f, 0f, 1.0f, 0.62f, Random.Range(-30f, 30f), false, 0.7f);
        }

        // 6) faiscas incandescentes (riscos alinhados a velocidade, caem com gravidade)
        var spark = SpriteFor(1);
        for (int i = 0; i < 30; i++)
        {
            float a = Random.Range(8f, 172f) * Mathf.Deg2Rad;
            float spd = Random.Range(4.5f, 11f);
            Vector3 v = new Vector3(Mathf.Cos(a) * spd, Mathf.Sin(a) * spd, 0f);
            self.Burst(spark, c + new Vector3(Random.Range(-0.2f, 0.2f), 0f, 0f), new Color(1f, 0.98f, 0.74f), new Color(1f, 0.62f, 0.14f), new Color(0.78f, 0.2f, 0.05f),
                new Vector3(Random.Range(0.28f, 0.5f), 0.09f, 1f), 0.35f, Random.Range(0.5f, 1.05f), v, 21620, 0.6f, 10f, 0.7f, 1f, 0f, true);
        }

        // 7) estilhacos escuros
        var chunk = SpriteFor(2);
        for (int i = 0; i < 12; i++)
        {
            float a = Random.Range(25f, 155f) * Mathf.Deg2Rad;
            float spd = Random.Range(3f, 8f);
            Color dark = Color.Lerp(new Color(0.14f, 0.11f, 0.09f), new Color(0.32f, 0.27f, 0.22f), Random.value);
            self.Burst(chunk, c, dark, dark, dark, Vector3.one * Random.Range(0.12f, 0.24f), 1f, Random.Range(0.7f, 1.2f),
                new Vector3(Mathf.Cos(a) * spd, Mathf.Sin(a) * spd, 0f), 21560, 0.3f, 15f, 0.35f, 1f, Random.Range(-420f, 420f));
        }

        // 8) coluna de fumaca (maior, lenta, sobe e se espalha)
        for (int i = 0; i < 12; i++)
        {
            Vector3 v = new Vector3(Random.Range(-0.9f, 0.9f), Random.Range(0.8f, 1.9f), 0f);
            Color g0 = new Color(0.34f, 0.30f, 0.26f), g1 = new Color(0.2f, 0.19f, 0.18f), g2 = new Color(0.1f, 0.1f, 0.1f);
            self.Burst(blob, c + new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(0f, 0.4f), 0f), g0, g1, g2,
                Vector3.one * r * Random.Range(0.5f, 0.95f), 2.5f, Random.Range(1.0f, 1.9f), v, 21480, 1.2f, -0.25f, 1.1f, 1f, Random.Range(-25f, 25f), false, 0.68f);
        }

        // 9) marca de queimado
        if (!light) CreateScorch(new Vector3(pos.x, pos.y - 0.05f, pos.z), r * 1.15f);
    }

    /// <summary>
    /// Onda de choque do "Empurrar zumbi": sem fogo. Anel de pressao azul-esbranquicado, poeira varrida para os dois lados
    /// e um clarao curto. Tambem toca o som e treme a camera de leve.
    /// </summary>
    public static void Shockwave(Vector3 pos, float radius)
    {
        var self = Instance;
        ZombitAudio.PlayShockwave(pos);
        HeroDamageFeedback.Shake(0.28f);
        float r = Mathf.Max(1.5f, radius);
        var blob = BlobSprite(); var glow = GlowSprite(); var ring = RingSprite();
        Vector3 g = new Vector3(pos.x, pos.y + 0.05f, pos.z);

        self.Burst(glow, g + Vector3.up * 0.5f, new Color(0.9f, 0.98f, 1f, 0.9f), new Color(0.5f, 0.85f, 1f, 0.5f), new Color(0.2f, 0.5f, 0.8f, 0f),
            Vector3.one * 2.6f, 1.4f, 0.22f, Vector3.zero, 21600, 0f, 0f, 1.3f);
        self.Burst(ring, g, new Color(0.9f, 0.98f, 1f, 0.95f), new Color(0.45f, 0.82f, 1f, 0.7f), new Color(0.2f, 0.45f, 0.75f, 0f),
            Vector3.one * 0.8f, r * 2.1f / 0.8f, 0.42f, Vector3.zero, 21510, 0f, 0f, 1.2f, 0.34f);
        self.Burst(ring, g, new Color(1f, 1f, 1f, 0.6f), new Color(0.7f, 0.9f, 1f, 0.35f), new Color(0.3f, 0.5f, 0.8f, 0f),
            Vector3.one * 0.6f, r * 1.5f / 0.6f, 0.3f, Vector3.zero, 21511, 0f, 0f, 1.0f, 0.30f);
        for (int i = 0; i < 16; i++)
        {
            float dir = i % 2 == 0 ? 1f : -1f;
            Vector3 v = new Vector3(dir * Random.Range(4f, 10f), Random.Range(0.1f, 0.6f), 0f);
            self.Burst(blob, g + new Vector3(Random.Range(-0.3f, 0.3f), 0.1f, 0f), new Color(0.78f, 0.7f, 0.58f), new Color(0.6f, 0.54f, 0.45f), new Color(0.4f, 0.36f, 0.3f),
                Vector3.one * Random.Range(0.28f, 0.5f), 2.2f, Random.Range(0.45f, 0.8f), v, 21470, 3.4f, 0f, 1.0f, 0.6f, Random.Range(-30f, 30f), false, 0.7f);
        }
    }
}
