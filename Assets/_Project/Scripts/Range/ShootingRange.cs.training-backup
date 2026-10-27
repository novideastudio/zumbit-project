using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Estande de tiro: bonecos de treino + regua de distancia + painel de DPS.
/// Criado pelo MissionSetup quando GameSession.IsRange. Nao ha ondas, XP, loot nem penalidade.
/// Ajustes visuais (posicao do painel, distancias, presets) ficam nas constantes abaixo.
/// </summary>
public class ShootingRange : MonoBehaviour
{
    public static ShootingRange Instance { get; private set; }
    public static readonly DpsTracker Tracker = new DpsTracker();

    // ---- ajustes ----
    const float MinDistance = 3f;
    const float MaxDistanceCap = 21.5f;
    const float InfiniteHp = 1000000f;
    // O painel fica na faixa superior entre o painel da missao (esquerda) e o nick/mochila (direita).
    const float PanelTopOffset = 12f;
    const float PanelCenterXOffset = -110f;
    const float PanelW = 850f, PanelH = 320f;
    // O estande abre temporariamente a camera para caberem 20,5 m e uma margem de teste.
    const float RangeViewWidth = 30f;
    const float RangeCameraCenterFraction = 0.62f;

    struct Preset
    {
        public string name, shortName;
        public int count;
        public float hp, defense, height, spacing;
        public Preset(string n, string s, int c, float hp, float def, float h, float sp)
        { name = n; shortName = s; count = c; this.hp = hp; defense = def; height = h; spacing = sp; }
    }

    static readonly Preset[] Presets =
    {
        new Preset("ÚNICO",    "ÚNICO", 1, 1000f,  0f, 2.0f, 0f),
        new Preset("BLINDADO", "BLIND.", 1, 1000f, 40f, 2.0f, 0f),   // defesa 40 = -50% de dano
        new Preset("GRUPO",    "GRUPO", 5,  300f,  0f, 2.0f, 1.3f),
        new Preset("CHEFE",    "CHEFE", 1, 20000f, 20f, 3.2f, 0f),
    };

    Transform hero;
    int squadSize;
    float laneY;
    float distance = 8f;
    int presetIndex;
    readonly List<TrainingDummy> dummies = new List<TrainingDummy>();

    Transform rulerRoot;
    float lastReach = -1f;

    Canvas canvas;
    GameObject panel;
    Camera rangeCamera;
    float originalCameraOrtho;
    Vector3 originalCameraPosition;
    bool rangeCameraAdjusted;
    TMP_Text statsLeftText;
    TMP_Text statsRightText;
    TMP_Text distLabel;
    readonly List<Button> presetButtons = new List<Button>();
    Button pauseButton, lifeButton;
    TMP_Text pauseButtonLabel, lifeButtonLabel;

    bool infiniteLife = true;   // padrao: vida infinita, sem limite de tempo
    float nextRefresh;

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string N0(float v) => Mathf.RoundToInt(v).ToString("N0", Inv).Replace(',', '.');
    static string N1(float v) => v.ToString("F1", Inv).Replace('.', ',');
    static string ND(float v) => Mathf.Abs(v - Mathf.Round(v)) < 0.001f ? N0(v) : N1(v);

    public void Init(Transform heroTransform, int squad)
    {
        Instance = this;
        hero = heroTransform;
        squadSize = Mathf.Max(1, squad);
        // In the range the target must stand on the exact visible foot line of the hero,
        // not the average zombie lane used during squad missions.
        laneY = GetVisibleFeetY(heroTransform, GameTuning.Current.ZombieFeetY(squadSize));
        Tracker.Reset();
        Enemy.Damaged += OnDamaged;
        BuildUi();
        // Espera o WaveBackground/CameraFramer terminar o enquadramento antes de guardar/restaurar a camera.
        Invoke(nameof(InitializeRange), 0.15f);
        Invoke(nameof(SetHud), 0.4f);
    }

    void InitializeRange()
    {
        SetupRangeCamera();
        SpawnDummies();
    }

