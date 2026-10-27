using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Tela inicial do jogo (hub): arte da fogueira com os sobreviventes, barra superior, icones de menu e botao CAMPANHA.
// Parte de MainMenuController. Substitui "abrir direto no mapa": o mapa agora abre pelo botao CAMPANHA.
//
// Icones: coloque PNGs em Assets/_Project/Resources/Art/Home/ com os nomes abaixo para trocar os genericos:
//   icon_missao, icon_presentes, icon_eventos, icon_loja, icon_sobreviventes, icon_habilidades, icon_mochila,
//   icon_formacao, icon_campanha, icon_config
// Sem PNG, usa um icone parecido de Resources/Art/Skills e, se nem esse existir, a inicial do nome.
public partial class MainMenuController
{
    bool homeOrigin;                 // true = a tela utilitaria (loja, mochila...) foi aberta pela tela inicial
    string homeFocusId;              // sobrevivente destacado (1 toque)
    string homeLastTapId;
    float homeLastTapTime;
    RectTransform homeArtRect;
    readonly Dictionary<string, RectTransform> homeSeatRects = new Dictionary<string, RectTransform>();
    readonly Dictionary<string, Image> homePlates = new Dictionary<string, Image>();
    readonly Dictionary<string, Outline> homePlateOutlines = new Dictionary<string, Outline>();

    const float HomeDoubleTapSeconds = 0.5f;
    static readonly Vector2 HomeSeatHalfSize = new Vector2(0.032f, 0.11f);   // metade da area clicavel (fracao da arte)

    // Posicao inicial de cada sobrevivente na arte (0..1, origem embaixo a esquerda). AJUSTE com F9 (arraste).
    static Vector2 HomeSeatDefault(string id)
    {
        switch (id)
        {
            case "assaulter": return new Vector2(0.298f, 0.335f);
            case "sniper": return new Vector2(0.375f, 0.335f);
            case "medic": return new Vector2(0.459f, 0.335f);
            case "engineer": return new Vector2(0.570f, 0.335f);
            case "explorer": return new Vector2(0.641f, 0.335f);
            default: return new Vector2(0.5f, 0.4f);
        }
    }

