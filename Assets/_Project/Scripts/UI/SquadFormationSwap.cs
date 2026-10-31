using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Mid-mission seat swap. Dragging one squad card onto another exchanges their battlefield positions
/// (left card = front). Identity roles - primary, inventory owner, saves and rewards - are NOT changed;
/// only seat position and HUD order change. A global cooldown applies after each swap.
/// </summary>
public sealed class SquadFormationSwap : MonoBehaviour
{
    [Tooltip("Seconds before another swap is allowed.")]
    public float cooldownSeconds = 10f;
    [Tooltip("Seconds used to slide vertically to the new seat (and horizontally if SquadCombatPosture is missing).")]
    public float slideSeconds = 1.1f;

    static readonly Color ReadyColor = new Color(0.35f, 0.92f, 0.48f, 1f);
    static readonly Color CooldownColor = new Color(1f, 0.62f, 0.16f, 1f);
    static readonly Color HintColor = new Color(0.72f, 0.80f, 0.84f, 1f);

    readonly List<HeroSquad.Member> order = new List<HeroSquad.Member>();
    readonly List<Image> handleImages = new List<Image>();
    readonly List<TMP_Text> handleLabels = new List<TMP_Text>();
    readonly Vector3[] corners = new Vector3[4];

    Canvas canvas, hudCanvas;
    RectTransform root, barBg;
    CanvasGroup group;
    HudController hud;
    SquadCombatPosture posture;
    MissionFlow flow;
    TMP_Text status, ghost;
    Image barFill;

    float nextSwapTime, messageUntil, nextHudSearch;
    string message;
    Color messageColor;
    int dragSource = -1, hoverSeat = -1, selectedSeat = -1;
    bool ready;

    IEnumerator Start()
    {
        if (GameSession.IsRange) { Destroy(this); yield break; }
        // MissionSetup, HudController and SquadCombatPosture initialise during the first frames.
        yield return null;
        yield return null;
        order.Clear();
        foreach (var m in HeroSquad.Members)
            if (m != null && m.stats != null) order.Add(m);
        if (order.Count < 2) { Destroy(this); yield break; }
        posture = FindFirstObjectByType<SquadCombatPosture>();
        flow = FindFirstObjectByType<MissionFlow>();
        Build();
        ready = true;
    }

    void Build()
    {
        canvas = UIFactory.MakeCanvas("SquadFormationCanvas", 36);
        root = (RectTransform)canvas.transform;
        group = canvas.gameObject.GetComponent<CanvasGroup>();
        if (group == null) group = canvas.gameObject.AddComponent<CanvasGroup>();

        status = UIFactory.Txt(root, "", 14f, TextAlignmentOptions.MidlineLeft, HintColor, "SwapStatus");
        status.textWrappingMode = TextWrappingModes.NoWrap;
        status.overflowMode = TextOverflowModes.Truncate;
        status.fontStyle = FontStyles.Bold;
        status.raycastTarget = false;
        CenterAnchors(status.rectTransform);

        barFill = UIKit.Bar(root, ReadyColor, 1f, "SwapBar");
        barBg = (RectTransform)barFill.transform.parent;
        CenterAnchors(barBg);

        for (int i = 0; i < order.Count; i++)
        {
            var image = UIFactory.Img(root, new Color(1f, 1f, 1f, 0f), "SeatHandle_" + i);
            image.raycastTarget = true;
            CenterAnchors(image.rectTransform);
            var handle = image.gameObject.AddComponent<SquadSeatHandle>();
            handle.owner = this;
            handle.seat = i;

            var label = UIFactory.Txt(image.rectTransform, "", 30f, TextAlignmentOptions.Center, Color.white, "CooldownLabel");
            UIFactory.Stretch(label.rectTransform);
            label.fontStyle = FontStyles.Bold;
            label.raycastTarget = false;

            handleImages.Add(image);
            handleLabels.Add(label);
        }

        ghost = UIFactory.Txt(root, "", 22f, TextAlignmentOptions.Center, Color.white, "SwapGhost");
        CenterAnchors(ghost.rectTransform);
        ghost.rectTransform.sizeDelta = new Vector2(320f, 42f);
        ghost.fontStyle = FontStyles.Bold;
        ghost.raycastTarget = false;
        ghost.gameObject.SetActive(false);
    }

