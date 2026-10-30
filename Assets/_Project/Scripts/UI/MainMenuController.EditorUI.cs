using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Telas do menu vindas de PREFABS (editaveis na Unity), em vez de montadas por codigo.
// Ativado por UITheme.useEditorUI. Se o prefab nao existir, cai automaticamente no codigo antigo.
// Os prefabs sao gerados por ZomBit > UI > 1 - Gerar prefabs das telas (Editor/UIBaker.cs).
public partial class MainMenuController
{
    const string EditorUiResourcesPath = "UI/Screens/Screen_";

    // true somente enquanto o UIBaker gera os prefabs (nao anexa efeitos de runtime, nao le prefabs).
    bool editorUiBaking;

    // Chaves das telas que vieram de prefab (o codigo nao deve sobrescrever o visual delas).
    readonly HashSet<string> prefabScreens = new HashSet<string>();

    /// <summary>Instancia o prefab da tela. Retorna null (= usar codigo antigo) se a chave estiver desligada ou o prefab faltar.</summary>
    RectTransform EditorUiScreen(string key, bool replace = false)
    {
        if (editorUiBaking || !UITheme.Current.useEditorUI) return null;
        var prefab = Resources.Load<GameObject>(EditorUiResourcesPath + key);
        if (prefab == null)
        {
            Debug.LogWarning("[ZomBit UI] Prefab 'Screen_" + key + "' nao encontrado em Resources/UI/Screens. Usando a UI em codigo para essa tela. " +
                             "Rode ZomBit > UI > 1 - Gerar prefabs das telas.");
            return null;
        }
        if (replace && screens.TryGetValue(key, out var old) && old != null)
        {
            old.gameObject.SetActive(false);
            Destroy(old.gameObject);
        }
        var go = Instantiate(prefab, root, false);
        go.name = key;
        go.SetActive(false);
        var rt = (RectTransform)go.transform;
        NormalizePrefabScreenRoot(rt);
        screens[key] = rt;
        prefabScreens.Add(key);
        return rt;
    }

    /// <summary>
    /// Os prefabs Screen_* trazem Canvas + CanvasScaler + GraphicRaycaster na raiz so para o preview no Prefab Mode
    /// (escala 0 / ancoras 0 / tamanho 0 = valores "dirigidos" pelo Canvas do Editor). Em jogo a tela vive dentro do
    /// MenuCanvas, entao removemos esses componentes e esticamos a raiz para preencher o pai. Sem isso a tela fica
    /// invisivel (escala 0) e o jogo parece travado.
    /// </summary>
    static void NormalizePrefabScreenRoot(RectTransform rt)
    {
        if (rt == null) return;
        var raycaster = rt.GetComponent<GraphicRaycaster>();
        if (raycaster != null) DestroyImmediate(raycaster);
        var scaler = rt.GetComponent<CanvasScaler>();
        if (scaler != null) DestroyImmediate(scaler);
        var nestedCanvas = rt.GetComponent<Canvas>();
        if (nestedCanvas != null) DestroyImmediate(nestedCanvas);

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.anchoredPosition3D = Vector3.zero;
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;
    }

    // ---------- modelos (prefabs de peca repetida) ----------
    static readonly HashSet<string> warnedTemplates = new HashSet<string>();

    /// <summary>Prefab-modelo em Resources/UI/Screens. Retorna null (= usar o codigo antigo) se desligado, em bake ou ausente.</summary>
    GameObject EditorUiTemplate(string prefabName)
    {
        if (editorUiBaking || !UITheme.Current.useEditorUI) return null;
        var prefab = Resources.Load<GameObject>(EditorUiBinding.ResourcesFolder + prefabName);
        if (prefab == null && warnedTemplates.Add(prefabName))
            Debug.LogWarning("[ZomBit UI] Prefab '" + prefabName + "' nao encontrado. Usando a UI em codigo. " +
                             "Rode ZomBit > UI > 1b - Gerar so os prefabs que faltam.");
        return prefab;
    }

