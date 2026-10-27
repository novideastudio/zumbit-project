using TMPro;
using UnityEngine;

/// <summary>
/// Boneco de treino. E um Enemy de verdade (entra em Enemy.Alive, entao o heroi mira nele e todo o dano
/// passa por Enemy.Hit), mas: nao anda, nao ataca, nao da XP/loot/abate e renasce ao "morrer".
/// O tempo entre o primeiro acerto e a queda e registrado como TTK no DpsTracker.
/// </summary>
public class TrainingDummy : MonoBehaviour
{
    // Fracao da altura visivel, a partir dos pes, onde fica o ponto de mira da cabeca.
    public const float HeadAimFraction = 0.88f;
    Health hp;
    Enemy enemy;
    float firstHitAt = -1f;
    float baseHeight;
    bool infinite;
    float barWidth = 1.2f;
    Transform uiRoot;
    Transform barFill;
    TextMeshPro label;

    static Sprite whiteSprite, dummySprite;

    public static Sprite White()
    {
        if (whiteSprite != null) return whiteSprite;
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        tex.SetPixel(0, 0, Color.white); tex.Apply(false, true);
        whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        whiteSprite.name = "Zombit_RangeWhite";
        return whiteSprite;
    }

    /// <summary>Boneco de treino com arte importada; usa um fallback procedural caso o recurso nao exista.</summary>
    static Sprite DummySprite()
    {
        if (dummySprite != null) return dummySprite;

        // Arte oficial do boneco de treino, importada como Sprite e com pivot nos pes.
        // Se o recurso ainda nao tiver sido importado, mantem o boneco procedural como fallback.
        dummySprite = Resources.Load<Sprite>("Art/Range/training_dummy");
        if (dummySprite != null) return dummySprite;

        const int W = 24, H = 48;
        var px = new Color[W * H];
        Color wood = new Color(0.40f, 0.27f, 0.15f), woodDk = new Color(0.24f, 0.15f, 0.08f);
        Color burlap = new Color(0.78f, 0.66f, 0.44f), burlapDk = new Color(0.36f, 0.28f, 0.16f);
        Color red = new Color(0.80f, 0.14f, 0.14f), white = new Color(0.94f, 0.92f, 0.86f);

        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                Color c = Color.clear;
                // base e poste
                if (y <= 2 && x >= 5 && x <= 18) c = (y == 0 || x == 5 || x == 18) ? woodDk : wood;
                else if (y < 16 && x >= 11 && x <= 12) c = wood;
                // tronco (alvo concentrico)
                if (y >= 14 && y <= 33 && x >= 5 && x <= 18)
                {
                    bool edge = y == 14 || y == 33 || x == 5 || x == 18;
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(11.5f, 23.5f));
                    c = edge ? burlapDk : d < 2.2f ? red : d < 4.4f ? white : d < 6.6f ? red : burlap;
                }
                // cabeca (saco)
                float dh = Vector2.Distance(new Vector2(x, y), new Vector2(11.5f, 40f));
                if (dh <= 6.4f) c = dh > 5.2f ? burlapDk : burlap;
                px[y * W + x] = c;
            }
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels(px); tex.Apply(false, true);
        dummySprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), 24f);
        dummySprite.name = "Zombit_RangeDummy";
        return dummySprite;
    }

    /// <param name="feet">Posicao dos pes (o pivo do sprite fica nos pes).</param>
    /// <param name="infinite">Vida infinita: nunca cai nem renasce; a vida e reabastecida em silencio (sem TTK).</param>
    public static TrainingDummy Create(Vector3 feet, float height, float maxHp, float defense, string label, bool infinite = false)
    {
        var go = new GameObject("TrainingDummy");
        go.SetActive(false);
        go.transform.position = feet;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = DummySprite();
        // Usa somente os pixels visiveis para igualar a altura dos zumbis comuns (2 unidades),
        // ignorando margens transparentes e variacoes de Pixels Per Unit.
        float visibleSpriteHeight = sr.sprite != null ? SpriteMetrics.WorldHeight(sr.sprite) : 2f;
        float scale = height / Mathf.Max(0.01f, visibleSpriteHeight);
        go.transform.localScale = new Vector3(scale, scale, 1f);
        AlignVisibleFeet(go.transform, sr, feet);
        var hp = go.AddComponent<Health>();
        hp.defense = defense;

        var data = ScriptableObject.CreateInstance<EnemyData>();
        data.name = "TrainingDummyData";
        data.displayName = "Boneco de treino";
        data.tier = Tier.Common;
        data.maxHealth = maxHp;
        data.moveSpeed = 0f;
        data.damage = 0f;

        var en = go.AddComponent<Enemy>();
        en.data = data;
        en.isTrainingDummy = true;
        go.AddComponent<YSort>();

        var d = go.AddComponent<TrainingDummy>();
        d.baseHeight = height;
        d.infinite = infinite;
        d.BuildUi(label);
        go.SetActive(true);
        return d;
    }

    static void AlignVisibleFeet(Transform target, SpriteRenderer sr, Vector3 feet)
    {
        if (target == null || sr == null || sr.sprite == null)
        {
            if (target != null) target.position = feet;
            return;
        }

        var metrics = SpriteMetrics.Get(sr.sprite);
        float scaleY = Mathf.Abs(target.lossyScale.y);
        // Converte a margem transparente inferior para unidades do mundo; o primeiro pixel
        // visivel da base fica exatamente na linha dos pes do personagem.
        float visibleBottomLocal = sr.sprite.bounds.min.y + metrics.bottomPx / sr.sprite.pixelsPerUnit;
        Vector3 position = target.position;
        position.x = feet.x;
        position.y = feet.y - visibleBottomLocal * scaleY;
        position.z = feet.z;
        target.position = position;
    }

    void Awake()
    {
        hp = GetComponent<Health>();
        enemy = GetComponent<Enemy>();
    }

    void OnEnable()
    {
        if (hp == null) hp = GetComponent<Health>();
        hp.Died += OnDown;
        hp.Changed += OnChanged;
        Enemy.Damaged += OnDamaged;
    }

    void OnDisable()
    {
        if (hp != null) { hp.Died -= OnDown; hp.Changed -= OnChanged; }
        Enemy.Damaged -= OnDamaged;
    }

    void OnDestroy()
    {
        if (uiRoot != null) Destroy(uiRoot.gameObject);
        if (enemy != null && enemy.data != null) Destroy(enemy.data);
    }

    void OnDamaged(Enemy e, float applied, float raw, bool crit)
    {
        if (e == enemy && firstHitAt < 0f && applied > 0f) firstHitAt = Time.time;
    }

    void OnDown()
    {
        if (infinite) { hp.SetValues(hp.max, hp.max); return; }   // (so por seguranca; a vida infinita nao chega a zero)
        float ttk = firstHitAt >= 0f ? Time.time - firstHitAt : 0f;
        ShootingRange.Tracker.RecordKill(ttk);
        firstHitAt = -1f;
        hp.SetValues(hp.max, hp.max);   // renasce com a vida cheia
    }

    void OnChanged(float cur, float max)
    {
        // Vida infinita: reabastece quando cai abaixo de 50% (1.000.000 de HP mantem o float preciso) e a barra fica cheia.
        if (infinite)
        {
            if (cur > 0f && cur < max * 0.5f) hp.SetValues(max, max);
            return;
        }
        if (barFill == null || max <= 0f) return;
        float r = Mathf.Clamp01(cur / max);
        barFill.localScale = new Vector3(barWidth * r, 0.1f, 1f);
        barFill.localPosition = new Vector3(-barWidth * 0.5f + barWidth * r * 0.5f, 0f, 0f);
    }

    void BuildUi(string text)
    {
        // Raiz sem escala (o boneco pode ser escalado): segue o boneco no LateUpdate.
        uiRoot = new GameObject("DummyUI").transform;
        var bg = new GameObject("BarBg").AddComponent<SpriteRenderer>();
        bg.transform.SetParent(uiRoot, false);
        bg.sprite = White(); bg.color = new Color(0f, 0f, 0f, 0.75f); bg.sortingOrder = 6000;
        bg.transform.localScale = new Vector3(barWidth + 0.06f, 0.16f, 1f);
        var fill = new GameObject("BarFill").AddComponent<SpriteRenderer>();
        fill.transform.SetParent(uiRoot, false);
        fill.sprite = White(); fill.color = new Color(0.30f, 0.86f, 0.46f, 1f); fill.sortingOrder = 6001;
        fill.transform.localScale = new Vector3(barWidth, 0.1f, 1f);
        barFill = fill.transform;

        if (!string.IsNullOrEmpty(text))
        {
            var go = new GameObject("DummyLabel");
            go.transform.SetParent(uiRoot, false);
            go.transform.localPosition = new Vector3(0f, 0.28f, 0f);
            label = go.AddComponent<TextMeshPro>();
            label.text = text;
            label.fontSize = 2.6f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(1f, 0.88f, 0.55f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.sortingOrder = 6002;
        }
    }

    void LateUpdate()
    {
        if (uiRoot == null) return;
        uiRoot.position = transform.position + new Vector3(0f, baseHeight + 0.3f, 0f);
    }
}
