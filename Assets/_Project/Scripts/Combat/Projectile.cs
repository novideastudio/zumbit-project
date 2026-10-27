using System.Collections.Generic;
using UnityEngine;

/// <summary>Lightweight pixel-art projectile with a compact metallic sprite and subtle tracer.</summary>
public class Projectile : MonoBehaviour
{
    public float speed = 18f;
    public float hitRadius = 0.6f;

    const float MaxLife = 2f;
    /// <summary>Tiro que errou a mira (de raspao): causa so esta fracao do dano em quem atingir. Faz a precisao pesar de verdade.</summary>
    public const float MissDamageMult = 0.6f;
    /// <summary>O projetil some depois de percorrer alcance x esta margem (antes voava 2 s = 36 unidades).</summary>
    const float RangeSlack = 1.2f;

    Vector3 dir;
    float damage;
    bool critical;
    int pierceLeft;
    float pierceFalloff = 0.5f;
    readonly List<Enemy> alreadyHit = new List<Enemy>(3);
    // Alvo para o qual o tiro foi mirado: se o projetil passa dele sem acertar, mostra "MISS".
    Enemy intendedTarget;
    bool missReported;
    float life = MaxLife;
    TrailRenderer trail;
    SpriteRenderer spriteRenderer;
    bool released;

    static Sprite bulletSprite;
    static Material trailMaterial;

