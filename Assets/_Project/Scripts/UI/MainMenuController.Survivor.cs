using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Carregar jogo, selecao de sobrevivente e nickname (parte de MainMenuController).
public partial class MainMenuController
{
    // ---------- carregar ----------
    void ShowSlots()
    {
        var editorScreen = EditorUiScreen("slots", true);
        if (editorScreen != null) { BindSlotsFromPrefab(editorScreen); Show("slots"); return; }

        var s = Rebuild("slots");
        BuildSlotsFrame(s, out var list);
        for (int i = 1; i <= SaveSystem.SlotCount; i++) BuildSlotRowCode(list, i, SaveSystem.Load(i));
        Show("slots");
    }

    // Moldura da tela de slots (fundo, painel, titulo, lista vazia e botao voltar).
    void BuildSlotsFrame(RectTransform s, out RectTransform list)
    {
        AddCinematicBackground(s, 0.72f);
        var slotsPanel = UIKit.Frame(s, "SlotsPanel", new Color(0.018f, 0.031f, 0.043f, 0.94f),
            new Color(0.36f, 0.42f, 0.49f, 0.9f), 2f);
        UIFactory.Place(slotsPanel, Mid, new Vector2(0f, 8f), new Vector2(1450f, 670f));
        Title(s, "CARREGAR CAMPANHA");
        list = UIFactory.Node("Rows", s);
        UIFactory.Place(list, Mid, new Vector2(0, 20), new Vector2(1300, 560));
        UIFactory.VLayout(list, 18);
        BackButton(s, "Voltar", () => Show("main"), 0, "BtnBack");
    }

    string SlotInfoText(int slot, SaveData d)
    {
        if (d == null) return $"Slot {slot}  -  Vazio";
        var sv = GameDatabase.GetSurvivor(d.selectedSurvivor);
        string displayClass = sv != null ? sv.name : d.selectedSurvivor;
        int savedLevel = d.GetProgress(d.selectedSurvivor).level;
        return $"<color=#FFC04A><b>{GetPlayerNickname(d).ToUpperInvariant()}</b></color>  <color=#8999A2>· SLOT {slot}</color>\n" +
               $"<color=#E0E7EA>CLASSE: {displayClass.ToUpperInvariant()}  ·  NV. {savedLevel}</color>\n" +
               $"<color=#94A2A9>Ouro: {d.gold}    Missões: {d.CompletedLocationCount()}/{GameDatabase.Locations.Count}    {d.lastSaved}</color>";
    }

    // Linha de um slot montada por codigo (sem prefab, e modelo para gerar Row_slot).
    void BuildSlotRowCode(RectTransform list, int slot, SaveData d)
    {
        var row = UIFactory.Img(list, UIFactory.PanelColor, "Slot" + slot);
        UIFactory.Elem(row.gameObject, 1300, 150);

        var txt = UIFactory.Txt(row.transform, SlotInfoText(slot, d), 30, TextAlignmentOptions.Left, null, "SlotInfo");
        UIFactory.Stretch(txt.rectTransform, 0.02f, 0, 0.6f, 1);

        var load = UIFactory.Btn(row.transform, "Carregar", 30, () =>
        {
            var data = SaveSystem.Load(slot);
            if (data == null) return;
            UseSave(data);
            ShowMap();
        }, UIFactory.Red, "BtnLoad");
        UIFactory.Stretch((RectTransform)load.transform, 0.62f, 0.2f, 0.80f, 0.8f);
        load.interactable = d != null;

        var del = UIFactory.Btn(row.transform, "Apagar", 30, () =>
        {
            SaveSystem.Delete(slot);
            ShowSlots();
            Toast($"Slot {slot} apagado.");
        }, null, "BtnDelete");
        UIFactory.Stretch((RectTransform)del.transform, 0.82f, 0.2f, 0.98f, 0.8f);
        del.interactable = d != null;
    }

    // ---------- seleção de sobrevivente ----------
    // Campos extras da tela de selecao (visual novo).
    readonly List<Image> cardFills = new List<Image>();
    readonly TMP_Text[] statValues = new TMP_Text[StatRows];
    TMP_Text selWeapon;
    RectTransform selectDetailFrame;

