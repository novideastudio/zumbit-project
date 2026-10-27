using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Esquadrao: tres slots de sobrevivente no briefing da missao (parte de MainMenuController).
//  Slot 1 = FRENTE (principal, toma o dano primeiro)
//  Slot 2 = APOIO / MEIO (opcional; vazio = solo)
//  Slot 3 = FUNDO (opcional; so entra em campo no PESADELO - modo trio)
// XP dividida (50/50 na dupla, 40% cada no trio), ouro/mochila compartilhados, cada um com a propria arma e habilidade.
public partial class MainMenuController
{
    const int SquadSlotCount = 3;
    readonly RectTransform[] squadSlots = new RectTransform[SquadSlotCount];
    TMP_Text mapNickClass;
    Image mapXpFill;

    /// <summary>O 3o slot so vale no nivel escolhido no mapa = Pesadelo.</summary>
    bool TrioAllowed => selectedDiff >= HeroSquad.TrioDifficulty;
    bool TrioActive(SaveData save) => TrioAllowed && save != null && save.HasThird;

    /// <summary>Cria os 3 cartoes da equipe dentro de um HorizontalLayoutGroup (barra inferior do hub).</summary>
    void BuildSquadSlots(Transform parent)
    {
        for (int i = 0; i < SquadSlotCount; i++)
        {
            var slot = UIKit.Frame(parent, "SquadSlot_" + i, UIKit.Col.Surface, SlotBorder(i, true), 1.5f);
            var le = slot.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 200f;
            le.flexibleWidth = 1f;
            le.flexibleHeight = 1f;
            squadSlots[i] = slot;
        }
        RefreshSquadSlots();
    }

    static readonly Color SquadGreen = new Color(0.42f, 0.88f, 0.57f, 1f);
    static readonly Color SquadCardFill = new Color(0.05f, 0.07f, 0.09f, 1f);

    static string PositionName(int index) => index == 0 ? "FRENTE" : index == 1 ? "MEIO" : "ATRÁS";
    static Color PositionColor(int index) => index == 0 ? UIKit.Col.Accent : index == 1 ? UIKit.Col.Cyan : SquadGreen;

    static Color SlotBorder(int index, bool enabled)
    {
        return enabled ? PositionColor(index) : UIKit.Col.Border;
    }

    void RefreshSquadSlots()
    {
        var save = GameSession.Save;
        if (save == null) return;
        save.NormalizeSquad();
        for (int i = 0; i < SquadSlotCount; i++)
        {
            var slot = squadSlots[i];
            if (!slot) continue;
            for (int c = slot.childCount - 1; c >= 0; c--)
            {
                var child = slot.GetChild(c);
                if (child.name.StartsWith("Slot_"))
                {
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }
            }
            string id = i == 0 ? save.selectedSurvivor : i == 1 ? save.selectedPartner : save.selectedThird;
            var def = string.IsNullOrEmpty(id) ? null : GameDatabase.GetSurvivor(id);
            BuildSlotContent(slot, i, def, save);
        }
        RefreshMapHeader(save);
    }

    // Texto preso ao topo do cartao, da coluna do retrato ate a borda direita.
    TMP_Text SlotText(RectTransform slot, string name, string text, float size, Color color, float y, float h)
    {
        var t = UIFactory.Txt(slot, text, size, TextAlignmentOptions.MidlineLeft, color, name);
        var rt = t.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.offsetMin = new Vector2(104f, -(y + h));
        rt.offsetMax = new Vector2(-8f, -y);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Truncate;
        t.raycastTarget = false;
        return t;
    }