    void OnDestroy()
    {
        Enemy.Damaged -= OnDamaged;
        if (Instance == this) Instance = null;
        if (rulerRoot != null) Destroy(rulerRoot.gameObject);
        if (canvas != null) Destroy(canvas.gameObject);
        RestoreRangeCamera();
    }

    // Todo dano que removeu vida de um boneco entra na medicao (tiros, granada, onda de choque, torreta).
    void OnDamaged(Enemy e, float applied, float raw, bool crit)
    {
        if (e != null && e.isTrainingDummy) Tracker.Record(applied, crit);
    }

    void SetHud()
    {
        var hud = FindFirstObjectByType<HudController>();
        if (hud != null && hud.waveText != null) hud.waveText.text = "ESTANDE DE TIRO";
    }

    // =====================================================================
    // Bonecos e regua
    // =====================================================================

    void SetupRangeCamera()
    {
        rangeCamera = Camera.main;
        if (rangeCamera == null || !rangeCamera.orthographic || hero == null) return;

        originalCameraOrtho = rangeCamera.orthographicSize;
        originalCameraPosition = rangeCamera.transform.position;

        // Garante uma largura de mundo suficiente sem mudar a camera das missoes normais.
        float aspect = Mathf.Max(0.1f, rangeCamera.aspect);
        rangeCamera.orthographicSize = Mathf.Max(originalCameraOrtho, RangeViewWidth / (2f * aspect));
        float halfWidth = rangeCamera.orthographicSize * aspect;
        float centerOffset = halfWidth * RangeCameraCenterFraction;
        rangeCamera.transform.position = new Vector3(hero.position.x + centerOffset,
            originalCameraPosition.y, originalCameraPosition.z);
        rangeCameraAdjusted = true;
    }

    void RestoreRangeCamera()
    {
        if (!rangeCameraAdjusted || rangeCamera == null) return;
        rangeCamera.orthographicSize = originalCameraOrtho;
        rangeCamera.transform.position = originalCameraPosition;
        rangeCameraAdjusted = false;
    }

    static float GetVisibleFeetY(Transform actor, float fallback)
    {
        if (actor == null) return fallback;
        var sr = actor.GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return fallback;
        var metrics = SpriteMetrics.Get(sr.sprite);
        float scaleY = Mathf.Abs(actor.lossyScale.y);
        // SpriteRenderer.bounds.min is the canvas bottom; bottomPx moves it to the first
        // actually visible pixel, matching the ground alignment used for survivors/zombies.
        return sr.bounds.min.y + metrics.bottomPx / sr.sprite.pixelsPerUnit * scaleY;
    }

    float MaxX
    {
        get
        {
            if (rangeCameraAdjusted && rangeCamera != null && rangeCamera.orthographic)
                return rangeCamera.transform.position.x + rangeCamera.orthographicSize * rangeCamera.aspect - 1.25f;
            return CameraFramer.RightSpawnX(9f) - 3.0f;
        }
    }

    float MaxDistance
    {
        get
        {
            var p = Presets[presetIndex];
            float formationExtent = (p.count - 1) * p.spacing;
            return Mathf.Min(MaxDistanceCap, MaxX - hero.position.x - formationExtent);
        }
    }

    void SpawnDummies()
    {
        foreach (var d in dummies) if (d != null) Destroy(d.gameObject);
        dummies.Clear();

        var p = Presets[presetIndex];
        distance = Mathf.Clamp(distance, MinDistance, Mathf.Max(MinDistance, MaxDistance));
        float extent = (p.count - 1) * p.spacing;
        float baseX = Mathf.Min(hero.position.x + distance, MaxX - extent);
        for (int i = 0; i < p.count; i++)
        {
            string label = i == 0 && p.count == 1 ? p.name : (i == 2 ? p.name : null);
            var feet = new Vector3(baseX + i * p.spacing, laneY, 0f);
            dummies.Add(TrainingDummy.Create(feet, p.height, infiniteLife ? InfiniteHp : p.hp, p.defense, label, infiniteLife));
        }
        Tracker.Reset();   // alvo/distancia/vida diferentes = medicao nova
        RebuildRuler(true);
    }