    // Pool: antes cada tiro criava GameObject + SpriteRenderer + TrailRenderer e depois o destruia
    // (varios por segundo = picos de GC e de CPU em hordas). Agora os projeteis sao reaproveitados.
    static readonly Stack<Projectile> pool = new Stack<Projectile>(64);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        pool.Clear();
        bulletSprite = null;
        trailMaterial = null;
    }

    /// <summary>Pega um projetil do pool (ou cria um novo a partir do prefab / do zero).</summary>
    public static Projectile Spawn(GameObject prefab, Vector3 position)
    {
        while (pool.Count > 0)
        {
            Projectile pooled = pool.Pop();
            if (pooled == null) continue; // destruido por troca de cena
            pooled.transform.position = position;
            pooled.PrepareForReuse();
            return pooled;
        }

        GameObject go;
        if (prefab != null) go = Instantiate(prefab, position, Quaternion.identity);
        else
        {
            go = new GameObject("Bullet");
            go.transform.position = position;
        }
        Projectile p = go.GetComponent<Projectile>();
        if (p == null) p = go.AddComponent<Projectile>();
        return p;
    }

    void PrepareForReuse()
    {
        released = false;
        life = MaxLife;
        pierceLeft = 0;
        intendedTarget = null;
        missReported = false;
        alreadyHit.Clear();
        gameObject.SetActive(true);
        if (spriteRenderer != null) spriteRenderer.color = Color.white;
        if (trail != null) trail.Clear(); // sem risco rasteiro ligando a posicao antiga a nova
    }

    void Release()
    {
        if (released) return;
        released = true;
        gameObject.SetActive(false);
        pool.Push(this);
    }

    void Awake()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer = sr;
        sr.sprite = GetBulletSprite();
        sr.color = Color.white;
        sr.sortingOrder = 20000;
        transform.localScale = Vector3.one;

        trail = GetComponent<TrailRenderer>();
        if (trail == null) trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = 0.035f;
        trail.minVertexDistance = 0.04f;
        trail.startWidth = 0.026f;
        trail.endWidth = 0.002f;
        trail.numCapVertices = 0;
        trail.numCornerVertices = 0;
        trail.autodestruct = false;
        trail.alignment = LineAlignment.View;
        trail.textureMode = LineTextureMode.Stretch;
        trail.sortingOrder = 19999;
        trail.sharedMaterial = GetTrailMaterial();
        trail.startColor = critical ? new Color(1f, 0.55f, 0.2f, 0.75f) : new Color(1f, 0.86f, 0.62f, 0.65f);
        trail.endColor = new Color(1f, 0.7f, 0.38f, 0f);
        trail.Clear();
    }

    static Sprite GetBulletSprite()
    {
        if (bulletSprite != null) return bulletSprite;

        // Tiny 12x5 hand-authored pixel sprite: dark rim, brass casing and pale metal tip.
        const int w = 12, h = 5;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.name = "Zombit_PixelBullet_Texture";
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

        Color outline = new Color(0.16f, 0.12f, 0.09f, 1f);
        Color casing = new Color(0.64f, 0.34f, 0.13f, 1f);
        Color brass = new Color(0.91f, 0.68f, 0.32f, 1f);
        Color metal = new Color(0.93f, 0.88f, 0.72f, 1f);
        Color shine = new Color(1f, 0.98f, 0.87f, 1f);

        // Outer silhouette, narrowing toward the point on the right.
        SetPixel(pixels, w, h, 1, 1, outline); SetPixel(pixels, w, h, 1, 2, outline); SetPixel(pixels, w, h, 1, 3, outline);
        for (int x = 2; x <= 8; x++)
        {
            SetPixel(pixels, w, h, x, 0, outline);
            SetPixel(pixels, w, h, x, 4, outline);
        }
        SetPixel(pixels, w, h, 9, 1, outline); SetPixel(pixels, w, h, 10, 2, outline); SetPixel(pixels, w, h, 9, 3, outline);
        SetPixel(pixels, w, h, 2, 1, casing); SetPixel(pixels, w, h, 2, 2, casing); SetPixel(pixels, w, h, 2, 3, casing);
        for (int x = 3; x <= 7; x++)
        {
            SetPixel(pixels, w, h, x, 1, brass);
            SetPixel(pixels, w, h, x, 2, x < 5 ? brass : metal);
            SetPixel(pixels, w, h, x, 3, casing);
        }
        SetPixel(pixels, w, h, 8, 1, metal); SetPixel(pixels, w, h, 8, 2, shine); SetPixel(pixels, w, h, 8, 3, metal);
        SetPixel(pixels, w, h, 9, 2, shine);

        tex.SetPixels(pixels);
        tex.Apply(false, true);
        bulletSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 64f);
        bulletSprite.name = "Zombit_PixelBullet_Sprite";
        return bulletSprite;
    }

    static void SetPixel(Color[] pixels, int width, int height, int x, int y, Color color)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return;
        pixels[y * width + x] = color;
    }

    static Material GetTrailMaterial()
    {
        if (trailMaterial != null) return trailMaterial;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader != null) trailMaterial = new Material(shader) { name = "Zombit_BulletTracer_Material" };
        return trailMaterial;
    }

    /// <param name="missed">Tiro que errou a mira: dano multiplicado por MissDamageMult.</param>
    /// <param name="maxRange">Alcance da arma; o projetil some depois de alcance x 1,2. 0 = vida padrao (2 s).</param>
    public void Launch(Vector3 dest, float dmg, bool isCritical = false, int pierce = 0, float falloff = 0.5f,
        Enemy target = null, bool missed = false, float maxRange = 0f)
    {
        intendedTarget = target;
        missReported = false;
        pierceLeft = Mathf.Max(0, pierce);
        pierceFalloff = Mathf.Clamp01(falloff);
        alreadyHit.Clear();
        dest.z = transform.position.z;
        dir = dest - transform.position;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.right;
        dir.Normalize();
        damage = missed ? dmg * MissDamageMult : dmg;
        critical = isCritical;
        transform.right = dir;
        life = maxRange > 0f ? Mathf.Min(MaxLife, maxRange * RangeSlack / Mathf.Max(0.1f, speed)) : MaxLife;

        if (trail != null)
        {
            trail.startColor = critical ? new Color(1f, 0.42f, 0.12f, 0.8f) : new Color(1f, 0.86f, 0.62f, 0.6f);
            trail.endColor = new Color(1f, 0.7f, 0.38f, 0f);
        }
        if (spriteRenderer != null)
            spriteRenderer.color = critical ? new Color(1f, 0.58f, 0.28f, 1f) : Color.white;
    }

    void Update()
    {
        Vector3 from = transform.position;
        Vector3 to = from + dir * speed * Time.deltaTime;
        transform.position = to;
        life -= Time.deltaTime;
        if (life <= 0f) { Release(); return; }

        // Teste de colisao varrido: o segmento do quadro contra o ponto de mira do inimigo.
        // Acerta o PRIMEIRO inimigo no caminho (antes acertava o primeiro da lista, mesmo atras de outro)
        // e nao atravessa ninguem quando o FPS cai.
        Vector3 seg = to - from;
        float segLenSq = seg.sqrMagnitude;
        float radiusSq = hitRadius * hitRadius;
        Enemy best = null;
        float bestT = float.MaxValue;
        for (int i = Enemy.Alive.Count - 1; i >= 0; i--)
        {
            Enemy e = Enemy.Alive[i];
            if (e == null || (alreadyHit.Count > 0 && alreadyHit.Contains(e))) continue;
            Vector3 toEnemy = e.AimPoint - from;
            float t = segLenSq > 0.000001f ? Mathf.Clamp01(Vector3.Dot(toEnemy, seg) / segLenSq) : 0f;
            Vector3 closest = from + seg * t;
            if ((e.AimPoint - closest).sqrMagnitude < radiusSq && t < bestT)
            {
                bestT = t;
                best = e;
            }
        }

        if (best != null)
        {
            Vector3 impactPoint = transform.position;
            best.Hit(damage, critical);
            PixelCombatVFX.Impact(impactPoint, critical);
            ZombitAudio.PlayImpact(impactPoint, critical);
            if (best == intendedTarget) intendedTarget = null;   // acertou o alvo: nada de MISS
            if (pierceLeft > 0)
            {
                // Perfura: segue em frente com dano reduzido (nao acerta o mesmo inimigo duas vezes).
                pierceLeft--;
                damage *= pierceFalloff;
                alreadyHit.Add(best);
            }
            else Release();
            return;
        }

        // Errou: o projetil ja passou do alvo mirado sem acertar ninguem (e o alvo segue vivo).
        if (!missReported && intendedTarget != null)
        {
            if (intendedTarget.IsDead) intendedTarget = null;
            else if (Vector3.Dot(intendedTarget.AimPoint - transform.position, dir) < 0f)
            {
                missReported = true;
                FloatingDamageText.ShowMiss(intendedTarget.AimPoint);
                ZombitAudio.PlayMiss(intendedTarget.AimPoint);
                intendedTarget = null;
            }
        }
    }
}
