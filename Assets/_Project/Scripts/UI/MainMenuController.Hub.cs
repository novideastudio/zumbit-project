using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Hub do mapa no novo layout (parte de MainMenuController):
//   barra lateral (perfil + menu + ouro/sucata) | mapa com caminhos | painel da missao | barra inferior (chat + equipe)
// Tudo e posicionado por ancoras fracionarias / layout groups, entao acompanha a resolucao.
public partial class MainMenuController
{
    // ---- estado do painel de missao ----
    TMP_Text hubBadge, hubStatA, hubStatB, hubStatC, hubStatD, hubRecLevel;
    RectTransform hubEnemyRow;
    AspectRatioFitter hubHeroFitter;
    readonly Dictionary<string, GameObject> nodePins = new Dictionary<string, GameObject>();
    readonly Dictionary<string, Color> nodeBorders = new Dictionary<string, Color>();
    readonly Dictionary<string, Roster> hubRosters = new Dictionary<string, Roster>();

    static readonly Color HubDim = new Color(0.60f, 0.65f, 0.70f, 1f);
    static readonly Color HubTileFill = new Color(0.035f, 0.05f, 0.062f, 1f);

    // ================= helpers =================

    /// <summary>Faixa horizontal presa ao topo do pai: y/h em pixels de referencia, largura esticada (com margem pad).</summary>
    RectTransform HubRow(Transform parent, string name, float y, float h, float pad = 20f)
    {
        var rt = UIFactory.Node(name, parent);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(pad, -(y + h));
        rt.offsetMax = new Vector2(-pad, -y);
        return rt;
    }

    static RectTransform HubTL(RectTransform rt, float x, float y, float w, float h)
        => UIFactory.Place(rt, new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(w, h));

    TMP_Text HubLabel(Transform parent, string text, float y)
    {
        var row = HubRow(parent, "Eyebrow_" + text, y, 20f);
        var t = UIFactory.Txt(row, text, 12, TextAlignmentOptions.MidlineLeft, HubDim, "Eyebrow");
        UIFactory.Stretch(t.rectTransform);
        t.fontStyle = FontStyles.Bold;
        t.characterSpacing = 1.4f;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }

    HorizontalLayoutGroup HubTileRow(RectTransform row, float spacing)
    {
        var h = UIFactory.HLayout(row, spacing, TextAnchor.MiddleCenter);
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = true;
        return h;
    }

    RectTransform HubTile(Transform parent, string name, Sprite sprite, Color border, Color tint, float inset = 5f)
    {
        var tile = UIKit.Frame(parent, name, HubTileFill, border, 1.5f);
        var le = tile.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 40f;
        le.flexibleWidth = 1f;
        le.flexibleHeight = 1f;
        if (sprite != null)
        {
            var icon = UIFactory.Img(tile, tint, "Icon");
            icon.sprite = sprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            UIFactory.Inset(UIFactory.Stretch(icon.rectTransform), inset, inset);
        }
        return tile;
    }

    Sprite HubPortrait(SurvivorDef def)
    {
        if (def == null) return null;
        Sprite sp = def.portrait;
        if (sp == null) sp = ArtLoader.Get("Art/Characters/" + def.id + "_portrait");
        if (sp == null) sp = ArtLoader.Get("Art/Characters/" + def.id + "_card");
        return sp;
    }

    // ================= cabecalho =================

    RectTransform HubPill(Transform parent, string name, float width, float height = 56f)
    {
        var pill = UIKit.Frame(parent, name, new Color(0.06f, 0.09f, 0.11f, 1f), UIKit.Col.Border, 1.5f);
        UIFactory.Elem(pill.gameObject, width, height);
        return pill;
    }