    // Marcador de local no mapa. Posicao e cores de ESTADO (bloqueado/concluido/pendente) vem do codigo;
    // tamanho, sprites, fonte, textos fixos e posicao das pecas internas vem do prefab.
    bool BuildNodeFromPrefab(Transform parent, LocationDef loc, int index, SaveData save,
        bool unlocked, bool done, Color fill, Color border)
    {
        var prefab = EditorUiTemplate("Node_Location");
        if (prefab == null) return false;
        var go = Instantiate(prefab, parent, false);
        var button = go.GetComponent<Button>();
        if (button == null)
        {
            Debug.LogError("[ZomBit UI] Node_Location precisa do componente Button.");
            Destroy(go);
            return false;
        }
        go.name = "Node_" + loc.id;
        go.SetActive(true);

        // mapX/mapY sao normalizados dentro do MapMarkerLayer: ancora = posicao, pivo no centro.
        var rt = (RectTransform)go.transform;
        Vector2 anchor = new Vector2(Mathf.Clamp01(loc.mapX), Mathf.Clamp01(loc.mapY));
        rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.localRotation = Quaternion.identity; rt.localScale = Vector3.one;

        button.interactable = true;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = fill;
        colors.highlightedColor = Color.Lerp(fill, UIKit.Col.Gold, 0.18f);
        colors.pressedColor = Color.Lerp(fill, Color.black, 0.25f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.12f, 0.13f, 0.14f, 0.95f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        EditorUiSetText(rt, "Num", (index + 1).ToString("00"), unlocked ? UIKit.Col.Gold : UIKit.Col.TextDim);
        EditorUiSetText(rt, "LocationName", loc.name.ToUpperInvariant(), unlocked ? Color.white : new Color(0.72f, 0.77f, 0.80f));

        for (int i = 0; i < 4; i++)
        {
            var star = EditorUiBinding.FindDeep(rt, "Star_" + i);
            if (star == null) continue;
            star.gameObject.SetActive(unlocked);
            var img = star.GetComponent<Image>();
            if (img != null) img.color = save.IsCompleted(loc.id, i + 1) ? UIKit.Col.Gold : new Color(0.28f, 0.32f, 0.36f, 1f);
        }
        var lockLevel = EditorUiBinding.FindDeep(rt, "LockLevel");
        if (lockLevel != null)
        {
            lockLevel.gameObject.SetActive(!unlocked);
            var t = lockLevel.GetComponent<TMP_Text>();
            if (t != null) t.text = "NÍVEL " + loc.recommendedLevel;
        }
        var doneBadge = EditorUiBinding.FindDeep(rt, "DoneBadge");
        if (doneBadge != null) doneBadge.gameObject.SetActive(done);
        var lockIcon = EditorUiBinding.FindDeep(rt, "LockIcon");
        if (lockIcon != null) lockIcon.gameObject.SetActive(!done && !unlocked);

        var pin = EditorUiBinding.FindDeep(rt, "SelectedPin");
        if (pin != null) { pin.gameObject.SetActive(false); nodePins[loc.id] = pin.gameObject; }

        var outline = go.GetComponent<Outline>();
        if (outline == null) outline = go.AddComponent<Outline>();
        outline.effectColor = border;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.enabled = true;
        nodeOutlines[loc.id] = outline;
        nodeBorders[loc.id] = border;

        var location = loc;
        EditorUiHook(button, () => SelectLocation(location));
        return true;
    }

    // Quadradinho de inimigo no painel da missao. So a cor da borda (tier) e o sprite vem do codigo.
    bool HubEnemyTileFromPrefab(int i, EnemyEntry e, Color border)
    {
        var prefab = EditorUiTemplate("Tile_Hub");
        if (prefab == null || hubEnemyRow == null) return false;
        var go = Instantiate(prefab, hubEnemyRow, false);
        go.name = "Enemy_" + i;
        var b = go.transform.Find("Border");
        if (b != null) { var bi = b.GetComponent<Image>(); if (bi != null) bi.color = border; }
        var icon = EditorUiBinding.FindDeep(go.transform, "Icon");
        if (icon != null)
        {
            var img = icon.GetComponent<Image>();
            bool has = e != null && e.sprite != null;
            if (img != null && has) img.sprite = e.sprite;
            icon.gameObject.SetActive(has);
        }
        return true;
    }

    // Celula de equipamento da barra inferior do hub. Cor da borda por raridade e icone vem do codigo.
    bool BuildEquipCellFromPrefab(Transform grid, ItemSlot slot, ItemInstance item)
    {
        var prefab = EditorUiTemplate("Cell_Equip");
        if (prefab == null) return false;
        var go = Instantiate(prefab, grid, false);
        var b = go.GetComponent<Button>();
        if (b == null)
        {
            Debug.LogError("[ZomBit UI] Cell_Equip precisa do componente Button.");
            Destroy(go);
            return false;
        }
        go.name = "EquipSlot_" + slot;
        EditorUiHook(b, () => NavGo(NavTab.Bag));

        var outline = go.GetComponent<Outline>();
        if (outline != null && item != null) outline.effectColor = RarityColors.Get(item.rarity);

        var icon = EditorUiBinding.FindDeep(go.transform, "Icon");
        var iconImg = icon != null ? icon.GetComponent<Image>() : null;
        if (iconImg != null)
        {
            iconImg.sprite = item != null ? ItemVisual.Icon(item) : ItemVisual.IconForSlot(slot);
            iconImg.color = item != null ? ItemVisual.Tint(item) : new Color(1f, 1f, 1f, 0.28f);
        }
        var up = EditorUiBinding.FindDeep(go.transform, "Upgrade");
        if (up != null)
        {
            bool show = item != null && item.upgradeLevel > 0;
            up.gameObject.SetActive(show);
            var t = up.GetComponent<TMP_Text>();
            if (show && t != null) t.text = "+" + item.upgradeLevel;
        }
        return true;
    }

    // ---------- helpers de ligacao ----------
    static Transform EditorUiFindDeep(Transform parent, string objName) => EditorUiBinding.FindDeep(parent, objName);
    static T EditorUiFind<T>(Transform parent, string objName) where T : Component => EditorUiBinding.Find<T>(parent, objName);
    static void EditorUiHook(Button b, System.Action action) => EditorUiBinding.Hook(b, action);

    // ---------- ligacao das telas ----------
    void BindMainFromPrefab(RectTransform s)
    {
        var art = EditorUiFindDeep(s, "MenuArt");
        if (art != null)
        {
            MenuAmbience.Attach((RectTransform)art);
            // No codigo original os textos eram criados DEPOIS do efeito de fogueira (ficavam por cima das brasas).
            var tag = EditorUiFindDeep(art, "Tagline");
            var chap = EditorUiFindDeep(art, "MainMenuChapter");
            if (tag != null) tag.SetAsLastSibling();
            if (chap != null) chap.SetAsLastSibling();
        }

        EditorUiHook(EditorUiFind<Button>(s, "BtnNewGame"), OnNewGame);
        continueBtn = EditorUiFind<Button>(s, "BtnContinue");
        EditorUiHook(continueBtn, OnContinue);
        EditorUiHook(EditorUiFind<Button>(s, "BtnSlots"), ShowSlots);
        EditorUiHook(EditorUiFind<Button>(s, "BtnOptions"), () => { RefreshOptions(); Show("options"); });
        EditorUiHook(EditorUiFind<Button>(s, "BtnCredits"), () => Show("credits"));
        EditorUiHook(EditorUiFind<Button>(s, "BtnQuit"), OnQuit);
        EditorUiHook(EditorUiFind<Button>(s, "MainNicknameButton"), OpenNicknameEditorFromMainMenu);
    }

    void BindOptionsFromPrefab(RectTransform s)
    {
        EditorUiHook(EditorUiFind<Button>(s, "VolMinus"), () => ChangeVolume(-0.1f));
        EditorUiHook(EditorUiFind<Button>(s, "VolPlus"), () => ChangeVolume(0.1f));
        volumeLabel = EditorUiFind<TMP_Text>(s, "VolLabel");

        var fs = EditorUiFind<Button>(s, "BtnFullscreen");
        EditorUiHook(fs, ToggleFullscreen);
        fsLabel = fs != null ? fs.GetComponentInChildren<TMP_Text>() : null;

        var res = EditorUiFind<Button>(s, "BtnResolution");
        EditorUiHook(res, CycleResolution);
        resolutionLabel = res != null ? res.GetComponentInChildren<TMP_Text>() : null;

        var shake = EditorUiFind<Button>(s, "BtnShake");
        EditorUiHook(shake, ToggleScreenShake);
        shakeLabel = shake != null ? shake.GetComponentInChildren<TMP_Text>() : null;

        EditorUiHook(EditorUiFind<Button>(s, "BtnBack"), ReturnFromOptions);
    }

    void BindLoginFromPrefab(RectTransform s)
    {
        loginStatus = EditorUiFind<TMP_Text>(s, "LoginStatus");
        loginGoogleBtn = EditorUiFind<Button>(s, "LoginGoogleButton");
        loginRetryBtn = EditorUiFind<Button>(s, "LoginRetryButton");
        EditorUiHook(loginGoogleBtn, ZombitCloud.SignInWithGoogle);
        EditorUiHook(loginRetryBtn, ZombitCloud.Retry);
        // O texto "ENTRAR COM GOOGLE" vem do PREFAB (edite la). So o modo de teste local troca o texto.
        if (loginGoogleBtn != null && ZombitCloud.IsLocalBackend)
        {
            var t = loginGoogleBtn.GetComponentInChildren<TMP_Text>(true);
            if (t) t.text = "ENTRAR (CONTA LOCAL DE TESTE)";
        }
        if (loginRetryBtn != null) loginRetryBtn.gameObject.SetActive(false);
    }

    void BindNicknameFromPrefab(RectTransform s)
    {
        nicknameHelp = EditorUiFind<TMP_Text>(s, "NicknameHelp");
        nicknameInput = EditorUiFind<TMP_InputField>(s, "NicknameInput");
        if (nicknameInput != null) nicknameInput.onSubmit.AddListener(_ => SaveNickname());
        EditorUiHook(EditorUiFind<Button>(s, "BtnCancel"), CancelNicknameEditor);
        EditorUiHook(EditorUiFind<Button>(s, "BtnSave"), SaveNickname);
    }

    // ---------- selecao de sobrevivente ----------
    void BindSelectFromPrefab(RectTransform s)
    {
        playerIdentityInSelect = EditorUiFind<TMP_Text>(s, "NewGamePlayerIdentity");
        selNote = EditorUiFind<TMP_Text>(s, "Note");
        var cards = EditorUiFind<RectTransform>(s, "Cards");

        cardBtns.Clear(); cardImgs.Clear(); cardFills.Clear();
        EditorUiClearChildren(cards);
        if (cards != null)
            foreach (var def in GameDatabase.Survivors)
                if (!FillSurvivorCardFromPrefab(cards, def)) BuildSurvivorCardCode(cards, def);

        EditorUiHook(EditorUiFind<Button>(s, "BackButton"), () => { if (newGameMode) Show("main"); else ShowMap(); });
        var confirm = EditorUiFind<Button>(s, "ConfirmButton");
        EditorUiHook(confirm, OnConfirmSurvivor);
        confirmLabel = confirm != null ? confirm.GetComponentInChildren<TMP_Text>() : null;
    }

    // O chip "NV. x" das cartas era decidido so no Start(), antes de existir save (Continuar / Carregar campanha).
    // Aqui ele e atualizado toda vez que a tela abre. Leitura SEM efeito colateral (GetProgress criaria entradas no save).
    void RefreshSurvivorLevelChips()
    {
        var save = GameSession.Save;
        bool showChip = !newGameMode && save != null;
        for (int i = 0; i < cardBtns.Count && i < GameDatabase.Survivors.Count; i++)
        {
            if (cardBtns[i] == null) continue;
            var chip = EditorUiBinding.FindDeep(cardBtns[i].transform, "LevelChip");
            if (chip == null) continue;
            chip.gameObject.SetActive(showChip);
            if (!showChip) continue;
            int level = 1;
            string id = GameDatabase.Survivors[i].id;
            if (save.progress != null)
                foreach (var p in save.progress)
                    if (p != null && p.id == id) { level = p.level; break; }
            var label = EditorUiBinding.FindDeep(chip, "Level");
            var tmp = label != null ? label.GetComponent<TMP_Text>() : null;
            if (tmp != null) tmp.text = "NV. " + level;
        }
    }

    // Os prefabs de tela trazem copias de PREVIA (para voce enxergar o layout no Editor). Em jogo elas sao descartadas.
    static void EditorUiClearChildren(Transform t)
    {
        if (t == null) return;
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            var c = t.GetChild(i).gameObject;
            c.SetActive(false);   // some do layout ja neste frame
            Destroy(c);
        }
    }