    float ReachX()
    {
        var h = hero != null ? hero.GetComponent<Hero>() : null;
        if (h == null || h.weapon == null) return 0f;
        float r = h.weapon.range * h.rangeMult * (1f + h.affixRange);
        float dy = laneY - hero.position.y;
        return Mathf.Sqrt(Mathf.Max(0f, r * r - dy * dy));
    }

    void RebuildRuler(bool force)
    {
        float reach = ReachX();
        if (!force && Mathf.Abs(reach - lastReach) < 0.01f) return;
        lastReach = reach;
        if (rulerRoot != null) Destroy(rulerRoot.gameObject);
        rulerRoot = new GameObject("RangeRuler").transform;

        float y = laneY - 0.12f;
        int max = Mathf.FloorToInt(MaxX - hero.position.x);
        for (int d = 2; d <= max; d++)
        {
            bool major = d % 2 == 0;
            Tick(hero.position.x + d, y, major ? 0.34f : 0.18f, new Color(1f, 1f, 1f, major ? 0.7f : 0.4f));
            if (major) Label(hero.position.x + d, y - 0.34f, d + "m", new Color(1f, 1f, 1f, 0.75f), 2.2f);
        }
        if (reach > 0.5f)
        {
            Tick(hero.position.x + reach, y, 0.6f, new Color(1f, 0.76f, 0.29f, 1f));
            Label(hero.position.x + reach, y - 0.62f, "ALCANCE " + N1(reach) + "m", new Color(1f, 0.76f, 0.29f, 1f), 2.4f);
        }
    }

    void Tick(float x, float y, float h, Color c)
    {
        var sr = new GameObject("Tick").AddComponent<SpriteRenderer>();
        sr.transform.SetParent(rulerRoot, false);
        sr.sprite = TrainingDummy.White();
        sr.color = c;
        sr.sortingOrder = 4000;
        sr.transform.position = new Vector3(x, y, 0f);
        sr.transform.localScale = new Vector3(0.05f, h, 1f);
    }

    void Label(float x, float y, string text, Color c, float size)
    {
        var t = new GameObject("RulerLabel").AddComponent<TextMeshPro>();
        t.transform.SetParent(rulerRoot, false);
        t.transform.position = new Vector3(x, y, 0f);
        t.text = text; t.fontSize = size; t.color = c;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.sortingOrder = 4001;
    }

    // =====================================================================
    // Painel
    // =====================================================================