    void HubPillValue(RectTransform pill, string text, Color color)
    {
        var t = UIFactory.Txt(pill, text, UIKit.Size.Title, TextAlignmentOptions.Center, color, "Value");
        t.fontStyle = FontStyles.Bold;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.enableAutoSizing = true;
        t.fontSizeMax = t.fontSize;
        t.fontSizeMin = 14f;
        UIFactory.Stretch(t.rectTransform, 0.3f, 0f, 1f, 1f);
        t.rectTransform.offsetMax = new Vector2(-8f, 0f);
    }

    /// <summary>Engrenagem simples (dois quadrados girados + miolo) para a moeda de sucata.</summary>
    void HubGear(RectTransform parent, Vector2 pos, Color c)
    {
        var holder = UIFactory.Node("GearIcon", parent);
        UIFactory.Place(holder, Mid, pos, new Vector2(26f, 26f));
        var a = UIFactory.Img(holder, c, "GearA");
        UIFactory.Place(a.rectTransform, Mid, Vector2.zero, new Vector2(17f, 17f));
        a.raycastTarget = false;
        var b = UIFactory.Img(holder, c, "GearB");
        UIFactory.Place(b.rectTransform, Mid, Vector2.zero, new Vector2(17f, 17f));
        b.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        b.raycastTarget = false;
        var core = UIFactory.Img(holder, new Color(0.06f, 0.09f, 0.11f, 1f), "GearCore");
        UIKit.ApplyCircle(core, new Color(0.06f, 0.09f, 0.11f, 1f));
        UIFactory.Place(core.rectTransform, Mid, Vector2.zero, new Vector2(9f, 9f));
        core.raycastTarget = false;
    }

    /// <summary>Ouro e sucata no fim da coluna da esquerda (abaixo do botao TELA INICIAL).</summary>
    void BuildHubWallet(RectTransform s, SaveData save)
    {
        var wallet = UIFactory.Node("HubWallet", s);
        UIFactory.Stretch(wallet, 0.006f, 0.215f, 0.098f, 0.318f);
        var v = UIFactory.VLayout(wallet, 8f, TextAnchor.LowerCenter);
        v.childForceExpandWidth = true;

        var gold = HubPill(wallet, "GoldPill", 170f, 48f);
        UIKit.Diamond(gold, UIKit.Col.Gold, 18f, "GoldIcon").rectTransform.anchoredPosition = new Vector2(-57f, 0f);
        HubPillValue(gold, save.gold.ToString("N0"), UIKit.Col.Gold);

        var scrap = HubPill(wallet, "ScrapPill", 170f, 48f);
        HubGear(scrap, new Vector2(-57f, 0f), new Color(0.62f, 0.72f, 0.80f, 1f));
        HubPillValue(scrap, save.scrap.ToString("N0"), new Color(0.80f, 0.88f, 0.94f, 1f));
    }

    // ================= barra lateral =================

    void BuildHubSidebar(RectTransform s, SaveData save, SurvivorDef surv, SurvivorProgress prog)
    {
        var side = UIFactory.Node("HubSidebar", s);
        UIFactory.Stretch(side, 0.006f, 0.33f, 0.098f, 0.985f);
        var v = UIFactory.VLayout(side, 8f, TextAnchor.UpperCenter);
        v.childForceExpandWidth = true;

        BuildHubProfile(side, save, surv, prog);   // retrato + nick + nivel + XP, em cima do botao MUNDO
        AddSideButton(side, "MUNDO", NavTab.Map, true);
        AddSideButton(side, "EQUIPE", NavTab.Character, false);
        AddSideButton(side, "INVENTÁRIO", NavTab.Bag, false);
        AddSideButton(side, "HABILIDADES", NavTab.Skills, false);
        AddSideButton(side, "LOJA", NavTab.Shop, false);
        AddRangeSideButton(side);   // ESTANDE (MainMenuController.Range.cs)
        AddHomeSideButton(side);    // volta para a tela inicial (substitui o antigo botao MENU do cabecalho)
    }

    /// <summary>Salva e volta para a tela inicial do jogo.</summary>
    void GoToTitleScreen()
    {
        if (GameSession.Save != null && !GameSession.DebugRun) SaveSystem.Save(GameSession.Save);
        ShowHome();
    }