    static void CenterAnchors(RectTransform rt)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
    }

    void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
    }

    void LateUpdate()
    {
        if (!ready) return;
        if (hud == null && Time.unscaledTime >= nextHudSearch)
        {
            nextHudSearch = Time.unscaledTime + 0.5f;
            hud = FindFirstObjectByType<HudController>();
            hudCanvas = hud != null ? hud.GetComponentInParent<Canvas>() : null;
        }

        bool finished = flow != null && flow.IsFinished;
        var toggle = PanelToggle.Instance;
        bool inventoryOpen = toggle != null && toggle.panel != null && toggle.panel.activeInHierarchy;
        bool visible = hud != null && !finished && !inventoryOpen && Time.timeScale > 0f;
        group.alpha = visible ? 1f : 0f;
        group.blocksRaycasts = visible;
        group.interactable = visible;
        if (!visible) { CancelDrag(); return; }

        LayoutOverCards();
        RefreshVisuals();
    }

    void LayoutOverCards()
    {
        Camera hudCam = hudCanvas != null && hudCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? hudCanvas.worldCamera : null;
        Camera myCam = canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 pivotOffset = new Vector2(0.5f - root.pivot.x, 0.5f - root.pivot.y) * root.rect.size;

        float minX = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        bool any = false;
        for (int i = 0; i < handleImages.Count; i++)
        {
            var card = hud.transform.Find("HudSquadCard_" + i) as RectTransform;
            bool ok = card != null && card.gameObject.activeInHierarchy;
            if (handleImages[i].gameObject.activeSelf != ok) handleImages[i].gameObject.SetActive(ok);
            if (!ok) continue;

            card.GetWorldCorners(corners);
            Vector2 s0 = RectTransformUtility.WorldToScreenPoint(hudCam, corners[0]);
            Vector2 s1 = RectTransformUtility.WorldToScreenPoint(hudCam, corners[2]);
            Vector2 l0, l1;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, s0, myCam, out l0);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, s1, myCam, out l1);
            Vector2 center = (l0 + l1) * 0.5f;
            Vector2 size = new Vector2(Mathf.Abs(l1.x - l0.x), Mathf.Abs(l1.y - l0.y));
            var rt = handleImages[i].rectTransform;
            rt.anchoredPosition = center - pivotOffset;
            rt.sizeDelta = size;

            minX = Mathf.Min(minX, center.x - size.x * 0.5f);
            maxX = Mathf.Max(maxX, center.x + size.x * 0.5f);
            maxY = Mathf.Max(maxY, center.y + size.y * 0.5f);
            any = true;
        }

        status.gameObject.SetActive(any);
        barBg.gameObject.SetActive(any);
        if (!any) return;
        float width = Mathf.Max(120f, maxX - minX);
        status.rectTransform.sizeDelta = new Vector2(width, 22f);
        status.rectTransform.anchoredPosition = new Vector2((minX + maxX) * 0.5f, maxY + 16f) - pivotOffset;
        barBg.sizeDelta = new Vector2(width, 4f);
        barBg.anchoredPosition = new Vector2((minX + maxX) * 0.5f, maxY + 3f) - pivotOffset;
    }

    void RefreshVisuals()
    {
        float remain = Mathf.Max(0f, nextSwapTime - Time.time);
        bool cooling = remain > 0.001f;

        if (Time.time < messageUntil) { status.text = message; status.color = messageColor; }
        else if (cooling)
        {
            status.text = "TROCA DE POSIÇÃO DISPONÍVEL EM " + Mathf.CeilToInt(remain) + "s";
            status.color = CooldownColor;
        }
        else
        {
            status.text = "ARRASTE UM CARTÃO SOBRE OUTRO PARA TROCAR A POSIÇÃO (ESQUERDA = FRENTE)";
            status.color = HintColor;
        }

        barFill.fillAmount = cooling && cooldownSeconds > 0f ? 1f - remain / cooldownSeconds : 1f;
        barFill.color = cooling ? CooldownColor : ReadyColor;

        for (int i = 0; i < handleImages.Count; i++)
        {
            Color c = new Color(1f, 1f, 1f, 0f);
            string text = "";
            if (cooling) { c = new Color(0f, 0f, 0f, 0.40f); text = Mathf.CeilToInt(remain) + "s"; }
            if (i == dragSource || i == selectedSeat) c = new Color(1f, 0.78f, 0.28f, 0.30f);
            else if (dragSource >= 0 && i == hoverSeat) c = new Color(0.30f, 0.85f, 1f, 0.28f);
            handleImages[i].color = c;
            handleLabels[i].text = text;
            handleLabels[i].color = CooldownColor;
        }
    }

    string BlockReason()
    {
        if (flow != null && flow.IsFinished) return "MISSÃO ENCERRADA.";
        if (Time.timeScale <= 0f) return "JOGO PAUSADO.";
        float remain = nextSwapTime - Time.time;
        if (remain > 0.001f) return "AGUARDE " + Mathf.CeilToInt(remain) + "s PARA TROCAR DE NOVO.";
        return null;
    }

    void Flash(string text, Color color)
    {
        message = text;
        messageColor = color;
        messageUntil = Time.time + 2.5f;
    }

    // ---------- called by SquadSeatHandle ----------
    public void BeginDrag(int seat, PointerEventData e)
    {
        string reason = BlockReason();
        if (reason != null) { Flash(reason, CooldownColor); return; }
        if (seat < 0 || seat >= order.Count || !order[seat].Alive)
        {
            Flash("SÓ É POSSÍVEL TROCAR SOBREVIVENTES DE PÉ.", CooldownColor);
            return;
        }
        dragSource = seat;
        selectedSeat = -1;
        ghost.text = order[seat].Name.ToUpperInvariant();
        ghost.gameObject.SetActive(true);
        Drag(e);
    }

    public void Drag(PointerEventData e)
    {
        if (dragSource < 0 || ghost == null) return;
        Camera myCam = canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 local;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(root, e.position, myCam, out local))
        {
            Vector2 pivotOffset = new Vector2(0.5f - root.pivot.x, 0.5f - root.pivot.y) * root.rect.size;
            ghost.rectTransform.anchoredPosition = local - pivotOffset + new Vector2(0f, 56f);
        }
    }

    public void EndDrag(PointerEventData e) { CancelDrag(); }

    public void Hover(int seat) { hoverSeat = seat; }

    public void Drop(int seat)
    {
        if (dragSource < 0) return;
        int from = dragSource;
        CancelDrag();
        TrySwap(from, seat);
    }

    public void Click(int seat)
    {
        string reason = BlockReason();
        if (reason != null) { Flash(reason, CooldownColor); selectedSeat = -1; return; }
        if (seat < 0 || seat >= order.Count || !order[seat].Alive)
        {
            Flash("SÓ É POSSÍVEL TROCAR SOBREVIVENTES DE PÉ.", CooldownColor);
            return;
        }
        if (selectedSeat < 0) { selectedSeat = seat; Flash("TOQUE EM OUTRO CARTÃO PARA TROCAR.", HintColor); return; }
        int from = selectedSeat;
        selectedSeat = -1;
        if (from != seat) TrySwap(from, seat);
    }

    void CancelDrag()
    {
        dragSource = -1;
        hoverSeat = -1;
        if (ghost != null) ghost.gameObject.SetActive(false);
    }

    // ---------- swap ----------
    void TrySwap(int a, int b)
    {
        if (a == b || a < 0 || b < 0 || a >= order.Count || b >= order.Count) return;
        string reason = BlockReason();
        if (reason != null) { Flash(reason, CooldownColor); return; }
        var first = order[a];
        var second = order[b];
        if (!first.Alive || !second.Alive)
        {
            Flash("SÓ É POSSÍVEL TROCAR SOBREVIVENTES DE PÉ.", CooldownColor);
            return;
        }

        order[a] = second;
        order[b] = first;
        nextSwapTime = Time.time + Mathf.Max(0f, cooldownSeconds);

        int count = order.Count;
        float frontX = HeroSquad.FrontXFor(count), stepX = HeroSquad.OffsetXFor(count);
        var homeXs = new List<float>(count);
        for (int i = 0; i < count; i++) homeXs.Add(frontX - stepX * i);

        bool handledByPosture = posture != null && posture.ApplySeatOrder(order, homeXs);

        float dyFirst = GameTuning.Current.HeroFeetY(b) - GameTuning.Current.HeroFeetY(a);
        float dxFirst = handledByPosture ? 0f : homeXs[b] - homeXs[a];
        if (first.stats != null) StartCoroutine(Slide(first.stats.transform, new Vector2(dxFirst, dyFirst)));
        if (second.stats != null) StartCoroutine(Slide(second.stats.transform, new Vector2(-dxFirst, -dyFirst)));

        if (hud != null) hud.ApplySeatOrder(order);
        ZombitAudio.PlayUI();
        Flash("FORMAÇÃO: " + OrderText(), ReadyColor);
    }

    string OrderText()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < order.Count; i++)
        {
            if (i > 0) sb.Append("  /  ");
            sb.Append(order[i].Name.ToUpperInvariant());
        }
        return sb.ToString();
    }

    // Additive deltas keep compatibility with movement and bob written by other components.
    IEnumerator Slide(Transform target, Vector2 delta)
    {
        float duration = Mathf.Max(0.05f, slideSeconds);
        float elapsed = 0f, previous = 0f;
        while (elapsed < duration)
        {
            if (target == null) yield break;
            elapsed += Time.deltaTime;
            float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            Vector3 p = target.position;
            p.x += delta.x * (eased - previous);
            p.y += delta.y * (eased - previous);
            target.position = p;
            previous = eased;
            yield return null;
        }
    }
}
