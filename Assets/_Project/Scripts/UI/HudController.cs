using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// HUD de combate (layout de jogo moderno):
///  - Canto superior ESQUERDO: nome da fase, dificuldade, FASE/WAVE x/y com o tempo e abates (+ zumbis vivos).
///  - Canto superior DIREITO: somente o icone da MOCHILA (clicavel, atalho [I]).
///  - EMBAIXO, a esquerda: um cartao compacto por sobrevivente do esquadrao (1 a 3), lado a lado,
///    cada um com vida, XP, arma/municao/recarga e a PROPRIA habilidade.
///  - EMBAIXO, no centro: botao da Onda de choque / Empurrar zumbi [ESPACO] (clicavel) + dica do foco por clique.
/// Os widgets antigos da cena (vida/XP/municao do principal) ficam ocultos; wave/zumbis/ouro/abates
/// sao reaproveitados dentro das novas placas. Tudo e criado em runtime: nao exige mexer na cena.
/// </summary>
public class HudController : MonoBehaviour
{
    [Header("Referencias")]
    public Health heroHealth;
    public Hero hero;
    public PlayerStats stats;
    public LootDropper loot;

    [Header("Vida")]
    public Image hpFill;
    public TMP_Text hpText;

    [Header("XP / Nivel")]
    public Image xpFill;
    public TMP_Text levelText;

    [Header("Combate")]
    public TMP_Text ammoText;
    public TMP_Text waveText;
    public TMP_Text enemiesText;

    [Header("Recursos")]
    public TMP_Text goldText;
    public TMP_Text killsText;

    [Header("Paineis")]
    public GameObject levelUpBanner;
    public GameObject gameOverPanel;
    public GameObject victoryPanel;

    // ---- medidas (resolucao de referencia 1920x1080) ----
    const float Margin = 16f;
    const float CardW = 316f, CardH = 128f, CardGap = 12f;
    const float ShockW = 240f;
    const float RefWidth = 1920f;      // largura de referencia do CanvasScaler
    const float BackpackSize = 92f;

    static readonly Color PlateFill = new Color(0.01f, 0.02f, 0.03f, 0.84f);
    static readonly Color HudDim = new Color(0.62f, 0.72f, 0.76f, 1f);
    static readonly Color HpColor = new Color(0.88f, 0.12f, 0.15f, 1f);
    static readonly Color HpLow = new Color(1f, 0.32f, 0.10f, 1f);
    static readonly Color ReadyColor = new Color(0.35f, 0.92f, 0.48f, 1f);
    static readonly Color CooldownColor = new Color(1f, 0.62f, 0.16f, 1f);

    // ---- cartoes do esquadrao ----
    sealed class SquadCard
    {
        public RectTransform root;
        public Image hpFill, xpFill, reloadFill, abilityFill, abilityDot;
        public TMP_Text role, name, level, hp, weapon, ammo, ability;
        public PlayerStats stats;
        public Hero hero;
        public Health health;
        public SurvivorDef def;
        public int index;
        public long hpKey = long.MinValue;
        public int lvlKey = int.MinValue, ammoKey = int.MinValue, abilityKey = int.MinValue, xpKey = int.MinValue;
        public int aliveKey = -1;
    }
    readonly List<SquadCard> cards = new List<SquadCard>();
    int builtSquadCount = -1;

    // ---- placas / atalhos ----
    RectTransform wavePlate, accountPlate, shockPlate;
    TMP_Text shockStatus;
    Image shockFill;
    TMP_Text lowHpWarning;
    int lastEnemies = -1, lastGold = -1, lastKills = -1, lastShockKey = int.MinValue;
    int waveCur, waveTotal, lastWaveTimer = -2;
    bool built;

    // ---------- ciclo de vida ----------
    void OnEnable()
    {
        ResolveReferences();
        GameEvents.WaveChanged += OnWave;
        GameEvents.LevelUp += OnLevelUp;
        GameEvents.MissionFailed += ShowGameOver;
        GameEvents.MissionComplete += ShowVictory;
    }

    void OnDisable()
    {
        GameEvents.WaveChanged -= OnWave;
        GameEvents.LevelUp -= OnLevelUp;
        GameEvents.MissionFailed -= ShowGameOver;
        GameEvents.MissionComplete -= ShowVictory;
    }