    void AddHomeSideButton(RectTransform parent)
    {
        var b = UIKit.Button(parent, "TELA INICIAL", GoToTitleScreen, UIKit.BtnStyle.Secondary, 15f, "Side_Home");
        UIFactory.Elem(b.gameObject, 170f, 50f);
        UIFactory.SetButtonColor(b, new Color(0.17f, 0.05f, 0.07f, 0.96f));
        var t = b.GetComponentInChildren<TMP_Text>(true);
        if (t) { t.alignment = TextAlignmentOptions.MidlineLeft; t.margin = new Vector4(22f, 2f, 8f, 2f); }
        var stripe = UIFactory.Img(b.transform, new Color(0.80f, 0.12f, 0.15f, 1f), "SideStripe");
        UIFactory.Stretch(stripe.rectTransform, 0f, 0.12f, 0f, 0.88f);
        stripe.rectTransform.offsetMax = new Vector2(4f, 0f);
        stripe.raycastTarget = false;
    }

    /// <summary>Cartao do jogador no topo da barra lateral: avatar, nick (toque para editar), nivel e barra de XP.</summary>
    void BuildHubProfile(RectTransform parent, SaveData save, SurvivorDef surv, SurvivorProgress prog)
    {
        var card = UIKit.Frame(parent, "HubProfile", new Color(0.05f, 0.07f, 0.09f, 1f), UIKit.Col.Border, 1.5f);
        UIFactory.Elem(card.gameObject, 170f, 92f);

        var frame = UIKit.Frame(card, "HubAvatar", new Color(0.05f, 0.07f, 0.09f, 1f), UIKit.Col.Gold, 2f);
        HubTL(frame, 8f, 8f, 58f, 58f);
        var sp = HubPortrait(surv);
        var pic = UIFactory.Img(frame, sp != null ? Color.white : surv.color, "Portrait");
        if (sp != null) pic.sprite = sp;
        pic.preserveAspect = true;
        pic.raycastTarget = false;
        UIFactory.Inset(UIFactory.Stretch(pic.rectTransform), 3f, 3f);

        var nickValue = UIFactory.Txt(card, TruncateNick(GetPlayerNickname(save), 15).ToUpperInvariant(), UIKit.Size.Title,
            TextAlignmentOptions.MidlineLeft, UIKit.Col.Gold, "MapNicknameValue");
        HubTL(nickValue.rectTransform, 74f, 8f, 90f, 28f);
        nickValue.fontStyle = FontStyles.Bold;
        nickValue.textWrappingMode = TextWrappingModes.NoWrap;
        nickValue.enableAutoSizing = true; nickValue.fontSizeMin = 11f; nickValue.fontSizeMax = 22f;
        nickValue.raycastTarget = false;

        var nickClass = UIFactory.Txt(card, "", UIKit.Size.Caption, TextAlignmentOptions.TopLeft, UIKit.Col.TextDim, "MapNicknameClass");
        HubTL(nickClass.rectTransform, 74f, 38f, 90f, 30f);
        nickClass.textWrappingMode = TextWrappingModes.Normal;
        nickClass.enableAutoSizing = true; nickClass.fontSizeMin = 9f; nickClass.fontSizeMax = 13f;
        nickClass.overflowMode = TextOverflowModes.Truncate;
        nickClass.raycastTarget = false;
        mapNickClass = nickClass;

        int xpNeeded = Mathf.Max(1, (int)PlayerStats.XpForLevel(prog.level));
        var xpBar = UIKit.Bar(card, UIKit.Col.Cyan, (float)prog.xp / xpNeeded, "MapXpBar");
        mapXpFill = xpBar;
        HubTL(xpBar.transform.parent as RectTransform, 8f, 76f, 154f, 6f);
        RefreshMapHeader(save);

        // Area clicavel (invisivel) para editar o nick.
        var hit = UIFactory.Img(card, new Color(0f, 0f, 0f, 0f), "MapNicknameHitArea");
        UIFactory.Stretch(hit.rectTransform);
        hit.raycastTarget = true;
        var hitButton = hit.gameObject.AddComponent<Button>();
        hitButton.targetGraphic = hit;
        hitButton.transition = Selectable.Transition.None;
        hitButton.onClick.AddListener(() => { ZombitAudio.PlayUI(); OpenNicknameEditor(); });
    }

