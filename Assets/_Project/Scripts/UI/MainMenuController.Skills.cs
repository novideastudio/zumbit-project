using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Arvore de habilidades no mapa (parte de MainMenuController).
// Layout (referencia 1920x1080): 3 paineis de ramo (6 habilidades cada) + painel de detalhes (especializacao, graus, evoluir) + legenda.
// Os icones ficam em Resources/Art/Skills (icon_*.png / ui_*.png). Se um PNG nao existir, a tela continua funcionando.
public partial class MainMenuController
{
    enum SkState { Maxed, Ranked, Available, NoPoints, Locked }

    const string SkArt = "Art/Skills/";

    // Geometria da tela (pixels da resolucao de referencia).
    const float SkTop = 108f;
    const float SkPanelH = 846f;
    const float SkBranchW = 430f;
    const float SkBranchGap = 14f;
    const float SkLeft = 24f;
    const float SkCardX = 16f;
    const float SkCardW = 398f;
    const float SkCardH = 112f;
    const float SkCardY0 = 104f;      // y do primeiro cartao dentro do painel do ramo
    const float SkCardStep = 122f;    // 6 cartoes cabem em 846 px

    static readonly Color SkPanelFill = new Color(0.025f, 0.040f, 0.052f, 0.96f);
    static readonly Color SkGreen = new Color(0.30f, 0.90f, 0.52f, 1f);
    static readonly Color SkGreenText = new Color(0.45f, 0.95f, 0.62f, 1f);
    static readonly Color SkGoldCol = new Color(1f, 0.72f, 0.20f, 1f);
    static readonly Color SkDim = new Color(0.60f, 0.68f, 0.72f, 1f);
    static readonly Color SkLockedLine = new Color(0.20f, 0.27f, 0.31f, 1f);