    void Start()
    {
        if (levelUpBanner) levelUpBanner.SetActive(false);
        if (gameOverPanel) gameOverPanel.SetActive(false);
        if (victoryPanel) victoryPanel.SetActive(false);
        BuildHud();
    }

    void ResolveReferences()
    {
        // O "principal" do HUD e sempre o sobrevivente da FRENTE do esquadrao.
        if (!hero) hero = HeroSquad.Primary ? HeroSquad.Primary.GetComponent<Hero>() : FindFirstObjectByType<Hero>();
        if (!heroHealth && hero) heroHealth = hero.GetComponent<Health>();
        if (!stats) stats = HeroSquad.Primary ? HeroSquad.Primary : FindFirstObjectByType<PlayerStats>();
        if (!loot) loot = FindFirstObjectByType<LootDropper>();
    }

    void Update()
    {
        if (!built) BuildHud();
        ResolveReferences();

        UpdateWaveLabel();
        if (enemiesText && Enemy.Alive.Count != lastEnemies) { lastEnemies = Enemy.Alive.Count; enemiesText.text = lastEnemies.ToString(); }
        if (loot)
        {
            if (goldText && loot.gold != lastGold) { lastGold = loot.gold; goldText.text = lastGold.ToString("N0"); }
            if (killsText && loot.kills != lastKills) { lastKills = loot.kills; killsText.text = lastKills.ToString("N0"); }
        }

        int squadCount = Mathf.Max(1, HeroSquad.Count);
        if (squadCount != builtSquadCount) BuildSquadCards();
        foreach (var c in cards) UpdateCard(c);

        UpdateShockButton();
        RefreshLowHealthWarning();
        HandleHotkeys();
    }

    // ---------- construcao ----------
    void BuildHud()
    {
        if (built) return;
        built = true;
        ResolveReferences();
        HideLegacyWidgets();
        BuildWavePlate();
        BuildAccountPlate();
        BuildShockPlate();
        if (GameSession.IsRange && shockPlate != null) { shockPlate.anchorMin = shockPlate.anchorMax = new Vector2(1f, 0f); shockPlate.pivot = new Vector2(1f, 0f); shockPlate.anchoredPosition = new Vector2(-16f, 230f); }
        BuildSquadCards();
        BuildLowHpWarning();
        // A mochila (InventoryPanel) e os banners da cena continuam POR CIMA das placas novas.
        if (levelUpBanner) levelUpBanner.transform.SetAsLastSibling();
        var toggle = PanelToggle.Instance;
        if (toggle && toggle.panel && toggle.panel.transform.parent == transform) toggle.panel.transform.SetAsLastSibling();
    }

    /// <summary>Esconde vida/XP/municao antigos do principal (agora estao no cartao dele).</summary>
    void HideLegacyWidgets()
    {
        foreach (var go in new GameObject[]
        {
            hpFill ? hpFill.gameObject : null, hpText ? hpText.gameObject : null,
            xpFill ? xpFill.gameObject : null, levelText ? levelText.gameObject : null,
            ammoText ? ammoText.gameObject : null, goldText ? goldText.gameObject : null
        })
            if (go) go.SetActive(false);
        foreach (var n in new[] { "HpBg", "XpBg", "EnemiesLbl", "ZombitHUDPlateTL", "ZombitHUDPlateTR", "ZombitHUDPlateBL" })
        {
            var t = transform.Find(n);
            if (t) t.gameObject.SetActive(false);
        }
    }

    RectTransform MakePlate(string name, Vector2 anchor, Vector2 pos, Vector2 size, Color accent)
    {
        var plate = UIKit.Frame(transform, name, PlateFill, new Color(accent.r, accent.g, accent.b, 0.55f), 2f);
        Place(plate, anchor, anchor, pos, size);
        var line = UIFactory.Img(plate, accent, "AccentLine");
        var lr = line.rectTransform;
        lr.anchorMin = new Vector2(0f, 1f); lr.anchorMax = new Vector2(1f, 1f); lr.pivot = new Vector2(0.5f, 1f);
        lr.offsetMin = new Vector2(10f, -3f); lr.offsetMax = new Vector2(-10f, 0f);
        line.raycastTarget = false;
        plate.SetAsFirstSibling();
        return plate;
    }

