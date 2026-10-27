using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Mapa, nos de localizacao e inicio de missao (parte de MainMenuController).
public partial class MainMenuController
{
    // ---------- mapa ----------
    void ShowMap()
    {
        var save = GameSession.Save;
        if (save == null) { Show("main"); return; }
        save.MigrateFromPreviousVersions();
        if (!GameSession.DebugRun && save.slot > 0)
        {
            try { SaveSystem.Save(save); }
            catch (System.Exception e) { Debug.LogWarning("Zombit: autosave do mapa falhou: " + e.Message); }
        }
        var surv = GameDatabase.GetSurvivor(save.selectedSurvivor);
        if (surv == null) { Toast("Sobrevivente do save não encontrado."); Show("main"); return; }
        var prog = save.GetProgress(surv.id);
        nodeOutlines.Clear();
        nodePins.Clear();
        nodeBorders.Clear();

        var s = Rebuild("map");
        var mapBackdrop = UIFactory.Img(s, new Color(0.012f, 0.018f, 0.023f, 1f), "MapBackdrop");
        UIFactory.Stretch(mapBackdrop.rectTransform);
        mapBackdrop.raycastTarget = false;
        mapBackdrop.transform.SetAsFirstSibling();

        // ---- layout mobile: barra superior (perfil, ouro, sucata, capitulo) + menu estreito ----
        BuildHubSidebarMobile(s);
        BuildHubTopBar(s, save, surv, prog);
        BuildHubChapterSelector(s, save);

        // ---- mapa ----
        var mapArea = UIKit.Frame(s, "MapArea", new Color(0.018f, 0.026f, 0.031f, 1f), UIKit.Col.Border, 2f);
        UIFactory.Stretch(mapArea, 0.09f, 0.225f, 0.70f, 0.885f);
        var mapInner = UIFactory.Node("MapInner", mapArea);
        UIFactory.Inset(UIFactory.Stretch(mapInner), 2f, 2f);
        mapInner.gameObject.AddComponent<RectMask2D>();

        var mapArt = UIFactory.Img(mapInner, Color.white, "CityMapArt");
        mapArt.sprite = ArtLoader.Get("Art/Map/campaign_map");
        mapArt.preserveAspect = false;
        UIFactory.Stretch(mapArt.rectTransform);
        mapArt.raycastTarget = false;
        mapArt.color = Color.white;
        var mapShade = UIFactory.Img(mapInner, new Color(0.008f, 0.016f, 0.022f, 0.035f), "CityMapShade");
        UIFactory.Stretch(mapShade.rectTransform);
        mapShade.raycastTarget = false;

        // Camada dos marcadores: mapX/mapY sao normalizados neste retangulo (o mesmo da arte e dos caminhos).
        var markerLayer = UIFactory.Node("MapMarkerLayer", mapInner);
        UIFactory.Stretch(markerLayer, 0f, 0f, 1f, 1f);
        markerLayer.localScale = Vector3.one;
        BuildHubPaths(markerLayer, save);   // fica atras dos marcadores (primeiro filho)
        Canvas.ForceUpdateCanvases();

        int markerCount = 0;
        for (int i = 0; i < GameDatabase.Locations.Count; i++)
        {
            BuildNode(markerLayer, GameDatabase.Locations[i], i, save, prog.level);
            markerCount++;
        }
        Debug.Log($"Zombit: marcadores do mapa criados: {markerCount}/{GameDatabase.Locations.Count}.");

        // (cabecalho da regiao sobre o mapa removido: agora vive no seletor de capitulo do topo)

        // ---- painel da missao (rolavel) + faixa da equipe ----
        BuildHubMissionPanelMobile(s);
        BuildHubBottomMobile(s, save, surv, prog);

        LocationDef start = selectedLoc;
        if (start == null)
            foreach (var l in GameDatabase.Locations)
                if (GameDatabase.IsUnlocked(l, save) && save.HighestCompleted(l.id) < (int)Difficulty.Normal) { start = l; break; }
        if (start == null) start = GameDatabase.Locations[0];

        // Esta tela possui layout responsivo proprio; nao aplicar o antigo Layout_map.prefab,
        // que sobrescreve âncoras e dimensoes do mapa/cartoes redesenhados. As outras telas
        // continuam utilizando os prefabs editaveis normalmente.
        Show("map");
        SelectLocation(start);

        // Aviso pendente (ex.: jogo fechado no meio de uma missao, abandono, derrota).
        if (!string.IsNullOrEmpty(save.pendingNotice))
        {
            string notice = save.pendingNotice;
            save.pendingNotice = "";
            ShowNotice(notice);
            if (!GameSession.DebugRun)
            {
                try { SaveSystem.Save(save); } catch (System.Exception e) { Debug.LogWarning("Zombit: " + e.Message); }
            }
        }
    }

