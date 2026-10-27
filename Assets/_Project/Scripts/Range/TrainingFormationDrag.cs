using UnityEngine;
using UnityEngine.EventSystems;
public sealed class TrainingFormationDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    public ShootingRange owner;
    public int slot;
    static TrainingFormationDrag source;
    public void OnBeginDrag(PointerEventData e)
    {
        if(owner != null && owner.CanDragTrainingSlot(slot)) { source=this; transform.localScale=Vector3.one*1.05f; }
    }
    public void OnDrag(PointerEventData e) { }
    public void OnDrop(PointerEventData e)
    {
        if(source != null && source.owner==owner) owner.MoveTrainingSlot(source.slot,slot);
    }
    public void OnEndDrag(PointerEventData e) { source=null;transform.localScale=Vector3.one; }
    void OnDisable() { if(source==this) source=null; }
}