    static void EditorUiSetText(Transform root, string objName, string value, Color? color = null)
    {
        var t = EditorUiFind<TMP_Text>(root, objName);
        if (t == null) return;
        t.text = value;
        if (color.HasValue) t.color = color.Value;
    }

    // Instancia o prefab Card_Survivor e preenche com os dados da classe (cores, textos, arte).
    bool FillSurvivorCardFromPrefab(RectTransform cards, SurvivorDef d)
    {
        var prefab = Resources.Load<GameObject>(EditorUiBinding.ResourcesFolder + "Card_Survivor");
        if (prefab == null)
        {
            Debug.LogWarning("[ZomBit UI] Prefab 'Card_Survivor' nao encontrado. Usando a carta em codigo.");
            return false;
        }
        var go = Instantiate(prefab, cards, false);
        go.name = "Card_" + d.id;
        var card = go.GetComponent<Button>();
        var rt = go.transform;
        if (card == null) { Debug.LogError("[ZomBit UI] Card_Survivor precisa do componente Button."); Destroy(go); return false; }

        var blurb = BlurbFor(d);
        Color cc = new Color(d.color.r, d.color.g, d.color.b, 1f);
        Color dim = Color.Lerp(cc, Color.black, 0.45f);
        Color boxFill = new Color(0.02f, 0.035f, 0.047f, 0.92f);

        cardBtns.Add(card);
        cardImgs.Add(card.GetComponent<Image>());
        var fillT = rt.Find("Fill");
        cardFills.Add(fillT != null ? fillT.GetComponent<Image>() : null);
        if (fillT != null) fillT.GetComponent<Image>().color = Color.Lerp(UIKit.Col.Surface, cc, 0.12f);

        // arte: a largura/altura da area vem do prefab; so a altura da imagem depende da proporcao do sprite
        var art = EditorUiFind<Image>(rt, "Art");
        var mask = EditorUiFindDeep(rt, "ArtMask") as RectTransform;
        if (art != null && mask != null)
        {
            Sprite sp = ArtLoader.Get("Art/Characters/" + d.id + "_card");
            if (sp == null) sp = d.portrait;
            if (sp == null) sp = ArtLoader.Get("Art/Characters/" + d.id + "_portrait");
            art.sprite = sp;
            art.color = sp != null ? Color.white : cc;
            float inner = mask.sizeDelta.x, artH = mask.sizeDelta.y;
            float artImgH = sp != null ? inner * sp.rect.height / Mathf.Max(1f, sp.rect.width) : artH;
            art.rectTransform.sizeDelta = new Vector2(inner, Mathf.Max(artH, artImgH));
        }

        EditorUiSetText(rt, "Name", d.name.ToUpperInvariant());
        EditorUiSetText(rt, "ClassTitle", blurb.title, Color.Lerp(cc, Color.white, 0.2f));

        var chip = EditorUiFindDeep(rt, "LevelChip");
        if (chip != null)
        {
            bool showChip = !newGameMode && GameSession.Save != null;
            chip.gameObject.SetActive(showChip);
            if (showChip) EditorUiSetText(chip, "Level", "NV. " + GameSession.Save.GetProgress(d.id).level);
        }

        var w = WeaponCatalog.Preview(d);
        float dmg = w.damage * d.damageMult;
        float rate = w.shotsPerSecond * d.fireRateMult;
        foreach (var boxName in new[] { "WeaponBox", "AbilityBox", "SpecBox" })
            UIKit.SetFrameColors(EditorUiFindDeep(rt, boxName) as RectTransform, boxFill, dim);
        EditorUiSetText(rt, "WeaponName", w.displayName);
        EditorUiSetText(rt, "WeaponClass", w.weaponClass);
        EditorUiSetText(rt, "WeaponStats", "DANO " + dmg.ToString("0.#") + "  ·  " + rate.ToString("0.#") + "/s  ·  PENTE " + w.magazineSize + "  ·  VIDA x" + d.healthMult.ToString("0.0#"));
        EditorUiSetText(rt, "AbilityName", d.utilityName.ToUpperInvariant(), Color.Lerp(cc, Color.white, 0.15f));
        EditorUiSetText(rt, "AbilityDesc", d.utilityDesc);
        EditorUiSetText(rt, "SpecTitle", "ESPECIALIZAÇÃO", Color.Lerp(cc, Color.white, 0.15f));
        EditorUiSetText(rt, "SpecText", blurb.specialization);
        EditorUiSetText(rt, "IdealFor", blurb.ideal);

        EditorUiHook(card, () => SelectSurvivor(d));
        return true;
    }

