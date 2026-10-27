using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Caminhos tracejados entre pontos NORMALIZADOS (0..1) do proprio RectTransform.
/// Usado no mapa do hub para ligar os locais (mesmas coordenadas mapX/mapY dos marcadores),
/// entao acompanha qualquer resolucao sem recalcular posicoes.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UIDashedPath : MaskableGraphic
{
    public struct Seg
    {
        public Vector2 a, b;     // 0..1, origem no canto inferior esquerdo
        public Color color;
        public float bend;       // curvatura (fracao do comprimento; sinal = lado)
    }

    readonly List<Seg> segs = new List<Seg>();
    public float thickness = 4f;
    public float dash = 14f;
    public float gap = 9f;

    public void Clear()
    {
        segs.Clear();
        SetVerticesDirty();
    }

    public void Add(Vector2 a, Vector2 b, Color color, float bend = 0.08f)
    {
        segs.Add(new Seg { a = a, b = b, color = color, bend = bend });
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        if (r.width <= 1f || r.height <= 1f) return;

        float period = Mathf.Max(1f, dash + gap);
        for (int s = 0; s < segs.Count; s++)
        {
            Seg seg = segs[s];
            Vector2 p0 = new Vector2(r.xMin + seg.a.x * r.width, r.yMin + seg.a.y * r.height);
            Vector2 p2 = new Vector2(r.xMin + seg.b.x * r.width, r.yMin + seg.b.y * r.height);
            Vector2 dir = p2 - p0;
            float len = dir.magnitude;
            if (len < 2f) continue;
            Vector2 normal = new Vector2(-dir.y, dir.x) / len;
            Vector2 p1 = (p0 + p2) * 0.5f + normal * (len * seg.bend);   // ponto de controle (bezier quadratica)

            int steps = Mathf.Max(8, Mathf.CeilToInt(len / 4f));
            Vector2 prev = p0;
            float walked = 0f;
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                float u = 1f - t;
                Vector2 cur = u * u * p0 + 2f * u * t * p1 + t * t * p2;
                Vector2 d = cur - prev;
                float dl = d.magnitude;
                if (dl > 0.001f)
                {
                    if ((walked % period) < dash) AddQuad(vh, prev, cur, d / dl, seg.color);
                    walked += dl;
                }
                prev = cur;
            }
        }
    }

    void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 dir, Color c)
    {
        Vector2 n = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);
        Color32 col = c * color;
        int idx = vh.currentVertCount;
        vh.AddVert(a - n, col, Vector2.zero);
        vh.AddVert(a + n, col, Vector2.zero);
        vh.AddVert(b + n, col, Vector2.zero);
        vh.AddVert(b - n, col, Vector2.zero);
        vh.AddTriangle(idx, idx + 1, idx + 2);
        vh.AddTriangle(idx + 2, idx + 3, idx);
    }
}