    bool skResetArmed;   // 1o clique em REDISTRIBUIR arma a confirmacao

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
        if (invSurvivorId != survivor.id) skResetArmed = false;
        invSurvivorId = survivor.id;   // Mochila e Habilidades editam o mesmo sobrevivente
        var progress = save.GetProgress(survivor.id);
        var nodes = SkillTreeSystem.GetNodes(survivor.id);
        if (string.IsNullOrEmpty(nodeId)) nodeId = selectedSkillId;
        if (!string.IsNullOrEmpty(nodeId) && SkillTreeSystem.GetNode(survivor.id, nodeId) == null) nodeId = null;
        if (string.IsNullOrEmpty(nodeId) && nodes.Count > 0)
        {
            // Sem selecao: abre na primeira habilidade que da para evoluir (senao, na primeira).
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
        // Six-rank screen owns its geometry; do not apply legacy three-node layout.
        Show("mapSkills");
        FixSkillScreenPresentation(s);
        ApplyPcSkillTheme(s, survivor, progress, nodes, branchAccents);
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
            skResetArmed = false;
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
        int ranksDone = 0, ranksMax = 0;
        for (int j = 0; j < nodes.Count; j++)
        {
            if (nodes[j].branchIndex != branch) continue;
            if (first == null) { first = nodes[j]; branchName = nodes[j].branchName.ToUpperInvariant(); }
            ranksMax += SkillTreeSystem.MaxRank;
            ranksDone += SkillTreeSystem.GetRank(progress, nodes[j].id);
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
        MapText(panel, "GRAUS  " + ranksDone + " / " + ranksMax, UIKit.Size.Caption,
            TextAlignmentOptions.MidlineLeft, SkDim, 102, 58, 300, 22, "BranchProgress");

        // Barra de progresso do ramo (graus comprados / total).
        var track = UIFactory.Img(panel, new Color(0.20f, 0.26f, 0.30f, 1f), "BranchBarTrack");
        UIFactory.Place(track.rectTransform, new Vector2(0f, 1f), new Vector2(102f, -84f), new Vector2(304f, 7f));
        track.raycastTarget = false;
        float ratio = ranksMax > 0 ? (float)ranksDone / ranksMax : 0f;
        var fillBar = UIFactory.Img(panel, accent, "BranchBarFill");
        UIFactory.Place(fillBar.rectTransform, new Vector2(0f, 1f), new Vector2(102f, -84f), new Vector2(Mathf.Max(1f, 304f * ratio), 7f));
        fillBar.raycastTarget = false;

        for (int j = 0; j < nodes.Count; j++)
        {
            if (nodes[j].branchIndex != branch) continue;
            SkBuildCard(panel, nodes[j], accent, survivor, progress);
        }
    }

    // ---------- cartao de habilidade ----------
    SkState SkStateOf(SkillTreeNode node, SurvivorProgress progress, out int rank, out bool prereqMet, out bool levelMet)
    {
        rank = SkillTreeSystem.GetRank(progress, node.id);
        prereqMet = string.IsNullOrEmpty(node.prerequisiteId) || SkillTreeSystem.IsUnlocked(progress, node.prerequisiteId);
        levelMet = rank >= SkillTreeSystem.MaxRank || progress.level >= SkillTreeSystem.RequiredLevelFor(node, rank + 1);
        if (rank >= SkillTreeSystem.MaxRank) return SkState.Maxed;
        if (SkillTreeSystem.CanUnlock(progress, node, out string _)) return SkState.Available;
        if (prereqMet && levelMet) return SkState.NoPoints;
        return rank > 0 ? SkState.Ranked : SkState.Locked;
    }

    void SkBuildCard(RectTransform panel, SkillTreeNode node, Color accent, SurvivorDef survivor, SurvivorProgress progress)
    {
        var state = SkStateOf(node, progress, out int rank, out bool prereqMet, out bool levelMet);
        bool selected = node.id == selectedSkillId;
        bool locked = state == SkState.Locked;
        Color gold = new Color(1f, 0.78f, 0.28f, 1f);

        Color fill, border;
        switch (state)
        {
            case SkState.Maxed:
                fill = new Color(0.12f, 0.09f, 0.03f, 1f); border = gold; break;
            case SkState.Ranked:
                fill = new Color(0.040f, 0.155f, 0.115f, 1f); border = new Color(0.25f, 0.85f, 0.50f, 0.95f); break;
            case SkState.Available:
                fill = Color.Lerp(new Color(0.045f, 0.058f, 0.068f, 1f), accent, 0.14f); border = new Color(accent.r, accent.g, accent.b, 0.95f); break;
            case SkState.NoPoints:
                fill = Color.Lerp(new Color(0.045f, 0.058f, 0.068f, 1f), accent, 0.07f); border = new Color(accent.r, accent.g, accent.b, 0.55f); break;
            default:
                fill = new Color(0.030f, 0.042f, 0.052f, 0.98f); border = SkLockedLine; break;
        }
        // Ranked com grau mas sem poder evoluir agora continua verde; Available com grau tambem.
        if (rank > 0 && state == SkState.Available) { fill = Color.Lerp(new Color(0.040f, 0.155f, 0.115f, 1f), accent, 0.12f); }

        float cardY = SkCardY0 + (node.tier - 1) * SkCardStep;
        float bw = 2f;
        if (selected)
        {
            border = Color.Lerp(border, Color.white, 0.30f);
            fill = Color.Lerp(fill, accent, 0.10f);
            bw = 3.5f;
            SkImage(panel, "ui_glow", new Color(accent.r, accent.g, accent.b, 0.70f), "SelectedGlow",
                new Vector2(0f, 1f), new Vector2(0f, -(cardY - 12f)), new Vector2(SkBranchW, SkCardH + 24f), false);
        }

        var card = UIKit.Frame(panel, "Skill_" + node.id, fill, border, bw);
        UIFactory.Place(card, new Vector2(0f, 1f), new Vector2(SkCardX, -cardY), new Vector2(SkCardW, SkCardH));

        var hit = card.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        hit.raycastTarget = true;
        var btn = card.gameObject.AddComponent<Button>();
        btn.targetGraphic = hit;
        btn.transition = Selectable.Transition.None;
        string id = node.id;
        btn.onClick.AddListener(() => { ZombitAudio.PlayUI(); skResetArmed = false; ShowMapSkills(id); });
        card.gameObject.AddComponent<UIButtonFx>();

        Color nameCol = state == SkState.Maxed ? gold : rank > 0 ? SkGreenText : locked ? new Color(0.66f, 0.72f, 0.76f, 1f) : Color.white;

        // Icone.
        Color iconFill = locked ? new Color(0.035f, 0.048f, 0.058f, 1f) : Color.Lerp(new Color(0.035f, 0.048f, 0.058f, 1f), accent, 0.20f);
        var iconFrame = UIKit.Frame(card, "IconFrame", iconFill, border, 2f);
        UIFactory.Place(iconFrame, new Vector2(0f, 1f), new Vector2(12f, -16f), new Vector2(80f, 80f));
        Color iconTint = locked ? new Color(0.50f, 0.54f, 0.60f, 0.85f) : Color.white;
        SkImage(iconFrame, "icon_" + SkIconKey(survivor.id, node), iconTint, "SkillIcon",
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f));

        // Nome + grau.
        var nameText = MapText(card, node.displayName, 21, TextAlignmentOptions.MidlineLeft, nameCol, 104, 10, 232, 32, "SkillName");
        nameText.fontStyle = FontStyles.Bold;
        SkFit(nameText, 14f);

        string rankLine;
        Color rankCol;
        if (rank > 0) { rankLine = SkillTreeSystem.RankName(rank).ToUpperInvariant() + "  " + rank + "/" + SkillTreeSystem.MaxRank; rankCol = state == SkState.Maxed ? gold : SkGreenText; }
        else if (locked) { rankLine = (!prereqMet ? "REQUER A ANTERIOR" : "REQUER NV. " + SkillTreeSystem.RequiredLevelFor(node, 1)); rankCol = new Color(0.58f, 0.66f, 0.70f, 1f); }
        else { rankLine = "SEM GRAU  ·  PRÓXIMO: " + SkillTreeSystem.RankName(1).ToUpperInvariant(); rankCol = SkGoldCol; }
        var rl = MapText(card, rankLine, 14, TextAlignmentOptions.MidlineLeft, rankCol, 104, 44, 286, 22, "SkillRank");
        rl.fontStyle = FontStyles.Bold;
        SkFit(rl, 10f);

        // Seis segmentos: um por grau.
        for (int i = 0; i < SkillTreeSystem.MaxRank; i++)
        {
            bool got = i < rank;
            Color pip = got ? (state == SkState.Maxed ? gold : accent) : new Color(0.20f, 0.26f, 0.30f, 1f);
            var p = UIFactory.Img(card, pip, "RankPip_" + i);
            UIFactory.Place(p.rectTransform, new Vector2(0f, 1f), new Vector2(104f + i * 46f, -78f), new Vector2(40f, 9f));
            p.raycastTarget = false;
        }

        // Selo no canto: cadeado / pontos / estrela de grau maximo.
        string glyph = state == SkState.Maxed ? "ui_check" : locked ? "ui_lock" : state == SkState.Available ? "ui_points" : null;
        if (glyph != null)
        {
            Color gc = state == SkState.Maxed ? gold : locked ? new Color(0.58f, 0.66f, 0.70f, 1f) : Color.white;
            SkImage(card, glyph, gc, "StateGlyph", new Vector2(1f, 0f), new Vector2(-12f, 12f), new Vector2(26f, 26f));
            var gi = card.Find("StateGlyph");
            if (gi) { var rt = (RectTransform)gi; rt.pivot = new Vector2(1f, 0f); rt.anchoredPosition = new Vector2(-12f, 10f); }
        }
    }