    void BuildUi()
    {
        canvas = UIFactory.MakeCanvas("RangeCanvas", 40);
        var frame = UIKit.Frame(canvas.transform, "RangePanel",
            new Color(0.02f, 0.03f, 0.04f, 0.94f), UIKit.Col.Border, 2f);
        // Fica alinhado com a faixa superior do nick/mochila, mas no espaco central livre.
        UIFactory.Place(frame, new Vector2(0.5f, 1f),
            new Vector2(PanelCenterXOffset, -PanelTopOffset), new Vector2(PanelW, PanelH));
        panel = frame.gameObject;

        var title = UIFactory.Txt(frame, "MEDIÇÃO DE DANO  /  DPS", 16,
            TextAlignmentOptions.Center, UIKit.Col.Text, "RangeTitle");
        title.fontStyle = FontStyles.Bold;
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -5f), new Vector2(PanelW - 28f, 22f));

        // Colunas compactas para mostrar as estatisticas em cima, sem cobrir os controles laterais do HUD.
        var leftColumn = UIKit.Frame(frame, "RangeStatsColumnLeft",
            new Color(0.035f, 0.055f, 0.065f, 0.94f), new Color(0.20f, 0.31f, 0.34f, 0.95f), 1.25f);
        UIFactory.Place(leftColumn, new Vector2(0.5f, 1f), new Vector2(-199f, -31f), new Vector2(390f, 112f));
        var leftTitle = UIFactory.Txt(leftColumn, "DPS E DANO", 12.5f,
            TextAlignmentOptions.Left, UIKit.Col.Cyan, "RangeStatsLeftTitle");
        leftTitle.fontStyle = FontStyles.Bold;
        UIFactory.Place(leftTitle.rectTransform, new Vector2(0f, 1f), new Vector2(12f, -5f), new Vector2(360f, 18f));
        statsLeftText = UIFactory.Txt(leftColumn, "", 14f,
            TextAlignmentOptions.TopLeft, UIKit.Col.Text, "RangeStatsLeft");
        statsLeftText.textWrappingMode = TextWrappingModes.NoWrap;
        statsLeftText.richText = true;
        UIFactory.Place(statsLeftText.rectTransform, new Vector2(0f, 1f), new Vector2(12f, -24f), new Vector2(366f, 82f));

        var rightColumn = UIKit.Frame(frame, "RangeStatsColumnRight",
            new Color(0.035f, 0.055f, 0.065f, 0.94f), new Color(0.34f, 0.25f, 0.15f, 0.95f), 1.25f);
        UIFactory.Place(rightColumn, new Vector2(0.5f, 1f), new Vector2(199f, -31f), new Vector2(390f, 112f));
        var rightTitle = UIFactory.Txt(rightColumn, "DESEMPENHO E ALVO", 12.5f,
            TextAlignmentOptions.Left, UIKit.Col.Gold, "RangeStatsRightTitle");
        rightTitle.fontStyle = FontStyles.Bold;
        UIFactory.Place(rightTitle.rectTransform, new Vector2(0f, 1f), new Vector2(12f, -5f), new Vector2(360f, 18f));
        statsRightText = UIFactory.Txt(rightColumn, "", 14f,
            TextAlignmentOptions.TopLeft, UIKit.Col.Text, "RangeStatsRight");
        statsRightText.textWrappingMode = TextWrappingModes.NoWrap;
        statsRightText.richText = true;
        UIFactory.Place(statsRightText.rectTransform, new Vector2(0f, 1f), new Vector2(12f, -24f), new Vector2(366f, 82f));

        // Linha 1: selecao do alvo.
        float[] presetX = { -246f, -82f, 82f, 246f };
        for (int i = 0; i < Presets.Length; i++)
        {
            int idx = i;
            var b = UIKit.Button(frame, Presets[i].shortName, () => SelectPreset(idx), UIKit.BtnStyle.Secondary, 13f, "Preset_" + i);
            UIFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(presetX[i], -155f), new Vector2(150f, 34f));
            presetButtons.Add(b);
        }

        // Linha 2: distancia configuravel em passos de meio metro.
        var minus = UIKit.Button(frame, "−", () => ChangeDistance(-0.5f), UIKit.BtnStyle.Secondary, 21f, "DistMinus");
        UIFactory.Place((RectTransform)minus.transform, new Vector2(0.5f, 1f), new Vector2(-160f, -196f), new Vector2(60f, 34f));
        var plus = UIKit.Button(frame, "+", () => ChangeDistance(0.5f), UIKit.BtnStyle.Secondary, 21f, "DistPlus");
        UIFactory.Place((RectTransform)plus.transform, new Vector2(0.5f, 1f), new Vector2(160f, -196f), new Vector2(60f, 34f));
        distLabel = UIFactory.Txt(frame, "", 16f, TextAlignmentOptions.Center, UIKit.Col.Gold, "DistLabel");
        UIFactory.Place(distLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -196f), new Vector2(220f, 34f));

        // Linha 3: acoes do estande.
        lifeButton = UIKit.Button(frame, "", ToggleLife, UIKit.BtnStyle.Secondary, 12f, "RangeLife");
        UIFactory.Place((RectTransform)lifeButton.transform, new Vector2(0.5f, 1f), new Vector2(-160f, -236f), new Vector2(150f, 34f));
        lifeButtonLabel = lifeButton.GetComponentInChildren<TMP_Text>(true);
        pauseButton = UIKit.Button(frame, "PAUSAR", TogglePause, UIKit.BtnStyle.Secondary, 13f, "RangePause");
        UIFactory.Place((RectTransform)pauseButton.transform, new Vector2(0.5f, 1f), new Vector2(0f, -236f), new Vector2(150f, 34f));
        pauseButtonLabel = pauseButton.GetComponentInChildren<TMP_Text>(true);
        var reset = UIKit.Button(frame, "ZERAR", ResetStats, UIKit.BtnStyle.Danger, 13f, "RangeReset");
        UIFactory.Place((RectTransform)reset.transform, new Vector2(0.5f, 1f), new Vector2(160f, -236f), new Vector2(150f, 34f));

        var hint = UIFactory.Txt(frame, "[I] mochila   [F1] ocultar   [ESC] sair", 12f,
            TextAlignmentOptions.Center, UIKit.Col.TextDim, "RangeHint");
        UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 9f), new Vector2(PanelW - 20f, 18f));

        RefreshPresetButtons();
        RefreshButtons();
    }

    void RefreshPresetButtons()
    {
        for (int i = 0; i < presetButtons.Count; i++)
            UIFactory.SetButtonColor(presetButtons[i], i == presetIndex ? new Color(0.36f, 0.07f, 0.09f, 1f) : new Color(0.075f, 0.10f, 0.13f, 0.96f));
    }

    void SelectPreset(int i)
    {
        presetIndex = Mathf.Clamp(i, 0, Presets.Length - 1);
        RefreshPresetButtons();
        SpawnDummies();
    }

    void ChangeDistance(float delta)
    {
        distance = Mathf.Clamp(distance + delta, MinDistance, Mathf.Max(MinDistance, MaxDistance));
        SpawnDummies();   // reposiciona e zera (distancia diferente = teste diferente)
    }

    /// <summary>Zera dano, tempo e picos. O cronometro volta a esperar o proximo tiro.</summary>
    void ResetStats()
    {
        Tracker.Reset();
        RefreshButtons();
    }

    /// <summary>Pausa a MEDICAO (cronometro e contagem de dano); o jogo continua rodando.</summary>
    void TogglePause()
    {
        Tracker.SetPaused(!Tracker.Paused);
        RefreshButtons();
    }

    void ToggleLife()
    {
        infiniteLife = !infiniteLife;
        SpawnDummies();   // recria os bonecos com a nova regra de vida (e zera a medicao)
        RefreshButtons();
    }

    void RefreshButtons()
    {
        if (pauseButtonLabel != null) pauseButtonLabel.text = Tracker.Paused ? "CONTINUAR" : "PAUSAR";
        if (pauseButton != null)
            UIFactory.SetButtonColor(pauseButton, Tracker.Paused ? new Color(0.12f, 0.38f, 0.24f, 1f) : new Color(0.075f, 0.10f, 0.13f, 0.96f));
        if (lifeButtonLabel != null) lifeButtonLabel.text = infiniteLife ? "VIDA: INFINITA" : "VIDA: NORMAL";
        if (lifeButton != null)
            UIFactory.SetButtonColor(lifeButton, infiniteLife ? new Color(0.12f, 0.38f, 0.24f, 1f) : new Color(0.075f, 0.10f, 0.13f, 0.96f));
    }

    // =====================================================================
    // Atualizacao
    // =====================================================================

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.f1Key.wasPressedThisFrame && panel != null) panel.SetActive(!panel.activeSelf);

        Tracker.Tick(Time.deltaTime);   // cronometro: comeca no 1o dano, sem limite, congela na pausa

        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.1f;
        RebuildRuler(false);   // o alcance muda ao equipar itens
        RefreshStats();
    }

    static string Clock(float t) => ((int)(t / 60f)).ToString() + ":" + (t % 60f).ToString("00.0", Inv).Replace('.', ',');

    readonly StringBuilder leftSb = new StringBuilder(384);
    readonly StringBuilder rightSb = new StringBuilder(384);

    void RefreshStats()
    {
        if (statsLeftText == null || statsRightText == null || hero == null) return;
        var p = Presets[presetIndex];

        // DPS teorico: soma de todo o esquadrao (habilidades nao entram; o medido inclui tudo).
        float burst = 0f, sustained = 0f, avgHit = 0f;
        if (HeroSquad.Count > 0)
        {
            foreach (var m in HeroSquad.Members)
            {
                if (m == null) continue;
                var h = m.hero != null ? m.hero : (m.stats != null ? m.stats.GetComponent<Hero>() : null);
                DpsTracker.Theoretical(h, out float b, out float s, out float a);
                burst += b;
                sustained += s;
                if (a > avgHit) avgHit = a;
            }
        }
        else DpsTracker.Theoretical(hero.GetComponent<Hero>(), out burst, out sustained, out avgHit);

        float defPct = Health.UsePercentDefense ? p.defense / (p.defense + Health.DefenseHalfPoint) * 100f : 0f;
        bool outOfRange = dummies.Count > 0 && dummies[0] != null && distance > ReachX() + 0.05f;
        string state = Tracker.Paused ? "<color=#FFC24B>PAUSADO</color>"
            : !Tracker.Started ? "<color=#9AA6B2>AGUARDANDO TIRO</color>"
            : "<color=#48DE7C>MEDINDO</color>";

        // Coluna esquerda: medicao real, em tres linhas compactas.
        leftSb.Clear();
        leftSb.Append("<b>").Append(state).Append("</b>  ·  Tempo <b>").Append(Clock(Tracker.Elapsed)).Append("</b>\n");
        leftSb.Append("DANO TOTAL <b><color=#FFC24B>").Append(N0(Tracker.Total)).Append("</color></b>  ·  DPS 5s <b>")
            .Append(N0(Tracker.DpsWindow())).Append("</b>\n");
        leftSb.Append("DPS médio <b>").Append(N0(Tracker.DpsSession)).Append("</b>  ·  Pico <b>").Append(N0(Tracker.PeakDps)).Append("</b>");
        statsLeftText.text = leftSb.ToString();

        // Coluna direita: estimativas, precisao, alvo e, quando habilitado, abates/TTK.
        rightSb.Clear();
        rightSb.Append("Teórico <b>").Append(N0(burst)).Append("</b>  ·  Sustentado <b>").Append(N0(sustained)).Append("</b>\n");
        rightSb.Append("Acertos <b>").Append(Tracker.Hits).Append("</b>  ·  Críticos <b>")
            .Append(Mathf.RoundToInt(Tracker.CritRate * 100f)).Append("%</b>\n");
        rightSb.Append("Maior <b>").Append(N0(Tracker.MaxHit)).Append("</b>  ·  Médio <b>")
            .Append(N0(Tracker.AvgHit)).Append("</b>\n");
        rightSb.Append("Alvo <b>").Append(p.name).Append("</b>");
        if (p.count > 1) rightSb.Append(" x").Append(p.count);
        rightSb.Append("  ·  ");
        if (infiniteLife) rightSb.Append("Vida <b>INFINITA</b>");
        else rightSb.Append("Vida <b>").Append(N0(p.hp)).Append(" HP</b>");
        if (defPct > 0f) rightSb.Append("  ·  <color=#9AA6B2>-").Append(Mathf.RoundToInt(defPct)).Append("% dano</color>");
        if (!infiniteLife)
        {
            rightSb.Append("\nAbates <b>").Append(Tracker.Kills).Append("</b>  ·  TTK ");
            if (Tracker.Kills > 0) rightSb.Append(N1(Tracker.LastTtk)).Append("s (média ").Append(N1(Tracker.AvgTtk)).Append("s)");
            else rightSb.Append("<color=#9AA6B2>-</color>");
        }
        if (outOfRange) rightSb.Append("\n<color=#FF5050>FORA DE ALCANCE: aproxime o alvo</color>");
        statsRightText.text = rightSb.ToString();

        if (distLabel != null) distLabel.text = "DISTÂNCIA " + ND(distance) + "m";
    }
}