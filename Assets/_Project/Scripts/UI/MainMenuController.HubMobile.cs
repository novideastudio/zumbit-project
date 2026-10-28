using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Hub do mapa - layout mobile (paisagem). Parte de MainMenuController.
public partial class MainMenuController
{
    const float MTopY = 0.895f;
    const float MMapL = 0.09f;
    const float MMapR = 0.70f;
    const float MPanelL = 0.705f;

    static LayoutElement MLe(GameObject go, float h = -1f, float w = -1f, float flexW = -1f)
    {
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        if (h >= 0f) le.preferredHeight = h;
        if (w >= 0f) le.preferredWidth = w;
        if (flexW >= 0f) le.flexibleWidth = flexW;
        return le;
    }

    TMP_Text MLabel(Transform parent, string text)
    {
        var t = UIFactory.Txt(parent, text, 15, TextAlignmentOptions.MidlineLeft, HubDim, "Eyebrow_" + text);
        t.fontStyle = FontStyles.Bold;
        t.characterSpacing = 1.4f;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        bool compact = Screen.width < 1100 || Screen.height < 650;
        MLe(t.gameObject, compact ? 18f : 20f);
        return t;
    }

    void BuildHubTopBar(RectTransform s, SaveData save, SurvivorDef surv, SurvivorProgress prog)
    {
        var bar = UIFactory.Img(s, new Color(0.02f, 0.03f, 0.04f, 0.98f), "HubTopBarM");
        UIFactory.Stretch(bar.rectTransform, MMapL, MTopY, MMapR, 0.995f);
        bar.raycastTarget = false;

        var row = UIFactory.Node("TopRow", bar.transform);
        UIFactory.Stretch(row);
        row.offsetMin = new Vector2(8f, 4f);
        row.offsetMax = new Vector2(-8f, -4f);
        var h = UIFactory.HLayout(row, 12f, TextAnchor.MiddleLeft);
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        h.childControlWidth = true;
        h.childControlHeight = true;

        BuildHubProfile(row, save, surv, prog);

        var gold = HubPill(row, "GoldPill", 210f, 60f);
        UIKit.Diamond(gold, UIKit.Col.Gold, 22f, "GoldIcon").rectTransform.anchoredPosition = new Vector2(-66f, 0f);
        HubPillValue(gold, save.gold.ToString("N0"), UIKit.Col.Gold);

        var scrap = HubPill(row, "ScrapPill", 210f, 60f);
        HubGear(scrap, new Vector2(-66f, 0f), new Color(0.62f, 0.72f, 0.80f, 1f));
        HubPillValue(scrap, save.scrap.ToString("N0"), new Color(0.80f, 0.88f, 0.94f, 1f));
    }

    void BuildHubChapterSelector(RectTransform s, SaveData save)
    {
        var box = UIKit.Frame(s, "ChapterSelectorM", new Color(0.04f, 0.055f, 0.07f, 0.98f), UIKit.Col.Border, 2f);
        UIFactory.Stretch(box, MPanelL, MTopY, 0.995f, 0.995f);

        var prev = UIFactory.Txt(box, "<", 36, TextAlignmentOptions.Center, UIKit.Col.TextDim, "ChapterPrev");
        UIFactory.Stretch(prev.rectTransform, 0f, 0f, 0.12f, 1f);
        prev.raycastTarget = false;
        var next = UIFactory.Txt(box, ">", 36, TextAlignmentOptions.Center, UIKit.Col.TextDim, "ChapterNext");
        UIFactory.Stretch(next.rectTransform, 0.88f, 0f, 1f, 1f);
        next.raycastTarget = false;

        int total = GameDatabase.Locations.Count;
        int done = save.CompletedLocationCount();
        var eyebrow = UIFactory.Txt(box, "CAPÍTULO 1  ·  " + done + "/" + total, 16, TextAlignmentOptions.Center, HubDim, "ChapterEyebrow");
        UIFactory.Stretch(eyebrow.rectTransform, 0.12f, 0.58f, 0.88f, 0.95f);
        eyebrow.fontStyle = FontStyles.Bold; eyebrow.characterSpacing = 1.4f;
        eyebrow.raycastTarget = false;
        var title = UIFactory.Txt(box, "REGIÃO URBANA", 30, TextAlignmentOptions.Center, Color.white, "ChapterTitle");
        UIFactory.Stretch(title.rectTransform, 0.12f, 0.05f, 0.88f, 0.60f);
        title.fontStyle = FontStyles.Bold;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.enableAutoSizing = true; title.fontSizeMin = 18f; title.fontSizeMax = 30f;
        title.raycastTarget = false;
    }

