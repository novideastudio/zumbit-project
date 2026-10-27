using TMPro;
using UnityEngine;
using UnityEngine.UI;
public partial class MainMenuController
{
    static void ExactMapRect(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.localScale=Vector3.one;rt.localRotation=Quaternion.identity;
        rt.anchorMin=min;rt.anchorMax=max;rt.pivot=new Vector2(0.5f,0.5f);
        rt.offsetMin=Vector2.zero;rt.offsetMax=Vector2.zero;
    }
    void BuildExactMapNode(Transform parent,LocationDef loc,int index,SaveData save)
    {
        if(parent==null || loc==null || save==null) return;
        Rect r;
        switch(loc.id)
        {
            case "suburbio":r=new Rect(144f,32f,175f,55f);break;
            case "residencial":r=new Rect(507f,132f,210f,55f);break;
            case "shopping":r=new Rect(784f,242f,206f,55f);break;
            case "hospital":r=new Rect(219f,367f,174f,53f);break;
            case "delegacia":r=new Rect(646f,530f,180f,55f);break;
            case "metro":r=new Rect(1104f,177f,206f,55f);break;
            case "centro":r=new Rect(1277f,373f,205f,54f);break;
            case "base":r=new Rect(1613f,90f,192f,57f);break;
            case "industrial":r=new Rect(1700f,508f,162f,54f);break;
            default:return;
        }
        var go=new GameObject("Node_"+loc.id,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Button));
        var rt=(RectTransform)go.transform;rt.SetParent(parent,false);
        ExactMapRect(rt,new Vector2(r.xMin/1939f,1f-r.yMax/811f),new Vector2(r.xMax/1939f,1f-r.yMin/811f));
        var image=go.GetComponent<Image>();image.sprite=UIFactory.White;image.type=Image.Type.Simple;image.color=new Color(0.015f,0.02f,0.018f,1f);
        var button=go.GetComponent<Button>();button.targetGraphic=image;button.transition=Selectable.Transition.None;
        var location=loc;button.onClick.AddListener(()=>ToggleCampaignLocation(location));
        // No theme button sprite, automatic LayoutElement or hover scaling.
        bool unlocked=GameDatabase.IsUnlocked(loc,save);
        var num=UIFactory.Txt(rt,(index+1).ToString("00"),18,TextAlignmentOptions.Center,unlocked?UIKit.Col.Gold:UIKit.Col.TextDim,"Num");
        ExactMapRect(num.rectTransform,new Vector2(0.01f,0.05f),new Vector2(0.24f,0.95f));
        num.enableAutoSizing=true;num.fontSizeMin=8;num.fontSizeMax=18;num.fontStyle=FontStyles.Bold;num.raycastTarget=false;
        var name=UIFactory.Txt(rt,loc.name.ToUpperInvariant(),14,TextAlignmentOptions.MidlineLeft,unlocked?Color.white:UIKit.Col.TextDim,"LocationName");
        ExactMapRect(name.rectTransform,new Vector2(0.27f,0.40f),new Vector2(0.98f,0.98f));
        name.fontStyle=FontStyles.Bold;name.enableAutoSizing=true;name.fontSizeMin=7;name.fontSizeMax=14;
        name.textWrappingMode=TextWrappingModes.NoWrap;name.overflowMode=TextOverflowModes.Truncate;name.raycastTarget=false;
        for(int i=0;i<4;i++)
        {
            var starGo=new GameObject("Star_"+i,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
            var sr=(RectTransform)starGo.transform;sr.SetParent(rt,false);
            ExactMapRect(sr,new Vector2(0.28f+i*0.13f,0.07f),new Vector2(0.38f+i*0.13f,0.37f));
            var star=starGo.GetComponent<Image>();star.sprite=UIKit.Star;star.type=Image.Type.Simple;star.preserveAspect=true;
            star.color=save.IsCompleted(loc.id,i+1)?UIKit.Col.Gold:new Color(0.25f,0.28f,0.26f);star.raycastTarget=false;
        }
        Color border=unlocked?UIKit.Col.Gold:new Color(0.35f,0.38f,0.36f);
        var outline=go.AddComponent<Outline>();outline.effectColor=border;outline.effectDistance=new Vector2(1f,-1f);
        nodeOutlines[loc.id]=outline;nodeBorders[loc.id]=border;
    }
}