    // ---------- painel de detalhes ----------
    void SkBuildDetail(RectTransform s, SurvivorDef survivor, IReadOnlyList<SkillTreeNode> nodes,
        SurvivorProgress progress, Color[] branchAccents)
    {
        float detailX = SkLeft + 3 * (SkBranchW + SkBranchGap);
        float detailW = 1920f - SkLeft - detailX;
        SkillTreeNode node = SkillTreeSystem.GetNode(survivor.id, selectedSkillId);
        Color accent = node != null ? branchAccents[Mathf.Clamp(node.branchIndex, 0, 2)] : UIKit.Col.Border;
        Color gold = new Color(1f, 0.78f, 0.28f, 1f);

        var panel = UIKit.Frame(s, "SelectedSkillPanel", new Color(0.028f, 0.042f, 0.052f, 0.97f),
            new Color(accent.r, accent.g, accent.b, 0.55f), 2f);
        UIFactory.Place(panel, new Vector2(0f, 1f), new Vector2(detailX, -SkTop), new Vector2(detailW, SkPanelH));
        float inner = detailW - 32f;

        SkBuildSpecialization(panel, survivor, progress, inner);

        if (node == null)
        {
            MapText(panel, "NENHUMA HABILIDADE", 24, TextAlignmentOptions.Center, Color.white, 32, 380, detailW - 64f, 80, "NoSkill");
            return;
        }

        var state = SkStateOf(node, progress, out int rank, out bool prereqMet, out bool levelMet);
        bool canUnlock = state == SkState.Available;
        bool maxed = state == SkState.Maxed;

        // Nome, ramo e grau atual.
        var line = UIFactory.Img(panel, new Color(accent.r, accent.g, accent.b, 0.35f), "SkillTopLine");
        UIFactory.Place(line.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -190f), new Vector2(inner, 1.5f));
        line.raycastTarget = false;