    // ---------- carregar campanha ----------
    void BindSlotsFromPrefab(RectTransform s)
    {
        var rows = EditorUiFind<RectTransform>(s, "Rows");
        var rowPrefab = Resources.Load<GameObject>(EditorUiBinding.ResourcesFolder + "Row_slot");
        EditorUiClearChildren(rows);
        if (rows != null)
        {
            for (int i = 1; i <= SaveSystem.SlotCount; i++)
            {
                int slot = i;
                var d = SaveSystem.Load(slot);
                if (rowPrefab == null) { BuildSlotRowCode(rows, slot, d); continue; }

                var rowGo = Instantiate(rowPrefab, rows, false);
                rowGo.name = "Slot" + slot;
                var rt = rowGo.transform;
                EditorUiSetText(rt, "SlotInfo", SlotInfoText(slot, d));

                var load = EditorUiFind<Button>(rt, "BtnLoad");
                EditorUiHook(load, () =>
                {
                    var data = SaveSystem.Load(slot);
                    if (data == null) return;
                    UseSave(data);
                    ShowMap();
                });
                if (load != null) load.interactable = d != null;

                var del = EditorUiFind<Button>(rt, "BtnDelete");
                EditorUiHook(del, () =>
                {
                    SaveSystem.Delete(slot);
                    ShowSlots();
                    Toast($"Slot {slot} apagado.");
                });
                if (del != null) del.interactable = d != null;
            }
        }
        EditorUiHook(EditorUiFind<Button>(s, "BtnBack"), () => Show("main"));
    }

