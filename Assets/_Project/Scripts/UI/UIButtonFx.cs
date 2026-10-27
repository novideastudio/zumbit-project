using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Feedback de hover/clique para botoes: leve aumento ao passar o mouse e "afundar" ao clicar.
/// Usa tempo NAO escalado, entao funciona tambem nas telas de pausa e de resultado (timeScale = 0).
/// E adicionado automaticamente por UIFactory.Btn.
/// </summary>
[DisallowMultipleComponent]
public sealed class UIButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    const float HoverScale = 1.035f;
    const float PressScale = 0.965f;
    const float Speed = 18f;

    Selectable selectable;
    Vector3 baseScale = Vector3.one;
    float current = 1f, target = 1f;
    bool hovered;

    void Awake()
    {
        selectable = GetComponent<Selectable>();
        baseScale = transform.localScale;
    }

    void OnEnable()
    {
        hovered = false;
        current = target = 1f;
        transform.localScale = baseScale;
    }

    void OnDisable() { transform.localScale = baseScale; }

    bool Interactable => selectable == null || selectable.IsInteractable();

    public void OnPointerEnter(PointerEventData e) { hovered = true; target = Interactable ? HoverScale : 1f; }
    public void OnPointerExit(PointerEventData e) { hovered = false; target = 1f; }
    public void OnPointerDown(PointerEventData e) { if (Interactable) target = PressScale; }
    public void OnPointerUp(PointerEventData e) { target = hovered && Interactable ? HoverScale : 1f; }

    void Update()
    {
        if (Mathf.Approximately(current, target)) return;
        current = Mathf.Lerp(current, target, 1f - Mathf.Exp(-Speed * Time.unscaledDeltaTime));
        if (Mathf.Abs(current - target) < 0.0005f) current = target;
        transform.localScale = baseScale * current;
    }
}
