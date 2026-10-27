using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Arvore de habilidades no mapa (parte de MainMenuController).
// Layout (referencia 1920x1080): 3 paineis de ramo + painel de detalhes + legenda.
// Os icones ficam em Resources/Art/Skills (icon_*.png / ui_*.png). Se um PNG nao existir,
// a tela continua funcionando (so aquele icone some). Para trocar a arte, basta substituir o PNG.
public partial class MainMenuController
{
    enum SkState { Unlocked, Available, NoPoints, Locked }

    const string SkArt = "Art/Skills/";

    // Geometria da tela (pixels da resolucao de referencia).
    const float SkTop = 108f;
    const float SkPanelH = 846f;
    const float SkBranchW = 430f;
    const float SkBranchGap = 14f;
    const float SkLeft = 24f;
    const float SkCardX = 16f;
    const float SkCardW = 398f;
    const float SkCardH = 212f;
    const float SkCardY0 = 116f;      // y do primeiro cartao dentro do painel do ramo
    const float SkCardStep = 252f;    // distancia entre cartoes (212 de cartao + 40 de conector)

    static readonly Color SkPanelFill = new Color(0.025f, 0.040f, 0.052f, 0.96f);
    static readonly Color SkDark = new Color(0.030f, 0.042f, 0.052f, 1f);
    static readonly Color SkGreen = new Color(0.30f, 0.90f, 0.52f, 1f);
    static readonly Color SkGreenText = new Color(0.45f, 0.95f, 0.62f, 1f);
    static readonly Color SkGoldCol = new Color(1f, 0.72f, 0.20f, 1f);
    static readonly Color SkDim = new Color(0.60f, 0.68f, 0.72f, 1f);
    static readonly Color SkLockedLine = new Color(0.20f, 0.27f, 0.31f, 1f);

    // ---------- habilidades no mapa ----------
    /// <summary>Sobrevivente das habilidades: o que esta sendo editado na Mochila (retratos) ou, sem ele, o principal.</summary>
    string SkillsSurvivorId(SaveData save)
    {
        if (!string.IsNullOrEmpty(invSurvivorId) && GameDatabase.Survivors.Exists(sv => sv.id == invSurvivorId))
            return invSurvivorId;
        return save.selectedSurvivor;
    }

    void ShowMapSkills(string nodeId = null)
    {
        var save = GameSession.Save;
        if (save == null) { Show("main"); return; }
        var survivor = GameDatabase.GetSurvivor(SkillsSurvivorId(save));
        if (survivor == null) { ShowMap(); return; }
        invSurvivorId = survivor.id;   // Mochila e Habilidades editam o mesmo sobrevivente
        var progress = save.GetProgress(survivor.id);
        var nodes = SkillTreeSystem.GetNodes(survivor.id);
        if (string.IsNullOrEmpty(nodeId)) nodeId = selectedSkillId;
        if (!string.IsNullOrEmpty(nodeId) && SkillTreeSystem.GetNode(survivor.id, nodeId) == null) nodeId = null;
        if (string.IsNullOrEmpty(nodeId) && nodes.Count > 0)
        {
            // Sem selecao: abre na primeira habilidade que da para desbloquear (senao, na primeira).
            nodeId = nodes[0].id;
            for (int i = 0; i < nodes.Count; i++)
                if (SkillTreeSystem.CanUnlock(progress, nodes[i], out string _)) { nodeId = nodes[i].id; break; }
        }
        selectedSkillId = nodeId;

        var s = Rebuild("mapSkills");
        CreateUtilityBackdrop(s, "MapSkillsBackdrop");
        SkBuildHeader(s, survivor, progress);

        Color[] branchAccents = {
            new Color(0.94f, 0.20f, 0.15f, 1f),   // ramo 1 (vermelho)
            new Color(1.00f, 0.68f, 0.12f, 1f),   // ramo 2 (amarelo)
            new Color(0.12f, 0.82f, 0.66f, 1f)    // ramo 3 (verde)
        };
        for (int branch = 0; branch < 3; branch++)
            SkBuildBranch(s, branch, branchAccents[branch], survivor, nodes, progress);

        SkBuildDetail(s, survivor, nodes, progress, branchAccents);
        SkBuildLegend(s);
        UILayoutPrefabApplier.Apply(s, "mapSkills");
        Show("mapSkills");
    }

    // ---------- cabecalho ----------
    void SkBuildHeader(RectTransform s, SurvivorDef survivor, SurvivorProgress progress)
    {
        var header = CreateNavHeader(s, NavTab.Skills, false, out var left, out var chips);
        var tick = UIFactory.Img(left, UIKit.Col.Accent, "TitleTick");
        UIFactory.Place(tick.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(6f, 44f));
        tick.raycastTarget = false;
        var title = UIFactory.Txt(left, "ÁRVORE DE HABILIDADES", UIKit.Size.Title, TextAlignmentOptions.MidlineLeft, UIKit.Col.Text, "UtilityTitle");
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 1.5f;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        UIFactory.Stretch(title.rectTransform);
        title.rectTransform.offsetMin = new Vector2(20f, 0f);

        // Pilula de pontos: icone + "PONTOS" + numero grande.
        var pill = UIKit.Frame(chips, "PointsPill", new Color(0.075f, 0.058f, 0.030f, 1f), new Color(1f, 0.76f, 0.29f, 0.85f), 2f);
        UIFactory.Elem(pill.gameObject, 190f, 56f);
        SkImage(pill, "ui_points", Color.white, "PointsIcon", new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(34f, 34f));
        SkLabel(pill, "PONTOS", 16f, TextAlignmentOptions.MidlineLeft, UIKit.Col.Gold, 54f, 70f, "PointsLabel").fontStyle = FontStyles.Bold;
        var num = SkLabel(pill, progress.skillPoints.ToString(), 32f, TextAlignmentOptions.MidlineRight, SkGoldCol, 120f, 14f, "PointsValue");
        num.fontStyle = FontStyles.Bold;

        // Troca de personagem sem sair da tela (mesmo estado da Mochila: invSurvivorId).
        SkBuildSurvivorSwitcher(header, survivor.id);
    }