    void BuildHubSidebarMobile(RectTransform s)
    {
        // Marca do jogo no topo da barra lateral, como no novo conceito visual.
        var brand = UIKit.Frame(s, "ZombitBrand", new Color(0.018f, 0.026f, 0.030f, 0.98f), UIKit.Col.Border, 1.5f);
        UIFactory.Stretch(brand, 0.006f, MTopY, 0.085f, 0.995f);
        var brandMain = UIFactory.Txt(brand, "ZOMBIT", 24f, TextAlignmentOptions.Center, new Color(0.91f, 0.92f, 0.86f, 1f), "BrandMain");
        UIFactory.Stretch(brandMain.rectTransform, 0.02f, 0.43f, 0.98f, 0.89f);
        brandMain.fontStyle = FontStyles.Bold;
        brandMain.characterSpacing = 0.7f;
        brandMain.enableAutoSizing = true; brandMain.fontSizeMin = 14f; brandMain.fontSizeMax = 24f;
        brandMain.raycastTarget = false;
        var brandSub = UIFactory.Txt(brand, "SURVIVAL", 17f, TextAlignmentOptions.Center, new Color(0.91f, 0.12f, 0.14f, 1f), "BrandSub");
        UIFactory.Stretch(brandSub.rectTransform, 0.02f, 0.10f, 0.98f, 0.48f);
        brandSub.fontStyle = FontStyles.Bold;
        brandSub.characterSpacing = 1f;
        brandSub.enableAutoSizing = true; brandSub.fontSizeMin = 10f; brandSub.fontSizeMax = 17f;
        brandSub.raycastTarget = false;

        var side = UIFactory.Node("HubSidebarM", s);
        UIFactory.Stretch(side, 0.006f, 0.225f, 0.085f, MTopY - 0.01f);
        var v = UIFactory.VLayout(side, 8f, TextAnchor.UpperCenter);
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        v.childControlWidth = true;
        v.childControlHeight = true;

        AddSideButtonM(side, "MUNDO", NavTab.Map, true);
        AddSideButtonM(side, "EQUIPE", NavTab.Character, false);
        AddSideButtonM(side, "INVENTÁRIO", NavTab.Bag, false);
        AddSideButtonM(side, "HABILIDADES", NavTab.Skills, false);
        AddSideButtonM(side, "LOJA", NavTab.Shop, false);
        AddRangeSideButton(side);
        AddHomeSideButton(side);
    }