    static Vector2 HomeSeatPos(string id)
    {
        string raw = PlayerPrefs.GetString("zombit_seat_camp_v2_" + id, "");
        if (!string.IsNullOrEmpty(raw))
        {
            var p = raw.Split(';');
            if (p.Length == 2 && float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                return new Vector2(Mathf.Clamp01(x), Mathf.Clamp01(y));
        }
        return HomeSeatDefault(id);
    }

    static void HomeSaveSeatPos(string id, Vector2 pos)
    {
        PlayerPrefs.SetString("zombit_seat_camp_v2_" + id, pos.x.ToString("0.####", CultureInfo.InvariantCulture) + ";" + pos.y.ToString("0.####", CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
        Debug.Log("Zombit: posicao de '" + id + "' = new Vector2(" + pos.x.ToString("0.###", CultureInfo.InvariantCulture) + "f, " + pos.y.ToString("0.###", CultureInfo.InvariantCulture) + "f)");
    }

    // ---------- navegacao ----------
    /// <summary>Volta de uma tela utilitaria (loja, mochila, habilidades...) para a tela inicial ou para o mapa.</summary>
    void ReturnFromUtility()
    {
        if (homeOrigin) { homeOrigin = false; ShowHome(); }
        else ShowMap();
    }

    void OpenFromHome(System.Action open)
    {
        ZombitAudio.PlayUI();
        homeOrigin = true;
        open();
    }

    // ---------- helpers ----------
    static Sprite HomeSprite(string key, string fallbackSkillIcon)
    {
        Sprite sp = ArtLoader.Get("Art/Home/" + key);
        if (sp == null && !string.IsNullOrEmpty(fallbackSkillIcon)) sp = ArtLoader.Get("Art/Skills/icon_" + fallbackSkillIcon);
        return sp;
    }

    static int HomeLevel(SaveData save, string id)
    {
        if (save != null && save.progress != null)
            foreach (var p in save.progress) if (p != null && p.id == id) return Mathf.Max(1, p.level);
        return 1;
    }

    static bool HomeAnySkillPoints(SaveData save)
    {
        if (save == null || save.progress == null) return false;
        foreach (var p in save.progress) if (p != null && p.skillPoints > 0) return true;
        return false;
    }

    LocationDef HomeNextLocation(SaveData save)
    {
        foreach (var l in GameDatabase.Locations)
            if (GameDatabase.IsUnlocked(l, save) && save.HighestCompleted(l.id) < (int)Difficulty.Normal) return l;
        return GameDatabase.Locations[GameDatabase.Locations.Count - 1];
    }

    // ---------- tela ----------
    void ShowHome()
    {
        var save = GameSession.Save;
        if (save == null) { Show("main"); return; }
        save.MigrateFromPreviousVersions();
        homeOrigin = false;
        homeFocusId = null;
        homeLastTapId = null;
        homeSeatRects.Clear(); homePlates.Clear(); homePlateOutlines.Clear();
        HomeSeatDrag.Calibrating = false;

        var surv = GameDatabase.GetSurvivor(save.selectedSurvivor);
        var s = Rebuild("home");

        // Arte da fogueira ("cover") com brilho e brasas, igual ao menu principal.
        Sprite art = ArtLoader.Get("Art/Home/camp_home");
        homeArtRect = null;
        if (art != null)
        {
            var artImg = UIFactory.Img(s, Color.white, "HomeArt");
            artImg.sprite = art;
            artImg.preserveAspect = false;
            artImg.raycastTarget = false;
            homeArtRect = artImg.rectTransform;
            UIFactory.Stretch(homeArtRect);
            var fit = artImg.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = art.rect.width / art.rect.height;
            if (!editorUiBaking) { var ambience = MenuAmbience.Attach(homeArtRect); ambience.breathZoom = 0f; }
        }
        else AddCinematicBackground(s, 0.45f);

        // Sombras no topo e na base para os botoes ficarem legiveis.
        var topShade = UIFactory.Img(s, new Color(0.004f, 0.007f, 0.011f, 0.70f), "HomeTopShade");
        UIFactory.Stretch(topShade.rectTransform, 0f, 0.86f, 1f, 1f);
        topShade.raycastTarget = false;
        var bottomShade = UIFactory.Img(s, new Color(0.004f, 0.007f, 0.011f, 0.78f), "HomeBottomShade");
        UIFactory.Stretch(bottomShade.rectTransform, 0f, 0f, 1f, 0.22f);
        bottomShade.raycastTarget = false;

        if (homeArtRect != null)
            foreach (var def in GameDatabase.Survivors) BuildHomeSeat(homeArtRect, def, save);

        BuildHomeTopBar(s, save, surv);
        BuildHomeLeftColumn(s);
        BuildHomeBottomBar(s, save);
        // Removido: BuildHomeNextMission(s, save);
        BuildHomeCampaignButton(s, save);

        // Aviso do modo calibrar (F9).
        var notice = UIFactory.Txt(s, "MODO CALIBRAR (F9)  —  arraste cada sobrevivente para cima do personagem dele na arte",
            22, TextAlignmentOptions.Center, new Color(1f, 0.9f, 0.3f), "CalibrateNotice");
        UIFactory.Place(notice.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(1400f, 40f));
        notice.fontStyle = FontStyles.Bold;
        notice.raycastTarget = false;
        AddTextShadow(notice.gameObject);
        notice.gameObject.SetActive(false);
        var key = s.gameObject.AddComponent<HomeCalibrationKey>();
        key.notice = notice.gameObject;

        Show("home");
        RefreshHomeSeatStyles();
    }

    // ---------- barra superior ----------
    void BuildHomeTopBar(RectTransform s, SaveData save, SurvivorDef surv)
    {
        var prog = save.GetProgress(surv.id);
        int power = PowerRating.Of(save, surv.id);

        // Perfil: retrato do principal + nick + nivel e poder + barra de XP.
        var card = UIKit.Frame(s, "HomeProfile", new Color(0.02f, 0.03f, 0.04f, 0.86f), UIKit.Col.Border, 2f);
        UIFactory.Place(card, new Vector2(0f, 1f), new Vector2(24f, -20f), new Vector2(560f, 98f));
        var frame = UIKit.Frame(card, "HomeAvatar", new Color(0.05f, 0.07f, 0.09f, 1f), UIKit.Col.Gold, 2f);
        HubTL(frame, 10f, 10f, 78f, 78f);
        var sp = HubPortrait(surv);
        var pic = UIFactory.Img(frame, sp != null ? Color.white : surv.color, "Portrait");
        if (sp != null) pic.sprite = sp;
        pic.preserveAspect = true; pic.raycastTarget = false;
        UIFactory.Inset(UIFactory.Stretch(pic.rectTransform), 3f, 3f);

        var nick = UIFactory.Txt(card, TruncateNick(GetPlayerNickname(save), 16).ToUpperInvariant(), 30, TextAlignmentOptions.MidlineLeft, UIKit.Col.Gold, "HomeNick");
        HubTL(nick.rectTransform, 100f, 8f, 440f, 38f);
        nick.fontStyle = FontStyles.Bold; nick.textWrappingMode = TextWrappingModes.NoWrap;
        nick.enableAutoSizing = true; nick.fontSizeMin = 16f; nick.fontSizeMax = 30f; nick.raycastTarget = false;
        var info = UIFactory.Txt(card, surv.name.ToUpperInvariant() + "   ·   NV. " + prog.level + "   ·   PODER " + power, 18,
            TextAlignmentOptions.MidlineLeft, new Color(0.84f, 0.89f, 0.91f), "HomeInfo");
        HubTL(info.rectTransform, 100f, 46f, 440f, 26f);
        info.textWrappingMode = TextWrappingModes.NoWrap; info.enableAutoSizing = true; info.fontSizeMin = 11f; info.fontSizeMax = 18f; info.raycastTarget = false;
        int need = Mathf.Max(1, PlayerStats.XpForLevel(prog.level));
        var bar = UIKit.Bar(card, UIKit.Col.Cyan, Mathf.Clamp01((float)prog.xp / need), "HomeXpBar");
        HubTL(bar.transform.parent as RectTransform, 100f, 78f, 440f, 8f);

        // Moedas + engrenagem (opcoes) + sair para o menu de campanhas.
        var row = UIFactory.Node("HomeCurrencies", s);
        UIFactory.Place(row, new Vector2(1f, 1f), new Vector2(-190f, -26f), new Vector2(470f, 64f));
        var h = UIFactory.HLayout(row, 12f, TextAnchor.MiddleRight);
        h.childForceExpandWidth = false; h.childForceExpandHeight = false;
        h.childControlWidth = true; h.childControlHeight = true;
        var gold = HubPill(row, "GoldPill", 220f, 60f);
        UIKit.Diamond(gold, UIKit.Col.Gold, 22f, "GoldIcon").rectTransform.anchoredPosition = new Vector2(-70f, 0f);
        HubPillValue(gold, save.gold.ToString("N0"), UIKit.Col.Gold);
        var scrap = HubPill(row, "ScrapPill", 220f, 60f);
        HubGear(scrap, new Vector2(-70f, 0f), new Color(0.62f, 0.72f, 0.80f, 1f));
        HubPillValue(scrap, save.scrap.ToString("N0"), new Color(0.80f, 0.88f, 0.94f, 1f));

        BuildHomeCornerButton(s, "HomeSettings", "icon_config", "gear", "OPÇÕES", new Vector2(-24f, -22f), () => { ZombitAudio.PlayUI(); RefreshOptions(); Show("options"); });
        BuildHomeCornerButton(s, "HomeMenu", "icon_menu", "clock", "MENU", new Vector2(-110f, -22f), () => { ZombitAudio.PlayUI(); GoToTitleScreenKeepingHome(); });
    }

    void GoToTitleScreenKeepingHome()
    {
        if (GameSession.Save != null && !GameSession.DebugRun) SaveSystem.Save(GameSession.Save);
        Show("main");
    }

    void BuildHomeCornerButton(RectTransform s, string name, string iconKey, string fallback, string label, Vector2 pos, System.Action action)
    {
        var b = UIKit.Button(s, "", action, UIKit.BtnStyle.Secondary, 12, name);
        var rt = (RectTransform)b.transform;
        UIFactory.Place(rt, new Vector2(1f, 1f), pos, new Vector2(72f, 72f));
        UIFactory.SetButtonColor(b, new Color(0.08f, 0.10f, 0.13f, 0.95f));
        var t = b.GetComponentInChildren<TMP_Text>(true);
        var icon = HomeSprite(iconKey, fallback);
        if (t) t.text = icon == null ? label.Substring(0, 1) : "";
        if (icon != null)
        {
            var img = UIFactory.Img(b.transform, Color.white, "Icon");
            img.sprite = icon; img.preserveAspect = true; img.raycastTarget = false;
            UIFactory.Inset(UIFactory.Stretch(img.rectTransform), 12f, 12f);
        }
    }

    // ---------- coluna esquerda: missoes, presentes, eventos (ainda sem sistema) ----------
    void BuildHomeLeftColumn(RectTransform s)
    {
        var col = UIFactory.Node("HomeLeftColumn", s);
        UIFactory.Place(col, new Vector2(0f, 0.5f), new Vector2(24f, 90f), new Vector2(150f, 3f * 178f));
        var v = UIFactory.VLayout(col, 4f, TextAnchor.UpperLeft);
        v.childForceExpandWidth = false; v.childForceExpandHeight = false;
        v.childControlWidth = true; v.childControlHeight = true;
        HomeIcon(col, "Missao", "MISSÃO DIÁRIA", "icon_missao", "clock", () => Toast("Missões diárias: em breve."), false);
        HomeIcon(col, "Presentes", "PRESENTES", "icon_presentes", "backpack", () => Toast("Presentes: em breve."), false);
        HomeIcon(col, "Eventos", "EVENTOS", "icon_eventos", "star_up", () => Toast("Eventos: em breve."), false);
    }

    // ---------- barra de baixo: icones do jogo ----------
    void BuildHomeBottomBar(RectTransform s, SaveData save)
    {
        var bar = UIFactory.Node("HomeBottomBar", s);
        UIFactory.Place(bar, new Vector2(0f, 0f), new Vector2(24f, 16f), new Vector2(1220f, 178f));
        var h = UIFactory.HLayout(bar, 14f, TextAnchor.LowerLeft);
        h.childForceExpandWidth = false; h.childForceExpandHeight = false;
        h.childControlWidth = true; h.childControlHeight = true;

        HomeIcon(bar, "Loja", "LOJA", "icon_loja", "coins", () => OpenFromHome(ShowShop), false);
        HomeIcon(bar, "Sobreviventes", "SOBREVIVENTES", "icon_sobreviventes", "heart", () => OpenFromHome(() => { newGameMode = false; ShowSelect(); }), false);
        HomeIcon(bar, "Habilidades", "HABILIDADES", "icon_habilidades", "star_up", () => OpenFromHome(() => ShowMapSkills(null)), HomeAnySkillPoints(save));
        HomeIcon(bar, "Mochila", "MOCHILA", "icon_mochila", "backpack", () => OpenFromHome(() => ShowMapInventory(null, true)), false);


        HomeIcon(bar, "Treinamento", "TREINAMENTO", "icon_treinamento", "crosshair", OpenRangePicker, false);
    }

    RectTransform HomeIcon(Transform parent, string name, string label, string iconKey, string fallback, System.Action action, bool badge)
    {
        var cell = UIFactory.Node("HomeIcon_" + name, parent);
        UIFactory.Elem(cell.gameObject, 150f, 178f);

        var btn = UIKit.Button(cell, "", () => { ZombitAudio.PlayUI(); action(); }, UIKit.BtnStyle.Secondary, 12, "Btn_" + name);
        var brt = (RectTransform)btn.transform;
        UIFactory.Place(brt, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(126f, 126f));
        UIFactory.SetButtonColor(btn, new Color(0.08f, 0.10f, 0.13f, 0.95f));
        var t = btn.GetComponentInChildren<TMP_Text>(true);
        var icon = HomeSprite(iconKey, fallback);
        if (icon != null)
        {
            if (t) t.text = "";
            var img = UIFactory.Img(btn.transform, Color.white, "Icon");
            img.sprite = icon; img.preserveAspect = true; img.raycastTarget = false;
            UIFactory.Inset(UIFactory.Stretch(img.rectTransform), 20f, 20f);
        }
        else if (t)
        {
            t.text = label.Substring(0, 1);   // icone generico: a inicial (troque pelo PNG em Resources/Art/Home)
            t.fontSize = 56f; t.fontStyle = FontStyles.Bold; t.color = UIKit.Col.Gold;
        }

        var lb = UIFactory.Txt(cell, label, 18, TextAlignmentOptions.Center, Color.white, "Label");
        UIFactory.Place(lb.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(150f, 40f));
        lb.fontStyle = FontStyles.Bold; lb.textWrappingMode = TextWrappingModes.NoWrap; lb.raycastTarget = false;
        lb.enableAutoSizing = true; lb.fontSizeMin = 11f; lb.fontSizeMax = 18f;
        AddTextShadow(lb.gameObject);

        if (badge)
        {
            var dot = UIFactory.Img(btn.transform, new Color(0.9f, 0.12f, 0.14f, 1f), "Badge");
            UIKit.ApplyCircle(dot, new Color(0.9f, 0.12f, 0.14f, 1f));
            dot.raycastTarget = false;
            UIFactory.Place(dot.rectTransform, new Vector2(1f, 1f), new Vector2(6f, 6f), new Vector2(34f, 34f));
            var dt = UIFactory.Txt(dot.transform, "!", 22f, TextAlignmentOptions.Center, Color.white, "BadgeText");
            dt.fontStyle = FontStyles.Bold; dt.raycastTarget = false;
            UIFactory.Stretch(dt.rectTransform);
        }
        return cell;
    }

    // ---------- proxima missao + botao CAMPANHA ----------
    void BuildHomeNextMission(RectTransform s, SaveData save)
    {
        var loc = HomeNextLocation(save);
        var prog = save.GetProgress(save.selectedSurvivor);
        var panel = UIKit.Frame(s, "HomeNextMission", new Color(0.02f, 0.03f, 0.04f, 0.86f), UIKit.Col.Border, 2f);
        UIFactory.Place(panel, new Vector2(1f, 1f), new Vector2(-24f, -128f), new Vector2(430f, 132f));
        MapText(panel, "PRÓXIMA MISSÃO", 14, TextAlignmentOptions.MidlineLeft, HubDim, 16, 8, 380, 22, "NextCaption").fontStyle = FontStyles.Bold;
        var n = MapText(panel, loc.name.ToUpperInvariant(), 28, TextAlignmentOptions.MidlineLeft, Color.white, 16, 32, 400, 40, "NextName");
        n.fontStyle = FontStyles.Bold; n.enableAutoSizing = true; n.fontSizeMin = 16f; n.fontSizeMax = 28f;
        string lvlColor = prog.level >= loc.recommendedLevel ? "#6CFF8A" : prog.level >= loc.recommendedLevel - 2 ? "#FFD84A" : "#FF5555";
        var l = MapText(panel, "Nível recomendado: <b><color=" + lvlColor + ">" + loc.recommendedLevel + "</color></b>   ·   Concluídas: " + save.CompletedLocationCount() + "/" + GameDatabase.Locations.Count,
            16, TextAlignmentOptions.MidlineLeft, new Color(0.84f, 0.89f, 0.91f), 16, 80, 400, 30, "NextInfo");
        l.richText = true; l.enableAutoSizing = true; l.fontSizeMin = 11f; l.fontSizeMax = 16f;
    }

    void BuildHomeCampaignButton(RectTransform s, SaveData save)
    {
        var b = UIKit.Button(s, "CAMPANHA", () => { ZombitAudio.PlayUI(); homeOrigin = false; ShowMap(); }, UIKit.BtnStyle.Primary, 40, "HomeCampaign");
        var rt = (RectTransform)b.transform;
        UIFactory.Place(rt, new Vector2(1f, 0f), new Vector2(-24f, 20f), new Vector2(260f, 190f));
        UIFactory.SetButtonColor(b, new Color(0.46f, 0.07f, 0.09f, 1f));
        var o = b.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(1f, 0.76f, 0.29f, 0.95f); o.effectDistance = new Vector2(2f, -2f);
        var t = b.GetComponentInChildren<TMP_Text>(true);
        if (t)
        {
            t.alignment = TextAlignmentOptions.Bottom;
            t.margin = new Vector4(0f, 0f, 0f, 22f);
            t.fontStyle = FontStyles.Bold; t.characterSpacing = 3f;
            t.enableAutoSizing = true; t.fontSizeMin = 24f; t.fontSizeMax = 44f;
        }
        var icon = HomeSprite("icon_campanha", "scope");
        if (icon != null)
        {
            var img = UIFactory.Img(b.transform, Color.white, "Icon");
            img.sprite = icon; img.preserveAspect = true; img.raycastTarget = false;
            UIFactory.Place(img.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(110f, 110f));
        }
        else
        {
            var g = UIFactory.Txt(b.transform, "MAPA", 34, TextAlignmentOptions.Center, UIKit.Col.Gold, "MapGlyph");
            UIFactory.Place(g.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(300f, 60f));
            g.fontStyle = FontStyles.Bold; g.raycastTarget = false;
        }
    }

    // ---------- sobreviventes na fogueira ----------
    void SetHomeSeatRect(RectTransform rt, Vector2 c)
    {
        rt.anchorMin = c - HomeSeatHalfSize;
        rt.anchorMax = c + HomeSeatHalfSize;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    void BuildHomeSeat(RectTransform artRect, SurvivorDef def, SaveData save)
    {
        string id = def.id;
        var region = UIFactory.Node("Seat_" + id, artRect);
        SetHomeSeatRect(region, HomeSeatPos(id));
        homeSeatRects[id] = region;

        var hit = region.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        hit.raycastTarget = true;
        var btn = region.gameObject.AddComponent<Button>();
        btn.targetGraphic = hit; btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => OnHomeSeatTap(id));

        var drag = region.gameObject.AddComponent<HomeSeatDrag>();
        drag.art = artRect; drag.survivorId = id; drag.hit = hit;
        drag.onMoved = (sid, n) => { if (homeSeatRects.TryGetValue(sid, out var r) && r) SetHomeSeatRect(r, n); };
        drag.onDropped = sid => { if (homeSeatRects.TryGetValue(sid, out var r) && r) HomeSaveSeatPos(sid, (r.anchorMin + r.anchorMax) * 0.5f); };

        // Placa com nome, nivel e poder, logo abaixo do personagem.
        int lvl = HomeLevel(save, id);
        int power = PowerRating.Of(save, id);
        var plate = UIFactory.Img(region, new Color(0.02f, 0.03f, 0.04f, 0.84f), "Plate");
        var prt = plate.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 1f);
        prt.pivot = new Vector2(0.5f, 0f);
        prt.anchoredPosition = new Vector2(0f, 12f);
        prt.sizeDelta = new Vector2(138f, 74f);
        plate.raycastTarget = false;
        var outline = plate.gameObject.AddComponent<Outline>();
        outline.effectDistance = new Vector2(2f, -2f);
        homePlates[id] = plate; homePlateOutlines[id] = outline;

        var nm = MapText(plate.transform, def.name.ToUpperInvariant(), 22, TextAlignmentOptions.MidlineLeft, Color.white, 12, 6, 118, 30, "SeatName");
        nm.fontStyle = FontStyles.Bold; nm.enableAutoSizing = true; nm.fontSizeMin = 12f; nm.fontSizeMax = 22f; nm.textWrappingMode = TextWrappingModes.NoWrap;
        var st = MapText(plate.transform, "NV. " + lvl + "  |  <color=#FFC04A>P " + power + "</color>", 17, TextAlignmentOptions.MidlineLeft, new Color(0.84f, 0.89f, 0.91f), 12, 38, 118, 28, "SeatStats");
        st.richText = true; st.fontStyle = FontStyles.Bold; st.enableAutoSizing = true; st.fontSizeMin = 11f; st.fontSizeMax = 17f; st.textWrappingMode = TextWrappingModes.NoWrap;

        string role = save.selectedSurvivor == id ? "PRINCIPAL" : save.selectedPartner == id ? "APOIO 1" : save.selectedThird == id ? "APOIO 2" : null;
        if (role != null)
        {
            var tag = UIFactory.Img(region, save.selectedSurvivor == id ? new Color(1f, 0.70f, 0.14f, 1f) : new Color(0.18f, 0.55f, 0.72f, 1f), "RoleTag");
            var trt = tag.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0f);
            trt.pivot = new Vector2(0.5f, 0f);
            trt.anchoredPosition = new Vector2(0f, 6f);
            trt.sizeDelta = new Vector2(140f, 28f);
            tag.raycastTarget = false;
            var rt2 = UIFactory.Txt(tag.transform, role, 15, TextAlignmentOptions.Center, new Color(0.10f, 0.06f, 0.02f), "RoleText");
            rt2.fontStyle = FontStyles.Bold; rt2.raycastTarget = false;
            UIFactory.Stretch(rt2.rectTransform);
        }
    }

    void RefreshHomeSeatStyles()
    {
        var save = GameSession.Save;
        if (save == null) return;
        foreach (var kv in homePlates)
        {
            if (!kv.Value) continue;
            bool primary = save.selectedSurvivor == kv.Key;
            bool focus = homeFocusId == kv.Key;
            kv.Value.color = focus ? new Color(0.10f, 0.12f, 0.06f, 0.94f) : new Color(0.02f, 0.03f, 0.04f, 0.84f);
            if (homePlateOutlines.TryGetValue(kv.Key, out var o) && o)
                o.effectColor = focus ? Color.white : primary ? new Color(1f, 0.76f, 0.29f, 1f) : new Color(0.20f, 0.27f, 0.31f, 1f);
            if (homeSeatRects.TryGetValue(kv.Key, out var r) && r) r.localScale = focus ? new Vector3(1.04f, 1.04f, 1f) : Vector3.one;
        }
    }

    void OnHomeSeatTap(string id)
    {
        if (HomeSeatDrag.Calibrating) return;
        var save = GameSession.Save;
        if (save == null) return;
        var def = GameDatabase.GetSurvivor(id);
        float now = Time.unscaledTime;

        // 2 toques seguidos no mesmo sobrevivente = escolher como principal.
        if (homeLastTapId == id && now - homeLastTapTime <= HomeDoubleTapSeconds)
        {
            homeLastTapId = null;
            SelectHomeSurvivor(id);
            return;
        }
        homeLastTapId = id; homeLastTapTime = now; homeFocusId = id;
        RefreshHomeSeatStyles();
        Toast(def.name + "  ·  NV. " + HomeLevel(save, id) + "  ·  PODER " + PowerRating.Of(save, id) + "   (toque 2x para escolher)");
    }

    void SelectHomeSurvivor(string id)
    {
        var save = GameSession.Save;
        if (save == null) return;
        var def = GameDatabase.GetSurvivor(id);
        if (save.selectedSurvivor == id) { Toast(def.name + " já é o sobrevivente principal."); homeFocusId = null; RefreshHomeSeatStyles(); return; }
        save.SetPrimarySurvivor(id);   // se estava como apoio, troca de lugar com o principal anterior
        save.GetProgress(id);
        if (!GameSession.DebugRun)
        {
            try { SaveSystem.Save(save); }
            catch (System.Exception e) { Debug.LogWarning("Zombit: falha ao salvar a escolha do sobrevivente: " + e.Message); }
        }
        ShowHome();
        Toast(def.name + " é o novo sobrevivente principal.");
    }
}