    /// <summary>Faixa de retratos no cabecalho: clique troca o sobrevivente cuja arvore esta aberta.</summary>
    void SkBuildSurvivorSwitcher(RectTransform header, string activeId)
    {
        var save = GameSession.Save;
        var list = GameDatabase.Survivors;
        if (save == null || list == null || list.Count == 0) return;

        var strip = UIFactory.Node("SurvivorSwitcher", header);
        UIFactory.Stretch(strip);
        strip.offsetMin = new Vector2(880f, 12f);     // depois da pilula de pontos
        strip.offsetMax = new Vector2(-340f, -12f);   // antes do botao VOLTAR AO MUNDO
        var h = UIFactory.HLayout(strip, 8f, TextAnchor.MiddleRight);
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;

        int n = Mathf.Max(1, list.Count);
        float chipW = Mathf.Floor(Mathf.Min(134f, (700f - 8f * (n - 1)) / n));
        foreach (var def in list) SkBuildSurvivorChip(strip, def, save, def.id == activeId, chipW);
    }

    void SkBuildSurvivorChip(RectTransform strip, SurvivorDef def, SaveData save, bool active, float width)
    {
        string id = def.id;
        var b = UIKit.Button(strip, "", () =>
        {
            if (id == invSurvivorId) return;
            invSurvivorId = id;
            selectedSkillId = null;      // cada personagem tem arvore propria
            ShowMapSkills(null);
        }, UIKit.BtnStyle.Secondary, 12, "SkSurvivor_" + id);
        UIFactory.Elem(b.gameObject, width, 64f);
        UIFactory.SetButtonColor(b, active ? UIKit.Col.Accent : UIKit.Col.Border);
        var lbl = b.GetComponentInChildren<TMP_Text>(true);
        if (lbl) lbl.text = "";

        Color fillColor = active ? new Color(0.20f, 0.09f, 0.10f, 1f) : new Color(0.045f, 0.07f, 0.085f, 1f);
        var fill = UIFactory.Img(b.transform, fillColor, "Fill");
        UIKit.ApplyChamfer(fill, fillColor);
        UIFactory.Inset(UIFactory.Stretch(fill.rectTransform), 2f, 2f);
        fill.raycastTarget = false;
        fill.transform.SetAsFirstSibling();

        var portrait = UIFactory.Img(b.transform, Color.white, "Portrait");
        portrait.sprite = SurvivorPortraitSprite(def);
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;
        portrait.color = portrait.sprite != null ? Color.white : def.color;
        UIFactory.Place(portrait.rectTransform, new Vector2(0f, 0.5f), new Vector2(7f, 0f), new Vector2(46f, 52f));

        float textX = 58f, textW = width - textX - 6f;
        var nameText = UIFactory.Txt(b.transform, def.name.ToUpperInvariant(), 12f, TextAlignmentOptions.MidlineLeft,
            active ? UIKit.Col.Text : UIKit.Col.TextDim, "Name");
        nameText.fontStyle = FontStyles.Bold;
        UIFactory.Place(nameText.rectTransform, new Vector2(0f, 1f), new Vector2(textX, -9f), new Vector2(textW, 24f));
        SkFit(nameText, 9f);

        var lvText = UIFactory.Txt(b.transform, "NV. " + SurvivorLevel(save, id), 12f, TextAlignmentOptions.MidlineLeft,
            UIKit.Col.Gold, "Level");
        lvText.fontStyle = FontStyles.Bold;
        UIFactory.Place(lvText.rectTransform, new Vector2(0f, 1f), new Vector2(textX, -33f), new Vector2(textW, 22f));
        SkFit(lvText, 9f);

        // Selo dourado: este personagem tem pontos de habilidade para gastar.
        int pts = SkPoints(save, id);
        if (pts > 0)
        {
            var dot = UIFactory.Img(b.transform, SkGoldCol, "PointsDot");
            UIKit.ApplyCircle(dot, SkGoldCol);
            dot.raycastTarget = false;
            UIFactory.Place(dot.rectTransform, new Vector2(1f, 1f), new Vector2(7f, 7f), new Vector2(24f, 24f));
            var dt = UIFactory.Txt(dot.transform, pts > 9 ? "9+" : pts.ToString(), 13f, TextAlignmentOptions.Center,
                new Color(0.10f, 0.06f, 0.02f, 1f), "PointsDotText");
            dt.fontStyle = FontStyles.Bold;
            dt.textWrappingMode = TextWrappingModes.NoWrap;
            UIFactory.Stretch(dt.rectTransform);
        }
    }

    /// <summary>Pontos de habilidade sem criar entrada de progresso para quem ainda nao jogou.</summary>
    static int SkPoints(SaveData save, string id)
    {
        if (save != null && save.progress != null)
            foreach (var p in save.progress)
                if (p != null && p.id == id) return Mathf.Max(0, p.skillPoints);
        return 0;
    }