        var iconFrame = UIKit.Frame(panel, "DetailIconFrame", new Color(0.020f, 0.030f, 0.040f, 1f), new Color(accent.r, accent.g, accent.b, 0.70f), 2f);
        UIFactory.Place(iconFrame, new Vector2(0f, 1f), new Vector2(16f, -202f), new Vector2(84f, 84f));
        SkImage(iconFrame, "icon_" + SkIconKey(survivor.id, node), Color.white, "DetailIcon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 64f));

        var nm = MapText(panel, node.displayName, 30, TextAlignmentOptions.MidlineLeft, UIKit.Col.Gold, 112, 202, inner - 100f, 40, "SkillDetailName");
        nm.fontStyle = FontStyles.Bold;
        SkFit(nm, 18f);
        string chipText = node.branchName.ToUpperInvariant();
        MapText(panel, "<color=#" + ColorUtility.ToHtmlStringRGB(accent) + ">" + chipText + "</color>  ·  HABILIDADE " + node.tier + " DE 6", 14,
            TextAlignmentOptions.MidlineLeft, SkDim, 112, 244, inner - 100f, 22, "SkillDetailMeta").richText = true;
        string cur = rank > 0 ? SkillTreeSystem.RankName(rank).ToUpperInvariant() + "  (" + rank + "/" + SkillTreeSystem.MaxRank + ")" : "SEM GRAU";
        var curT = MapText(panel, "GRAU ATUAL:  " + cur, 16, TextAlignmentOptions.MidlineLeft, maxed ? gold : rank > 0 ? SkGreenText : SkDim, 112, 264, inner - 100f, 24, "SkillCurrentRank");
        curT.fontStyle = FontStyles.Bold;
        SkFit(curT, 11f);

        // Tabela dos 6 graus.
        float tableY = 304f;
        var th = MapText(panel, "GRAUS", 13, TextAlignmentOptions.MidlineLeft, SkDim, 22, tableY, 200, 20, "RanksLabel");
        th.fontStyle = FontStyles.Bold; th.characterSpacing = 2f;
        MapText(panel, "BÔNUS TOTAL", 12, TextAlignmentOptions.MidlineLeft, SkDim, 218, tableY, 160, 20, "RanksBonusLabel").fontStyle = FontStyles.Bold;
        MapText(panel, "NÍVEL", 12, TextAlignmentOptions.MidlineRight, SkDim, detailW - 16f - 100f, tableY, 100, 20, "RanksLevelLabel").fontStyle = FontStyles.Bold;
        for (int r = 1; r <= SkillTreeSystem.MaxRank; r++)
        {
            float y = tableY + 24f + (r - 1) * 34f;
            bool owned = r <= rank, next = r == rank + 1;
            Color c = owned ? SkGreenText : next ? SkGoldCol : new Color(0.55f, 0.62f, 0.66f, 1f);
            var row = UIFactory.Img(panel, owned ? new Color(0.05f, 0.16f, 0.11f, 0.9f) : next ? new Color(0.14f, 0.10f, 0.03f, 0.9f) : new Color(0.04f, 0.055f, 0.065f, 0.9f), "RankRow_" + r);
            UIFactory.Place(row.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -y), new Vector2(inner, 30f));
            row.raycastTarget = false;
            MapText(panel, (owned ? "+ " : next ? "> " : "· ") + SkillTreeSystem.RankName(r).ToUpperInvariant(), 16, TextAlignmentOptions.MidlineLeft, c, 24, y, 190, 30, "RankName_" + r).fontStyle = FontStyles.Bold;
            string total = SkillTreeSystem.LineFor(node.stat, node.bonuses.Get(node.stat) * r);
            var tv = MapText(panel, total, 15, TextAlignmentOptions.MidlineLeft, c, 218, y, 230, 30, "RankBonus_" + r);
            SkFit(tv, 10f);
            MapText(panel, "NV. " + SkillTreeSystem.RequiredLevelFor(node, r), 15, TextAlignmentOptions.MidlineRight, progress.level >= SkillTreeSystem.RequiredLevelFor(node, r) ? c : new Color(0.96f, 0.60f, 0.52f, 1f),
                detailW - 16f - 100f - 10f, y, 100, 30, "RankLevel_" + r);
        }