    void AddSideButton(RectTransform parent, string label, NavTab tab, bool active)
    {
        var b = UIKit.Button(parent, label, () => { if (!active) NavGo(tab); },
            active ? UIKit.BtnStyle.Primary : UIKit.BtnStyle.Secondary, 17f, "Side_" + tab);
        UIFactory.Elem(b.gameObject, 170f, 60f);
        UIFactory.SetButtonColor(b, active ? new Color(0.36f, 0.07f, 0.09f, 1f) : new Color(0.075f, 0.10f, 0.13f, 0.96f));
        var t = b.GetComponentInChildren<TMP_Text>(true);
        if (t) { t.alignment = TextAlignmentOptions.MidlineLeft; t.margin = new Vector4(22f, 2f, 8f, 2f); }
        var stripe = UIFactory.Img(b.transform, active ? UIKit.Col.Gold : UIKit.Col.Border, "SideStripe");
        UIFactory.Stretch(stripe.rectTransform, 0f, 0.12f, 0f, 0.88f);
        stripe.rectTransform.offsetMax = new Vector2(4f, 0f);
        stripe.raycastTarget = false;
        if (active)
        {
            var o = b.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(1f, 0.76f, 0.29f, 0.9f);
            o.effectDistance = new Vector2(1.5f, -1.5f);
        }
    }

    // ================= cabecalho da regiao (sobre o mapa) =================

    void BuildHubRegionHeader(RectTransform mapInner, SaveData save)
    {
        var box = UIFactory.Img(mapInner, new Color(0.01f, 0.015f, 0.02f, 0.80f), "RegionBox");
        HubTL(box.rectTransform, 14f, 14f, 340f, 98f);
        box.raycastTarget = false;

        var eyebrow = UIFactory.Txt(box.transform, "REGIÃO ATUAL", 12, TextAlignmentOptions.MidlineLeft, HubDim, "RegionEyebrow");
        HubTL(eyebrow.rectTransform, 16f, 8f, 300f, 18f);
        eyebrow.fontStyle = FontStyles.Bold; eyebrow.characterSpacing = 1.4f;

        var title = UIFactory.Txt(box.transform, "ZONA URBANA", 34, TextAlignmentOptions.MidlineLeft, Color.white, "RegionTitle");
        HubTL(title.rectTransform, 16f, 26f, 320f, 42f);
        title.fontStyle = FontStyles.Bold;
        title.textWrappingMode = TextWrappingModes.NoWrap;

        int total = GameDatabase.Locations.Count;
        int done = save.CompletedLocationCount();
        var star = UIFactory.Img(box.transform, UIKit.Col.Gold, "RegionStar");
        star.sprite = UIKit.Star; star.raycastTarget = false;
        HubTL(star.rectTransform, 16f, 72f, 18f, 18f);
        var count = UIFactory.Txt(box.transform, done + "/" + total, 16, TextAlignmentOptions.MidlineLeft, Color.white, "RegionCount");
        HubTL(count.rectTransform, 40f, 70f, 60f, 22f);
        count.fontStyle = FontStyles.Bold;
        count.textWrappingMode = TextWrappingModes.NoWrap;
        var bar = UIKit.Bar(box.transform, UIKit.Col.Accent, total > 0 ? (float)done / total : 0f, "RegionBar");
        HubTL(bar.transform.parent as RectTransform, 100f, 77f, 220f, 8f);

        // Seletor de capitulo (so um capitulo por enquanto).
        var chap = UIKit.Frame(mapInner, "ChapterBox", new Color(0.04f, 0.055f, 0.07f, 0.92f), UIKit.Col.Border, 1.5f);
        UIFactory.Place(chap, new Vector2(1f, 1f), new Vector2(-14f, -14f), new Vector2(220f, 44f));
        chap.pivot = new Vector2(1f, 1f);
        var ct = UIFactory.Txt(chap, "CAPÍTULO 1", 20, TextAlignmentOptions.Center, Color.white, "ChapterText");
        UIFactory.Stretch(ct.rectTransform);
        ct.fontStyle = FontStyles.Bold; ct.characterSpacing = 1.5f;
        ct.textWrappingMode = TextWrappingModes.NoWrap;
    }