    // ---------- niveis de dificuldade ----------
    readonly Button[] diffButtons = new Button[4];
    TMP_Text dDiffInfo, dInsuranceInfo;
    Button dInsurance;
    int selectedDiff = (int)Difficulty.Normal;
    bool buyInsurance;
    GameObject noticeBox;

    void BuildDifficultyPanel(Transform panel)
    {
        HubLabel(panel, "DIFICULDADE", 590f);

        var btnRow = HubRow(panel, "DifficultyRow", 610f, 56f);
        HubTileRow(btnRow, 8f);
        for (int i = 0; i < 4; i++)
        {
            int level = i + 1;
            var d = Difficulties.Get(level);
            var b = UIFactory.Btn(btnRow, d.name, 17, () => { ZombitAudio.PlayUI(); SelectDifficulty(level); },
                new Color(0.10f, 0.14f, 0.17f), "Difficulty_" + d.name);
            var le = b.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 40f; le.flexibleWidth = 1f; le.flexibleHeight = 1f;
            var text = b.GetComponentInChildren<TMP_Text>();
            if (text)
            {
                text.textWrappingMode = TextWrappingModes.Normal;
                text.overflowMode = TextOverflowModes.Truncate;
                text.margin = new Vector4(4, 2, 4, 2);
                text.enableAutoSizing = true; text.fontSizeMin = 9f; text.fontSizeMax = 17f;
            }
            diffButtons[i] = b;
        }

        var infoRow = HubRow(panel, "DifficultyInfoRow", 674f, 176f);
        dDiffInfo = UIFactory.Txt(infoRow, "", 15, TextAlignmentOptions.TopLeft, new Color(0.80f, 0.86f, 0.88f), "DifficultyInfo");
        UIFactory.Stretch(dDiffInfo.rectTransform);
        dDiffInfo.textWrappingMode = TextWrappingModes.Normal;
        dDiffInfo.overflowMode = TextOverflowModes.Truncate;
        dDiffInfo.raycastTarget = false;
        dDiffInfo.enableAutoSizing = true; dDiffInfo.fontSizeMin = 10f; dDiffInfo.fontSizeMax = 15f;   // mais linhas (poder, furia)

        var insRow = HubRow(panel, "InsuranceRow", 856f, 40f);
        dInsurance = UIFactory.Btn(insRow, "", 16, () => { ZombitAudio.PlayUI(); buyInsurance = !buyInsurance; RefreshDifficultyUI(); },
            new Color(0.10f, 0.14f, 0.17f), "InsuranceToggle");
        UIFactory.Stretch((RectTransform)dInsurance.transform);
        dInsuranceInfo = dInsurance.GetComponentInChildren<TMP_Text>();
        if (dInsuranceInfo)
        {
            dInsuranceInfo.textWrappingMode = TextWrappingModes.NoWrap;
            dInsuranceInfo.enableAutoSizing = true; dInsuranceInfo.fontSizeMin = 9f; dInsuranceInfo.fontSizeMax = 16f;
        }
    }

