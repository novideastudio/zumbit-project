using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class MainMenuController
{
    void RefPcApplyStrongSelection()
    {
        var save=GameSession.Save;if(save==null)return;
        foreach(var kv in refPcPlates)
        {
            var plate=kv.Value;if(!plate)continue;
            bool selected=save.selectedSurvivor==kv.Key;
            var child=plate.transform.Find("PcSelectionFrame");
            Image frame;
            if(child==null)
            {
                var go=new GameObject("PcSelectionFrame",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
                var rt=(RectTransform)go.transform;rt.SetParent(plate.transform,false);
                rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.pivot=new Vector2(0.5f,0.5f);
                rt.offsetMin=new Vector2(-3f,-3f);rt.offsetMax=new Vector2(3f,3f);
                frame=go.GetComponent<Image>();
                frame.sprite=RefPcSprite("selected_frame");frame.type=Image.Type.Simple;frame.raycastTarget=false;
            }
            else frame=child.GetComponent<Image>();
            if(frame!=null)frame.gameObject.SetActive(selected);
            var name=plate.transform.Find("Class");
            if(name!=null)
            {
                var t=name.GetComponent<TMP_Text>();
                if(t)t.color=selected?new Color(1f,0.80f,0.35f,1f):new Color(0.95f,0.94f,0.85f,1f);
            }
            // The gold border and selected tag are sufficient; avoid a second rectangular Outline.
            if(refPcBorders.TryGetValue(kv.Key,out var outline) && outline)outline.enabled=false;
            if(refPcTags.TryGetValue(kv.Key,out var tag) && tag)tag.SetActive(selected);
        }
    }
}