    // ================= caminhos entre locais =================

    void BuildHubPaths(RectTransform layer, SaveData save)
    {
        var go = UIFactory.Node("MapPaths", layer);
        UIFactory.Stretch(go);
        var path = go.gameObject.AddComponent<UIDashedPath>();
        path.raycastTarget = false;
        path.thickness = 4f;
        go.SetAsFirstSibling();

        foreach (var loc in GameDatabase.Locations)
        {
            if (loc.requires == null) continue;
            foreach (var reqId in loc.requires)
            {
                var req = GameDatabase.GetLocation(reqId);
                if (req == null) continue;
                bool reqDone = save.HighestCompleted(req.id) >= (int)Difficulty.Normal;
                bool locDone = save.HighestCompleted(loc.id) >= (int)Difficulty.Normal;
                Color c = reqDone && locDone ? new Color(0.47f, 0.87f, 0.51f, 0.96f)
                        : reqDone ? new Color(0.99f, 0.87f, 0.62f, 0.96f)
                        : new Color(0.94f, 0.84f, 0.64f, 0.84f);
                float bend = ((req.id.GetHashCode() ^ loc.id.GetHashCode()) & 1) == 0 ? 0.08f : -0.08f;
                path.Add(new Vector2(req.mapX, req.mapY), new Vector2(loc.mapX, loc.mapY), c, bend);
            }
        }
    }

    // ================= painel de missao (direita) =================

