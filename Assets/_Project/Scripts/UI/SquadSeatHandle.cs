using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Transparent hit area over one squad card. Forwards drag/tap events to SquadFormationSwap.</summary>
public sealed class SquadSeatHandle : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerClickHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    public SquadFormationSwap owner;
    public int seat;

    public void OnBeginDrag(PointerEventData eventData) { if (owner != null) owner.BeginDrag(seat, eventData); }
    public void OnDrag(PointerEventData eventData) { if (owner != null) owner.Drag(eventData); }
    public void OnEndDrag(PointerEventData eventData) { if (owner != null) owner.EndDrag(eventData); }
    public void OnDrop(PointerEventData eventData) { if (owner != null) owner.Drop(seat); }
    public void OnPointerClick(PointerEventData eventData) { if (owner != null) owner.Click(seat); }
    public void OnPointerEnter(PointerEventData eventData) { if (owner != null) owner.Hover(seat); }
    public void OnPointerExit(PointerEventData eventData) { if (owner != null) owner.Hover(-1); }
}