    void AddSideButtonM(RectTransform parent, string label, NavTab tab, bool active)
    {
        var b = UIKit.Button(parent, label, () => { if (!active) NavGo(tab); },
            active ? UIKit.BtnStyle.Primary : UIKit.BtnStyle.Secondary, 16f, "Side_" + tab);
        UIFactory.Elem(b.gameObject, 150f, 66f);
        UIFactory.SetButtonColor(b, active ? new Color(0.36f, 0.07f, 0.09f, 1f) : new Color(0.075f, 0.10f, 0.13f, 0.96f));
        var t = b.GetComponentInChildren<TMP_Text>(true);
        if (t)
        {
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.margin = new Vector4(16f, 2f, 4f, 2f);
            t.enableAutoSizing = true; t.fontSizeMin = 11f; t.fontSizeMax = 17f;
            t.textWrappingMode = TextWrappingModes.NoWrap;
        }
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

    void BuildHubMissionPanelMobile(RectTransform s)
    {
        bool compact = Screen.width < 1100 || Screen.height < 650;
        var panel = UIKit.Frame(s, "LocationPanelM", new Color(0.018f, 0.027f, 0.036f, 0.98f),
            new Color(0.20f, 0.24f, 0.29f, 0.95f), 2f);
        UIFactory.Stretch(panel, MPanelL, 0.02f, 0.995f, MTopY - 0.01f);

        var titleRow = HubRow(panel, "TitleBar", 6f, 54f, 6f);
        var titleBg = UIFactory.Img(titleRow, new Color(0.018f, 0.025f, 0.032f, 0.98f), "TitleBg");
        UIFactory.Stretch(titleBg.rectTransform);
        titleBg.raycastTarget = false;
        var badge = UIKit.Frame(titleRow, "NumBadge", new Color(0.12f, 0.07f, 0.04f, 1f), UIKit.Col.Gold, 2f);
        UIFactory.Place(badge, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(48f, 38f));
        hubBadge = UIFactory.Txt(badge, "00", 22, TextAlignmentOptions.Center, UIKit.Col.Gold, "Num");
        UIFactory.Stretch(hubBadge.rectTransform);
        hubBadge.fontStyle = FontStyles.Bold;
        dName = UIFactory.Txt(titleRow, "", 30, TextAlignmentOptions.MidlineLeft, Color.white, "DName");
        UIFactory.Stretch(dName.rectTransform);
        dName.rectTransform.offsetMin = new Vector2(68f, 0f);
        dName.rectTransform.offsetMax = new Vector2(-66f, 0f);
        dName.fontStyle = FontStyles.Bold;
        dName.textWrappingMode = TextWrappingModes.NoWrap;
        dName.enableAutoSizing = true; dName.fontSizeMin = 18f; dName.fontSizeMax = 30f;

        var scrollRt = UIFactory.Node("MissionScroll", panel);
        UIFactory.Stretch(scrollRt);
        scrollRt.offsetMin = new Vector2(6f, 92f);
        scrollRt.offsetMax = new Vector2(-6f, -66f);
        var catcher = scrollRt.gameObject.AddComponent<Image>();
        catcher.color = new Color(0f, 0f, 0f, 0f);
        scrollRt.gameObject.AddComponent<RectMask2D>();

        var content = UIFactory.Node("MissionContent", scrollRt);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;
        var vl = UIFactory.VLayout(content, compact ? 4f : 6f, TextAnchor.UpperCenter);
        vl.childForceExpandWidth = true;
        vl.childForceExpandHeight = false;
        vl.childControlWidth = true;
        vl.childControlHeight = true;
        vl.padding = new RectOffset(8, 8, 6, 12);
        var fit = content.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = scrollRt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 40f;

        var heroRow = UIFactory.Node("Hero", content);
        MLe(heroRow.gameObject, compact ? 120f : 160f);
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

        dDesc = UIFactory.Txt(content, "", 18, TextAlignmentOptions.TopLeft, UIKit.Col.TextDim, "DDesc");
        dDesc.textWrappingMode = TextWrappingModes.Normal;
        dDesc.enableAutoSizing = true; dDesc.fontSizeMin = 14f; dDesc.fontSizeMax = 18f;
        MLe(dDesc.gameObject, compact ? 42f : 50f);

        dInfo = UIFactory.Txt(content, "", 16, TextAlignmentOptions.MidlineLeft, Color.white, "DInfo");
        dInfo.textWrappingMode = TextWrappingModes.NoWrap;
        dInfo.enableAutoSizing = true; dInfo.fontSizeMin = 11f; dInfo.fontSizeMax = 16f;
        MLe(dInfo.gameObject, compact ? 16f : 20f);

        var statRow = UIFactory.Node("StatRow", content);
        MLe(statRow.gameObject, compact ? 44f : 56f);
        HubTileRow(statRow, 8f);
        hubStatA = MStatBox(statRow, "StatLevel");
        hubStatB = MStatBox(statRow, "StatWaves");
        hubStatD = MStatBox(statRow, "StatReward");
        hubStatC = null;

        MLabel(content, "INIMIGOS PRINCIPAIS");
        hubEnemyRow = UIFactory.Node("EnemyRow", content);
        MLe(hubEnemyRow.gameObject, compact ? 52f : 64f);
        HubTileRow(hubEnemyRow, 8f);

        MLabel(content, "ITENS POSSÍVEIS");
        var itemRow = UIFactory.Node("ItemRow", content);
        MLe(itemRow.gameObject, compact ? 44f : 52f);
        HubTileRow(itemRow, 6f);
        BuildHubItems(itemRow);

        BuildDifficultyPanelMobile(content);

        dStart = UIFactory.Btn(panel, "INICIAR MISSÃO  »", 30, OnStartMission, new Color(0.62f, 0.07f, 0.09f, 1f), "StartMission");
        var sr = (RectTransform)dStart.transform;
        sr.anchorMin = new Vector2(0f, 0f); sr.anchorMax = new Vector2(1f, 0f); sr.pivot = new Vector2(0.5f, 0f);
        sr.offsetMin = new Vector2(12f, 12f); sr.offsetMax = new Vector2(-12f, 80f);
        var startOutline = dStart.gameObject.AddComponent<Outline>();
        startOutline.effectColor = new Color(1f, 0.30f, 0.30f, 0.85f);
        startOutline.effectDistance = new Vector2(1.5f, -1.5f);
        var startLabel = dStart.GetComponentInChildren<TMP_Text>(true);
        if (startLabel) { startLabel.fontStyle = FontStyles.Bold; startLabel.characterSpacing = 1.5f; }
    }

    TMP_Text MStatBox(RectTransform row, string name)
    {
        var box = UIKit.Frame(row, name, HubTileFill, UIKit.Col.Border, 1.5f);
        var le = box.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 40f; le.flexibleWidth = 1f; le.flexibleHeight = 1f;
        var t = UIFactory.Txt(box, "", 16, TextAlignmentOptions.Center, UIKit.Col.Text, name + "Text");
        UIFactory.Stretch(t.rectTransform);
        t.rectTransform.offsetMin = new Vector2(4f, 2f);
        t.rectTransform.offsetMax = new Vector2(-4f, -2f);
        t.textWrappingMode = TextWrappingModes.Normal;
        t.enableAutoSizing = true; t.fontSizeMin = 11f; t.fontSizeMax = 17f;
        t.raycastTarget = false;
        return t;
    }

    void BuildDifficultyPanelMobile(Transform content)
    {
        MLabel(content, "DIFICULDADE");

        var btnRow = UIFactory.Node("DifficultyRow", content);
        MLe(btnRow.gameObject, (Screen.width < 1100 || Screen.height < 650) ? 44f : 52f);
        HubTileRow(btnRow, 8f);
        for (int i = 0; i < 4; i++)
        {
            int level = i + 1;
            var d = Difficulties.Get(level);
            var b = UIFactory.Btn(btnRow, d.name, 18, () => { ZombitAudio.PlayUI(); SelectDifficulty(level); },
                new Color(0.10f, 0.14f, 0.17f), "Difficulty_" + d.name);
            var le = b.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 40f; le.flexibleWidth = 1f; le.flexibleHeight = 1f;
            var text = b.GetComponentInChildren<TMP_Text>();
            if (text)
            {
                text.textWrappingMode = TextWrappingModes.Normal;
                text.overflowMode = TextOverflowModes.Truncate;
                text.margin = new Vector4(2, 2, 2, 2);
                text.enableAutoSizing = true; text.fontSizeMin = 10f; text.fontSizeMax = 18f;
            }
            diffButtons[i] = b;
        }

        var insRow = UIFactory.Node("InsuranceRow", content);
        MLe(insRow.gameObject, (Screen.width < 1100 || Screen.height < 650) ? 28f : 34f);
        dInsurance = UIFactory.Btn(insRow, "", 16, () => { ZombitAudio.PlayUI(); buyInsurance = !buyInsurance; RefreshDifficultyUI(); },
            new Color(0.10f, 0.14f, 0.17f), "InsuranceToggle");
        UIFactory.Stretch((RectTransform)dInsurance.transform);
        dInsuranceInfo = dInsurance.GetComponentInChildren<TMP_Text>();
        if (dInsuranceInfo)
        {
            dInsuranceInfo.textWrappingMode = TextWrappingModes.NoWrap;
            dInsuranceInfo.enableAutoSizing = true; dInsuranceInfo.fontSizeMin = 10f; dInsuranceInfo.fontSizeMax = 17f;
        }

        var infoRow = UIFactory.Node("DifficultyInfoRow", content);
        MLe(infoRow.gameObject, 200f);
        dDiffInfo = UIFactory.Txt(infoRow, "", 16, TextAlignmentOptions.TopLeft, new Color(0.80f, 0.86f, 0.88f), "DifficultyInfo");
        UIFactory.Stretch(dDiffInfo.rectTransform);
        dDiffInfo.textWrappingMode = TextWrappingModes.Normal;
        dDiffInfo.overflowMode = TextOverflowModes.Truncate;
        dDiffInfo.raycastTarget = false;
        dDiffInfo.enableAutoSizing = true; dDiffInfo.fontSizeMin = 11f; dDiffInfo.fontSizeMax = 16f;
        var infoBox = infoRow.gameObject;
        infoBox.SetActive(false);

        var contentRt = (RectTransform)content;
        var toggle = UIFactory.Btn(content, "(i)  REGRAS DA DIFICULDADE", 16, () =>
        {
            ZombitAudio.PlayUI();
            infoBox.SetActive(!infoBox.activeSelf);
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);
        }, new Color(0.075f, 0.10f, 0.13f, 0.96f), "DifficultyInfoToggle");
        MLe(toggle.gameObject, (Screen.width < 1100 || Screen.height < 650) ? 32f : 38f);
        infoRow.SetAsLastSibling();
    }