    /// <summary>Nick do jogador (salvo no menu); se vazio, o nome da classe jogada.</summary>
    static string PlayerNick()
    {
        string n = GameSession.Save != null ? GameSession.Save.playerNickname : null;
        if (string.IsNullOrWhiteSpace(n) && GameSession.Survivor != null) n = GameSession.Survivor.name;
        n = string.IsNullOrWhiteSpace(n) ? "Sobrevivente" : n.Trim().Replace("\n", "").Replace("\r", "");
        return n.Length > 18 ? n.Substring(0, 18) : n;
    }

    void BuildWavePlate()
    {
        const float w = 372f, h = 148f;
        wavePlate = MakePlate("HudPlate_Wave", new Vector2(0f, 1f), new Vector2(Margin, -Margin), new Vector2(w, h), UIKit.Col.Accent);

        // Linha 1: nome da fase.   Linha 2: dificuldade (colorida).
        var loc = GameSession.Location;
        string zone = loc != null && !string.IsNullOrWhiteSpace(loc.name) ? loc.name.ToUpperInvariant() : "MISSÃO";
        var zoneLabel = Label(wavePlate, zone, 23f, new Color(1f, 0.90f, 0.66f, 1f), "HudZoneName", 20f, 8f, w - 40f, 30f);
        zoneLabel.characterSpacing = 1.0f;

        var diff = Difficulties.Get(GameSession.Difficulty);
        var dot = UIFactory.Node("HudDiffDot", wavePlate);
        Put(dot, wavePlate, 22f, 45f, 1f, 1f);
        UIKit.Diamond(dot, diff.color, 11f, "DiffDiamond");
        Label(wavePlate, "DIFICULDADE", 11f, HudDim, "HudDiffLabel", 34f, 38f, 100f, 18f);
        var diffText = Label(wavePlate, diff.name.ToUpperInvariant(), 14f, diff.color, "HudDifficulty", 134f, 37f, w - 154f, 20f);
        diffText.characterSpacing = 1.6f;

        // separador fino
        var sep = UIFactory.Img(wavePlate, new Color(UIKit.Col.Accent.r, UIKit.Col.Accent.g, UIKit.Col.Accent.b, 0.35f), "HudSep");
        sep.raycastTarget = false;
        Put(sep.rectTransform, wavePlate, 20f, 62f, w - 40f, 2f);

        // Linha 3: FASE/WAVE x/y + tempo.
        if (waveText)
        {
            StyleText(waveText, 30f, new Color(1f, 0.88f, 0.62f, 1f), true);
            waveText.alignment = TextAlignmentOptions.MidlineLeft;
            waveText.enableAutoSizing = true; waveText.fontSizeMin = 18f; waveText.fontSizeMax = 30f;
            Put(waveText.rectTransform, wavePlate, 20f, 66f, w - 40f, 40f);
            waveText.gameObject.SetActive(true);
        }

        // Linha 4: abates (+ zumbis vivos, menor).
        Label(wavePlate, "ABATES", 14f, HudDim, "HudKillsLabel", 20f, 112f, 72f, 28f);
        if (killsText)
        {
            StyleText(killsText, 23f, Color.white, true);
            killsText.alignment = TextAlignmentOptions.MidlineLeft;
            Put(killsText.rectTransform, wavePlate, 94f, 111f, 80f, 30f);
            killsText.gameObject.SetActive(true);
        }
        Label(wavePlate, "ZUMBIS", 12f, HudDim, "HudEnemiesLabel", 200f, 114f, 66f, 24f);
        if (enemiesText)
        {
            StyleText(enemiesText, 19f, new Color(0.86f, 0.90f, 0.92f, 1f), true);
            enemiesText.alignment = TextAlignmentOptions.MidlineLeft;
            Put(enemiesText.rectTransform, wavePlate, 266f, 112f, 90f, 28f);
            enemiesText.gameObject.SetActive(true);
        }
    }