    void BuildHubMissionPanel(RectTransform s)
    {
        var panel = UIKit.Frame(s, "LocationPanel", new Color(0.018f, 0.027f, 0.036f, 0.98f),
            new Color(0.20f, 0.24f, 0.29f, 0.95f), 2f);
        UIFactory.Stretch(panel, 0.715f, 0.02f, 0.995f, 0.985f);

        // Titulo: [04]  SHOPPING
        var titleRow = HubRow(panel, "TitleBar", 6f, 48f, 6f);
        var titleBg = UIFactory.Img(titleRow, new Color(0.46f, 0.06f, 0.08f, 1f), "TitleBg");
        UIFactory.Stretch(titleBg.rectTransform);
        titleBg.raycastTarget = false;
        var badge = UIKit.Frame(titleRow, "NumBadge", new Color(0.12f, 0.07f, 0.04f, 1f), UIKit.Col.Gold, 2f);
        UIFactory.Place(badge, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(46f, 36f));
        hubBadge = UIFactory.Txt(badge, "00", 20, TextAlignmentOptions.Center, UIKit.Col.Gold, "Num");
        UIFactory.Stretch(hubBadge.rectTransform);
        hubBadge.fontStyle = FontStyles.Bold;
        dName = UIFactory.Txt(titleRow, "", 30, TextAlignmentOptions.MidlineLeft, Color.white, "DName");
        UIFactory.Stretch(dName.rectTransform);
        dName.rectTransform.offsetMin = new Vector2(66f, 0f);
        dName.rectTransform.offsetMax = new Vector2(-8f, 0f);
        dName.fontStyle = FontStyles.Bold;
        dName.textWrappingMode = TextWrappingModes.NoWrap;
        dName.enableAutoSizing = true; dName.fontSizeMin = 18f; dName.fontSizeMax = 30f;

        // Imagem do local (recorte "cover" do cenario de combate).
        var heroRow = HubRow(panel, "Hero", 62f, 200f);
        var heroFrame = UIKit.Frame(heroRow, "HeroFrame", Color.black, UIKit.Col.Border, 2f);
        UIFactory.Stretch(heroFrame);
        var mask = UIFactory.Node("HeroMask", heroFrame);
        UIFactory.Inset(UIFactory.Stretch(mask), 2f, 2f);
        mask.gameObject.AddComponent<RectMask2D>();
        selectedLocationPreview = UIFactory.Img(mask, Color.white, "SelectedLocationPreview");
        UIFactory.Stretch(selectedLocationPreview.rectTransform);
        selectedLocationPreview.preserveAspect = false;
        selectedLocationPreview.raycastTarget = false;
        hubHeroFitter = selectedLocationPreview.gameObject.AddComponent<AspectRatioFitter>();
        hubHeroFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        hubHeroFitter.aspectRatio = 2.5f;

        // Descricao
        var descRow = HubRow(panel, "Desc", 268f, 52f);
        dDesc = UIFactory.Txt(descRow, "", 15, TextAlignmentOptions.TopLeft, UIKit.Col.TextDim, "DDesc");
        UIFactory.Stretch(dDesc.rectTransform);
        dDesc.textWrappingMode = TextWrappingModes.Normal;
        dDesc.overflowMode = TextOverflowModes.Truncate;

        // Atributos (2 colunas)
        hubStatA = HubStat(panel, 324f, true);
        hubStatB = HubStat(panel, 324f, false);
        hubStatC = HubStat(panel, 350f, true);
        hubStatD = HubStat(panel, 350f, false);

        // Aviso (bloqueado / concluido / nivel baixo)
        var noteRow = HubRow(panel, "Note", 378f, 22f);
        dInfo = UIFactory.Txt(noteRow, "", 14, TextAlignmentOptions.MidlineLeft, Color.white, "DInfo");
        UIFactory.Stretch(dInfo.rectTransform);
        dInfo.textWrappingMode = TextWrappingModes.NoWrap;
        dInfo.enableAutoSizing = true; dInfo.fontSizeMin = 10f; dInfo.fontSizeMax = 14f;

        // Inimigos principais
        HubLabel(panel, "INIMIGOS PRINCIPAIS", 406f);
        hubEnemyRow = HubRow(panel, "EnemyRow", 426f, 70f);
        HubTileRow(hubEnemyRow, 8f);

        // Itens possiveis
        HubLabel(panel, "ITENS POSSÍVEIS", 504f);
        var itemRow = HubRow(panel, "ItemRow", 524f, 58f);
        HubTileRow(itemRow, 6f);
        BuildHubItems(itemRow);

        // Dificuldade (botoes + regras + seguro)
        BuildDifficultyPanel(panel);

        // Iniciar missao
        dStart = UIFactory.Btn(panel, "INICIAR MISSÃO  »", 28, OnStartMission, new Color(0.62f, 0.07f, 0.09f, 1f), "StartMission");
        var sr = (RectTransform)dStart.transform;
        sr.anchorMin = new Vector2(0f, 0f); sr.anchorMax = new Vector2(1f, 0f); sr.pivot = new Vector2(0.5f, 0f);
        sr.offsetMin = new Vector2(20f, 18f); sr.offsetMax = new Vector2(-20f, 88f);
        var startOutline = dStart.gameObject.AddComponent<Outline>();
        startOutline.effectColor = new Color(1f, 0.30f, 0.30f, 0.85f);
        startOutline.effectDistance = new Vector2(1.5f, -1.5f);
        var startLabel = dStart.GetComponentInChildren<TMP_Text>(true);
        if (startLabel) { startLabel.fontStyle = FontStyles.Bold; startLabel.characterSpacing = 1.5f; }
    }