    void BuildSlotContent(RectTransform slot, int index, SurvivorDef def, SaveData save)
    {
        // Slot 3: precisa de um apoio no slot 2; e so conta no Pesadelo.
        bool needsPartner = index == 2 && !save.HasPartner;
        bool locked = index == 2 && !TrioAllowed;
        bool dim = needsPartner || locked;
        Color pos = dim ? UIKit.Col.TextDim : PositionColor(index);
        UIKit.SetFrameColors(slot, dim ? new Color(0.03f, 0.045f, 0.055f, 1f) : UIKit.Col.Surface, SlotBorder(index, !dim));

        // Faixa da posicao: 1 · FRENTE / 2 · MEIO / 3 · ATRÁS
        var ribbon = UIFactory.Img(slot, new Color(pos.r * 0.28f, pos.g * 0.28f, pos.b * 0.28f, 1f), "Slot_Ribbon");
        var rr = ribbon.rectTransform;
        rr.anchorMin = new Vector2(0f, 1f); rr.anchorMax = new Vector2(1f, 1f); rr.pivot = new Vector2(0.5f, 1f);
        rr.offsetMin = new Vector2(2f, -26f); rr.offsetMax = new Vector2(-2f, -2f);
        ribbon.raycastTarget = false;
        var notch = UIFactory.Img(ribbon.transform, pos, "Notch");
        UIFactory.Stretch(notch.rectTransform, 0f, 0f, 0f, 1f);
        notch.rectTransform.offsetMax = new Vector2(4f, 0f);
        notch.raycastTarget = false;
        var role = UIFactory.Txt(ribbon.transform, (index + 1) + "  ·  " + PositionName(index), 12,
            TextAlignmentOptions.MidlineLeft, pos, "Role");
        UIFactory.Stretch(role.rectTransform);
        role.rectTransform.offsetMin = new Vector2(14f, 0f);
        role.fontStyle = FontStyles.Bold;
        role.characterSpacing = 2f;
        role.textWrappingMode = TextWrappingModes.NoWrap;
        role.raycastTarget = false;

        // Retrato (ou "+" no slot vazio).
        var picFrame = UIKit.Frame(slot, "Slot_Portrait", new Color(0.04f, 0.06f, 0.08f, 1f),
            def != null && !dim ? pos : UIKit.Col.Border, 1.5f);
        HubTL(picFrame, 10f, 36f, 80f, 80f);
        if (def != null)
        {
            var pic = UIFactory.Img(picFrame, Color.white, "Portrait");
            pic.preserveAspect = true;
            pic.sprite = HubPortrait(def);
            if (pic.sprite == null) pic.color = def.color;
            if (dim) pic.color = new Color(pic.color.r, pic.color.g, pic.color.b, 0.35f);
            UIFactory.Inset(UIFactory.Stretch(pic.rectTransform), 3f, 3f);
        }
        else
        {
            var plus = UIFactory.Txt(picFrame, "+", 40, TextAlignmentOptions.Center, UIKit.Col.TextDim, "Plus");
            UIFactory.Stretch(plus.rectTransform);
        }
        foreach (var g in picFrame.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;

        string title = def != null ? def.name.ToUpperInvariant() : "VAZIO";
        var name = SlotText(slot, "Slot_Name", title, 20, def != null && !dim ? Color.white : UIKit.Col.TextDim, 36f, 26f);
        name.fontStyle = FontStyles.Bold;
        name.enableAutoSizing = true; name.fontSizeMin = 12f; name.fontSizeMax = 20f;

        string lvlText, wpnText = "";
        if (needsPartner) lvlText = "Defina o MEIO antes";
        else if (def != null)
        {
            lvlText = "NÍV. " + save.GetProgress(def.id).level;
            wpnText = WeaponCatalog.ForSurvivor(def).displayName;
        }
        else lvlText = index == 1 ? "Jogar solo" : "Sem trio";
        var info = SlotText(slot, "Slot_Info", lvlText, 14, def != null && !dim ? UIKit.Col.Gold : UIKit.Col.TextDim, 64f, 18f);
        info.fontStyle = FontStyles.Bold;
        var wpn = SlotText(slot, "Slot_Weapon", wpnText, 12, UIKit.Col.TextDim, 84f, 16f);
        if (locked)
        {
            wpn.text = "SÓ NO PESADELO";
            wpn.color = Difficulties.Get(HeroSquad.TrioDifficulty).color;
            wpn.fontStyle = FontStyles.Bold;
        }

        // Barra de XP sob o retrato.
        if (def != null && !dim)
        {
            var prog = save.GetProgress(def.id);
            float need = Mathf.Max(1, (int)PlayerStats.XpForLevel(prog.level));
            var fill = UIKit.Bar(slot, UIKit.Col.Cyan, prog.xp / need, "Slot_Xp");
            HubTL(fill.transform.parent as RectTransform, 10f, 122f, 80f, 6f);
        }

        if (needsPartner) return;

        // Botao de acao (a carta inteira e clicavel; isto so deixa claro o que acontece).
        var pill = UIKit.Frame(slot, "Slot_Action", new Color(0.06f, 0.09f, 0.11f, 1f),
            def != null && !dim ? pos : UIKit.Col.Border, 1.5f);
        UIFactory.Place(pill, new Vector2(1f, 0f), new Vector2(-10f, 9f), new Vector2(124f, 26f));
        var actionText = UIFactory.Txt(pill, def != null ? "TROCAR" : "ADICIONAR", 12, TextAlignmentOptions.Center,
            def != null ? UIKit.Col.Text : UIKit.Col.TextDim, "Label");
        UIFactory.Stretch(actionText.rectTransform);
        actionText.fontStyle = FontStyles.Bold;
        actionText.characterSpacing = 1.6f;
        foreach (var g in pill.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;

        // Area clicavel por cima de tudo, com realce ao passar o mouse.
        var hit = UIFactory.Img(slot, Color.white, "Slot_Hit");
        UIFactory.Stretch(hit.rectTransform);
        hit.raycastTarget = true;
        hit.canvasRenderer.SetColor(Color.clear);
        var btn = hit.gameObject.AddComponent<Button>();
        btn.targetGraphic = hit;
        btn.transition = Selectable.Transition.ColorTint;
        var cb = btn.colors;
        cb.normalColor = new Color(1f, 1f, 1f, 0f);
        cb.highlightedColor = new Color(1f, 1f, 1f, 0.07f);
        cb.pressedColor = new Color(1f, 1f, 1f, 0.14f);
        cb.selectedColor = cb.normalColor;
        cb.disabledColor = cb.normalColor;
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.08f;
        btn.colors = cb;
        int slotIndex = index;
        btn.onClick.AddListener(() => { ZombitAudio.PlayUI(); OpenSquadPicker(slotIndex); });
    }

    // ---------- janela de escolha do sobrevivente ----------
    Canvas squadPicker;

    void CloseSquadPicker()
    {
        if (squadPicker != null) Destroy(squadPicker.gameObject);
        squadPicker = null;
    }

    void OpenSquadPicker(int index)
    {
        var save = GameSession.Save;
        if (save == null || squadPicker != null) return;
        save.NormalizeSquad();
        var order = new[] { save.selectedSurvivor ?? "", save.selectedPartner ?? "", save.selectedThird ?? "" };

        squadPicker = UIFactory.MakeCanvas("SquadPicker", 200);
        var dim = UIFactory.Img(squadPicker.transform, new Color(0.01f, 0.015f, 0.02f, 0.82f), "PickerDim");
        UIFactory.Stretch(dim.rectTransform);
        dim.raycastTarget = true;
        var dimButton = dim.gameObject.AddComponent<Button>();
        dimButton.targetGraphic = dim;
        dimButton.transition = Selectable.Transition.None;
        dimButton.onClick.AddListener(CloseSquadPicker);

        var panel = UIKit.Frame(squadPicker.transform, "PickerPanel", new Color(0.106f, 0.133f, 0.169f, 0.98f),
            new Color(0.36f, 0.42f, 0.49f, 0.95f), 2f);
        UIFactory.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180f, 560f));
        foreach (var g in panel.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = true;   // cliques no painel nao fecham

        Color pc = PositionColor(index);
        var title = UIFactory.Txt(panel, "ESCOLHER  ·  " + PositionName(index), 40, TextAlignmentOptions.Center, pc, "PickerTitle");
        title.fontStyle = FontStyles.Bold;
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(1000f, 56f));
        string sub = index == 0 ? "Quem lidera o esquadrão e toma o dano primeiro."
            : index == 1 ? "Apoio que luta ao lado do líder. Deixe vazio para jogar solo."
            : "Reserva do trio. Só entra em campo no Pesadelo.";
        var subText = UIFactory.Txt(panel, sub, 21, TextAlignmentOptions.Center, UIKit.Col.Text, "PickerSub");
        UIFactory.Place(subText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(1100f, 30f));
        var note = UIFactory.Txt(panel, "Se o sobrevivente escolhido já estiver no esquadrão, os dois trocam de posição.", 16,
            TextAlignmentOptions.Center, UIKit.Col.TextDim, "PickerNote");
        UIFactory.Place(note.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -116f), new Vector2(1100f, 24f));

