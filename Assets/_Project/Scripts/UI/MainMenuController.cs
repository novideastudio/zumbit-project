using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public partial class MainMenuController : MonoBehaviour
{
    const string PendingNicknameKey = "zombit_pending_nick";
    static readonly Vector2 TopC = new Vector2(0.5f, 1f);
    static readonly Vector2 BotC = new Vector2(0.5f, 0f);
    static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);

    Canvas canvas;
    RectTransform root;
    int responsiveCanvasMode = -1;
    readonly Dictionary<string, RectTransform> screens = new Dictionary<string, RectTransform>();

    Button continueBtn;
    TMP_Text toast;
    Coroutine toastCo;
    TMP_Text volumeLabel, fsLabel, resolutionLabel;
    TMP_InputField nicknameInput;
    TMP_Text nicknameHelp;
    string nicknameReturnScreen = "main";
    bool nicknameEditsCurrentSave;
    bool nicknameForNewGame;
    TMP_Text playerIdentityInSelect;
    int resolutionIndex = -1;

    SurvivorDef selected;
    bool newGameMode;
    readonly List<Image> cardImgs = new List<Image>();
    readonly List<Button> cardBtns = new List<Button>();
    TMP_Text selName, selDesc, selUtil, selNote, confirmLabel;
    readonly Image[] statFills = new Image[StatRows];
    const int StatRows = 5;   // VIDA + 4 atributos da arma exclusiva

    LocationDef selectedLoc;
    TMP_Text dName, dInfo, dDesc;
    Button dStart;
    Image selectedLocationPreview;
    string mapInventorySelectedUid;
    string selectedSkillId;
    readonly Dictionary<string, Outline> nodeOutlines = new Dictionary<string, Outline>();

    void Start()
    {
        Time.timeScale = 1f;
        AudioListener.volume = PlayerPrefs.GetFloat("zombit_vol", 1f);
        if (PlayerPrefs.HasKey("zombit_fs")) Screen.fullScreen = PlayerPrefs.GetInt("zombit_fs") == 1;

        canvas = UIFactory.MakeCanvas("MenuCanvas", 0);
        root = (RectTransform)canvas.transform;
        RefreshResponsiveCanvasScale(true);
        var bg = UIFactory.Img(root, UIFactory.Dark, "Background");
        // Ignore the legacy composite menu image (fundo-menu.png), which has a baked-in
        // survivor. Use the clean gameplay background instead so the main menu can show
        // the scenery without a second character layered over it.
        var legacyBackgroundFitter = bg.GetComponent<AspectRatioFitter>();
        if (legacyBackgroundFitter != null)
        {
            legacyBackgroundFitter.enabled = false;
            Destroy(legacyBackgroundFitter);
        }
        Sprite cleanBackground = ArtLoader.Get(SceneBackgrounds.DefaultPath);
        bg.sprite = cleanBackground != null ? cleanBackground : UIFactory.White;
        bg.color = cleanBackground != null ? Color.white : UIFactory.Dark;
        bg.preserveAspect = false;
        UIFactory.Stretch(bg.rectTransform);

        // Camada decorativa discreta para dar identidade ao menu sem depender de novos assets.
        var topShade = UIFactory.Img(root, new Color(0.015f, 0.02f, 0.025f, 0.72f), "TopShade");
        UIFactory.Stretch(topShade.rectTransform, 0f, 0.76f, 1f, 1f);
        var bottomShade = UIFactory.Img(root, new Color(0.015f, 0.02f, 0.025f, 0.58f), "BottomShade");
        UIFactory.Stretch(bottomShade.rectTransform, 0f, 0f, 1f, 0.17f);

        toast = UIFactory.Txt(root, "", 30, TextAlignmentOptions.Center, UIFactory.Gold, "Toast");
        UIFactory.Place(toast.rectTransform, new Vector2(0.5f, 0.9f), new Vector2(0, -10), new Vector2(1200, 44));
        toast.gameObject.SetActive(false);

        ApplySavedResolution();
        BuildMain();
        BuildOptions();
        BuildCredits();
        BuildSelect();
        BuildNicknameEditor();

        // Conta online (login Google + nick unico). O ZombitCloud sobrevive entre cenas: ao voltar de uma missao
        // a conta ja esta pronta e o menu abre direto no mapa, como antes.
        ZombitCloud.Ensure();
        BuildLogin();
        BuildAccountBadge();
        HidePcTitleLegacyAccount();
        HookNicknameInput();
        ZombitCloud.StateChanged += OnCloudStateChanged;

        if (ZombitCloud.IsReady)
        {
            ApplyAccountNickPrefs();
            if (GameSession.OpenMapOnLoad && GameSession.Save != null)
            {
                GameSession.OpenMapOnLoad = false;
                ShowMap();
            }
            else Show("main");
        }
        else
        {
            GameSession.OpenMapOnLoad = false;
            if (ZombitCloud.State == CloudState.NeedsNick) OpenAccountNick("main", true);
            else ShowLogin();
        }
    }

    // Recalibra a escala quando a janela Web/PC muda de tamanho ou quando o jogo abre no celular.
    // O modo compacto usa uma resolucao de referencia menor para evitar textos e botoes minúsculos.
    void Update() => RefreshResponsiveCanvasScale(false);

    void RefreshResponsiveCanvasScale(bool force)
    {
        if (canvas == null) return;
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) return;
        bool compact = Screen.width < 1100 || Screen.height < 650;
        int mode = compact ? 1 : 0;
        if (!force && mode == responsiveCanvasMode) return;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referenceResolution = compact ? new Vector2(1280f, 720f) : new Vector2(1920f, 1080f);
        responsiveCanvasMode = mode;
        Canvas.ForceUpdateCanvases();
    }

    // ---------- infra ----------
    RectTransform NewScreen(string key)
    {
        var rt = UIFactory.Node(key, root);
        UIFactory.Stretch(rt);
        rt.gameObject.SetActive(false);
        screens[key] = rt;
        return rt;
    }

    RectTransform Rebuild(string key)
    {
        if (screens.TryGetValue(key, out var old) && old != null)
        {
            old.gameObject.SetActive(false);
            Destroy(old.gameObject);
        }
        return NewScreen(key);
    }

    void Show(string key)
    {
        currentScreen = key;
        foreach (var kv in screens) if (kv.Value) kv.Value.gameObject.SetActive(kv.Key == key);
        if (key == "main" && continueBtn) continueBtn.interactable = PcTitleHasSave();
        if (key == "main") RefreshAccountLabel();
        // Telas vindas de prefab mantem o visual do Editor (WYSIWYG), a menos que UITheme.polishPrefabButtons esteja ligado.
        bool usesEditablePrefabLayout = prefabScreens.Contains(key) || UILayoutPrefabApplier.HasLayout(key);
        if (screens.TryGetValue(key, out var visibleScreen) && visibleScreen &&
            (!usesEditablePrefabLayout || UITheme.Current.polishPrefabButtons))
            PolishScreenButtons(visibleScreen);
        if (toast) toast.transform.SetAsLastSibling();
    }

    // Consistent hover/press feedback across menu, map, inventory and skill screens.
    void PolishScreenButtons(RectTransform screen)
    {
        if (!screen) return;
        var buttons = screen.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (!button || button.name == "MapNicknameHitArea") continue;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock cb = button.colors;
            Color normal = cb.normalColor;
            cb.highlightedColor = new Color(
                Mathf.Lerp(normal.r, UIFactory.Gold.r, 0.14f),
                Mathf.Lerp(normal.g, UIFactory.Gold.g, 0.14f),
                Mathf.Lerp(normal.b, UIFactory.Gold.b, 0.14f), normal.a);
            cb.pressedColor = new Color(normal.r * 0.72f, normal.g * 0.72f, normal.b * 0.72f, normal.a);
            cb.selectedColor = cb.highlightedColor;
            cb.fadeDuration = 0.08f;
            button.colors = cb;
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label && !string.IsNullOrWhiteSpace(label.text) && label.fontSize >= 14f)
                label.fontStyle |= FontStyles.Bold;
        }
    }

    void Toast(string msg)
    {
        toast.text = msg;
        toast.gameObject.SetActive(true);
        if (toastCo != null) StopCoroutine(toastCo);
        toastCo = StartCoroutine(HideToast());
    }

    IEnumerator HideToast()
    {
        yield return new WaitForSecondsRealtime(2.2f);
        toast.gameObject.SetActive(false);
    }

    void Title(RectTransform s, string text, float size = 64)
    {
        var t = UIFactory.Txt(s, text, size, TextAlignmentOptions.Center, Color.white, "Title");
        UIFactory.Place(t.rectTransform, TopC, new Vector2(0, -30), new Vector2(1500, 90));
    }

    Button BackButton(RectTransform s, string label, System.Action onClick, float x = 0, string name = "Button")
    {
        var b = UIFactory.Btn(s, label, 30, onClick, null, name);
        UIFactory.Place((RectTransform)b.transform, BotC, new Vector2(x, 20), new Vector2(360, 70));
        return b;
    }

    void AddCinematicBackground(RectTransform screen, float shade = 0.62f)
    {
        var art = UIFactory.Img(screen, Color.white, "CinematicBackground");
        art.sprite = ArtLoader.Get(SceneBackgrounds.DefaultPath);
        art.preserveAspect = false;
        UIFactory.Stretch(art.rectTransform);
        art.raycastTarget = false;
        art.color = new Color(0.84f, 0.88f, 0.92f, 1f);
        art.transform.SetAsFirstSibling();

        var dim = UIFactory.Img(screen, new Color(0.006f, 0.012f, 0.018f, shade), "CinematicShade");
        UIFactory.Stretch(dim.rectTransform);
        dim.raycastTarget = false;
        dim.transform.SetSiblingIndex(1);

        var vignetteTop = UIFactory.Img(screen, new Color(0.005f, 0.009f, 0.014f, 0.66f), "CinematicTopShade");
        UIFactory.Stretch(vignetteTop.rectTransform, 0f, 0.72f, 1f, 1f);
        vignetteTop.raycastTarget = false;
        var vignetteBottom = UIFactory.Img(screen, new Color(0.005f, 0.009f, 0.014f, 0.70f), "CinematicBottomShade");
        UIFactory.Stretch(vignetteBottom.rectTransform, 0f, 0f, 1f, 0.23f);
        vignetteBottom.raycastTarget = false;
    }

    // ---------- menu principal ----------
    void BuildMain()
    {
        BuildPcReferenceTitle();
    }

    static void AddTextShadow(GameObject go)
    {
        var sh = go.GetComponent<Shadow>();
        if (sh == null) sh = go.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.95f);
        sh.effectDistance = new Vector2(2f, -2f);
    }

    /// <summary>Botao horizontal da barra do menu principal (texto centralizado, filete de destaque embaixo).</summary>
    Button AddBarButton(RectTransform parent, string label, System.Action a, Color c, float width, string name = "Button")
    {
        var b = UIFactory.Btn(parent, label, 26, a, c, name);
        UIFactory.Elem(b.gameObject, width, 64f);
        var outline = b.gameObject.GetComponent<Outline>();
        if (!outline) outline = b.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.20f, 0.24f, 0.29f, 0.85f);
        outline.effectDistance = new Vector2(1f, -1f);
        bool primary = c.r > 0.4f && c.g < 0.2f;
        var stripe = UIFactory.Img(b.transform, primary ? UIKit.Col.Gold : UIKit.Col.BorderHi, "MenuStripe");
        UIFactory.Stretch(stripe.rectTransform, 0f, 0f, 1f, 0f);
        stripe.rectTransform.offsetMax = new Vector2(0f, 4f);
        stripe.raycastTarget = false;
        var t = b.GetComponentInChildren<TMP_Text>(true);
        if (t)
        {
            t.alignment = TextAlignmentOptions.Center;
            t.margin = new Vector4(10f, 2f, 10f, 4f);
            t.fontStyle = FontStyles.Bold;
            t.characterSpacing = 0.8f;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.enableAutoSizing = true; t.fontSizeMin = 18f; t.fontSizeMax = 26f;
        }
        return b;
    }

    Button AddMenuButton(RectTransform parent, string label, System.Action a, Color? c = null)
    {
        var b = UIFactory.Btn(parent, label, 34, a, c);
        UIFactory.Elem(b.gameObject, 438, 62);
        // CORREÇÃO: antes a cor era aplicada no Image E no ColorBlock do Button, que se
        // MULTIPLICAM na renderização (botões escuros ficavam ainda mais escos). Agora a
        // imagem fica branca e a cor vive só no ColorBlock (transições de hover/press).
        var outline = b.gameObject.GetComponent<Outline>();
        if (!outline) outline = b.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.20f, 0.24f, 0.29f, 0.85f);
        outline.effectDistance = new Vector2(1f, -1f);
        bool primary = c.HasValue && c.Value.r > 0.4f;
        var stripe = UIFactory.Img(b.transform, primary ? UIKit.Col.Gold : UIKit.Col.BorderHi, "MenuStripe");
        UIFactory.Stretch(stripe.rectTransform, 0f, 0f, 0f, 1f);
        stripe.rectTransform.offsetMax = new Vector2(5f, 0f);
        stripe.raycastTarget = false;
        var labelText = b.GetComponentInChildren<TMP_Text>(true);
        if (labelText) { labelText.alignment = TextAlignmentOptions.MidlineLeft; labelText.margin = new Vector4(28f, 3f, 12f, 3f); labelText.fontStyle = FontStyles.Bold; labelText.characterSpacing = 0.8f; }
        return b;
    }

    void OnNewGame()
    {
        if (GameDatabase.Survivors == null || GameDatabase.Survivors.Count == 0)
        {
            Toast("Não foi possível carregar as classes de sobrevivente.");
            return;
        }
        newGameMode = true;
        selected = GameDatabase.Survivors[0];
        // Com conta, o nick ja foi registrado no login: vai direto para a escolha de classe.
        if (ZombitCloud.IsReady && ZombitCloud.HasNick)
        {
            ApplyAccountNickPrefs();
            ShowSelect();
            return;
        }
        OpenNicknameForNewGame();
    }

    void OnContinue()
    {
        var d = SaveSystem.LoadMostRecent();
        if (d == null) { Toast("Nenhum jogo salvo."); return; }
        UseSave(d);
        ShowHome();
    }

    void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