    TMP_Text HubStat(Transform panel, float y, bool left)
    {
        var row = HubRow(panel, "Stat", y, 24f);
        var t = UIFactory.Txt(row, "", 16, TextAlignmentOptions.MidlineLeft, UIKit.Col.Text, "StatText");
        if (left) UIFactory.Stretch(t.rectTransform, 0f, 0f, 0.58f, 1f);
        else UIFactory.Stretch(t.rectTransform, 0.58f, 0f, 1f, 1f);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.enableAutoSizing = true; t.fontSizeMin = 10f; t.fontSizeMax = 16f;
        return t;
    }

    void BuildHubItems(RectTransform row)
    {
        var gold = HubTile(row, "Item_Gold", null, UIKit.Col.Border, Color.white);
        UIKit.Diamond(gold, UIKit.Col.Gold, 24f, "GoldIcon");

        ItemSlot[] slots = { ItemSlot.Arma, ItemSlot.Colete, ItemSlot.Capacete, ItemSlot.Calca, ItemSlot.Botas, ItemSlot.Mochila };
        foreach (var slot in slots)
            HubTile(row, "Item_" + slot, ItemVisual.IconForSlot(slot), UIKit.Col.Border, Color.white, 6f);
    }

    // ---------- inimigos do local selecionado ----------

    Roster HubRoster(string theme)
    {
        if (string.IsNullOrEmpty(theme)) return null;
        if (!hubRosters.TryGetValue(theme, out var r)) { r = EnemyRoster.Get(theme); hubRosters[theme] = r; }
        return r;
    }

    void RefreshHubEnemies(LocationDef loc)
    {
        if (!hubEnemyRow) return;
        for (int i = hubEnemyRow.childCount - 1; i >= 0; i--)
        {
            var c = hubEnemyRow.GetChild(i).gameObject;
            c.SetActive(false);
            Destroy(c);
        }

        var picks = new List<EnemyEntry>();
        var r = HubRoster(loc.theme);
        if (r != null)
        {
            var order = new List<EnemyEntry>();
            if (r.commons.Count > 0) order.Add(r.commons[0]);
            if (r.commons.Count > 1) order.Add(r.commons[1]);
            if (loc.hasBoss && r.boss != null) order.Add(r.boss);
            if (r.elites.Count > 0) order.Add(r.elites[0]);
            if (r.elites.Count > 1) order.Add(r.elites[1]);
            for (int i = 2; i < r.commons.Count; i++) order.Add(r.commons[i]);
            for (int i = 2; i < r.elites.Count; i++) order.Add(r.elites[i]);
            foreach (var e in order)
            {
                if (e == null || e.sprite == null || picks.Contains(e)) continue;
                picks.Add(e);
                if (picks.Count >= 4) break;
            }
        }

        for (int i = 0; i < 4; i++)
        {
            EnemyEntry e = i < picks.Count ? picks[i] : null;
            Color border = e == null ? UIKit.Col.Border
                : e.tier == Tier.Boss ? new Color(0.85f, 0.18f, 0.20f, 1f)
                : e.tier == Tier.Elite ? new Color(0.95f, 0.50f, 0.15f, 1f)
                : UIKit.Col.Border;
            if (!HubEnemyTileFromPrefab(i, e, border))
                HubTile(hubEnemyRow, "Enemy_" + i, e != null ? e.sprite : null, border, Color.white, 4f);
        }
    }

    // ================= barra inferior =================