    void SelectDifficulty(int level)
    {
        selectedDiff = level;
        RefreshDifficultyUI();
    }

    // Primeiro nivel liberado e ainda nao concluido (senao o mais alto liberado).
    int SuggestedDifficulty(LocationDef loc, SaveData save)
    {
        int best = (int)Difficulty.Facil;
        for (int d = 1; d <= 4; d++)
        {
            if (!GameDatabase.IsDifficultyUnlocked(loc, save, d)) break;
            best = d;
            if (!save.IsCompleted(loc.id, d)) return d;
        }
        return best;
    }

    void RefreshDifficultyUI()
    {
        var save = GameSession.Save;
        if (save == null || selectedLoc == null || diffButtons[0] == null) return;
        var loc = selectedLoc;
        bool locUnlocked = GameDatabase.IsUnlocked(loc, save);

        if (!GameDatabase.IsDifficultyUnlocked(loc, save, selectedDiff)) selectedDiff = SuggestedDifficulty(loc, save);

        for (int i = 0; i < 4; i++)
        {
            int level = i + 1;
            var d = Difficulties.Get(level);
            var b = diffButtons[i];
            if (b == null) continue;
            bool unlocked = GameDatabase.IsDifficultyUnlocked(loc, save, level);
            bool done = save.IsCompleted(loc.id, level);
            bool sel = level == selectedDiff;
            b.interactable = unlocked;

            Color baseColor = !unlocked ? new Color(0.06f, 0.07f, 0.08f)
                : sel ? Color.Lerp(new Color(0.10f, 0.14f, 0.17f), d.color, 0.55f) : new Color(0.10f, 0.14f, 0.17f);
            UIFactory.SetButtonColor(b, baseColor);

            var label = b.GetComponentInChildren<TMP_Text>();
            if (label)
            {
                string hex = ColorUtility.ToHtmlStringRGB(unlocked ? d.color : new Color(0.45f, 0.5f, 0.52f));
                label.text = $"<color=#{hex}>{d.name}</color>" + (done ? "\n<size=70%><color=#6CFF8A>concluído</color></size>"
                    : (!unlocked ? "\n<size=70%>bloqueado</size>" : ""));
            }
        }

        var def = Difficulties.Get(selectedDiff);
        string lockNote = "";
        if (locUnlocked && selectedDiff > 1 && !GameDatabase.IsDifficultyUnlocked(loc, save, selectedDiff))
            lockNote = "\n<color=#FF8888>Vença o nível anterior para liberar.</color>";
        string lostNote = "";
        int pending = DeathPenalty.PendingFor(save, loc.id, selectedDiff);
        if (pending > 0) lostNote = $"\n<color=#FFD84A>{pending} item(ns) perdido(s) aqui voltam como drop garantido ao vencer.</color>";
        string duoNote = "";
        if (TrioActive(save))
            duoNote = $"\n<color=#3FC8E8>Modo trio: zumbis x{HeroSquad.TrioHordeMult:0.#} com +{Mathf.RoundToInt((HeroSquad.TrioZombieHealthMult - 1f) * 100f)}% de vida · XP {Mathf.RoundToInt(HeroSquad.TrioXpShare * 100f)}% para cada · seguro cobre os três.</color>";
        else if (save.HasPartner)
            duoNote = $"\n<color=#3FC8E8>Modo dupla: zumbis x{HeroSquad.DuoHordeMult:0.#} · XP dividida 50/50 · seguro cobre os dois.</color>";
        string powerNote = PowerRating.Describe(save, save.selectedSurvivor, loc, selectedDiff);
        if (dDiffInfo) dDiffInfo.text = Difficulties.Describe(def) + powerNote + duoNote + lostNote + lockNote;

        // Seguro: so faz sentido se ha risco de perder equipamento.
        int cost = DeathPenalty.InsuranceCost(loc, selectedDiff);
        if (cost <= 0) buyInsurance = false;
        if (dInsurance)
        {
            dInsurance.gameObject.SetActive(cost > 0);
            bool canPay = save.gold >= cost;
            if (buyInsurance && !canPay) buyInsurance = false;
            UIFactory.SetButtonColor(dInsurance, buyInsurance ? new Color(0.13f, 0.38f, 0.22f) : new Color(0.10f, 0.14f, 0.17f));
            if (dInsuranceInfo)
                dInsuranceInfo.text = buyInsurance
                    ? $"SEGURO ATIVO  (-{cost} ouro ao iniciar)"
                    : $"Comprar seguro: {cost} ouro  <size=75%>(protege o equipamento nesta missão)</size>";
        }

        if (dStart) dStart.interactable = locUnlocked && GameDatabase.IsDifficultyUnlocked(loc, save, selectedDiff);
        RefreshSquadSlots();   // 3o slot trava/destrava conforme o nivel (trio so no Pesadelo)
    }