    void BuildHubBottomMobile(RectTransform s, SaveData save, SurvivorDef surv, SurvivorProgress prog)
    {
        var squad = UIKit.Frame(s, "HubSquadPanelM", new Color(0.03f, 0.045f, 0.058f, 0.97f), UIKit.Col.Border, 2f);
        UIFactory.Stretch(squad, MMapL, 0.012f, MMapR, 0.215f);
        var sTitle = UIFactory.Txt(squad, "EQUIPE", 18, TextAlignmentOptions.MidlineLeft, Color.white, "SquadTitle");
        HubTL(sTitle.rectTransform, 16f, 7f, 100f, 26f);
        sTitle.fontStyle = FontStyles.Bold; sTitle.characterSpacing = 2f;
        var sHint = UIFactory.Txt(squad, "XP DIVIDIDA  ·  OURO E MOCHILA COMPARTILHADOS", 12, TextAlignmentOptions.MidlineLeft, HubDim, "SquadHint");
        HubTL(sHint.rectTransform, 120f, 7f, 460f, 26f);
        sHint.characterSpacing = 1f;
        sHint.textWrappingMode = TextWrappingModes.NoWrap;

        var row = UIFactory.Node("SquadRow", squad);
        UIFactory.Stretch(row);
        row.offsetMin = new Vector2(10f, 10f);
        row.offsetMax = new Vector2(-10f, -38f);
        var h = UIFactory.HLayout(row, 8f, TextAnchor.MiddleCenter);
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = true;
        BuildSquadSlots(row);
    }
}