    void BuildHubBottom(RectTransform s, SaveData save, SurvivorDef surv, SurvivorProgress prog)
    {
        // ---- EQUIPE ----
        var squad = UIKit.Frame(s, "HubSquadPanel", new Color(0.03f, 0.045f, 0.058f, 0.97f), UIKit.Col.Border, 2f);
        UIFactory.Stretch(squad, 0.31f, 0.012f, 0.705f, 0.20f);
        var sTitle = UIFactory.Txt(squad, "EQUIPE", 15, TextAlignmentOptions.MidlineLeft, Color.white, "SquadTitle");
        HubTL(sTitle.rectTransform, 16f, 7f, 90f, 22f);
        sTitle.fontStyle = FontStyles.Bold; sTitle.characterSpacing = 2f;
        var sHint = UIFactory.Txt(squad, "XP DIVIDIDA  ·  OURO E MOCHILA COMPARTILHADOS", 11, TextAlignmentOptions.MidlineLeft, HubDim, "SquadHint");
        HubTL(sHint.rectTransform, 98f, 7f, 420f, 22f);
        sHint.characterSpacing = 1f;
        sHint.textWrappingMode = TextWrappingModes.NoWrap;

        var row = UIFactory.Node("SquadRow", squad);
        UIFactory.Stretch(row);
        row.offsetMin = new Vector2(10f, 10f);
        row.offsetMax = new Vector2(-10f, -34f);
        var h = UIFactory.HLayout(row, 8f, TextAnchor.MiddleCenter);
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = true;
        BuildSquadSlots(row);

        // ---- CHAT (espaco reservado; antes era o painel EQUIPAMENTO, que ja existe em INVENTARIO) ----
        var chat = UIKit.Frame(s, "HubChatPanel", new Color(0.03f, 0.045f, 0.058f, 0.97f), UIKit.Col.Border, 2f);
        UIFactory.Stretch(chat, 0.005f, 0.012f, 0.305f, 0.20f);
        var cTitle = UIFactory.Txt(chat, "CHAT  ·  EM BREVE", 15, TextAlignmentOptions.MidlineLeft, HubDim, "ChatTitle");
        HubTL(cTitle.rectTransform, 16f, 7f, 300f, 22f);
        cTitle.fontStyle = FontStyles.Bold; cTitle.characterSpacing = 2f;
        cTitle.textWrappingMode = TextWrappingModes.NoWrap;
    }

    ItemInstance HubEquipped(SurvivorProgress prog, ItemSlot slot)
    {
        if (prog == null || prog.equipped == null) return null;
        for (int i = 0; i < prog.equipped.Count; i++)
            if (prog.equipped[i] != null && prog.equipped[i].slot == slot) return prog.equipped[i];
        return null;
    }

    void BuildHubEquipCell(Transform grid, ItemSlot slot, ItemInstance item)
    {
        if (BuildEquipCellFromPrefab(grid, slot, item)) return;   // prefab Cell_Equip (se existir)

        var b = UIFactory.Btn(grid, string.Empty, 10f, () => NavGo(NavTab.Bag),
            new Color(0.05f, 0.07f, 0.09f, 1f), "EquipSlot_" + slot);
        var label = b.GetComponentInChildren<TMP_Text>(true);
        if (label) label.text = string.Empty;

        var outline = b.gameObject.AddComponent<Outline>();
        outline.effectColor = item != null ? RarityColors.Get(item.rarity) : new Color(0.20f, 0.24f, 0.29f, 0.95f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        var icon = UIFactory.Img(b.transform, Color.white, "Icon");
        icon.sprite = item != null ? ItemVisual.Icon(item) : ItemVisual.IconForSlot(slot);
        icon.color = item != null ? ItemVisual.Tint(item) : new Color(1f, 1f, 1f, 0.28f);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        UIFactory.Inset(UIFactory.Stretch(icon.rectTransform), 8f, 8f);

        if ((item != null && item.upgradeLevel > 0) || editorUiBaking)
        {
            var up = UIFactory.Txt(b.transform, "+" + (item != null ? item.upgradeLevel : 1), 13, TextAlignmentOptions.TopRight, UIKit.Col.Gold, "Upgrade");
            UIFactory.Stretch(up.rectTransform, 0f, 0.55f, 0.94f, 0.98f);
            up.fontStyle = FontStyles.Bold;
        }
    }
}
