using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Area clicavel de um sobrevivente sobre a arte da fogueira (tela inicial). No modo CALIBRAR (tecla F9)
/// ela pode ser arrastada para cima do personagem certo; a posicao fica gravada no aparelho (PlayerPrefs).
/// </summary>
public sealed class HomeSeatDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public static bool Calibrating;

    public RectTransform art;
    public string survivorId;
    public Image hit;
    public Action<string, Vector2> onMoved;
    public Action<string> onDropped;

    public void OnBeginDrag(PointerEventData e) { }

    public void OnDrag(PointerEventData e)
    {
        if (!Calibrating || art == null) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(art, e.position, e.pressEventCamera, out Vector2 local)) return;
        Rect r = art.rect;
        var n = new Vector2(Mathf.Clamp01((local.x - r.xMin) / r.width), Mathf.Clamp01((local.y - r.yMin) / r.height));
        onMoved?.Invoke(survivorId, n);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (Calibrating) onDropped?.Invoke(survivorId);
    }

    void Update()
    {
        if (hit != null) hit.color = Calibrating ? new Color(1f, 0.9f, 0.3f, 0.16f) : new Color(0f, 0f, 0f, 0f);
    }
}

/// <summary>Liga/desliga o modo calibrar com a tecla F9 e mostra um aviso na tela.</summary>
public sealed class HomeCalibrationKey : MonoBehaviour
{
    public GameObject notice;

    void OnEnable() { HomeSeatDrag.Calibrating = false; if (notice) notice.SetActive(false); }
    void OnDisable() { HomeSeatDrag.Calibrating = false; }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb.f9Key.wasPressedThisFrame) return;
        HomeSeatDrag.Calibrating = !HomeSeatDrag.Calibrating;
        if (notice) notice.SetActive(HomeSeatDrag.Calibrating);
    }
}
