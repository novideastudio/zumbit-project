using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mede a area VISIVEL (pixels nao transparentes) de um sprite, ignorando a margem transparente do PNG.
/// Usado para normalizar a ALTURA real de herois e zumbis, independente do tamanho do canvas de cada arte.
/// Requer Read/Write habilitado na textura (o ArtImporter ja faz isso para Zombies e Characters).
/// </summary>
public static class SpriteMetrics
{
    public struct Metrics
    {
        /// <summary>Altura visivel em pixels do sprite.</summary>
        public float heightPx;
        /// <summary>Distancia (px) entre a base do canvas e o primeiro pixel visivel (pes).</summary>
        public float bottomPx;
        public bool measured;
    }

    const byte AlphaThreshold = 20;
    static readonly Dictionary<Sprite, Metrics> cache = new Dictionary<Sprite, Metrics>();
    static bool warned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { cache.Clear(); muzzleCache.Clear(); warned = false; }

    public static Metrics Get(Sprite sprite)
    {
        if (sprite == null) return default;
        if (cache.TryGetValue(sprite, out var m)) return m;
        m = Measure(sprite);
        cache[sprite] = m;
        return m;
    }

    /// <summary>Altura visivel do sprite em unidades de mundo com escala 1.</summary>
    public static float WorldHeight(Sprite sprite)
    {
        if (sprite == null) return 1f;
        return Mathf.Max(0.01f, Get(sprite).heightPx / sprite.pixelsPerUnit);
    }

    // ---- Ponta do cano (deteccao automatica) ----
    static readonly Dictionary<Sprite, Vector2?> muzzleCache = new Dictionary<Sprite, Vector2?>();

    /// <summary>
    /// Acha a ponta do cano lendo o PNG: o ponto mais a DIREITA do desenho (heroi virado para a direita),
    /// com y no meio das linhas visiveis nas ultimas colunas (silenciador/cano). Devolve UV do sprite
    /// (x da esquerda, y da base; 0-1), o mesmo formato do muzzleUV. Falso se a textura nao for legivel.
    /// </summary>
    public static bool TryGetMuzzleUV(Sprite sprite, out Vector2 uv)
    {
        uv = default;
        if (sprite == null) return false;
        if (muzzleCache.TryGetValue(sprite, out var cached))
        {
            if (!cached.HasValue) return false;
            uv = cached.Value; return true;
        }

        Vector2? result = null;
        Texture2D tex = sprite.texture;
        if (tex != null && tex.isReadable)
        {
            Rect r = sprite.rect;
            int x0 = Mathf.RoundToInt(r.x), y0 = Mathf.RoundToInt(r.y);
            int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
            Color32[] px = tex.GetPixels32();
            int texW = tex.width;

            int xmax = -1;
            for (int x = w - 1; x >= 0 && xmax < 0; x--)
                for (int y = 0; y < h; y++)
                    if (px[(y0 + y) * texW + x0 + x].a > AlphaThreshold) { xmax = x; break; }

            if (xmax >= 0)
            {
                int band = Mathf.Max(2, Mathf.RoundToInt(w * 0.02f));
                int xmin = Mathf.Max(0, xmax - band);
                int yLo = int.MaxValue, yHi = -1;
                for (int y = 0; y < h; y++)
                    for (int x = xmin; x <= xmax; x++)
                        if (px[(y0 + y) * texW + x0 + x].a > AlphaThreshold)
                        {
                            if (y < yLo) yLo = y;
                            if (y > yHi) yHi = y;
                            break;
                        }
                if (yHi >= 0)
                    result = new Vector2((xmax + 1f) / w, ((yLo + yHi) * 0.5f + 0.5f) / h);
            }
        }
        muzzleCache[sprite] = result;
        if (!result.HasValue) return false;
        uv = result.Value;
        return true;
    }

    static Metrics Measure(Sprite sprite)
    {
        Rect r = sprite.rect;
        Texture2D tex = sprite.texture;
        var fallback = new Metrics { heightPx = r.height, bottomPx = 0f, measured = false };
        if (tex == null) return fallback;
        if (!tex.isReadable)
        {
            if (!warned)
            {
                warned = true;
                Debug.LogWarning("Zombit: textura sem Read/Write; usando a altura total do canvas. " +
                                 "Reimporte Assets/_Project/Resources/Art (Assets > Reimport) para normalizar o tamanho.");
            }
            return fallback;
        }

        int x0 = Mathf.RoundToInt(r.x), y0 = Mathf.RoundToInt(r.y);
        int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
        Color32[] px = tex.GetPixels32();
        int texW = tex.width;

        // Conta pixels visiveis por linha (0 = base). Linhas com pouquissimos pixels (pontos soltos
        // de dithering) sao ignoradas para nao inflar a altura.
        var rowCount = new int[h];
        int maxCount = 0;
        for (int y = 0; y < h; y++)
        {
            int count = 0;
            int rowStart = (y0 + y) * texW + x0;
            for (int x = 0; x < w; x++)
                if (px[rowStart + x].a > AlphaThreshold) count++;
            rowCount[y] = count;
            if (count > maxCount) maxCount = count;
        }
        if (maxCount == 0) return fallback;

        int threshold = Mathf.Max(2, Mathf.RoundToInt(maxCount * 0.04f));
        int bottom = -1, top = -1;
        for (int y = 0; y < h; y++)
        {
            if (rowCount[y] < threshold) continue;
            if (bottom < 0) bottom = y;
            top = y;
        }
        if (bottom < 0) return fallback;

        return new Metrics { heightPx = top - bottom + 1, bottomPx = bottom, measured = true };
    }
}