    // ---------- painel de um ramo ----------
    void SkBuildBranch(RectTransform s, int branch, Color accent, SurvivorDef survivor,
        IReadOnlyList<SkillTreeNode> nodes, SurvivorProgress progress)
    {
        float panelX = SkLeft + branch * (SkBranchW + SkBranchGap);
        var panel = UIKit.Frame(s, "SkillBranch_" + branch, SkPanelFill, new Color(accent.r, accent.g, accent.b, 0.60f), 2f);
        UIFactory.Place(panel, new Vector2(0f, 1f), new Vector2(panelX, -SkTop), new Vector2(SkBranchW, SkPanelH));

        string branchName = branch == 0 ? "RAMO ALFA" : branch == 1 ? "RAMO BRAVO" : "RAMO CHARLIE";
        SkillTreeNode first = null;
        int total = 0, unlockedCount = 0;
        for (int j = 0; j < nodes.Count; j++)
        {
            if (nodes[j].branchIndex != branch) continue;
            if (first == null) { first = nodes[j]; branchName = nodes[j].branchName.ToUpperInvariant(); }
            total++;
            if (SkillTreeSystem.IsUnlocked(progress, nodes[j].id)) unlockedCount++;
        }

        // Faixa do cabecalho do ramo.
        var band = UIKit.Frame(panel, "BranchBand", new Color(accent.r * 0.20f, accent.g * 0.20f, accent.b * 0.20f, 0.95f),
            new Color(accent.r, accent.g, accent.b, 0.30f), 1.5f);
        UIFactory.Place(band, new Vector2(0f, 1f), new Vector2(8f, -8f), new Vector2(SkBranchW - 16f, 90f));

        // Emblema: anel + disco escuro + icone da primeira habilidade do ramo.
        var ring = UIFactory.Img(panel, accent, "EmblemRing");
        UIKit.ApplyCircle(ring, accent);
        UIFactory.Place(ring.rectTransform, new Vector2(0f, 1f), new Vector2(20f, -19f), new Vector2(68f, 68f));
        ring.raycastTarget = false;
        var disc = UIFactory.Img(panel, new Color(0.04f, 0.05f, 0.06f, 1f), "EmblemDisc");
        UIKit.ApplyCircle(disc, new Color(0.04f, 0.05f, 0.06f, 1f));
        UIFactory.Place(disc.rectTransform, new Vector2(0f, 1f), new Vector2(23f, -22f), new Vector2(62f, 62f));
        disc.raycastTarget = false;
        if (first != null)
            SkImage(panel, "icon_" + SkIconKey(survivor.id, first), Color.white, "EmblemIcon",
                new Vector2(0f, 1f), new Vector2(30f, -29f), new Vector2(48f, 48f));

        var bt = MapText(panel, branchName, 26, TextAlignmentOptions.MidlineLeft, accent, 102, 22, 312, 34, "BranchTitle");
        bt.fontStyle = FontStyles.Bold;
        SkFit(bt, 16f);
        MapText(panel, unlockedCount + " / " + total + " DESBLOQUEADAS", UIKit.Size.Caption,
            TextAlignmentOptions.MidlineLeft, SkDim, 102, 58, 190, 22, "BranchProgress");

        // Segmentos de progresso (um por habilidade do ramo).
        for (int i = 0; i < total; i++)
        {
            bool done = i < unlockedCount;
            var pip = UIFactory.Img(panel, done ? accent : new Color(0.20f, 0.26f, 0.30f, 1f), "ProgressPip_" + i);
            UIFactory.Place(pip.rectTransform, new Vector2(0f, 1f), new Vector2(314f + i * 34f, -67f), new Vector2(28f, 7f));
            pip.raycastTarget = false;
        }

        // Conectores entre cartoes (linha + seta).
        for (int j = 0; j < nodes.Count; j++)
        {
            var n = nodes[j];
            if (n.branchIndex != branch || n.tier < 2) continue;
            float yTop = SkCardY0 + (n.tier - 2) * SkCardStep + SkCardH;
            float yBottom = SkCardY0 + (n.tier - 1) * SkCardStep;
            bool lit = SkillTreeSystem.IsUnlocked(progress, n.prerequisiteId);
            Color lc = lit ? new Color(accent.r, accent.g, accent.b, 0.95f) : new Color(accent.r, accent.g, accent.b, 0.35f);
            var line = UIFactory.Img(panel, lc, "TierConnector_" + n.tier);
            UIFactory.Place(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -yTop), new Vector2(4f, yBottom - yTop - 14f));
            line.raycastTarget = false;
            SkImage(panel, "ui_arrow_down", lc, "TierArrow_" + n.tier,
                new Vector2(0.5f, 1f), new Vector2(0f, -(yBottom - 24f)), new Vector2(24f, 24f));
        }