        // Requisitos do proximo grau.
        float reqY = tableY + 24f + 6 * 34f + 12f;   // ~544
        Color okCol = new Color(0.80f, 0.88f, 0.84f, 1f), badCol = new Color(0.96f, 0.60f, 0.52f, 1f);
        if (!maxed)
        {
            int need = SkillTreeSystem.RequiredLevelFor(node, rank + 1);
            SkRequirement(panel, progress.level >= need, "Nível do personagem: <b>" + need + "</b>" + (progress.level >= need ? "" : "   (atual: " + progress.level + ")"),
                reqY, okCol, badCol, "ReqLevel");
            if (!string.IsNullOrEmpty(node.prerequisiteId))
            {
                var prev = SkillTreeSystem.GetNode(survivor.id, node.prerequisiteId);
                SkRequirement(panel, prereqMet, "Habilidade anterior: <b>" + (prev != null ? prev.displayName : "?") + "</b> (grau 1)",
                    reqY + 32f, okCol, badCol, "ReqPrevious");
            }
            bool enough = progress.skillPoints >= node.pointCost;
            SkRequirement(panel, enough, "Pontos: <b>" + node.pointCost + "</b>   (você tem " + progress.skillPoints + ")", reqY + 64f, okCol, badCol, "ReqPoints");
        }
        else
        {
            MapText(panel, "Esta habilidade está no grau máximo.", 18, TextAlignmentOptions.MidlineLeft, gold, 22, reqY + 8f, inner - 12f, 30, "MaxedText");
        }

        // Botao principal.
        float by = 664f, bh = 52f;
        if (maxed)
        {
            SkStatusBlock(panel, by, bh, inner, new Color(0.14f, 0.10f, 0.03f, 1f), gold,
                "ui_check", gold, "GRAU MÁXIMO: " + SkillTreeSystem.RankName(SkillTreeSystem.MaxRank).ToUpperInvariant(), gold, "SkillMaxedStatus");
        }
        else if (canUnlock)
        {
            string unlockId = node.id;
            var b = MapButton(panel, "EVOLUIR PARA " + SkillTreeSystem.RankName(rank + 1).ToUpperInvariant(), 20, () => UnlockMapSkill(unlockId), 16, by, inner, bh,
                new Color(1f, 0.70f, 0.14f, 1f), "UnlockSkillButton");
            var lbl = b.GetComponentInChildren<TMP_Text>();
            if (lbl)
            {
                lbl.color = new Color(0.10f, 0.06f, 0.02f, 1f);
                lbl.fontStyle = FontStyles.Bold;
                lbl.characterSpacing = 2f;
            }
        }
        else
        {
            string why;
            if (!prereqMet) why = "REQUER A HABILIDADE ANTERIOR";
            else if (!levelMet) why = "REQUER NÍVEL " + SkillTreeSystem.RequiredLevelFor(node, rank + 1);
            else why = "PONTOS INSUFICIENTES";
            SkStatusBlock(panel, by, bh, inner, new Color(0.065f, 0.078f, 0.088f, 1f), SkLockedLine,
                state == SkState.Locked ? "ui_lock" : "ui_points", new Color(0.62f, 0.69f, 0.73f, 1f), why, new Color(0.70f, 0.77f, 0.81f, 1f), "SkillLockedStatus");
        }

