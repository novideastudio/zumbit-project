using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// New inventory presentation only: exclusive weapons are not equipment ItemInstances.
public partial class MainMenuController
{
    const string PcInvArt="Art/PCInventoryUI/";
    static readonly Dictionary<string,Sprite> pcInvSprites=new Dictionary<string,Sprite>();
    int pcInvLastCategory=int.MinValue;
    InvSort pcInvLastSort;
    string pcInvLastSurvivor;

    Sprite PcInvSprite(string name,bool slice=false)
    {
        if(pcInvSprites.TryGetValue(name,out var sp) && sp)return sp;
        var tex=Resources.Load<Texture2D>(PcInvArt+name);
        if(!tex)throw new InvalidOperationException("Arte do inventário ausente: "+PcInvArt+name);
        sp=Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),new Vector2(0.5f,0.5f),100f,0,SpriteMeshType.FullRect,slice?new Vector4(16,16,16,16):Vector4.zero);
        pcInvSprites[name]=sp;return sp;
    }
    static RectTransform PcInvRect(Transform parent,string name,float x,float y,float w,float h)
    {
        var rt=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;rt.SetParent(parent,false);
        PcInvPlace(rt,x,y,w,h);return rt;
    }
    static void PcInvPlace(RectTransform rt,float x,float y,float w,float h)
    {
        rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0f,1f);rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(w,h);rt.localScale=Vector3.one;
    }
    Image PcInvImage(Transform parent,string name,string asset,float x,float y,float w,float h,bool sliced=false)
    {
        var rt=PcInvRect(parent,name,x,y,w,h);var im=rt.gameObject.AddComponent<Image>();im.sprite=PcInvSprite(asset,sliced);im.type=sliced?Image.Type.Sliced:Image.Type.Simple;im.raycastTarget=false;return im;
    }
    TMP_Text PcInvText(Transform parent,string name,string text,float x,float y,float w,float h,float size,Color color,bool wrap=false)
    {
        var rt=PcInvRect(parent,name,x,y,w,h);var t=rt.gameObject.AddComponent<TextMeshProUGUI>();
        if(UITheme.Current.font)t.font=UITheme.Current.font;
        t.text=text;t.fontSize=size;t.fontSizeMax=size;t.fontSizeMin=Mathf.Min(size,12f);t.enableAutoSizing=true;
        t.alignment=TextAlignmentOptions.MidlineLeft;t.fontStyle=FontStyles.Bold;t.color=color;t.raycastTarget=false;
        t.textWrappingMode=wrap?TextWrappingModes.Normal:TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Truncate;return t;
    }
    RectTransform PcInvPanel(Transform parent,string name,string title,float x,float y,float w,float h)
    {
        var im=PcInvImage(parent,name,"panel",x,y,w,h,true);
        var tick=UIFactory.Img(im.transform,new Color(0.80f,0.06f,0.09f),"Tick");PcInvPlace(tick.rectTransform,14,16,5,28);tick.raycastTarget=false;
        PcInvText(im.transform,"PanelTitle",title,30,10,w-56,40,22,new Color(0.78f,0.86f,0.88f));
        return im.rectTransform;
    }
    void BuildPcInventoryScreen(SaveData save,List<ItemInstance> visible,ItemInstance selectedItem,bool selectedEquipped)
    {
        BuildSharedMenuInventory(save,visible,selectedItem,selectedEquipped);
    }
    void BuildPcEquipmentPanel(RectTransform panel,SaveData save,string selectedUid)
    {
        BuildSharedMenuEquipment(save,selectedUid);
    }
    void ReflowPcInventoryStats(RectTransform panel,SaveData save)
    {
        sharedInventoryView.ArrangeStatistics(GameDatabase.GetSurvivor(invSurvivorId),save,false);
    }
    void BuildPcInvSmallStat(RectTransform parent,string name,string label,string value,string sub,float x,float y,Color color)
    {
        var f=PcInvImage(parent,name,"panel",x,y,344,104,true);
        PcInvText(f.transform,"Label",label,18,5,308,25,15,new Color(0.60f,0.69f,0.73f));
        PcInvText(f.transform,"Value",value,18,28,308,44,32,color);
        PcInvText(f.transform,"Sub",sub,18,75,308,22,14,new Color(0.48f,0.59f,0.63f));
    }
    static void MovePcInv(RectTransform parent,string name,float x,float y,float w,float h)
    {
        var t=parent.Find(name);if(t)PcInvPlace((RectTransform)t,x,y,w,h);
    }
    void ReskinPcInventory(RectTransform stage)
    {
        foreach(var b in stage.GetComponentsInChildren<Button>(true))
        {
            var im=b.targetGraphic as Image;if(!im)continue;
            bool tile=b.name.StartsWith("Item_") || b.name.StartsWith("EquipSlot_") || b.name.StartsWith("Survivor_");
            var caption=b.GetComponentInChildren<TMP_Text>(true);string words=caption?caption.text:"";
            bool activeTab=b.name=="InvCat_TODOS" && invCategory==InvAll;
            for(int k=0;k<InvCategorySlots.Length;k++)if(b.name=="InvCat_"+InvCategoryNames[k] && invCategory==(int)InvCategorySlots[k])activeTab=true;
            string skin=tile?"slot":words.StartsWith("EQUIPAR")?"green":b.name=="BackButton" || b.name=="UpgradeMapItem" || words.StartsWith("VENDER") || activeTab?"red":"panel";
            im.sprite=PcInvSprite(skin,true);im.type=Image.Type.Sliced;im.color=Color.white;
            if(tile)
            {
                var fill=b.transform.Find("Fill");if(fill)fill.gameObject.SetActive(false);
                if(b.name=="Survivor_"+invSurvivorId)UIFactory.SetButtonColor(b,UIKit.Col.Gold);
            }
            else
            {
                var cb=b.colors;cb.normalColor=Color.white;cb.selectedColor=Color.white;cb.highlightedColor=new Color(1f,0.93f,0.80f);cb.pressedColor=new Color(0.72f,0.70f,0.64f);b.colors=cb;
            }
            foreach(var text in b.GetComponentsInChildren<TMP_Text>(true))
            {
                text.enableAutoSizing=true;text.fontSizeMax=text.fontSize;text.fontSizeMin=Mathf.Min(12f,text.fontSize);text.textWrappingMode=TextWrappingModes.NoWrap;
            }
        }
        foreach(var rt in stage.GetComponentsInChildren<RectTransform>(true))
            if(rt.name.StartsWith("Empty_"))
            {
                var im=rt.GetComponent<Image>();if(im){im.sprite=PcInvSprite("slot",true);im.type=Image.Type.Sliced;im.color=new Color(0.20f,0.27f,0.31f);}
            }
    }
}