        // Opcoes: (vazio, so nos slots 2 e 3) + todos os sobreviventes.
        var options = new List<SurvivorDef>();
        if (index > 0) options.Add(null);
        options.AddRange(GameDatabase.Survivors);
        const float cardW = 172f, cardH = 320f, gap = 12f;
        float total = options.Count * cardW + (options.Count - 1) * gap;
        for (int i = 0; i < options.Count; i++)
        {
            float x = -total * 0.5f + cardW * 0.5f + i * (cardW + gap);
            BuildSquadPickCard(panel, index, options[i], save, order, new Vector2(x, -150f), new Vector2(cardW, cardH));
        }

        var cancel = UIKit.Button(panel, "FECHAR", CloseSquadPicker, UIKit.BtnStyle.Danger, 20f, "PickerCancel");
        UIFactory.Place((RectTransform)cancel.transform, new Vector2(0.5f, 0f), new Vector2(0f, 38f), new Vector2(260f, 56f));
    }

    void BuildSquadPickCard(RectTransform panel, int index, SurvivorDef def, SaveData save, string[] order, Vector2 pos, Vector2 size)
    {
        string id = def != null ? def.id : "";
        bool current = order[index] == id;
        int inSquad = def != null ? System.Array.IndexOf(order, id) : -1;

        var card = UIKit.Frame(panel, "Card_" + (def != null ? def.id : "empty"),
            current ? new Color(0.16f, 0.13f, 0.07f, 1f) : SquadCardFill, current ? UIKit.Col.Gold : UIKit.Col.Border, 3f);
        UIFactory.Place(card, new Vector2(0.5f, 1f), pos + new Vector2(0f, -size.y * 0.5f), size);

        if (def != null)
        {
            var sp = HubPortrait(def);
            var pic = UIFactory.Img(card, sp != null ? Color.white : def.color, "Portrait");
            if (sp != null) pic.sprite = sp;
            pic.preserveAspect = true;
            pic.raycastTarget = false;
            UIFactory.Inset(UIFactory.Stretch(pic.rectTransform, 0f, 0.26f, 1f, 1f), 8f, 8f);

            var nameT = UIFactory.Txt(card, def.name.ToUpperInvariant(), 22, TextAlignmentOptions.Center, UIKit.Col.Text, "Name");
            nameT.fontStyle = FontStyles.Bold;
            UIFactory.Place(nameT.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 56f), new Vector2(size.x - 12f, 28f));
            var lvl = UIFactory.Txt(card, "NÍV. " + save.GetProgress(def.id).level, 16, TextAlignmentOptions.Center, UIKit.Col.Gold, "Level");
            lvl.fontStyle = FontStyles.Bold;
            UIFactory.Place(lvl.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(size.x - 12f, 22f));
            var wpn = UIFactory.Txt(card, WeaponCatalog.ForSurvivor(def).displayName, 14, TextAlignmentOptions.Center, UIKit.Col.TextDim, "Weapon");
            UIFactory.Place(wpn.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(size.x - 12f, 20f));

            // Etiqueta de onde ele esta hoje (e ATUAL, se for o deste slot).
            if (inSquad >= 0)
            {
                Color c = PositionColor(inSquad);
                var chip = UIFactory.Img(card, new Color(c.r * 0.28f, c.g * 0.28f, c.b * 0.28f, 0.95f), "Chip");
                UIFactory.Place(chip.rectTransform, new Vector2(0f, 1f), new Vector2(8f, -8f), new Vector2(size.x - 16f, 24f));
                chip.raycastTarget = false;
                var chipText = UIFactory.Txt(chip.transform, PositionName(inSquad) + (inSquad == index ? "  ·  ATUAL" : ""), 12,
                    TextAlignmentOptions.Center, c, "ChipText");
                UIFactory.Stretch(chipText.rectTransform);
                chipText.fontStyle = FontStyles.Bold;
                chipText.characterSpacing = 1.4f;
                chipText.raycastTarget = false;
            }
        }
        else
        {
            var plus = UIFactory.Txt(card, "—", 64, TextAlignmentOptions.Center, UIKit.Col.TextDim, "Empty");
            UIFactory.Place(plus.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 24f), new Vector2(size.x - 12f, 90f));
            plus.raycastTarget = false;
            var label = UIFactory.Txt(card, index == 1 ? "SEM APOIO" : "SEM TRIO", 22, TextAlignmentOptions.Center,
                current ? UIKit.Col.Gold : UIKit.Col.Text, "Name");
            label.fontStyle = FontStyles.Bold;
            UIFactory.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 56f), new Vector2(size.x - 12f, 28f));
            var hint = UIFactory.Txt(card, index == 1 ? "Jogar solo" : "Duplas apenas", 16, TextAlignmentOptions.Center, UIKit.Col.TextDim, "Hint");
            UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(size.x - 12f, 22f));
            if (current)
            {
                var chip = UIFactory.Img(card, new Color(0.16f, 0.13f, 0.07f, 0.95f), "Chip");
                UIFactory.Place(chip.rectTransform, new Vector2(0f, 1f), new Vector2(8f, -8f), new Vector2(size.x - 16f, 24f));
                chip.raycastTarget = false;
                var chipText = UIFactory.Txt(chip.transform, "ATUAL", 12, TextAlignmentOptions.Center, UIKit.Col.Gold, "ChipText");
                UIFactory.Stretch(chipText.rectTransform);
                chipText.fontStyle = FontStyles.Bold;
                chipText.characterSpacing = 1.4f;
                chipText.raycastTarget = false;
            }
        }

        var hit = UIFactory.Img(card, Color.white, "Hit");
        UIFactory.Stretch(hit.rectTransform);
        hit.raycastTarget = true;
        hit.canvasRenderer.SetColor(Color.clear);
        var btn = hit.gameObject.AddComponent<Button>();
        btn.targetGraphic = hit;
        btn.transition = Selectable.Transition.ColorTint;
        var cb = btn.colors;
        cb.normalColor = new Color(1f, 1f, 1f, 0f);
        cb.highlightedColor = new Color(1f, 1f, 1f, 0.08f);
        cb.pressedColor = new Color(1f, 1f, 1f, 0.16f);
        cb.selectedColor = cb.normalColor;
        cb.disabledColor = cb.normalColor;
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.08f;
        btn.colors = cb;
        btn.onClick.AddListener(() =>
        {
            ZombitAudio.PlayUI();
            CloseSquadPicker();
            ApplySquadPick(index, id);
        });
    }

    /// <summary>
    /// Coloca o sobrevivente na posicao escolhida. Se ele ja estava em outra posicao, os dois trocam de lugar.
    /// O lider (FRENTE) nunca fica vazio.
    /// </summary>
    void ApplySquadPick(int index, string id)
    {
        var save = GameSession.Save;
        if (save == null) return;
        id = id ?? "";
        var order = new[] { save.selectedSurvivor ?? "", save.selectedPartner ?? "", save.selectedThird ?? "" };
        string oldPrimary = order[0];
        if (id == order[index]) return;
        if (index == 0 && id.Length == 0) return;

        int from = -1;
        if (id.Length > 0)
            for (int i = 0; i < order.Length; i++)
                if (order[i] == id) { from = i; break; }
        string displaced = order[index];
        order[index] = id;
        if (from >= 0) order[from] = displaced;
        if (order[0].Length == 0) return;

        if (id.Length > 0) save.GetProgress(id);
        save.selectedSurvivor = order[0];
        save.selectedPartner = order[1];
        save.selectedThird = order[2];
        save.NormalizeSquad();
        TrySaveSquad(save);

        if (save.selectedSurvivor != oldPrimary)
        {
            // O lider muda o nivel recomendado e os marcadores: reconstroi o mapa.
            int keepDiff = selectedDiff;
            ShowMap();
            if (selectedLoc != null && GameDatabase.IsDifficultyUnlocked(selectedLoc, save, keepDiff)) SelectDifficulty(keepDiff);
            return;
        }
        RefreshSquadSlots();
        RefreshDifficultyUI();   // nota da dupla/trio (zumbis x1,5 / x2)
    }

    void TrySaveSquad(SaveData save)
    {
        if (GameSession.DebugRun || save.slot <= 0) return;
        try { SaveSystem.Save(save); }
        catch (System.Exception e) { Debug.LogWarning("Zombit: falha ao salvar a equipe: " + e.Message); }
    }

    void RefreshMapHeader(SaveData save)
    {
        var surv = GameDatabase.GetSurvivor(save.selectedSurvivor);
        if (surv == null) return;
        var prog = save.GetProgress(surv.id);
        if (mapNickClass) mapNickClass.text = "NÍVEL " + prog.level + "  ·  " + surv.name.ToUpperInvariant();
        if (mapXpFill) mapXpFill.fillAmount = Mathf.Clamp01((float)prog.xp / Mathf.Max(1, (int)PlayerStats.XpForLevel(prog.level)));
    }
}