    /// <summary>Canto direito: so o icone da mochila (mesmo PNG do menu). Clique ou [I] abre/fecha.</summary>
    void BuildAccountPlate()
    {
        accountPlate = MakePlate("HudPlate_Backpack", new Vector2(1f, 1f), new Vector2(-Margin, -Margin),
            new Vector2(BackpackSize, BackpackSize), UIKit.Col.Gold);

        var iconImg = UIFactory.Img(accountPlate, Color.white, "HudBackpackIcon");
        iconImg.sprite = ArtLoader.Get("Art/Skills/icon_backpack");
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;
        var ir = iconImg.rectTransform;
        ir.anchorMin = Vector2.zero; ir.anchorMax = Vector2.one;
        ir.offsetMin = new Vector2(8f, 8f); ir.offsetMax = new Vector2(-8f, -8f);
        if (iconImg.sprite == null) iconImg.enabled = false;   // sem PNG: so a placa e o selo [I]

        // selo da tecla de atalho, no canto inferior direito
        var key = UIKit.Frame(accountPlate, "HudBackpackKey", new Color(0.05f, 0.10f, 0.12f, 0.95f), UIKit.Col.Gold, 1.5f);
        Put(key, accountPlate, BackpackSize - 34f, BackpackSize - 26f, 28f, 20f);
        var kt = UIFactory.Txt(key, "I", 12f, TextAlignmentOptions.Center, UIKit.Col.Gold, "Label");
        kt.fontStyle = FontStyles.Bold; kt.raycastTarget = false;
        UIFactory.Stretch(kt.rectTransform);

        // Area clicavel sobre a placa inteira; o realce de hover tinge o icone.
        var hit = UIFactory.Img(accountPlate, new Color(1f, 1f, 1f, 0f), "HudBackpackHit");
        UIFactory.Stretch(hit.rectTransform);
        hit.raycastTarget = true;
        var btn = hit.gameObject.AddComponent<Button>();
        btn.targetGraphic = iconImg;
        btn.transition = Selectable.Transition.ColorTint;
        var cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1f, 0.92f, 0.66f, 1f);
        cb.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        cb.fadeDuration = 0.08f;
        btn.colors = cb;
        btn.onClick.AddListener(() => { ZombitAudio.PlayUI(); ToggleInventory(); });
        hit.transform.SetAsLastSibling();
    }

    void BuildShockPlate()
    {
        shockPlate = MakePlate("HudPlate_Shock", new Vector2(0.5f, 0f), new Vector2(0f, Margin), new Vector2(ShockW, CardH), UIKit.Col.Cyan);
        // A placa inteira e um botao: clicar dispara a onda de choque (igual a ESPACO).
        var hit = UIFactory.Img(shockPlate, new Color(1f, 1f, 1f, 0f), "HudShockHit");
        UIFactory.Stretch(hit.rectTransform);
        hit.raycastTarget = true;
        var btn = hit.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(TriggerShock);

        var key = UIKit.Frame(shockPlate, "HudShockKey", new Color(0.05f, 0.10f, 0.12f, 1f), UIKit.Col.Cyan, 1.5f);
        Put(key, shockPlate, 16f, 12f, 82f, 22f);
        var kt = UIFactory.Txt(key, "ESPAÇO", 11f, TextAlignmentOptions.Center, UIKit.Col.Cyan, "Label");
        kt.fontStyle = FontStyles.Bold; kt.raycastTarget = false;
        UIFactory.Stretch(kt.rectTransform);
        Label(shockPlate, "EMPURRAR ZUMBI", 15f, Color.white, "HudShockTitle", 16f, 42f, ShockW - 32f, 22f);
        shockStatus = Label(shockPlate, "PRONTA", 13f, ReadyColor, "HudShockStatus", 16f, 66f, ShockW - 32f, 20f);
        shockFill = UIKit.Bar(shockPlate, ReadyColor, 1f, "HudShockBar");
        Put((RectTransform)shockFill.transform.parent, shockPlate, 16f, 90f, ShockW - 32f, 5f);
        Label(shockPlate, "CLIQUE NO ZUMBI: FOCAR", 10f, HudDim, "HudFocusHint", 16f, 102f, ShockW - 32f, 16f);
        hit.transform.SetAsLastSibling();
    }

    void BuildLowHpWarning()
    {
        lowHpWarning = UIFactory.Txt(transform, "VIDA BAIXA", 22f, TextAlignmentOptions.Center, new Color(1f, 0.22f, 0.18f, 1f), "HudLowHealthWarning");
        Place(lowHpWarning.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(360f, 32f));
        lowHpWarning.fontStyle = FontStyles.Bold;
        lowHpWarning.raycastTarget = false;
        lowHpWarning.gameObject.SetActive(false);
    }

    void BuildSquadCards()
    {
        foreach (var c in cards) if (c.root) Destroy(c.root.gameObject);
        cards.Clear();
        if (GameSession.IsRange) { builtSquadCount = Mathf.Max(1, HeroSquad.Count); return; }

        var members = new List<(PlayerStats ps, SurvivorDef def)>();
        if (HeroSquad.Count > 0) foreach (var m in HeroSquad.Members) members.Add((m.stats, m.def));
        else if (stats) members.Add((stats, GameSession.Survivor));
        builtSquadCount = Mathf.Max(1, HeroSquad.Count);
        if (members.Count == 0) return;

        int n = members.Count;
        // Zona dos cartoes: da margem esquerda ate antes do botao central (Empurrar).
        float zoneRight = RefWidth * 0.5f - ShockW * 0.5f - 16f;
        float avail = zoneRight - Margin;
        float cardW = Mathf.Min(CardW, (avail - (n - 1) * CardGap) / n);
        for (int i = 0; i < n; i++)
        {
            float x = Margin + i * (cardW + CardGap);
            cards.Add(BuildCard(i, n, members[i].ps, members[i].def, x, cardW));
        }
    }

    SquadCard BuildCard(int index, int count, PlayerStats ps, SurvivorDef def, float leftX, float cardW)
    {
        var c = new SquadCard { index = index, stats = ps, def = def };
        if (ps)
        {
            c.hero = ps.GetComponent<Hero>();
            c.health = ps.GetComponent<Health>();
        }
        Color accent = index == 0 ? UIKit.Col.Accent : UIKit.Col.Cyan;
        c.root = MakePlate("HudSquadCard_" + index, new Vector2(0f, 0f), new Vector2(leftX, Margin), new Vector2(cardW, CardH), accent);
        float inner = cardW - 28f;

        string role = index == 0 ? "FRENTE" : count > 2 ? (index == 1 ? "MEIO" : "FUNDO") : "APOIO";
        c.role = Label(c.root, role, 11f, accent, "Role", 14f, 10f, 64f, 18f);
        c.name = Label(c.root, def != null ? def.name.ToUpperInvariant() : "SOBREVIVENTE", 15f, Color.white, "Name", 78f, 9f, cardW - 78f - 80f, 20f);
        c.level = Label(c.root, "", 13f, UIKit.Col.Gold, "Level", cardW - 88f, 9f, 74f, 20f);
        c.level.alignment = TextAlignmentOptions.MidlineRight;

        // Vida (fina) com numeros por cima.
        c.hpFill = UIKit.Bar(c.root, HpColor, 1f, "HpBar");
        var hpBg = (RectTransform)c.hpFill.transform.parent;
        Put(hpBg, c.root, 14f, 34f, inner, 14f);
        hpBg.GetComponent<Image>().color = new Color(0.12f, 0.03f, 0.04f, 1f);
        c.hp = Label(c.root, "", 11f, Color.white, "HpText", 14f, 34f, inner, 14f);
        c.hp.alignment = TextAlignmentOptions.Center;

        // XP do proprio sobrevivente (cada um tem o seu nivel).
        c.xpFill = UIKit.Bar(c.root, UIKit.Col.Cyan, 0f, "XpBar");
        var xpBg = (RectTransform)c.xpFill.transform.parent;
        Put(xpBg, c.root, 14f, 51f, inner, 4f);
        xpBg.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.10f, 1f);

        // Arma + municao/recarga.
        c.weapon = Label(c.root, "", 12f, HudDim, "Weapon", 14f, 62f, inner * 0.42f, 22f);
        c.ammo = Label(c.root, "", 17f, Color.white, "Ammo", 14f + inner * 0.42f, 61f, inner * 0.58f, 24f);
        c.ammo.alignment = TextAlignmentOptions.MidlineRight;
        c.reloadFill = UIKit.Bar(c.root, CooldownColor, 0f, "ReloadBar");
        Put((RectTransform)c.reloadFill.transform.parent, c.root, 14f, 87f, inner, 3f);

        // Habilidade propria.
        var dotHolder = UIFactory.Node("AbilityDotHolder", c.root);
        Put(dotHolder, c.root, 19f, 104f, 1f, 1f);
        c.abilityDot = UIKit.Diamond(dotHolder, ReadyColor, 11f, "AbilityDot");
        c.ability = Label(c.root, "", 12f, Color.white, "Ability", 30f, 94f, inner - 16f, 20f);
        c.abilityFill = UIKit.Bar(c.root, ReadyColor, 1f, "AbilityBar");
        Put((RectTransform)c.abilityFill.transform.parent, c.root, 14f, 117f, inner, 3f);
        return c;
    }

    // ---------- atualizacao dos cartoes (textos so mudam quando o valor muda) ----------
    void UpdateCard(SquadCard c)
    {
        if (c == null || !c.root) return;
        bool alive = c.health == null || c.health.Current > 0f;
        int aliveKey = alive ? 1 : 0;
        if (aliveKey != c.aliveKey)
        {
            c.aliveKey = aliveKey;
            var cg = c.root.GetComponent<CanvasGroup>();
            if (!cg) cg = c.root.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = alive ? 1f : 0.55f;
            cg.blocksRaycasts = false;
            if (!alive) c.ability.text = "";
        }

        // Vida
        if (c.health)
        {
            int cur = Mathf.CeilToInt(Mathf.Max(0f, c.health.Current)), max = Mathf.CeilToInt(c.health.max);
            long key = cur + max * 1000000L;
            if (key != c.hpKey)
            {
                c.hpKey = key;
                float r = max > 0 ? Mathf.Clamp01((float)cur / max) : 0f;
                c.hpFill.fillAmount = r;
                c.hpFill.color = r <= 0.25f ? HpLow : HpColor;
                c.hp.text = alive ? $"{cur}/{max}" : "CAIU  ·  FORA DE COMBATE";
            }
        }

        // Nivel + XP
        if (c.stats)
        {
            if (c.stats.Level != c.lvlKey) { c.lvlKey = c.stats.Level; c.level.text = $"NV {c.lvlKey}"; }
            int xpPermil = c.stats.XpToNext > 0 ? Mathf.RoundToInt(Mathf.Clamp01((float)c.stats.Xp / c.stats.XpToNext) * 1000f) : 0;
            if (xpPermil != c.xpKey) { c.xpKey = xpPermil; c.xpFill.fillAmount = xpPermil / 1000f; }
        }

        // Arma / municao / recarga
        var h = c.hero;
        if (h != null && h.weapon != null)
        {
            bool reloading = alive && h.Reloading;
            int pct = reloading ? Mathf.FloorToInt(h.ReloadProgress * 10f) * 10 : -1;
            int ak = !alive ? -2 : reloading ? 100000 + pct : h.Ammo;
            if (ak != c.ammoKey)
            {
                c.ammoKey = ak;
                c.weapon.text = (h.weapon.displayName ?? "").ToUpperInvariant();
                if (!alive) { c.ammo.text = "-"; c.ammo.color = HudDim; }
                else if (reloading) { c.ammo.text = $"RECARREGANDO {pct}%"; c.ammo.color = CooldownColor; c.ammo.fontSize = 13f; }
                else { c.ammo.text = $"{h.Ammo}/{h.MagSize}"; c.ammo.color = h.Ammo <= Mathf.Max(1, h.MagSize / 5) ? CooldownColor : Color.white; c.ammo.fontSize = 17f; }
            }
            // Barra de recarga suave (so numeros).
            c.reloadFill.fillAmount = reloading ? Mathf.Clamp01(h.ReloadProgress) : 0f;
        }

        if (alive) UpdateCardAbility(c);
    }

    void UpdateCardAbility(SquadCard c)
    {
        var ab = c.stats ? c.stats.GetComponent<SurvivorAbilities>() : null;
        var def = ab != null && ab.Definition != null ? ab.Definition : c.def;
        if (def == null) return;

        int state, seconds = 0;
        float fill = 1f;
        Color col = AccentFor(def.utility);
        if (def.utility == UtilityType.Grenade)
        {
            if (ab == null) { state = 1; fill = 0f; }
            else if (ab.GrenadeInFlight) { state = 2; fill = ab.GrenadeReadyRatio; }
            else if (ab.GrenadeCooldownRemaining > 0f) { state = 3; seconds = Mathf.CeilToInt(ab.GrenadeCooldownRemaining); fill = ab.GrenadeReadyRatio; }
            else state = 4;
            if (state != 4) col = CooldownColor;
        }
        else if (def.utility == UtilityType.Precision)
        {
            state = 5;
            seconds = Mathf.RoundToInt(Mathf.Clamp01(c.hero ? c.hero.critChance : def.critChance) * 100f);
        }
        else state = 10 + (int)def.utility;

        int key = state * 1000 + Mathf.Clamp(seconds, 0, 999);
        if (key != c.abilityKey)
        {
            c.abilityKey = key;
            string name = (string.IsNullOrWhiteSpace(def.utilityName) ? def.utility.ToString() : def.utilityName).ToUpperInvariant();
            string st;
            switch (state)
            {
                case 1: st = "PREPARANDO"; break;
                case 2: st = "EM VOO"; break;
                case 3: st = $"RECARGA {seconds}s"; break;
                case 4: st = "PRONTA"; break;
                case 5: st = $"CRÍTICO {seconds}%"; break;
                default:
                    st = def.utility == UtilityType.FieldMedic ? "CURA ATIVA"
                       : def.utility == UtilityType.Turret ? "TORRETA ATIVA"
                       : def.utility == UtilityType.Scavenger ? "SAQUE ATIVO" : "ATIVA";
                    break;
            }
            c.ability.text = $"{name}  <color=#{ColorUtility.ToHtmlStringRGB(col)}>{st}</color>";
            c.abilityDot.color = col;
        }
        c.abilityFill.fillAmount = fill;
        c.abilityFill.color = col;
    }

    // ---------- onda de choque ----------
    ActiveSkills CurrentActive()
    {
        foreach (var m in HeroSquad.Members)
            if (m.stats && m.Alive) { var a = m.stats.GetComponent<ActiveSkills>(); if (a) return a; }
        return hero ? hero.GetComponent<ActiveSkills>() : null;
    }

    void TriggerShock()
    {
        var a = CurrentActive();
        if (a != null && a.TryShockWave()) ZombitAudio.PlayUI();
    }

    void UpdateShockButton()
    {
        if (!shockStatus || !shockFill) return;
        var a = CurrentActive();
        int remain = a != null ? Mathf.CeilToInt(a.ShockCooldownRemaining) : -1;
        if (remain != lastShockKey)
        {
            lastShockKey = remain;
            if (remain < 0) { shockStatus.text = "INDISPONÍVEL"; shockStatus.color = HudDim; }
            else if (remain == 0) { shockStatus.text = "PRONTA  ·  CLIQUE OU ESPAÇO"; shockStatus.color = ReadyColor; }
            else { shockStatus.text = $"RECARGA {remain}s"; shockStatus.color = CooldownColor; }
        }
        float ratio = a != null ? a.ShockReadyRatio : 0f;
        shockFill.fillAmount = ratio;
        shockFill.color = ratio >= 1f ? ReadyColor : CooldownColor;
    }

    // ---------- atalhos ----------
    void HandleHotkeys()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        // [H] (painel de habilidades, que pausava) foi desativado: a missao nunca pausa pela UI.
        // [I] continua no PanelToggle (mochila) e [ESC] no MissionPauseMenu.
    }

    void ToggleInventory()
    {
        var t = PanelToggle.Instance;
        if (t != null) t.Toggle();
    }

    void ToggleSkills() => SquadSkillsOverlay.Toggle();

    void OpenPause()
    {
        var p = FindFirstObjectByType<MissionPauseMenu>();
        if (p != null) p.Open();
    }

    // ---------- avisos ----------
    void RefreshLowHealthWarning()
    {
        if (!lowHpWarning) return;
        // Vale para quem esta NA FRENTE (e quem esta tomando o dano).
        var front = HeroSquad.FrontMember();
        Health hp = front != null ? front.health : heroHealth;
        bool show = false;
        if (hp != null && hp.max > 0f) show = hp.Current > 0f && hp.Current / hp.max <= 0.25f;
        if (lowHpWarning.gameObject.activeSelf != show) lowHpWarning.gameObject.SetActive(show);
        if (show)
        {
            if (front != null && HeroSquad.IsDuo) lowHpWarning.text = "VIDA BAIXA  ·  " + front.Name.ToUpperInvariant();
            lowHpWarning.color = new Color(1f, 0.20f, 0.16f, 0.62f + 0.38f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4.5f)));
        }
    }

    void OnWave(int cur, int total)
    {
        waveCur = cur; waveTotal = total; lastWaveTimer = -2;
        UpdateWaveLabel();
    }

    // Mostra a contagem para a proxima onda (pressao de tempo) sem realocar string a cada frame.
    void UpdateWaveLabel()
    {
        if (!waveText || waveTotal <= 0) return;
        if (GameEvents.HordeMode)
        {
            // Horda continua: "FASE 2/5  1:32" (tempo restante); FURIA em vermelho quando comeca.
            float hl = GameEvents.HordeTimeLeft;
            int secs = hl < 0f ? -1 : Mathf.CeilToInt(hl);
            int key = secs * 2 + (GameEvents.HordeFury ? 1 : 0);
            if (key == lastWaveTimer) return;
            lastWaveTimer = key;
            string clock = secs < 0 ? "" : secs == 0 ? "ELIMINE TODOS" : $"{secs / 60}:{secs % 60:00}";
            string fury = GameEvents.HordeFury ? "  <color=#FF5050>FÚRIA</color>" : "";
            waveText.text = $"FASE {waveCur}/{waveTotal}  <size=60%><color=#9AA6B2>{clock}</color>{fury}</size>";
            return;
        }
        float left = GameEvents.WaveTimeLeft;
        int t = left < 0f ? -1 : Mathf.CeilToInt(left);
        if (t == lastWaveTimer) return;
        lastWaveTimer = t;
        waveText.text = t < 0 ? $"WAVE {waveCur}/{waveTotal}" : $"WAVE {waveCur}/{waveTotal}  <size=60%><color=#9AA6B2>PRÓXIMA EM {t}s</color></size>";
    }

    // O aviso de level up agora e o LevelUpEffect (faixa animada + efeitos no personagem).
    // O banner antigo da cena fica sempre oculto.
    void OnLevelUp(int lvl) { if (levelUpBanner) levelUpBanner.SetActive(false); }

    // Os painéis legados de GameOver/Victory da cena NÃO são mais ativados aqui:
    // o MissionFlow constroi a tela de resultados completa (com estatísticas, recompensas
    // e avaliação por estrelas). Ativar os dois criava interfaces duplicadas sobrepostas.
    void ShowGameOver() { }
    void ShowVictory() { }

    // ---------- utilitarios ----------
    static Color AccentFor(UtilityType utility)
    {
        switch (utility)
        {
            case UtilityType.Grenade: return new Color(1f, 0.48f, 0.13f, 1f);
            case UtilityType.Precision: return new Color(0.34f, 0.78f, 1f, 1f);
            case UtilityType.FieldMedic: return new Color(0.28f, 0.90f, 0.53f, 1f);
            case UtilityType.Turret: return new Color(0.73f, 0.78f, 0.30f, 1f);
            case UtilityType.Scavenger: return new Color(1f, 0.79f, 0.20f, 1f);
            default: return ReadyColor;
        }
    }

    static void StyleText(TMP_Text text, float size, Color color, bool bold)
    {
        if (!text) return;
        var theme = UITheme.Current;
        if (theme != null && theme.displayFont != null && size >= 20f) text.font = theme.displayFont;
        text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        text.fontSize = size;
        text.color = color;
        text.richText = true;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Truncate;
        text.raycastTarget = false;
    }

    TMP_Text Label(RectTransform parent, string text, float size, Color color, string name, float x, float y, float w, float h)
    {
        var t = UIFactory.Txt(parent, text, size, TextAlignmentOptions.MidlineLeft, color, name);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Truncate;
        t.fontStyle = FontStyles.Bold;
        t.characterSpacing = 1.2f;
        t.richText = true;
        t.raycastTarget = false;
        Put(t.rectTransform, parent, x, y, w, h);
        return t;
    }

    /// <summary>Coloca um elemento dentro da placa, em coordenadas locais (canto superior esquerdo, y para baixo).</summary>
    static void Put(RectTransform rt, RectTransform parent, float x, float y, float w, float h)
    {
        if (!rt || !parent) return;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        rt.localScale = Vector3.one;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}