        for (int j = 0; j < nodes.Count; j++)
        {
            if (nodes[j].branchIndex != branch) continue;
            SkBuildCard(panel, nodes[j], accent, survivor, progress);
        }
    }

    // ---------- cartao de habilidade ----------
    void SkBuildCard(RectTransform panel, SkillTreeNode node, Color accent, SurvivorDef survivor, SurvivorProgress progress)
    {
        bool unlocked = SkillTreeSystem.IsUnlocked(progress, node.id);
        bool canUnlock = SkillTreeSystem.CanUnlock(progress, node, out string _);
        bool prereqMet = string.IsNullOrEmpty(node.prerequisiteId) || SkillTreeSystem.IsUnlocked(progress, node.prerequisiteId);
        bool levelMet = progress.level >= node.requiredLevel;
        SkState state = unlocked ? SkState.Unlocked : canUnlock ? SkState.Available :
            (prereqMet && levelMet) ? SkState.NoPoints : SkState.Locked;
        bool selected = node.id == selectedSkillId;
        bool locked = state == SkState.Locked;

        Color fill, border;
        switch (state)
        {
            case SkState.Unlocked:
                fill = new Color(0.040f, 0.155f, 0.115f, 1f);
                border = new Color(0.25f, 0.85f, 0.50f, 0.95f); break;
            case SkState.Available:
                fill = Color.Lerp(new Color(0.045f, 0.058f, 0.068f, 1f), accent, 0.14f);
                border = new Color(accent.r, accent.g, accent.b, 0.95f); break;
            case SkState.NoPoints:
                fill = Color.Lerp(new Color(0.045f, 0.058f, 0.068f, 1f), accent, 0.07f);
                border = new Color(accent.r, accent.g, accent.b, 0.55f); break;
            default:
                fill = new Color(0.030f, 0.042f, 0.052f, 0.98f);
                border = SkLockedLine; break;
        }
        float cardY = SkCardY0 + (node.tier - 1) * SkCardStep;
        float bw = 2f;
        if (selected)
        {
            border = Color.Lerp(accent, Color.white, 0.25f);
            fill = Color.Lerp(fill, accent, 0.10f);
            bw = 3.5f;
            SkImage(panel, "ui_glow", new Color(accent.r, accent.g, accent.b, 0.80f), "SelectedGlow",
                new Vector2(0f, 1f), new Vector2(0f, -(cardY - 16f)), new Vector2(SkBranchW, SkCardH + 32f), false);
        }

        var card = UIKit.Frame(panel, "Skill_" + node.id, fill, border, bw);
        UIFactory.Place(card, new Vector2(0f, 1f), new Vector2(SkCardX, -cardY), new Vector2(SkCardW, SkCardH));

        // Area clicavel (invisivel) + botao sem transicao de cor; UIButtonFx cuida do hover.
        var hit = card.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        hit.raycastTarget = true;
        var btn = card.gameObject.AddComponent<Button>();
        btn.targetGraphic = hit;
        btn.transition = Selectable.Transition.None;
        string id = node.id;
        btn.onClick.AddListener(() => { ZombitAudio.PlayUI(); ShowMapSkills(id); });
        card.gameObject.AddComponent<UIButtonFx>();

        Color nameCol = unlocked ? SkGreenText : locked ? new Color(0.66f, 0.72f, 0.76f, 1f) : Color.white;
        Color descCol = locked ? new Color(0.58f, 0.65f, 0.69f, 1f) : new Color(0.84f, 0.89f, 0.91f, 1f);

        // Moldura do icone.
        Color iconFill = locked ? new Color(0.035f, 0.048f, 0.058f, 1f) : Color.Lerp(new Color(0.035f, 0.048f, 0.058f, 1f), accent, 0.20f);
        Color iconBorder = unlocked ? new Color(0.25f, 0.85f, 0.50f, 0.95f) : locked ? SkLockedLine : new Color(accent.r, accent.g, accent.b, 0.85f);
        var iconFrame = UIKit.Frame(card, "IconFrame", iconFill, iconBorder, 2f);
        UIFactory.Place(iconFrame, new Vector2(0f, 1f), new Vector2(16f, -18f), new Vector2(100f, 100f));
        Color iconTint = locked ? new Color(0.50f, 0.54f, 0.60f, 0.85f) : Color.white;
        SkImage(iconFrame, "icon_" + SkIconKey(survivor.id, node), iconTint, "SkillIcon",
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(76f, 76f));

        // Selo de "desbloqueada" no canto do icone e estrela de "selecionada".
        if (unlocked)
        {
            var seal = SkBadge(iconFrame, "SealUnlocked", SkGreen, new Color(0.03f, 0.18f, 0.11f, 1f), 30f, "ui_check", SkGreen, 16f);
            UIFactory.Place(seal, new Vector2(1f, 0f), new Vector2(9f, -9f), new Vector2(30f, 30f));
        }
        if (selected)
        {
            var star = SkBadge(iconFrame, "SelectedStar", accent, new Color(0.04f, 0.05f, 0.06f, 1f), 28f, null, SkGoldCol, 14f);
            var st = UIFactory.Img(star, SkGoldCol, "Star");
            st.sprite = UIKit.Star; st.type = Image.Type.Simple; st.raycastTarget = false;
            UIFactory.Place(st.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 16f));
            UIFactory.Place(star, new Vector2(1f, 1f), new Vector2(9f, 9f), new Vector2(28f, 28f));
        }

        // Selo do tier (01/02/03) sobre o canto do icone.
        var tier = UIKit.Frame(card, "TierBadge", new Color(0.03f, 0.04f, 0.05f, 1f), iconBorder, 1.5f);
        UIFactory.Place(tier, new Vector2(0f, 1f), new Vector2(7f, -7f), new Vector2(40f, 26f));
        var tt = UIFactory.Txt(tier, node.tier.ToString("00"), 15f, TextAlignmentOptions.Center,
            unlocked ? SkGreenText : locked ? new Color(0.62f, 0.68f, 0.72f, 1f) : accent, "TierNumber");
        tt.fontStyle = FontStyles.Bold;
        UIFactory.Stretch(tt.rectTransform);

        // Nome, ramo + nivel, descricao.
        var nameText = MapText(card, node.displayName, 22, TextAlignmentOptions.MidlineLeft, nameCol, 130, 14, 254, 34, "SkillName");
        nameText.fontStyle = FontStyles.Bold;
        SkFit(nameText, 15f);

        string accHex = ColorUtility.ToHtmlStringRGB(locked ? new Color(accent.r * 0.75f, accent.g * 0.75f, accent.b * 0.75f) : accent);
        var meta = MapText(card, "<color=#" + accHex + ">" + node.branchName.ToUpperInvariant() + "</color>  ·  REQUER NV. " + node.requiredLevel,
            13, TextAlignmentOptions.MidlineLeft, SkDim, 130, 50, 254, 22, "SkillMeta");
        meta.richText = true;
        meta.characterSpacing = 0.3f;
        SkFit(meta, 11f);

        var desc = MapText(card, node.description, 17, TextAlignmentOptions.TopLeft, descCol, 130, 78, 254, 64, "SkillDescription");
        desc.textWrappingMode = TextWrappingModes.Normal;

        var div = UIFactory.Img(card, new Color(border.r, border.g, border.b, 0.55f), "FooterDivider");
        UIFactory.Place(div.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -152f), new Vector2(SkCardW - 32f, 1.5f));
        div.raycastTarget = false;

        // Rodape: icone + estado, centralizados.
        var footer = UIFactory.Node("Footer", card);
        UIFactory.Place(footer, new Vector2(0f, 1f), new Vector2(0f, -154f), new Vector2(SkCardW, 50f));
        UIFactory.HLayout(footer, 8f, TextAnchor.MiddleCenter);
        string footerText; Color footerCol; string footerGlyph; Color glyphCol;
        switch (state)
        {
            case SkState.Unlocked:
                footerText = "DESBLOQUEADA"; footerCol = SkGreenText; footerGlyph = "ui_check"; glyphCol = SkGreen; break;
            case SkState.Available:
                footerText = "DISPONÍVEL  ·  " + node.pointCost + (node.pointCost == 1 ? " PONTO" : " PONTOS");
                footerCol = SkGoldCol; footerGlyph = "ui_points"; glyphCol = Color.white; break;
            case SkState.NoPoints:
                footerText = "SEM PONTOS  ·  PRECISA DE " + node.pointCost;
                footerCol = new Color(0.86f, 0.62f, 0.30f, 1f); footerGlyph = "ui_points"; glyphCol = new Color(1f, 1f, 1f, 0.55f); break;
            default:
                footerText = "BLOQUEADA  ·  " + (!prereqMet ? "HABILIDADE ANTERIOR" : "REQUER NV. " + node.requiredLevel);
                footerCol = new Color(0.58f, 0.66f, 0.70f, 1f); footerGlyph = "ui_lock"; glyphCol = new Color(0.58f, 0.66f, 0.70f, 1f); break;
        }
        SkGlyph(footer, footerGlyph, glyphCol, 22f, "FooterGlyph");
        var ft = UIFactory.Txt(footer, footerText, UIKit.Size.Caption, TextAlignmentOptions.Center, footerCol, "SkillState");
        ft.fontStyle = FontStyles.Bold;
        ft.textWrappingMode = TextWrappingModes.NoWrap;
        ft.characterSpacing = 0.5f;
    }

    // ---------- painel de detalhes ----------
    void SkBuildDetail(RectTransform s, SurvivorDef survivor, IReadOnlyList<SkillTreeNode> nodes,
        SurvivorProgress progress, Color[] branchAccents)
    {
        float detailX = SkLeft + 3 * (SkBranchW + SkBranchGap);
        float detailW = 1920f - SkLeft - detailX;
        SkillTreeNode node = SkillTreeSystem.GetNode(survivor.id, selectedSkillId);
        Color accent = node != null ? branchAccents[Mathf.Clamp(node.branchIndex, 0, 2)] : UIKit.Col.Border;

        var panel = UIKit.Frame(s, "SelectedSkillPanel", new Color(0.028f, 0.042f, 0.052f, 0.97f),
            new Color(accent.r, accent.g, accent.b, 0.55f), 2f);
        UIFactory.Place(panel, new Vector2(0f, 1f), new Vector2(detailX, -SkTop), new Vector2(detailW, SkPanelH));

        if (node == null)
        {
            MapText(panel, "NENHUMA HABILIDADE", 24, TextAlignmentOptions.Center, Color.white, 32, 380, detailW - 64f, 80, "NoSkill");
            return;
        }

        bool unlocked = SkillTreeSystem.IsUnlocked(progress, node.id);
        bool canUnlock = SkillTreeSystem.CanUnlock(progress, node, out string _);
        bool prereqMet = string.IsNullOrEmpty(node.prerequisiteId) || SkillTreeSystem.IsUnlocked(progress, node.prerequisiteId);
        bool levelMet = progress.level >= node.requiredLevel;
        bool locked = !unlocked && !canUnlock && !(prereqMet && levelMet);
        float inner = detailW - 32f;   // largura dos blocos internos (margem de 16)

        // Titulo da secao.
        var cap = MapText(panel, "DETALHES DA HABILIDADE", 14, TextAlignmentOptions.MidlineLeft, SkDim, 22, 14, 400, 26, "DetailCaption");
        cap.fontStyle = FontStyles.Bold;
        cap.characterSpacing = 2f;
        var capLine = UIFactory.Img(panel, new Color(accent.r, accent.g, accent.b, 0.35f), "DetailCaptionLine");
        UIFactory.Place(capLine.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -46f), new Vector2(inner, 1.5f));
        capLine.raycastTarget = false;

        // Banner: brilho da cor do ramo + icone grande (recortado na moldura).
        var banner = UIKit.Frame(panel, "SkillBanner", new Color(0.020f, 0.030f, 0.040f, 1f), new Color(accent.r, accent.g, accent.b, 0.60f), 2f);
        UIFactory.Place(banner, new Vector2(0f, 1f), new Vector2(16f, -60f), new Vector2(inner, 214f));
        var clip = UIFactory.Node("BannerClip", banner);
        UIFactory.Stretch(clip);
        UIFactory.Inset(clip, 2f, 2f);
        clip.gameObject.AddComponent<RectMask2D>();
        SkImage(clip, "ui_glow", new Color(accent.r, accent.g, accent.b, 0.55f), "BannerGlow",
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(inner + 40f, 340f), false);
        SkImage(clip, "ui_glow", new Color(1f, 1f, 1f, 0.10f), "BannerCore",
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260f, 200f), false);
        SkImage(clip, "icon_" + SkIconKey(survivor.id, node), unlocked || !locked ? Color.white : new Color(0.55f, 0.58f, 0.64f, 0.9f),
            "BannerIcon", new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(168f, 168f));

        // Nome.
        var nm = MapText(panel, node.displayName, 34, TextAlignmentOptions.MidlineLeft, UIKit.Col.Gold, 22, 288, inner - 12f, 50, "SkillDetailName");
        nm.fontStyle = FontStyles.Bold;
        SkFit(nm, 22f);

        // Chip do ramo + nivel.
        string chipText = node.branchName.ToUpperInvariant();
        float chipW = Mathf.Clamp(28f + chipText.Length * 10.5f, 110f, 330f);
        var chip = UIKit.Frame(panel, "BranchChip", new Color(accent.r * 0.25f, accent.g * 0.25f, accent.b * 0.25f, 1f),
            new Color(accent.r, accent.g, accent.b, 0.90f), 1.5f);
        UIFactory.Place(chip, new Vector2(0f, 1f), new Vector2(22f, -346f), new Vector2(chipW, 34f));
        var ct = UIFactory.Txt(chip, chipText, UIKit.Size.Caption, TextAlignmentOptions.Center, accent, "BranchChipText");
        ct.fontStyle = FontStyles.Bold;
        UIFactory.Stretch(ct.rectTransform);
        SkFit(ct, 11f);
        MapText(panel, "NÍVEL " + node.requiredLevel, 17, TextAlignmentOptions.MidlineLeft, SkDim, 22 + chipW + 14f, 346, 160, 34, "SkillDetailLevel").fontStyle = FontStyles.Bold;

        // Descricao.
        var desc = MapText(panel, node.description, 23, TextAlignmentOptions.TopLeft, Color.white, 22, 396, inner - 12f, 96, "SkillDetailDescription");
        desc.textWrappingMode = TextWrappingModes.Normal;

        // Proxima habilidade do ramo.
        SkillTreeNode next = null;
        for (int j = 0; j < nodes.Count; j++)
            if (nodes[j].prerequisiteId == node.id) { next = nodes[j]; break; }
        var nextBox = UIKit.Frame(panel, "NextSkillBox", new Color(0.040f, 0.056f, 0.066f, 1f), new Color(0.20f, 0.27f, 0.31f, 1f), 1.5f);
        UIFactory.Place(nextBox, new Vector2(0f, 1f), new Vector2(16f, -500f), new Vector2(inner, 100f));
        SkImage(nextBox, "ui_chevrons", accent, "NextChevrons", new Vector2(0f, 1f), new Vector2(14f, -11f), new Vector2(22f, 22f));
        var nl = MapText(nextBox, "PRÓXIMA NO RAMO", 14, TextAlignmentOptions.MidlineLeft, accent, 44, 8, 300, 26, "NextLabel");
        nl.fontStyle = FontStyles.Bold;
        nl.characterSpacing = 1.5f;
        if (next != null)
        {
            var nn = MapText(nextBox, next.displayName + "  ·  NV. " + next.requiredLevel, 19, TextAlignmentOptions.MidlineLeft, Color.white, 16, 36, inner - 32f, 28, "NextName");
            nn.fontStyle = FontStyles.Bold;
            SkFit(nn, 14f);
            var nd = MapText(nextBox, next.description, 16, TextAlignmentOptions.MidlineLeft, SkDim, 16, 64, inner - 32f, 28, "NextDescription");
            SkFit(nd, 12f);
        }
        else
        {
            MapText(nextBox, "Última habilidade deste ramo.", 17, TextAlignmentOptions.MidlineLeft, SkDim, 16, 40, inner - 32f, 30, "NextNone");
        }

        // Requisitos.
        var rq = MapText(panel, "REQUISITOS", 14, TextAlignmentOptions.MidlineLeft, SkDim, 22, 614, 300, 24, "ReqLabel");
        rq.fontStyle = FontStyles.Bold;
        rq.characterSpacing = 2f;
        float rowY = 644f;
        Color okCol = new Color(0.80f, 0.88f, 0.84f, 1f), badCol = new Color(0.96f, 0.60f, 0.52f, 1f);
        SkRequirement(panel, levelMet, "Nível do personagem: <b>" + node.requiredLevel + "</b>" + (levelMet ? "" : "   (atual: " + progress.level + ")"),
            rowY, okCol, badCol, "ReqLevel");
        rowY += 36f;
        if (!string.IsNullOrEmpty(node.prerequisiteId))
        {
            var prev = SkillTreeSystem.GetNode(survivor.id, node.prerequisiteId);
            SkRequirement(panel, prereqMet, "Habilidade anterior: <b>" + (prev != null ? prev.displayName : "?") + "</b>",
                rowY, okCol, badCol, "ReqPrevious");
        }

        // Custo e pontos disponiveis.
        var costLine = UIFactory.Img(panel, new Color(accent.r, accent.g, accent.b, 0.30f), "CostLine");
        UIFactory.Place(costLine.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -722f), new Vector2(inner, 1.5f));
        costLine.raycastTarget = false;
        SkImage(panel, "ui_points", Color.white, "CostIcon", new Vector2(0f, 1f), new Vector2(22f, -734f), new Vector2(38f, 38f));
        MapText(panel, "CUSTO", 13, TextAlignmentOptions.MidlineLeft, SkDim, 72, 730, 200, 18, "CostLabel").fontStyle = FontStyles.Bold;
        var cv = MapText(panel, node.pointCost + (node.pointCost == 1 ? " PONTO" : " PONTOS") + " DE HABILIDADE", 20,
            TextAlignmentOptions.MidlineLeft, SkGoldCol, 72, 748, 280, 28, "CostValue");
        cv.fontStyle = FontStyles.Bold;
        SkFit(cv, 14f);
        MapText(panel, "SEUS PONTOS", 13, TextAlignmentOptions.MidlineRight, SkDim, detailW - 16f - 160f, 730, 160, 18, "PointsHaveLabel").fontStyle = FontStyles.Bold;
        bool enough = progress.skillPoints >= node.pointCost;
        MapText(panel, progress.skillPoints.ToString(), 26, TextAlignmentOptions.MidlineRight, enough ? SkGreenText : badCol,
            detailW - 16f - 160f, 746, 160, 32, "PointsHaveValue").fontStyle = FontStyles.Bold;

        // Botao / estado.
        float by = 790f, bh = 44f;
        if (unlocked)
        {
            SkStatusBlock(panel, by, bh, inner, new Color(0.06f, 0.22f, 0.14f, 1f), new Color(0.25f, 0.85f, 0.50f, 0.95f),
                "ui_check", SkGreen, "HABILIDADE DESBLOQUEADA", SkGreenText, "SkillAlreadyUnlocked");
        }
        else if (canUnlock)
        {
            string unlockId = node.id;
            var b = MapButton(panel, "DESBLOQUEAR HABILIDADE", 20, () => UnlockMapSkill(unlockId), 16, by, inner, bh,
                new Color(1f, 0.70f, 0.14f, 1f), "UnlockSkillButton");
            var lbl = b.GetComponentInChildren<TMP_Text>();
            if (lbl)
            {
                lbl.color = new Color(0.10f, 0.06f, 0.02f, 1f);
                lbl.fontStyle = FontStyles.Bold;
                lbl.characterSpacing = 2f;
            }
            SkImage(b.transform, "ui_points", Color.white, "UnlockGlyph", new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(30f, 30f));
        }
        else
        {
            string why;
            if (!prereqMet)
            {
                var prev = SkillTreeSystem.GetNode(survivor.id, node.prerequisiteId);
                why = "REQUER: " + (prev != null ? prev.displayName.ToUpperInvariant() : "HABILIDADE ANTERIOR");
            }
            else if (!levelMet) why = "REQUER NÍVEL " + node.requiredLevel;
            else why = "PONTOS INSUFICIENTES";
            SkStatusBlock(panel, by, bh, inner, new Color(0.065f, 0.078f, 0.088f, 1f), SkLockedLine,
                locked ? "ui_lock" : "ui_points", new Color(0.62f, 0.69f, 0.73f, 1f), why, new Color(0.70f, 0.77f, 0.81f, 1f), "SkillLockedStatus");
        }
    }

    void SkRequirement(RectTransform panel, bool met, string richText, float y, Color okCol, Color badCol, string name)
    {
        SkImage(panel, met ? "ui_check" : "ui_cross", met ? SkGreen : new Color(0.95f, 0.38f, 0.32f, 1f), name + "Mark",
            new Vector2(0f, 1f), new Vector2(24f, -(y + 5f)), new Vector2(22f, 22f));
        var t = MapText(panel, richText, 18, TextAlignmentOptions.MidlineLeft, met ? okCol : badCol, 56, y, 440, 32, name);
        t.richText = true;
        SkFit(t, 13f);
    }

    void SkStatusBlock(RectTransform panel, float y, float h, float width, Color fill, Color border,
        string glyph, Color glyphCol, string text, Color textCol, string name)
    {
        var box = UIKit.Frame(panel, name, fill, border, 2f);
        UIFactory.Place(box, new Vector2(0f, 1f), new Vector2(16f, -y), new Vector2(width, h));
        var row = UIFactory.Node("Row", box);
        UIFactory.Stretch(row);
        UIFactory.HLayout(row, 10f, TextAnchor.MiddleCenter);
        SkGlyph(row, glyph, glyphCol, 24f, "StatusGlyph");
        var t = UIFactory.Txt(row, text, 19f, TextAlignmentOptions.Center, textCol, "StatusText");
        t.fontStyle = FontStyles.Bold;
        t.characterSpacing = 1.5f;
        t.textWrappingMode = TextWrappingModes.NoWrap;
    }

    // ---------- legenda ----------
    void SkBuildLegend(RectTransform s)
    {
        float w = 1920f - SkLeft * 2f;
        var bar = UIKit.Frame(s, "SkillLegend", new Color(0.020f, 0.030f, 0.040f, 0.78f), new Color(0.16f, 0.22f, 0.26f, 1f), 1.5f);
        UIFactory.Place(bar, new Vector2(0f, 1f), new Vector2(SkLeft, -(SkTop + SkPanelH + 14f)), new Vector2(w, 52f));

        string[] labels = { "Habilidade desbloqueada", "Disponível para desbloquear", "Bloqueada (requer anterior)", "Habilidade selecionada" };
        float cell = w / 4f;
        for (int i = 0; i < 4; i++)
        {
            var item = UIFactory.Node("LegendItem_" + i, bar);
            UIFactory.Place(item, new Vector2(0f, 0.5f), new Vector2(i * cell, 0f), new Vector2(cell, 52f));
            UIFactory.HLayout(item, 12f, TextAnchor.MiddleCenter);

            RectTransform mark;
            if (i == 0)
                mark = SkBadge(item, "LegendUnlocked", SkGreen, new Color(0.03f, 0.18f, 0.11f, 1f), 28f, "ui_check", SkGreen, 16f);
            else if (i == 1)
            {
                mark = SkBadge(item, "LegendAvailable", SkGoldCol, new Color(0.07f, 0.055f, 0.03f, 1f), 28f, null, SkGoldCol, 0f);
                UIKit.Diamond(mark, SkGoldCol, 18f, "AvailableDiamond");
            }
            else if (i == 2)
                mark = SkBadge(item, "LegendLocked", new Color(0.55f, 0.62f, 0.66f, 1f), new Color(0.05f, 0.065f, 0.075f, 1f), 28f, "ui_lock", new Color(0.62f, 0.69f, 0.73f, 1f), 15f);
            else
            {
                mark = SkBadge(item, "LegendSelected", SkGoldCol, new Color(0.07f, 0.055f, 0.03f, 1f), 28f, null, SkGoldCol, 0f);
                var st = UIFactory.Img(mark, SkGoldCol, "Star");
                st.sprite = UIKit.Star; st.type = Image.Type.Simple; st.raycastTarget = false;
                UIFactory.Place(st.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 16f));
            }

            var t = UIFactory.Txt(item, labels[i], 17f, TextAlignmentOptions.MidlineLeft, new Color(0.74f, 0.80f, 0.84f, 1f), "LegendText");
            t.textWrappingMode = TextWrappingModes.NoWrap;
        }
    }

    // ---------- pecas pequenas ----------
    /// <summary>Chave do icone de uma habilidade, escolhida pelo bonus principal dela.</summary>
    static string SkIconKey(string survivorId, SkillTreeNode node)
    {
        var b = node != null ? node.bonuses : null;
        if (b == null) return "star_up";
        if (b.grenadeRadiusPercent > 0f) return "explosion";
        if (b.abilityPowerPercent > 0f) return survivorId == "engineer" ? "chip" : "grenade";
        if (b.turretFireRatePercent > 0f) return "gear";
        if (b.abilityCooldownReduction > 0f) return "clock";
        if (b.healingPercent > 0f) return "medcross";
        if (b.critDamagePercent > 0f) return "skull";
        if (b.critChance > 0f) return "crosshair";
        if (b.accuracyBonus > 0f) return "bullseye";
        if (b.rangePercent > 0f) return "scope";
        if (b.fireRatePercent > 0f) return "bolt";
        if (b.damagePercent > 0f) return "bullets";
        if (b.goldPercent > 0f) return "coins";
        if (b.dropPercent > 0f) return "backpack";
        if (b.xpPercent > 0f) return "star_up";
        if (b.healthPercent > 0f) return "heart";
        if (b.defenseFlat > 0f) return "shield";
        return "star_up";
    }

    /// <summary>Imagem posicionada (ancora = pivo). Retorna null se o PNG nao existir.</summary>
    static Image SkImage(Transform parent, string spriteName, Color tint, string name,
        Vector2 anchor, Vector2 pos, Vector2 size, bool keepAspect = true)
    {
        var sp = ArtLoader.Get(SkArt + spriteName);
        if (sp == null) return null;
        var img = UIFactory.Img(parent, tint, name);
        img.sprite = sp;
        img.type = Image.Type.Simple;
        img.preserveAspect = keepAspect;
        img.raycastTarget = false;
        UIFactory.Place(img.rectTransform, anchor, pos, size);
        return img;
    }

    /// <summary>Imagem para dentro de um layout group (tamanho fixo). Retorna null se o PNG nao existir.</summary>
    static Image SkGlyph(Transform parent, string spriteName, Color tint, float size, string name)
    {
        var sp = ArtLoader.Get(SkArt + spriteName);
        if (sp == null) return null;
        var img = UIFactory.Img(parent, tint, name);
        img.sprite = sp;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
        img.raycastTarget = false;
        UIFactory.Elem(img.gameObject, size, size);
        return img;
    }

    /// <summary>Selo redondo: anel colorido + disco escuro + glifo opcional. Entra em layout (tamanho fixo).</summary>
    static RectTransform SkBadge(Transform parent, string name, Color ring, Color disc, float size, string glyph, Color glyphTint, float glyphSize)
    {
        var root = UIFactory.Node(name, parent);
        UIFactory.Elem(root.gameObject, size, size);
        root.sizeDelta = new Vector2(size, size);
        var r = UIFactory.Img(root, ring, "Ring");
        UIKit.ApplyCircle(r, ring);
        UIFactory.Stretch(r.rectTransform);
        r.raycastTarget = false;
        var d = UIFactory.Img(root, disc, "Disc");
        UIKit.ApplyCircle(d, disc);
        UIFactory.Stretch(d.rectTransform);
        UIFactory.Inset(d.rectTransform, 3f, 3f);
        d.raycastTarget = false;
        if (!string.IsNullOrEmpty(glyph))
            SkImage(root, glyph, glyphTint, "Glyph", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(glyphSize, glyphSize));
        return root;
    }

    /// <summary>Texto esticado na largura do pai, com margens esquerda/direita.</summary>
    static TMP_Text SkLabel(Transform parent, string value, float size, TextAlignmentOptions al, Color col,
        float leftPad, float rightPad, string name)
    {
        var t = UIFactory.Txt(parent, value, size, al, col, name);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        UIFactory.Stretch(t.rectTransform);
        t.rectTransform.offsetMin = new Vector2(leftPad, 0f);
        t.rectTransform.offsetMax = new Vector2(-rightPad, 0f);
        return t;
    }

    /// <summary>Uma linha so; se o texto nao couber, diminui a fonte ate o minimo (depois corta).</summary>
    static TMP_Text SkFit(TMP_Text t, float min)
    {
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.enableAutoSizing = true;
        t.fontSizeMax = t.fontSize;
        t.fontSizeMin = min;
        t.overflowMode = TextOverflowModes.Truncate;   // a fonte padrao nao tem o glifo de reticencias
        return t;
    }

    void UnlockMapSkill(string nodeId)
    {
        var save = GameSession.Save;
        if (save == null) return;
        if (SkillTreeSystem.TryUnlock(save, SkillsSurvivorId(save), nodeId, out string message))
        {
            if (!GameSession.DebugRun) SaveSystem.Save(save);
            Toast(message);
            ShowMapSkills(nodeId);
        }
        else Toast(message);
    }
}