    // Aviso longo (o Toast padrao e curto demais para mensagens de penalidade).
    void ShowNotice(string text)
    {
        if (noticeBox != null) Destroy(noticeBox);
        var box = UIKit.Frame(root, "NoticeBox", new Color(0.055f, 0.028f, 0.030f, 0.97f),
            new Color(0.847f, 0.141f, 0.169f, 0.95f), 2f);
        noticeBox = box.gameObject;
        UIFactory.Place(box, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 330f));
        var t = UIFactory.Txt(box.transform, text, 28, TextAlignmentOptions.Center, Color.white, "NoticeText");
        UIFactory.Stretch(t.rectTransform, 0.04f, 0.28f, 0.96f, 0.95f);
        t.textWrappingMode = TextWrappingModes.Normal;
        var ok = UIFactory.Btn(box.transform, "ENTENDI", 26, () => { if (noticeBox) Destroy(noticeBox); noticeBox = null; }, new Color(0.588f, 0.075f, 0.09f, 1f));
        UIFactory.Place((RectTransform)ok.transform, new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(300f, 64f));
        box.transform.SetAsLastSibling();
    }


    // ---------- utilitários de UI das telas abertas pelo mapa ----------
    void CreateUtilityBackdrop(RectTransform screen, string name)
    {
        var art = UIFactory.Img(screen, Color.white, name + "Art");
        art.sprite = ArtLoader.Get(SceneBackgrounds.DefaultPath);
        art.preserveAspect = false;
        art.color = new Color(0.62f, 0.69f, 0.73f, 0.28f);
        UIFactory.Stretch(art.rectTransform);
        art.raycastTarget = false;
        art.transform.SetAsFirstSibling();
        var backdrop = UIFactory.Img(screen, new Color(0.007f, 0.013f, 0.019f, 0.80f), name);
        UIFactory.Stretch(backdrop.rectTransform);
        backdrop.raycastTarget = false;
        backdrop.transform.SetSiblingIndex(1);
    }


    TMP_Text MapText(Transform parent, string value, float size, TextAlignmentOptions alignment,
        Color color, float x, float y, float width, float height, string name)
    {
        var text = UIFactory.Txt(parent, value, size, alignment, color, name);
        UIFactory.Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(width, height));
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Truncate;
        text.raycastTarget = false;
        return text;
    }

    Button MapButton(Transform parent, string label, float fontSize, System.Action action,
        float x, float y, float width, float height, Color color, string name)
    {
        var button = UIFactory.Btn(parent, label, fontSize, action, color, name);
        UIFactory.Place((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(width, height));
        var text = button.GetComponentInChildren<TMP_Text>();
        if (text)
        {
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Truncate;
            text.margin = new Vector4(8, 4, 8, 4);
        }
        return button;
    }

    void BuildNode(Transform parent, LocationDef loc, int index, SaveData save, int playerLevel)
    {
        if (parent == null || loc == null || save == null) return;

        bool unlocked = GameDatabase.IsUnlocked(loc, save);
        bool done = save.HighestCompleted(loc.id) >= (int)Difficulty.Normal;
        Color fill = !unlocked ? new Color(0.035f, 0.043f, 0.046f, 0.98f)
            : done ? new Color(0.035f, 0.10f, 0.065f, 0.98f)
            : new Color(0.035f, 0.038f, 0.035f, 0.98f);
        Color border = !unlocked ? new Color(0.34f, 0.37f, 0.36f, 0.98f)
            : done ? new Color(0.36f, 0.78f, 0.43f, 1f)
            : new Color(0.88f, 0.48f, 0.12f, 1f);
        var location = loc;

        // Cartao ilustrado: imagem da regiao em cima, numero no canto, nome/estrelas no rodape.
        // Os sprites de Art/Map/Locations podem ser trocados sem editar o codigo.
        var button = UIFactory.Btn(parent, string.Empty, 10f, () => SelectLocation(location), fill, "Node_" + loc.id);
        var rt = (RectTransform)button.transform;
        Vector2 anchor = new Vector2(Mathf.Clamp01(loc.mapX), Mathf.Clamp01(loc.mapY));
        rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        bool compactLayout = Screen.width < 1100 || Screen.height < 650;
        rt.sizeDelta = compactLayout ? new Vector2(148f, 114f) : new Vector2(190f, 154f);
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;
        button.gameObject.SetActive(true);
        button.interactable = true;
        button.transition = Selectable.Transition.ColorTint;

        var background = button.GetComponent<Image>();
        if (background)
        {
            background.sprite = UIKit.Chamfer;
            background.type = Image.Type.Sliced;
            background.color = Color.white;
            background.raycastTarget = true;
            button.targetGraphic = background;
        }
        ColorBlock colors = button.colors;
        colors.normalColor = fill;
        colors.highlightedColor = Color.Lerp(fill, UIKit.Col.Gold, 0.24f);
        colors.pressedColor = Color.Lerp(fill, Color.black, 0.25f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.12f, 0.13f, 0.14f, 0.98f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        // Foto ilustrativa da regiao (existem imagens para todos os locais no projeto).
        var art = UIFactory.Img(button.transform, Color.white, "LocationArt");
        art.sprite = ArtLoader.Get("Art/Map/Locations/" + loc.id);
        art.preserveAspect = false;
        UIFactory.Stretch(art.rectTransform, 0.025f, 0.315f, 0.975f, 0.975f);
        art.raycastTarget = false;
        if (!unlocked) art.color = new Color(0.63f, 0.66f, 0.61f, 1f);

        // Sombra sutil sobre a imagem melhora a legibilidade do selo numerado.
        var artShade = UIFactory.Img(button.transform, new Color(0.012f, 0.018f, 0.016f, unlocked ? 0.10f : 0.30f), "ArtShade");
        UIFactory.Stretch(artShade.rectTransform, 0.025f, 0.315f, 0.975f, 0.975f);
        artShade.raycastTarget = false;

        var footer = UIFactory.Img(button.transform, new Color(0.018f, 0.024f, 0.022f, 0.98f), "Footer");
        UIFactory.Stretch(footer.rectTransform, 0.02f, 0.025f, 0.98f, 0.365f);
        footer.raycastTarget = false;
        var footerLine = UIFactory.Img(button.transform, unlocked ? border : new Color(0.30f, 0.34f, 0.32f, 1f), "FooterLine");
        UIFactory.Stretch(footerLine.rectTransform, 0.025f, 0.355f, 0.975f, 0.365f);
        footerLine.raycastTarget = false;

        var numPlate = UIKit.Frame(button.transform, "NumBox", new Color(0.055f, 0.060f, 0.052f, 0.98f),
            unlocked ? UIKit.Col.Gold : new Color(0.48f, 0.51f, 0.48f), 1.5f);
        float numSize = compactLayout ? 30f : 38f;
        UIFactory.Place(numPlate, new Vector2(0f, 1f), new Vector2(6f, -6f), new Vector2(numSize, numSize * 0.88f));
        // UIKit.Frame usa Images nos filhos Border/Fill; a raiz nao possui Image.
        // Os filhos ja sao criados com raycastTarget=false em UIKit.Frame.
        var num = UIFactory.Txt(numPlate, (index + 1).ToString("00"), compactLayout ? 16f : 19f, TextAlignmentOptions.Center,
            unlocked ? UIKit.Col.Gold : UIKit.Col.TextDim, "Num");
        UIFactory.Stretch(num.rectTransform);
        num.fontStyle = FontStyles.Bold;
        num.raycastTarget = false;

        var nameText = UIFactory.Txt(button.transform, loc.name.ToUpperInvariant(), 12.5f,
            TextAlignmentOptions.Center, unlocked ? Color.white : new Color(0.69f, 0.73f, 0.70f), "LocationName");
        UIFactory.Stretch(nameText.rectTransform, 0.045f, 0.165f, 0.955f, 0.345f);
        nameText.fontStyle = FontStyles.Bold;
        nameText.characterSpacing = 0.4f;
        nameText.textWrappingMode = TextWrappingModes.NoWrap;
        nameText.overflowMode = TextOverflowModes.Truncate;
        nameText.enableAutoSizing = true; nameText.fontSizeMin = compactLayout ? 8f : 9f; nameText.fontSizeMax = compactLayout ? 11f : 12.5f;
        nameText.raycastTarget = false;

        // As quatro estrelas acompanham a progressao real de dificuldade.
        for (int i = 0; i < 4; i++)
        {
            bool got = save.IsCompleted(loc.id, i + 1);
            var star = UIFactory.Img(button.transform, got ? UIKit.Col.Gold : new Color(0.28f, 0.32f, 0.30f, 1f), "Star_" + i);
            star.sprite = UIKit.Star;
            float starSize = compactLayout ? 13f : 16f;
            UIFactory.Place(star.rectTransform, new Vector2(0f, 0f), new Vector2(10f + i * (compactLayout ? 18f : 21f), 6f), new Vector2(starSize, starSize));
            star.raycastTarget = false;
        }

        if (!unlocked)
        {
            var level = UIFactory.Txt(button.transform, "NV. " + loc.recommendedLevel, 10f,
                TextAlignmentOptions.MidlineRight, new Color(1f, 0.37f, 0.32f, 1f), "LockLevel");
            UIFactory.Place(level.rectTransform, new Vector2(1f, 0f), new Vector2(-7f, 8f), new Vector2(compactLayout ? 42f : 50f, 17f));
            level.fontStyle = FontStyles.Bold;
            level.enableAutoSizing = true; level.fontSizeMin = 8f; level.fontSizeMax = 10f;
            level.textWrappingMode = TextWrappingModes.NoWrap;
            level.raycastTarget = false;
            BuildHubLock(button.transform, new Vector2(1f, 1f), new Vector2(-8f, compactLayout ? -25f : -31f), compactLayout ? 14f : 17f);
        }
        else if (done)
        {
            var doneBadge = UIKit.Frame(button.transform, "DoneBadge", new Color(0.08f, 0.30f, 0.18f, 0.96f), UIKit.Col.Success, 1f);
            UIFactory.Place(doneBadge, new Vector2(1f, 1f), new Vector2(-6f, -6f), new Vector2(26f, 22f));
            var doneText = UIFactory.Txt(doneBadge, "✓", 15f, TextAlignmentOptions.Center, Color.white, "DoneText");
            UIFactory.Stretch(doneText.rectTransform);
            doneText.raycastTarget = false;
        }

        // Marcador do local escolhido: pino laranja acima do cartão.
        var pin = UIFactory.Img(button.transform, new Color(0.98f, 0.30f, 0.08f, 1f), "SelectedPin");
        UIKit.ApplyCircle(pin, new Color(0.98f, 0.30f, 0.08f, 1f));
        UIFactory.Place(pin.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 6f), compactLayout ? new Vector2(17f, 17f) : new Vector2(22f, 22f));
        pin.raycastTarget = false;
        var pinDot = UIFactory.Img(pin.transform, new Color(0.18f, 0.05f, 0.02f, 1f), "PinDot");
        UIKit.ApplyCircle(pinDot, new Color(0.18f, 0.05f, 0.02f, 1f));
        UIFactory.Place(pinDot.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, compactLayout ? new Vector2(5f, 5f) : new Vector2(7f, 7f));
        pinDot.raycastTarget = false;
        pin.gameObject.SetActive(false);
        nodePins[loc.id] = pin.gameObject;

        var outline = button.gameObject.GetComponent<Outline>();
        if (outline == null) outline = button.gameObject.AddComponent<Outline>();
        outline.effectColor = border;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.enabled = true;
        nodeOutlines[loc.id] = outline;
        nodeBorders[loc.id] = border;
    }

    /// <summary>Cadeado desenhado com formas simples (a fonte padrao nao tem o glifo).</summary>
    void BuildHubLock(Transform parent, Vector2 anchor, Vector2 pos, float size)
    {
        Color metal = new Color(0.72f, 0.77f, 0.80f, 1f);
        var holder = UIFactory.Node("LockIcon", parent);
        UIFactory.Place(holder, anchor, pos, new Vector2(size, size));
        holder.pivot = new Vector2(1f, 0.5f);

        var ring = UIFactory.Img(holder, metal, "Shackle");
        UIKit.ApplyCircle(ring, metal);
        UIFactory.Place(ring.rectTransform, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(size * 0.62f, size * 0.62f));
        ring.raycastTarget = false;
        var hole = UIFactory.Img(holder, new Color(0.035f, 0.045f, 0.055f, 1f), "ShackleHole");
        UIKit.ApplyCircle(hole, new Color(0.035f, 0.045f, 0.055f, 1f));
        UIFactory.Place(hole.rectTransform, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(size * 0.34f, size * 0.34f));
        hole.raycastTarget = false;
        var body = UIFactory.Img(holder, metal, "Body");
        UIFactory.Stretch(body.rectTransform, 0.08f, 0f, 0.92f, 0.52f);
        body.raycastTarget = false;
    }

    void SelectLocation(LocationDef loc)
    {
        selectedLoc = loc;
        foreach (var kv in nodeOutlines)
        {
            if (!kv.Value) continue;
            bool sel = kv.Key == loc.id;
            Color baseBorder = nodeBorders.TryGetValue(kv.Key, out var bc) ? bc : Color.gray;
            kv.Value.effectColor = sel ? UIKit.Col.Gold : baseBorder;
            kv.Value.effectDistance = sel ? new Vector2(3f, -3f) : new Vector2(1.5f, -1.5f);
            if (nodePins.TryGetValue(kv.Key, out var pin) && pin) pin.SetActive(sel);
            if (sel) kv.Value.transform.SetAsLastSibling();   // o selecionado fica por cima dos vizinhos
        }

        var save = GameSession.Save;
        var surv = GameDatabase.GetSurvivor(save.selectedSurvivor);
        int lvl = save.GetProgress(surv.id).level;
        bool unlocked = GameDatabase.IsUnlocked(loc, save);
        bool done = save.HighestCompleted(loc.id) >= (int)Difficulty.Normal;

        string lvlColor = lvl >= loc.recommendedLevel ? "#6CFF8A" : lvl >= loc.recommendedLevel - 2 ? "#FFD84A" : "#FF5555";
        const string dim = "#9AA6B2";
        if (hubBadge) hubBadge.text = (GameDatabase.Locations.IndexOf(loc) + 1).ToString("00");
        if (dName) dName.text = loc.name.ToUpperInvariant();
        if (dDesc) dDesc.text = loc.description;
        if (hubStatA) hubStatA.text = $"<color={dim}>Nível recomendado:</color> <b><color={lvlColor}>{loc.recommendedLevel}</color></b>";
        if (hubStatB) hubStatB.text = $"<color={dim}>Ondas:</color> <b>{loc.waves}</b>";
        if (hubStatC) hubStatC.text = $"<color={dim}>Dificuldade:</color> <b><color=#FFD84A>{LocationDef.DifficultyName(loc.difficulty)}</color></b> ({loc.difficulty}/5)";
        if (hubStatD) hubStatD.text = $"<color={dim}>Recompensa:</color> <b><color=#FFC24B>{loc.goldReward}</color></b> ouro";

        string note = "";
        if (!unlocked) note = "<color=#FF8888>Bloqueado: conclua " + RequirementNames(loc) + ".</color>";
        else if (done) note = "<color=#6CFF8A>Concluído (" + save.CompletedLevelCount(loc.id) + "/4 níveis)</color>";
        else if (lvl < loc.recommendedLevel - 2) note = "<color=#FF5555>Atenção: seu nível está muito abaixo!</color>";
        if (dInfo) dInfo.text = note;

        if (selectedLocationPreview)
        {
            Sprite hero = ArtLoader.Get(SceneBackgrounds.PathFor(loc));
            selectedLocationPreview.sprite = hero;
            selectedLocationPreview.color = hero == null ? new Color(0.12f, 0.16f, 0.18f, 1f)
                : unlocked ? Color.white : new Color(0.42f, 0.45f, 0.48f, 1f);
            if (hero != null && hubHeroFitter) hubHeroFitter.aspectRatio = hero.rect.width / hero.rect.height;
        }
        RefreshHubEnemies(loc);

        selectedDiff = SuggestedDifficulty(loc, save);
        RefreshDifficultyUI();
    }

    string RequirementNames(LocationDef loc)
    {
        var names = new List<string>();
        foreach (var r in loc.requires)
        {
            var l = GameDatabase.GetLocation(r);
            if (l != null) names.Add(l.name);
        }
        return string.Join(" ou ", names);
    }

    void OnStartMission()
    {
        if (selectedLoc == null) return;
        GameSession.Location = selectedLoc;
        GameSession.Survivor = GameDatabase.GetSurvivor(GameSession.Save.selectedSurvivor);
        GameSession.Save.NormalizeSquad();
        GameSession.Partner = GameSession.Save.HasPartner ? GameDatabase.GetSurvivor(GameSession.Save.selectedPartner) : null;
        // Trio so no Pesadelo; nos outros niveis o 3o fica guardado no save mas nao entra em campo.
        GameSession.Third = GameSession.Partner != null && selectedDiff >= HeroSquad.TrioDifficulty && GameSession.Save.HasThird
            ? GameDatabase.GetSurvivor(GameSession.Save.selectedThird) : null;
        GameSession.DebugRun = false;

        var save = GameSession.Save;
        if (!GameDatabase.IsDifficultyUnlocked(selectedLoc, save, selectedDiff)) { Toast("Este nível ainda está bloqueado."); return; }
        GameSession.Difficulty = selectedDiff;

        // Seguro pago em ouro: protege o equipamento so nesta run.
        GameSession.InsuranceBought = false;
        int cost = DeathPenalty.InsuranceCost(selectedLoc, selectedDiff);
        if (buyInsurance && cost > 0)
        {
            if (save.gold < cost) { Toast("Ouro insuficiente para o seguro."); return; }
            save.gold -= cost;
            GameSession.InsuranceBought = true;
        }
        buyInsurance = false;
        // Disco cheio / pasta protegida nao pode impedir o jogador de iniciar a missao.
        try { SaveSystem.Save(GameSession.Save); }
        catch (System.Exception e)
        {
            Debug.LogWarning("Zombit: falha ao salvar antes da missão: " + e.Message);
        }
        SceneManager.LoadScene(GameSession.GameSceneIndex);
    }
}