    void BindCreditsFromPrefab(RectTransform s)
    {
        EditorUiHook(EditorUiFind<Button>(s, "BtnBack"), () => Show("main"));
    }

#if UNITY_EDITOR
    /// <summary>
    /// Builds editable visual-layout snapshots for the screens whose data/content remains code-driven.
    /// This is an Editor-only preview using temporary sample data; it never writes a save file.
    /// </summary>
    public static List<KeyValuePair<string, GameObject>> EditorUiBakeLayoutOverrides(out GameObject host)
    {
        host = new GameObject("__ZombitLayoutBake", typeof(RectTransform));
        var canvas = host.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = host.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var oldSave = GameSession.Save;
        var oldSurvivor = GameSession.Survivor;
        var oldPartner = GameSession.Partner;
        var oldThird = GameSession.Third;
        var oldLocation = GameSession.Location;
        bool oldDebugRun = GameSession.DebugRun;
        bool oldOpenMapOnLoad = GameSession.OpenMapOnLoad;
        int oldDifficulty = GameSession.Difficulty;
        bool oldInsurance = GameSession.InsuranceBought;
        bool oldSuppress = UILayoutPrefabApplier.SuppressApply;

        try
        {
            UILayoutPrefabApplier.SuppressApply = true;
            var save = new SaveData
            {
                slot = 0,
                selectedSurvivor = "assaulter",
                playerNickname = "PREVIEW OPERADOR",
                gold = 1850,
                scrap = 75,
                starterGiven = true
            };
            var prog = save.GetProgress("assaulter");
            prog.level = 10;
            prog.xp = 340;
            prog.skillPoints = 6;
            prog.equipped.Add(new ItemInstance { uid = "layout_equipped_weapon", name = "Fuzil de Assalto", description = "Arma de pré-visualização.", slot = ItemSlot.Arma, rarity = Rarity.Raro, bonusDamage = 6, sellValue = 220 });
            prog.equipped.Add(new ItemInstance { uid = "layout_equipped_vest", name = "Colete Reforçado", description = "Proteção de pré-visualização.", slot = ItemSlot.Colete, rarity = Rarity.Incomum, bonusHealth = 18, bonusDefense = 3, sellValue = 130 });
            save.inventory.Add(new ItemInstance { uid = "layout_bag_helmet", name = "Capacete Tático", description = "Item de exemplo para pré-visualização.", slot = ItemSlot.Capacete, rarity = Rarity.Incomum, bonusHealth = 8, bonusDefense = 2, sellValue = 90 });
            save.inventory.Add(new ItemInstance { uid = "layout_bag_boots", name = "Botas de Combate", description = "Item de exemplo para pré-visualização.", slot = ItemSlot.Botas, rarity = Rarity.Comum, bonusHealth = 4, bonusDefense = 1, sellValue = 55 });
            save.inventory.Add(new ItemInstance { uid = "layout_bag_scope", name = "Mira Óptica", description = "Item de exemplo para pré-visualização.", slot = ItemSlot.Arma, rarity = Rarity.Epico, bonusDamage = 10, sellValue = 240 });

            GameSession.Save = save;
            GameSession.DebugRun = true;
            GameSession.OpenMapOnLoad = false;
            GameSession.Survivor = GameDatabase.GetSurvivor("assaulter");
            GameSession.Partner = null;
            GameSession.Third = null;
            GameSession.Location = GameDatabase.Locations.Count > 0 ? GameDatabase.Locations[0] : null;
            GameSession.Difficulty = (int)Difficulty.Normal;
            GameSession.InsuranceBought = false;

            var ctrl = host.AddComponent<MainMenuController>();
            ctrl.editorUiBaking = true;
            ctrl.root = (RectTransform)host.transform;

            // The same builders used in the game create these layouts. Only the layout snapshots are saved;
            // callbacks and save-dependent values are still populated by the original runtime code.
            ctrl.ShowMap();
            ctrl.ShowMapInventory(null, true);
            ctrl.ShowMapSkills(null);
            ctrl.ShowShop();

            var result = new List<KeyValuePair<string, GameObject>>();
            foreach (string key in new[] { "map", "mapInventory", "mapSkills", "shop" })
                if (ctrl.screens.TryGetValue(key, out var rt) && rt != null)
                    result.Add(new KeyValuePair<string, GameObject>(key, rt.gameObject));
            return result;
        }
        finally
        {
            GameSession.Save = oldSave;
            GameSession.Survivor = oldSurvivor;
            GameSession.Partner = oldPartner;
            GameSession.Third = oldThird;
            GameSession.Location = oldLocation;
            GameSession.DebugRun = oldDebugRun;
            GameSession.OpenMapOnLoad = oldOpenMapOnLoad;
            GameSession.Difficulty = oldDifficulty;
            GameSession.InsuranceBought = oldInsurance;
            UILayoutPrefabApplier.SuppressApply = oldSuppress;
        }
    }

