using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Botao ESTANDE na barra lateral do mapa + escolha do esquadrao + inicio do estande (parte de MainMenuController).
public partial class MainMenuController
{
    Canvas rangePicker;
    const int RangeMaxSquad = 3;
    readonly System.Collections.Generic.List<string> rangeSel = new System.Collections.Generic.List<string>();
    readonly System.Collections.Generic.Dictionary<string, RectTransform> rangeCards = new System.Collections.Generic.Dictionary<string, RectTransform>();
    readonly System.Collections.Generic.Dictionary<string, GameObject> rangeCardBadge = new System.Collections.Generic.Dictionary<string, GameObject>();
    readonly System.Collections.Generic.Dictionary<string, TMP_Text> rangeCardNum = new System.Collections.Generic.Dictionary<string, TMP_Text>();
    readonly System.Collections.Generic.Dictionary<string, Image> rangeCardPic = new System.Collections.Generic.Dictionary<string, Image>();
    static readonly Color RangeCardFill = new Color(0.05f, 0.07f, 0.09f, 1f);
    TMP_Text rangeCounter;
    Button rangeStartButton;

    void AddRangeSideButton(RectTransform parent)
    {
        var b = UIKit.Button(parent, "ESTANDE", OpenRangePicker, UIKit.BtnStyle.Secondary, 17f, "Side_Range");
        UIFactory.Elem(b.gameObject, 170f, 60f);
        UIFactory.SetButtonColor(b, new Color(0.075f, 0.10f, 0.13f, 0.96f));
        var t = b.GetComponentInChildren<TMP_Text>(true);
        if (t) { t.alignment = TextAlignmentOptions.MidlineLeft; t.margin = new Vector4(22f, 2f, 8f, 2f); }
        var stripe = UIFactory.Img(b.transform, UIKit.Col.Cyan, "SideStripe");
        UIFactory.Stretch(stripe.rectTransform, 0f, 0.12f, 0f, 0.88f);
        stripe.rectTransform.offsetMax = new Vector2(4f, 0f);
        stripe.raycastTarget = false;
    }