        // Redistribuir pontos (1a vez gratis; depois custa ouro). Pede confirmacao com um segundo clique.
        int cost = SkillTreeSystem.ResetCost(progress);
        string resetLabel = skResetArmed ? "CONFIRMAR: DEVOLVER TODOS OS PONTOS" : (cost == 0 ? "REDISTRIBUIR PONTOS  (GRÁTIS 1ª VEZ)" : "REDISTRIBUIR PONTOS  (" + cost + " OURO)");
        var rb = MapButton(panel, resetLabel, 16, () =>
        {
            if (!skResetArmed) { skResetArmed = true; ShowMapSkills(selectedSkillId); return; }
            skResetArmed = false;
            ResetMapSkills();
        }, 16, 734f, inner, 44f, skResetArmed ? new Color(0.62f, 0.07f, 0.09f, 1f) : new Color(0.10f, 0.14f, 0.17f, 1f), "ResetSkillsButton");
        var rlb = rb.GetComponentInChildren<TMP_Text>();
        if (rlb) { rlb.fontStyle = FontStyles.Bold; SkFit(rlb, 11f); }
        MapText(panel, "Devolve os pontos gastos e a especialização. Os bônus da missão em andamento não mudam.", 12, TextAlignmentOptions.TopLeft, SkDim, 22, 786f, inner - 12f, 40, "ResetHint").textWrappingMode = TextWrappingModes.Normal;
    }

    /// <summary>Caixa no topo do painel de detalhes: especializacao (bloqueada, escolha ou escolhida).</summary>
    void SkBuildSpecialization(RectTransform panel, SurvivorDef survivor, SurvivorProgress progress, float inner)
    {
        var box = UIKit.Frame(panel, "SpecBox", new Color(0.040f, 0.056f, 0.066f, 1f), new Color(0.20f, 0.27f, 0.31f, 1f), 1.5f);
        UIFactory.Place(box, new Vector2(0f, 1f), new Vector2(16f, -14f), new Vector2(inner, 164f));
        var specs = SkillTreeSystem.GetSpecs(survivor.id);
        var chosen = SkillTreeSystem.GetSpec(progress);
        var cap = MapText(box, "ESPECIALIZAÇÃO", 13, TextAlignmentOptions.MidlineLeft, SkDim, 12, 6, 240, 20, "SpecCaption");
        cap.fontStyle = FontStyles.Bold; cap.characterSpacing = 2f;

        if (chosen != null)
        {
            var n = MapText(box, chosen.name.ToUpperInvariant(), 24, TextAlignmentOptions.MidlineLeft, UIKit.Col.Gold, 12, 28, inner - 24f, 32, "SpecName");
            n.fontStyle = FontStyles.Bold;
            var d = MapText(box, chosen.description, 14, TextAlignmentOptions.TopLeft, new Color(0.84f, 0.89f, 0.91f, 1f), 12, 62, inner - 24f, 40, "SpecDesc");
            d.textWrappingMode = TextWrappingModes.Normal;
            var e = MapText(box, SkillTreeSystem.Describe(chosen.bonuses, "  ·  "), 13, TextAlignmentOptions.TopLeft, SkGreenText, 12, 104, inner - 24f, 54, "SpecEffects");
            e.textWrappingMode = TextWrappingModes.Normal;
            return;
        }
        if (progress.level < SkillTreeSystem.SpecializationLevel)
        {
            MapText(box, "Disponível no nível " + SkillTreeSystem.SpecializationLevel + ".", 20, TextAlignmentOptions.MidlineLeft, Color.white, 12, 34, inner - 24f, 30, "SpecLocked").fontStyle = FontStyles.Bold;
            var names = new System.Text.StringBuilder("Escolha 1 de 2:  ");
            for (int i = 0; i < specs.Count; i++) names.Append(i > 0 ? "  ou  " : "").Append(specs[i].name);
            var t = MapText(box, names.ToString(), 15, TextAlignmentOptions.TopLeft, SkDim, 12, 72, inner - 24f, 70, "SpecPreview");
            t.textWrappingMode = TextWrappingModes.Normal;
            SkImage(box, "ui_lock", new Color(0.58f, 0.66f, 0.70f, 1f), "SpecLock", new Vector2(1f, 0f), new Vector2(-14f, 12f), new Vector2(30f, 30f));
            return;
        }

        // Escolha: dois cartoes lado a lado.
        float cw = (inner - 36f) / 2f;
        for (int i = 0; i < specs.Count && i < 2; i++)
        {
            var sp = specs[i];
            string spId = sp.id;
            var card = UIKit.Frame(box, "SpecCard_" + i, new Color(0.07f, 0.075f, 0.04f, 1f), new Color(1f, 0.72f, 0.20f, 0.9f), 2f);
            UIFactory.Place(card, new Vector2(0f, 1f), new Vector2(12f + i * (cw + 12f), -28f), new Vector2(cw, 128f));
            var hit = card.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            var btn = card.gameObject.AddComponent<Button>();
            btn.targetGraphic = hit; btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => { ZombitAudio.PlayUI(); ChooseMapSpecialization(spId); });
            card.gameObject.AddComponent<UIButtonFx>();
            var n = MapText(card, sp.name.ToUpperInvariant(), 16, TextAlignmentOptions.MidlineLeft, UIKit.Col.Gold, 8, 4, cw - 16f, 26, "SpecCardName");
            n.fontStyle = FontStyles.Bold; SkFit(n, 11f);
            var e = MapText(card, SkillTreeSystem.Describe(sp.bonuses, "\n"), 12, TextAlignmentOptions.TopLeft, new Color(0.84f, 0.89f, 0.91f, 1f), 8, 32, cw - 16f, 92, "SpecCardEffects");
            e.textWrappingMode = TextWrappingModes.Normal;
            e.enableAutoSizing = true; e.fontSizeMin = 9f; e.fontSizeMax = 12f;
        }
        MapText(box, "TOQUE PARA ESCOLHER", 11, TextAlignmentOptions.MidlineRight, SkDim, 12, 6, inner - 24f, 20, "SpecHint");
    }

    void SkRequirement(RectTransform panel, bool met, string richText, float y, Color okCol, Color badCol, string name)
    {
        SkImage(panel, met ? "ui_check" : "ui_cross", met ? SkGreen : new Color(0.95f, 0.38f, 0.32f, 1f), name + "Mark",
            new Vector2(0f, 1f), new Vector2(24f, -(y + 5f)), new Vector2(22f, 22f));
        var t = MapText(panel, richText, 17, TextAlignmentOptions.MidlineLeft, met ? okCol : badCol, 56, y, 460, 32, name);
        t.richText = true;
        SkFit(t, 12f);
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
        var t = UIFactory.Txt(row, text, 18f, TextAlignmentOptions.Center, textCol, "StatusText");
        t.fontStyle = FontStyles.Bold;
        t.characterSpacing = 1.2f;
        t.textWrappingMode = TextWrappingModes.NoWrap;
    }

    // ---------- legenda ----------
    void SkBuildLegend(RectTransform s)
    {
        float w = 1920f - SkLeft * 2f;
        var bar = UIKit.Frame(s, "SkillLegend", new Color(0.020f, 0.030f, 0.040f, 0.78f), new Color(0.16f, 0.22f, 0.26f, 1f), 1.5f);
        UIFactory.Place(bar, new Vector2(0f, 1f), new Vector2(SkLeft, -(SkTop + SkPanelH + 14f)), new Vector2(w, 52f));

        string[] labels = { "Com grau (evoluída)", "Pode evoluir agora", "Bloqueada (nível ou anterior)", "Grau máximo" };
        float cell = w / 4f;
        Color gold = new Color(1f, 0.78f, 0.28f, 1f);
        for (int i = 0; i < 4; i++)
        {
            var item = UIFactory.Node("LegendItem_" + i, bar);
            UIFactory.Place(item, new Vector2(0f, 0.5f), new Vector2(i * cell, 0f), new Vector2(cell, 52f));
            UIFactory.HLayout(item, 12f, TextAnchor.MiddleCenter);

            RectTransform mark;
            if (i == 0)
                mark = SkBadge(item, "LegendRanked", SkGreen, new Color(0.03f, 0.18f, 0.11f, 1f), 28f, "ui_check", SkGreen, 16f);
            else if (i == 1)
            {
                mark = SkBadge(item, "LegendAvailable", SkGoldCol, new Color(0.07f, 0.055f, 0.03f, 1f), 28f, null, SkGoldCol, 0f);
                UIKit.Diamond(mark, SkGoldCol, 18f, "AvailableDiamond");
            }
            else if (i == 2)
                mark = SkBadge(item, "LegendLocked", new Color(0.55f, 0.62f, 0.66f, 1f), new Color(0.05f, 0.065f, 0.075f, 1f), 28f, "ui_lock", new Color(0.62f, 0.69f, 0.73f, 1f), 15f);
            else
                mark = SkBadge(item, "LegendMaxed", gold, new Color(0.12f, 0.09f, 0.03f, 1f), 28f, "ui_check", gold, 16f);

            var t = UIFactory.Txt(item, labels[i], 17f, TextAlignmentOptions.MidlineLeft, new Color(0.74f, 0.80f, 0.84f, 1f), "LegendText");
            t.textWrappingMode = TextWrappingModes.NoWrap;
        }
    }

    // ---------- pecas pequenas ----------
    /// <summary>Chave do icone de uma habilidade, escolhida pela estatistica dela.</summary>
    static string SkIconKey(string survivorId, SkillTreeNode node)
    {
        if (node == null) return "star_up";
        switch (node.stat)
        {
            case SkillStat.Damage: return "bullets";
            case SkillStat.FireRate: return "bolt";
            case SkillStat.Health: return "heart";
            case SkillStat.Range: return "scope";
            case SkillStat.CritChance: return "crosshair";
            case SkillStat.CritDamage: return "skull";
            case SkillStat.Accuracy: return "bullseye";
            case SkillStat.Defense: return "shield";
            case SkillStat.Xp: return "star_up";
            case SkillStat.Gold: return "coins";
            case SkillStat.Drop: return "backpack";
            case SkillStat.AbilityPower: return survivorId == "engineer" ? "chip" : "grenade";
            case SkillStat.TurretFireRate: return "gear";
            case SkillStat.Cooldown: return "clock";
            case SkillStat.Healing: return "medcrate";
            case SkillStat.GrenadeRadius: return "explosive";
            default: return "star_up";
        }
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

    // ---------- acoes ----------
    void SkSaveNow(SaveData save)
    {
        if (GameSession.DebugRun) return;
        try { SaveSystem.Save(save); }
        catch (System.Exception e) { Debug.LogWarning("Zombit: falha ao salvar habilidades: " + e.Message); }
    }

    void UnlockMapSkill(string nodeId)
    {
        var save = GameSession.Save;
        if (save == null) return;
        string sid = SkillsSurvivorId(save);
        if (SkillTreeSystem.TryUnlock(save, sid, nodeId, out string msg))
        {
            ZombitAudio.PlayUI();
            SkSaveNow(save);
        }
        Toast(msg);
        ShowMapSkills(nodeId);
    }

    void ChooseMapSpecialization(string specId)
    {
        var save = GameSession.Save;
        if (save == null) return;
        string sid = SkillsSurvivorId(save);
        if (SkillTreeSystem.TryChooseSpecialization(save, sid, specId, out string msg)) SkSaveNow(save);
        Toast(msg);
        ShowMapSkills(selectedSkillId);
    }

    void ResetMapSkills()
    {
        var save = GameSession.Save;
        if (save == null) return;
        string sid = SkillsSurvivorId(save);
        if (SkillTreeSystem.TryReset(save, sid, out string msg)) SkSaveNow(save);
        Toast(msg);
        ShowMapSkills(selectedSkillId);
    }
}