    /// <summary>Usado somente pelo UIBaker (Editor): roda os builders em codigo e devolve as telas prontas.</summary>
    public static List<KeyValuePair<string, GameObject>> EditorUiBake(out GameObject host)
    {
        host = new GameObject("__ZombitUiBake", typeof(RectTransform));
        var canvas = host.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = host.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var ctrl = host.AddComponent<MainMenuController>();
        ctrl.editorUiBaking = true;
        ctrl.root = (RectTransform)host.transform;
        ctrl.BuildMain();
        ctrl.BuildOptions();
        ctrl.BuildCredits();
        ctrl.BuildLogin();
        ctrl.BuildNicknameEditor();
        ctrl.BuildSelect();
        var slotsScreen = ctrl.NewScreen("slots");
        ctrl.BuildSlotsFrame(slotsScreen, out _);

        var list = new List<KeyValuePair<string, GameObject>>();
        foreach (var kv in ctrl.screens)
            if (kv.Value) list.Add(new KeyValuePair<string, GameObject>(kv.Key, kv.Value.gameObject));

        // Modelos de peca repetida (carta de sobrevivente e linha de slot), montados com dados de exemplo.
        var tmp = UIFactory.Node("__Templates", ctrl.root);
        if (GameDatabase.Survivors.Count > 0)
        {
            ctrl.BuildSurvivorCardCode(tmp, GameDatabase.Survivors[0]);
            list.Add(new KeyValuePair<string, GameObject>("Card_Survivor", tmp.GetChild(tmp.childCount - 1).gameObject));
        }
        ctrl.BuildSlotRowCode(tmp, 1, null);   // slot vazio: nao embute nenhum save pessoal no prefab
        list.Add(new KeyValuePair<string, GameObject>("Row_slot", tmp.GetChild(tmp.childCount - 1).gameObject));

        // Modelos do hub (mapa): marcador de local, quadrado de inimigo e celula de equipamento.
        if (GameDatabase.Locations.Count > 0)
        {
            ctrl.BuildNode(tmp, GameDatabase.Locations[0], 0, new SaveData(), 1);
            list.Add(new KeyValuePair<string, GameObject>("Node_Location", tmp.GetChild(tmp.childCount - 1).gameObject));
        }
        var tile = ctrl.HubTile(tmp, "Tile_Hub", UIKit.Star, UIKit.Col.Border, Color.white, 4f);   // Star = icone de exemplo
        list.Add(new KeyValuePair<string, GameObject>("Tile_Hub", tile.gameObject));
        ctrl.BuildHubEquipCell(tmp, ItemSlot.Colete, null);
        list.Add(new KeyValuePair<string, GameObject>("Cell_Equip", tmp.GetChild(tmp.childCount - 1).gameObject));
        return list;
    }
#endif
}