    // Texto de cada classe exibido nos cartoes (titulo da classe, especializacao e "ideal para").
    struct SurvivorBlurb { public string title, specialization, ideal; }
    static SurvivorBlurb BlurbFor(SurvivorDef d)
    {
        switch (d.id)
        {
            case "assaulter": return new SurvivorBlurb { title = "Granadeiro",
                specialization = "Dano explosivo  ·  Cadência de tiro  ·  Redução da recarga da granada",
                ideal = "Ideal para enfrentar grupos grandes e abrir espaço quando o sobrevivente está cercado." };
            case "sniper": return new SurvivorBlurb { title = "Atirador de Elite",
                specialization = "Dano crítico  ·  Precisão  ·  Alcance  ·  Dano por disparo",
                ideal = "Ideal para eliminar inimigos resistentes e chefes." };
            case "medic": return new SurvivorBlurb { title = "Médica",
                specialization = "Regeneração  ·  Vida máxima  ·  Defesa  ·  Recuperação",
                ideal = "Ideal para jogadores que preferem uma abordagem mais segura e resistente." };
            case "engineer": return new SurvivorBlurb { title = "Engenheira",
                specialization = "Dano da torreta  ·  Velocidade de disparo  ·  Duração  ·  Resistência",
                ideal = "Ideal para manter pressão constante contra as hordas." };
            case "explorer": return new SurvivorBlurb { title = "Exploradora",
                specialization = "Ouro  ·  XP  ·  Recompensas  ·  Sobrevivência",
                ideal = "Ideal para quem quer desenvolver equipamentos e personagens mais rapidamente." };
            default: return new SurvivorBlurb { title = d.role, specialization = d.role, ideal = d.description };
        }
    }