    /// <summary>
    /// Janela do estande: retratos dos 5 sobreviventes. Clique seleciona (ate 3), clique de novo desmarca;
    /// com 3 marcados, clicar em outro nao faz nada. O 1o marcado fica na frente do esquadrao.
    /// Nao altera o esquadrao salvo em EQUIPE.
    /// </summary>
    void OpenRangePicker()
    {
        var save = GameSession.Save;
        if (save == null || rangePicker != null) return;
        save.NormalizeSquad();

        // Comeca com o esquadrao atual ja marcado.
        rangeSel.Clear();
        rangeSel.Add(save.selectedSurvivor);
        if (save.HasPartner) rangeSel.Add(save.selectedPartner);
        if (save.HasThird) rangeSel.Add(save.selectedThird);
        rangeCards.Clear(); rangeCardBadge.Clear(); rangeCardNum.Clear(); rangeCardPic.Clear();

        rangePicker = UIFactory.MakeCanvas("RangeSquadPicker", 200);
        var dim = UIFactory.Img(rangePicker.transform, new Color(0.01f, 0.015f, 0.02f, 0.82f), "PickerDim");
        UIFactory.Stretch(dim.rectTransform);
        var panel = UIKit.Frame(rangePicker.transform, "PickerPanel", new Color(0.106f, 0.133f, 0.169f, 0.98f),
            new Color(0.36f, 0.42f, 0.49f, 0.95f), 2f);
        UIFactory.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1120f, 600f));

        var title = UIFactory.Txt(panel, "ESTANDE DE TIRO", 44, TextAlignmentOptions.Center, UIKit.Col.Gold, "PickerTitle");
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(1000f, 60f));
        var sub = UIFactory.Txt(panel, "Clique nos retratos para escolher de 1 a 3 sobreviventes. O primeiro marcado fica na frente.", 21,
            TextAlignmentOptions.Center, UIKit.Col.TextDim, "PickerSub");
        UIFactory.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(1060f, 34f));

        var all = GameDatabase.Survivors;
        const float cardW = 192f, cardH = 270f, gap = 14f;
        float total = all.Count * cardW + (all.Count - 1) * gap;
        for (int i = 0; i < all.Count; i++)
        {
            float x = -total * 0.5f + cardW * 0.5f + i * (cardW + gap);
            BuildRangeCard(panel, all[i], save, new Vector2(x, -150f), new Vector2(cardW, cardH));
        }

        rangeCounter = UIFactory.Txt(panel, "", 22, TextAlignmentOptions.Center, UIKit.Col.Text, "PickerCounter");
        UIFactory.Place(rangeCounter.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 128f), new Vector2(900f, 34f));

        rangeStartButton = UIKit.Button(panel, "ENTRAR NO ESTANDE", () => StartRange(), UIKit.BtnStyle.Primary, 22f, "PickerStart");
        UIFactory.Place((RectTransform)rangeStartButton.transform, new Vector2(0.5f, 0f), new Vector2(-170f, 56f), new Vector2(320f, 64f));
        var cancel = UIKit.Button(panel, "CANCELAR", CloseRangePicker, UIKit.BtnStyle.Danger, 20f, "PickerCancel");
        UIFactory.Place((RectTransform)cancel.transform, new Vector2(0.5f, 0f), new Vector2(170f, 56f), new Vector2(260f, 64f));

        RefreshRangePicker();
    }

    void BuildRangeCard(RectTransform panel, SurvivorDef def, SaveData save, Vector2 pos, Vector2 size)
    {
        var card = UIKit.Frame(panel, "Card_" + def.id, RangeCardFill, UIKit.Col.Border, 3f);
        rangeCards[def.id] = card;
        UIFactory.Place(card, new Vector2(0.5f, 1f), pos + new Vector2(0f, -size.y * 0.5f), size);

        var sp = HubPortrait(def);
        var pic = UIFactory.Img(card, sp != null ? Color.white : def.color, "Portrait");
        if (sp != null) pic.sprite = sp;
        pic.preserveAspect = true;
        pic.raycastTarget = false;
        var pr = UIFactory.Stretch(pic.rectTransform, 0f, 0.2f, 1f, 1f);
        UIFactory.Inset(pr, 8f, 8f);
        rangeCardPic[def.id] = pic;

        var nameT = UIFactory.Txt(card, def.name.ToUpperInvariant(), 22, TextAlignmentOptions.Center, UIKit.Col.Text, "Name");
        nameT.fontStyle = FontStyles.Bold;
        UIFactory.Place(nameT.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 38f), new Vector2(size.x - 12f, 30f));
        var lvl = UIFactory.Txt(card, "NÍV. " + save.GetProgress(def.id).level, 16, TextAlignmentOptions.Center, UIKit.Col.TextDim, "Level");
        UIFactory.Place(lvl.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(size.x - 12f, 24f));

        // numero da ordem (1 = frente)
        var badge = UIKit.Frame(card, "Badge", UIKit.Col.AccentDk, UIKit.Col.Gold, 2f);
        UIFactory.Place(badge, new Vector2(0f, 1f), new Vector2(26f, -26f), new Vector2(40f, 40f));
        var num = UIFactory.Txt(badge, "1", 24, TextAlignmentOptions.Center, UIKit.Col.Gold, "Num");
        UIFactory.Stretch(num.rectTransform);
        foreach (var g in badge.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
        rangeCardBadge[def.id] = badge.gameObject;
        rangeCardNum[def.id] = num;

        // area clicavel por cima de tudo
        var hit = UIFactory.Img(card, new Color(0f, 0f, 0f, 0f), "Hit");
        UIFactory.Stretch(hit.rectTransform);
        hit.raycastTarget = true;
        var btn = hit.gameObject.AddComponent<Button>();
        btn.targetGraphic = hit;
        btn.transition = Selectable.Transition.None;
        string id = def.id;
        btn.onClick.AddListener(() => { ZombitAudio.PlayUI(); ToggleRangeSurvivor(id); });
    }

    void ToggleRangeSurvivor(string id)
    {
        if (rangeSel.Contains(id)) rangeSel.Remove(id);               // clicou de novo: desmarca
        else if (rangeSel.Count < RangeMaxSquad) rangeSel.Add(id);    // marca, ate 3
        // com 3 marcados, clicar em outro nao faz nada
        RefreshRangePicker();
    }

    void RefreshRangePicker()
    {
        foreach (var kv in rangeCards)
        {
            int idx = rangeSel.IndexOf(kv.Key);
            bool on = idx >= 0;
            // marcado: borda dourada e fundo levemente dourado (a moldura do Frame e recolorida)
            UIKit.SetFrameColors(kv.Value, on ? new Color(0.16f, 0.13f, 0.07f, 1f) : RangeCardFill, on ? UIKit.Col.Gold : UIKit.Col.Border);
            rangeCardBadge[kv.Key].SetActive(on);
            if (on) rangeCardNum[kv.Key].text = (idx + 1).ToString();
            rangeCardPic[kv.Key].color = on ? Color.white : new Color(0.62f, 0.62f, 0.62f, 1f);
        }
        if (rangeCounter != null)
        {
            string mode = rangeSel.Count == 1 ? "SOLO" : rangeSel.Count == 2 ? "DUPLA" : rangeSel.Count == 3 ? "TRIO" : "";
            rangeCounter.text = rangeSel.Count == 0
                ? "Escolha pelo menos 1 sobrevivente"
                : rangeSel.Count + "/" + RangeMaxSquad + " selecionados  ·  " + mode;
        }
        if (rangeStartButton != null)
        {
            rangeStartButton.interactable = rangeSel.Count > 0;
            UIFactory.SetButtonColor(rangeStartButton, rangeSel.Count > 0 ? new Color(0.36f, 0.07f, 0.09f, 1f) : new Color(0.075f, 0.10f, 0.13f, 0.7f));
        }
    }

    void CloseRangePicker()
    {
        if (rangePicker != null) Destroy(rangePicker.gameObject);
        rangePicker = null;
    }

    /// <summary>Entra no estande com os sobreviventes marcados (1o = frente). O esquadrao salvo em EQUIPE nao muda.</summary>
    void StartRange()
    {
        var save = GameSession.Save;
        if (save == null || rangeSel.Count == 0) return;
        var chosen = new System.Collections.Generic.List<string>(rangeSel);
        CloseRangePicker();
        GameSession.Location = new LocationDef
        {
            id = "range",
            isRange = true,
            name = "Estande de Tiro",
            description = "Area de treinamento para testar armas, alcance e dano.",
            theme = "escola",
            recommendedLevel = 1,
            difficulty = 1,
            waves = 0,
            countMult = 0f,
            goldReward = 0,
            goldMult = 0f,
            hasBoss = false,
            mapX = 0.5f,
            mapY = 0.5f
        };
        GameSession.Survivor = GameDatabase.GetSurvivor(chosen[0]);
        GameSession.Partner = chosen.Count >= 2 ? GameDatabase.GetSurvivor(chosen[1]) : null;
        GameSession.Third = chosen.Count >= 3 ? GameDatabase.GetSurvivor(chosen[2]) : null;
        GameSession.DebugRun = false;
        GameSession.Difficulty = (int)global::Difficulty.Normal;
        GameSession.InsuranceBought = false;
        SceneManager.LoadScene(GameSession.GameSceneIndex);
    }
}