    // Texto posicionado em pixels (origem no canto superior esquerdo do pai), sempre dentro da caixa.
    TMP_Text SelText(Transform parent, string value, float size, TextAlignmentOptions align, Color color,
        float x, float y, float w, float h, bool wrap, float minSize, string name, bool bold = false)
    {
        var t = UIFactory.Txt(parent, value, size, align, color, name);
        UIFactory.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(w, h));
        t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Truncate;
        t.enableAutoSizing = true;
        t.fontSizeMax = t.fontSize;
        t.fontSizeMin = Mathf.Min(minSize, t.fontSize);
        t.raycastTarget = false;
        if (bold) t.fontStyle = FontStyles.Bold;
        return t;
    }

    // Carta de sobrevivente montada por codigo (sem prefab, e modelo para gerar Card_Survivor).
    void BuildSurvivorCardCode(RectTransform cards, SurvivorDef d)
    {
        const float cardW = 340f, cardH = 770f;
            var blurb = BlurbFor(d);
            Color cc = new Color(d.color.r, d.color.g, d.color.b, 1f);
            Color dim = Color.Lerp(cc, Color.black, 0.45f);

            var card = UIKit.Button(cards, "", () => SelectSurvivor(d), UIKit.BtnStyle.Secondary, 10, "Card_" + d.id);
            UIFactory.Elem(card.gameObject, cardW, cardH);
            var emptyLabel = card.GetComponentInChildren<TMP_Text>(true);
            if (emptyLabel) emptyLabel.text = "";
            cardBtns.Add(card);
            cardImgs.Add(card.GetComponent<Image>());

            var fill = UIFactory.Img(card.transform, Color.Lerp(UIKit.Col.Surface, cc, 0.12f), "Fill");
            UIKit.ApplyChamfer(fill, fill.color);
            UIFactory.Inset(UIFactory.Stretch(fill.rectTransform), 3f, 3f);
            fill.raycastTarget = false;
            fill.transform.SetAsFirstSibling();
            cardFills.Add(fill);

            float inner = cardW - 6f;   // largura util (dentro da borda de 3 px)

            // ---- arte (cortada na area fixa) + nome + titulo da classe ----
            const float artH = 396f;
            var artMask = UIFactory.Node("ArtMask", card.transform);
            UIFactory.Place(artMask, new Vector2(0f, 1f), new Vector2(3f, -3f), new Vector2(inner, artH));
            artMask.gameObject.AddComponent<RectMask2D>();
            var art = UIFactory.Img(artMask, Color.white, "Art");
            Sprite sp = ArtLoader.Get("Art/Characters/" + d.id + "_card");
            if (sp == null) sp = d.portrait;
            if (sp == null) sp = ArtLoader.Get("Art/Characters/" + d.id + "_portrait");
            art.sprite = sp;
            art.color = sp != null ? Color.white : cc;
            art.raycastTarget = false;
            float artImgH = sp != null ? inner * sp.rect.height / Mathf.Max(1f, sp.rect.width) : artH;
            UIFactory.Place(art.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(inner, Mathf.Max(artH, artImgH)));

            var shade = UIFactory.Img(artMask, new Color(0.006f, 0.012f, 0.018f, 0.78f), "ArtShade");
            UIFactory.Place(shade.rectTransform, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(inner, 104f));
            shade.raycastTarget = false;
            var nm = SelText(artMask, d.name.ToUpperInvariant(), 38f, TextAlignmentOptions.MidlineLeft, Color.white,
                14f, artH - 100f, inner - 28f, 48f, false, 20f, "Name", true);
            nm.characterSpacing = 1.5f;
            SelText(artMask, blurb.title, 24f, TextAlignmentOptions.MidlineLeft, Color.Lerp(cc, Color.white, 0.2f),
                14f, artH - 54f, inner - 28f, 34f, false, 14f, "ClassTitle", true);

            bool showChip = !newGameMode && GameSession.Save != null;
            if (showChip || editorUiBaking)
            {
                var chip = UIKit.Frame(artMask, "LevelChip", new Color(0.02f, 0.03f, 0.04f, 0.85f), UIKit.Col.Gold, 1.5f);
                UIFactory.Place(chip, new Vector2(1f, 1f), new Vector2(-10f, -10f), new Vector2(92f, 30f));
                var lt = UIFactory.Txt(chip, "NV. " + (showChip ? GameSession.Save.GetProgress(d.id).level : 1), 16f, TextAlignmentOptions.Center, UIKit.Col.Gold, "Level");
                lt.fontStyle = FontStyles.Bold; lt.textWrappingMode = TextWrappingModes.NoWrap; lt.raycastTarget = false;
                UIFactory.Stretch(lt.rectTransform);
                chip.gameObject.SetActive(showChip);
            }

            // ---- arma ----
            var w = WeaponCatalog.Preview(d);
            float dmg = w.damage * d.damageMult;
            float rate = w.shotsPerSecond * d.fireRateMult;
            float top = artH + 12f;
            var wb = UIKit.Frame(card.transform, "WeaponBox", new Color(0.02f, 0.035f, 0.047f, 0.92f), dim, 1.5f);
            UIFactory.Place(wb, new Vector2(0f, 1f), new Vector2(10f, -top), new Vector2(cardW - 20f, 84f));
            SelText(wb, w.displayName, 22f, TextAlignmentOptions.MidlineLeft, Color.white, 12f, 4f, cardW - 44f, 28f, false, 12f, "WeaponName", true);
            SelText(wb, w.weaponClass, 11f, TextAlignmentOptions.MidlineLeft, UIKit.Col.TextDim, 12f, 32f, cardW - 44f, 18f, false, 8f, "WeaponClass");
            SelText(wb, "DANO " + dmg.ToString("0.#") + "  ·  " + rate.ToString("0.#") + "/s  ·  PENTE " + w.magazineSize + "  ·  VIDA x" + d.healthMult.ToString("0.0#"),
                13f, TextAlignmentOptions.MidlineLeft, UIKit.Col.Gold, 12f, 54f, cardW - 44f, 24f, false, 9f, "WeaponStats", true);
            top += 84f + 8f;

            // ---- habilidade ----
            var ab = UIKit.Frame(card.transform, "AbilityBox", new Color(0.02f, 0.035f, 0.047f, 0.92f), dim, 1.5f);
            UIFactory.Place(ab, new Vector2(0f, 1f), new Vector2(10f, -top), new Vector2(cardW - 20f, 120f));
            SelText(ab, d.utilityName.ToUpperInvariant(), 19f, TextAlignmentOptions.MidlineLeft, Color.Lerp(cc, Color.white, 0.15f),
                12f, 4f, cardW - 44f, 28f, false, 11f, "AbilityName", true);
            SelText(ab, d.utilityDesc, 14f, TextAlignmentOptions.TopLeft, new Color(0.85f, 0.89f, 0.91f),
                12f, 34f, cardW - 44f, 80f, true, 10f, "AbilityDesc");
            top += 120f + 8f;

            // ---- especializacao ----
            var sb = UIKit.Frame(card.transform, "SpecBox", new Color(0.02f, 0.035f, 0.047f, 0.92f), dim, 1.5f);
            UIFactory.Place(sb, new Vector2(0f, 1f), new Vector2(10f, -top), new Vector2(cardW - 20f, 78f));
            SelText(sb, "ESPECIALIZAÇÃO", 13f, TextAlignmentOptions.MidlineLeft, Color.Lerp(cc, Color.white, 0.15f),
                12f, 3f, cardW - 44f, 20f, false, 9f, "SpecTitle", true).characterSpacing = 1.2f;
            SelText(sb, blurb.specialization, 13f, TextAlignmentOptions.TopLeft, new Color(0.85f, 0.89f, 0.91f),
                12f, 24f, cardW - 44f, 50f, true, 9f, "SpecText");
            top += 78f + 8f;

            // ---- ideal para ----
            SelText(card.transform, blurb.ideal, 14f, TextAlignmentOptions.TopLeft, UIKit.Col.TextDim,
                14f, top, cardW - 28f, cardH - top - 8f, true, 9f, "IdealFor");
    }

    void BuildSelect()
    {
        var editorScreen = EditorUiScreen("select");
        if (editorScreen != null) { BindSelectFromPrefab(editorScreen); return; }

        var s = NewScreen("select");
        AddCinematicBackground(s, 0.72f);
        var selectPanel = UIKit.Frame(s, "SurvivorSelectPanel", new Color(0.014f, 0.026f, 0.036f, 0.9f), UIKit.Col.Border, 2f);
        UIFactory.Stretch(selectPanel, 0.015f, 0.05f, 0.985f, 0.95f);
        Title(s, "ESCOLHER <color=#D8242B>SOBREVIVENTE</color>", 56);
        playerIdentityInSelect = UIFactory.Txt(s, "JOGADOR: " + GetPendingNickname("JOGADOR") + "  ·  ESCOLHA SUA CLASSE", 20,
            TextAlignmentOptions.Center, UIKit.Col.Gold, "NewGamePlayerIdentity");
        UIFactory.Place(playerIdentityInSelect.rectTransform, TopC, new Vector2(0, -104), new Vector2(1120, 30));
        playerIdentityInSelect.textWrappingMode = TextWrappingModes.NoWrap;

        const float cardW = 340f, cardH = 770f, gap = 14f;
        var cards = UIFactory.Node("Cards", s);
        UIFactory.Place(cards, TopC, new Vector2(0, -144), new Vector2(cardW * 5 + gap * 4, cardH));
        UIFactory.HLayout(cards, gap, TextAnchor.UpperCenter);

        cardBtns.Clear(); cardImgs.Clear(); cardFills.Clear();
        if (!editorUiBaking)
            foreach (var def in GameDatabase.Survivors) BuildSurvivorCardCode(cards, def);

        selNote = UIFactory.Txt(s, "", UIKit.Size.Body, TextAlignmentOptions.Center, UIKit.Col.TextDim, "Note");
        UIFactory.Place(selNote.rectTransform, BotC, new Vector2(0, 100), new Vector2(1400, 34));

        var back = UIKit.Button(s, "VOLTAR", () => { if (newGameMode) Show("main"); else ShowMap(); },
            UIKit.BtnStyle.Secondary, 26, "BackButton");
        UIFactory.Place((RectTransform)back.transform, BotC, new Vector2(-300, 20), new Vector2(360, 70));
        var confirm = UIKit.Button(s, "INICIAR CAMPANHA", OnConfirmSurvivor, UIKit.BtnStyle.Primary, 26, "ConfirmButton");
        UIFactory.Place((RectTransform)confirm.transform, BotC, new Vector2(250, 20), new Vector2(520, 70));
        confirmLabel = confirm.GetComponentInChildren<TMP_Text>();
    }

    void ShowSelect()
    {
        if (!newGameMode && GameSession.Save != null)
            selected = GameDatabase.GetSurvivor(GameSession.Save.selectedSurvivor);
        confirmLabel.text = newGameMode ? "INICIAR CAMPANHA" : "CONFIRMAR SOBREVIVENTE";
        if (playerIdentityInSelect)
        {
            string nick = newGameMode ? GetPendingNickname("JOGADOR") : GetPlayerNickname(GameSession.Save);
            playerIdentityInSelect.text = "JOGADOR: " + nick.ToUpperInvariant() + "  ·  ESCOLHA SUA CLASSE";
        }
        selNote.text = newGameMode && SaveSystem.FirstEmptySlot() < 0
            ? "Todos os slots estão cheios: o Slot 1 será sobrescrito."
            : (newGameMode ? "O progresso será salvo automaticamente." : "Cada sobrevivente tem seu próprio nível.");
        RefreshSurvivorLevelChips();
        SelectSurvivor(selected);
        Show("select");
    }

    void SelectSurvivor(SurvivorDef d)
    {
        selected = d;
        for (int i = 0; i < GameDatabase.Survivors.Count && i < cardBtns.Count; i++)
        {
            var def = GameDatabase.Survivors[i];
            bool on = def == d;
            Color cc = new Color(def.color.r, def.color.g, def.color.b, 1f);
            Color border = on ? Color.Lerp(cc, Color.white, 0.35f) : Color.Lerp(cc, UIKit.Col.Border, 0.55f);
            Color fill = Color.Lerp(UIKit.Col.Surface, cc, on ? 0.30f : 0.12f);
            UIFactory.SetButtonColor(cardBtns[i], border);
            if (i < cardFills.Count && cardFills[i]) cardFills[i].color = fill;
            cardBtns[i].transform.localScale = on ? new Vector3(1.025f, 1.025f, 1f) : Vector3.one;
        }
    }

    void OnConfirmSurvivor()
    {
        if (newGameMode && string.IsNullOrWhiteSpace(PlayerPrefs.GetString(PendingNicknameKey, string.Empty)))
        {
            OpenNicknameForNewGame();
            Toast("Defina seu nick antes de iniciar a campanha.");
            return;
        }
        if (newGameMode)
        {
            int slot = SaveSystem.FirstEmptySlot();
            if (slot < 0) slot = 1;
            var d = new SaveData { slot = slot, selectedSurvivor = selected.id, playerNickname = GetPendingNickname(selected.name) };
            d.GetProgress(selected.id);
            UseSave(d);
            SaveSystem.Save(d);
            newGameMode = false;
        }
        else
        {
            GameSession.Save.SetPrimarySurvivor(selected.id);   // se era o apoio, troca de lugar com o principal
            GameSession.Save.GetProgress(selected.id);
            SaveSystem.Save(GameSession.Save);
        }
        ShowMap();
    }


    // ---------- nickname do jogador ----------
    static string GetPlayerNickname(SaveData save)
    {
        if (save != null && !string.IsNullOrWhiteSpace(save.playerNickname))
            return save.playerNickname.Trim();
        // Legacy saves created before nicknames were introduced get a neutral display name,
        // so the player name does not duplicate the selected class underneath it.
        return "SOBREVIVENTE";
    }

    static string GetPendingNickname(string fallback)
    {
        string pending = PlayerPrefs.GetString(PendingNicknameKey, string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(pending)) return fallback;
        return pending.Length <= 18 ? pending : pending.Substring(0, 18);
    }

    static string TruncateNick(string value, int max)
    {
        if (string.IsNullOrEmpty(value)) return "Sobrevivente";
        return value.Length <= max ? value : value.Substring(0, Mathf.Max(1, max - 1)) + "...";
    }

    void BuildNicknameEditor()
    {
        var editorScreen = EditorUiScreen("nickname");
        if (editorScreen != null) { BindNicknameFromPrefab(editorScreen); return; }

        var s = NewScreen("nickname");
        AddCinematicBackground(s, 0.76f);
        Title(s, "IDENTIDADE DO JOGADOR", 58);
        var panel = UIFactory.Img(s, UIFactory.PanelColor, "NicknamePanel");
        UIFactory.Place(panel.rectTransform, Mid, new Vector2(0, 10), new Vector2(780, 410));

        nicknameHelp = UIFactory.Txt(panel.transform,
            "Defina o nick para novas campanhas.\nUse até 18 caracteres.",
            26, TextAlignmentOptions.Center, UIFactory.Gray, "NicknameHelp");
        UIFactory.Stretch(nicknameHelp.rectTransform, 0.06f, 0.68f, 0.94f, 0.92f);

        var inputGo = new GameObject("NicknameInput", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField));
        inputGo.transform.SetParent(panel.transform, false);
        var inputRect = (RectTransform)inputGo.transform;
        UIFactory.Place(inputRect, Mid, new Vector2(0, 15), new Vector2(620, 78));
        var bg = inputGo.GetComponent<Image>();
        bg.color = new Color(0.035f, 0.045f, 0.055f, 1f);
        bg.raycastTarget = true;

        var viewport = UIFactory.Node("TextViewport", inputGo.transform);
        UIFactory.Inset(UIFactory.Stretch(viewport), 16, 10);
        viewport.gameObject.AddComponent<RectMask2D>();
        var valueText = UIFactory.Txt(viewport, "", 32, TextAlignmentOptions.Left, Color.white, "Text");
        UIFactory.Stretch(valueText.rectTransform);
        valueText.alignment = TextAlignmentOptions.Left;
        valueText.textWrappingMode = TextWrappingModes.NoWrap;
        valueText.overflowMode = TextOverflowModes.Truncate;
        valueText.richText = false;

        var placeholder = UIFactory.Txt(viewport, "Digite seu nick...", 29, TextAlignmentOptions.Left, new Color(0.55f, 0.58f, 0.61f), "Placeholder");
        UIFactory.Stretch(placeholder.rectTransform);
        placeholder.alignment = TextAlignmentOptions.Left;
        placeholder.fontStyle = FontStyles.Italic;
        placeholder.textWrappingMode = TextWrappingModes.NoWrap;
        placeholder.raycastTarget = false;

        nicknameInput = inputGo.GetComponent<TMP_InputField>();
        nicknameInput.targetGraphic = bg;
        nicknameInput.textViewport = viewport;
        nicknameInput.textComponent = valueText;
        nicknameInput.placeholder = placeholder;
        nicknameInput.characterLimit = 18;
        nicknameInput.lineType = TMP_InputField.LineType.SingleLine;
        nicknameInput.contentType = TMP_InputField.ContentType.Standard;
        nicknameInput.onSubmit.AddListener(_ => SaveNickname());

        var cancel = UIFactory.Btn(s, "CANCELAR", 28, CancelNicknameEditor, null, "BtnCancel");
        UIFactory.Place((RectTransform)cancel.transform, BotC, new Vector2(-205, 35), new Vector2(300, 70));
        var save = UIFactory.Btn(s, "CONFIRMAR NICK", 28, SaveNickname, UIFactory.Red, "BtnSave");
        UIFactory.Place((RectTransform)save.transform, BotC, new Vector2(205, 35), new Vector2(300, 70));
    }

    void OpenNicknameEditorFromMainMenu()
    {
        if (ZombitCloud.IsReady) { OpenAccountNick("main", false); return; }
        nicknameEditsCurrentSave = false;
        nicknameForNewGame = false;
        nicknameReturnScreen = "main";
        if (nicknameHelp != null)
            nicknameHelp.text = "Defina o nick para novas campanhas.\nEm uma campanha salva, altere o nick pelo botão do mapa.\nUse até 18 caracteres.";
        if (nicknameInput != null)
        {
            nicknameInput.SetTextWithoutNotify(PlayerPrefs.GetString(PendingNicknameKey, string.Empty));
            Show("nickname");
            nicknameInput.caretPosition = nicknameInput.text.Length;
        }
    }

    void OpenNicknameForNewGame()
    {
        nicknameEditsCurrentSave = false;
        nicknameForNewGame = true;
        nicknameReturnScreen = "main";
        if (nicknameHelp != null)
            nicknameHelp.text = "PRIMEIRO, ESCOLHA SEU NICK.\nEle aparecerá em destaque durante a campanha. Depois você escolherá a classe. Até 18 caracteres.";
        if (nicknameInput != null)
        {
            nicknameInput.SetTextWithoutNotify(PlayerPrefs.GetString(PendingNicknameKey, string.Empty));
            Show("nickname");
            nicknameInput.ActivateInputField();
            nicknameInput.caretPosition = nicknameInput.text.Length;
        }
    }

    void CancelNicknameEditor()
    {
        if (nicknameForAccount)
        {
            bool firstTime = accountNickFirst;
            nicknameForAccount = false;
            accountNickFirst = false;
            // Sem nick a conta nao fica pronta: cancelar o primeiro registro volta para o login.
            if (firstTime) ZombitCloud.SignOut();
            else if (nicknameReturnScreen == "map") ShowMap();
            else Show(nicknameReturnScreen);
            return;
        }
        if (nicknameForNewGame)
        {
            nicknameForNewGame = false;
            newGameMode = false;
            Show("main");
            return;
        }
        Show(nicknameReturnScreen);
    }

    void OpenNicknameEditor()
    {
        if (ZombitCloud.IsReady) { OpenAccountNick("map", false); return; }
        nicknameForNewGame = false;
        if (GameSession.Save == null)
        {
            OpenNicknameEditorFromMainMenu();
            return;
        }
        nicknameEditsCurrentSave = true;
        nicknameReturnScreen = "map";
        if (nicknameHelp != null)
            nicknameHelp.text = "Seu nick aparecerá acima do personagem durante as waves.\nEste nick será salvo no slot atual. Use até 18 caracteres.";
        nicknameInput.SetTextWithoutNotify(GetPlayerNickname(GameSession.Save));
        Show("nickname");
        nicknameInput.caretPosition = nicknameInput.text.Length;
    }

    void SaveNickname()
    {
        if (nicknameInput == null) return;
        string value = (nicknameInput.text ?? string.Empty).Trim();
        value = value.Replace("\n", string.Empty).Replace("\r", string.Empty);
        if (string.IsNullOrWhiteSpace(value))
        {
            Toast("Digite um nick válido antes de salvar.");
            return;
        }
        if (value.Length > 18) value = value.Substring(0, 18);

        if (nicknameForAccount) { SubmitAccountNick(value); return; }

        if (nicknameEditsCurrentSave && GameSession.Save != null)
        {
            GameSession.Save.playerNickname = value;
            try
            {
                if (!GameSession.DebugRun) SaveSystem.Save(GameSession.Save);
                Toast("Nick salvo: " + value);
                ShowMap();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Zombit: falha ao salvar o nick: " + e.Message);
                Toast("Não foi possível salvar o nick. Veja o Console.");
            }
        }
        else
        {
            PlayerPrefs.SetString(PendingNicknameKey, value);
            PlayerPrefs.Save();
            if (nicknameForNewGame)
            {
                nicknameForNewGame = false;
                Toast("Nick definido: " + value + ". Agora escolha sua classe.");
                ShowSelect();
            }
            else
            {
                Toast("Nick guardado para a próxima campanha: " + value);
                Show("main");
            }
        }
    }
}
