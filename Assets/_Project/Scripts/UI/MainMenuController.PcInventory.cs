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
        float scrollY=1f;
        if(pcInvLastCategory==invCategory && pcInvLastSort==invSort && pcInvLastSurvivor==invSurvivorId && screens.TryGetValue("mapInventory",out var previous) && previous)
            foreach(var scr in previous.GetComponentsInChildren<ScrollRect>(true))if(scr.name=="MapItemsViewport")scrollY=scr.verticalNormalizedPosition;
        pcInvLastCategory=invCategory;pcInvLastSort=invSort;pcInvLastSurvivor=invSurvivorId;
        var screen=Rebuild("mapInventory");
        var matte=UIFactory.Img(screen,Color.black,"PcInventoryLetterbox");UIFactory.Stretch(matte.rectTransform);matte.raycastTarget=false;
        var stage=PcInvRect(screen,"PcInventoryStage",0,0,2048,1152);
        var fit=stage.gameObject.AddComponent<PcInventoryFit>();fit.viewport=screen;fit.content=stage;fit.escape=ReturnFromUtility;fit.Apply();
        var bg=UIFactory.Img(stage,Color.white,"Background");bg.sprite=ArtLoader.Get(SceneBackgrounds.DefaultPath);bg.raycastTarget=false;UIFactory.Stretch(bg.rectTransform);
        var shade=UIFactory.Img(stage,new Color(0.015f,0.02f,0.03f,0.72f),"Shade");shade.raycastTarget=false;UIFactory.Stretch(shade.rectTransform);
        PcInvImage(stage,"Header","panel",24,10,1160,86,true);
        var logo=PcInvImage(stage,"Logo","logo",34,13,220,79);logo.preserveAspect=true;
        PcInvText(stage,"Title","INVENTÁRIO TÁTICO",275,21,880,62,36,new Color(0.90f,0.86f,0.70f));
        PcInvImage(stage,"CurrencyPanel","panel",1200,10,400,86,true);
        PcInvText(stage,"Currency","OURO  "+save.gold.ToString("N0")+"    |    SUCATA  "+save.scrap.ToString("N0"),1220,24,360,54,25,UIKit.Col.Gold);
        var back=PcInvImage(stage,"BackButton","red",1616,10,408,86,true);back.raycastTarget=true;
        var bb=back.gameObject.AddComponent<Button>();bb.targetGraphic=back;bb.onClick.AddListener(()=>{ZombitAudio.PlayUI();ReturnFromUtility();});
        PcInvText(back.transform,"Label","VOLTAR",28,0,352,86,28,new Color(0.95f,0.90f,0.77f)).alignment=TextAlignmentOptions.Center;
        var equipment=PcInvPanel(stage,"MapEquipmentPanel","EQUIPAMENTO DO SOBREVIVENTE",24,112,624,710);
        var stats=PcInvPanel(stage,"MapStatsPanel","ATRIBUTOS DO SOBREVIVENTE",664,112,744,710);
        var details=PcInvPanel(stage,"MapItemDetailsPanel","DETALHES DO ITEM",1424,112,600,710);
        var bag=PcInvPanel(stage,"MapBackpackPanel","MOCHILA DE CAMPO",24,844,2000,284);
        BuildMapEquipmentPanel(equipment,save,mapInventorySelectedUid);
        BuildMapStatsPanel(stats,save);ReflowPcInventoryStats(stats,save);
        BuildMapBackpackPanel(bag,save,visible,mapInventorySelectedUid);
        BuildMapItemDetails(details,selectedItem,selectedEquipped,save);
        var grid=bag.GetComponentInChildren<GridLayoutGroup>(true);
        if(grid){grid.cellSize=new Vector2(120,120);grid.spacing=new Vector2(10,10);grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=15;}
        var vp=bag.Find("MapItemsViewport");if(vp){fit.bag=vp.GetComponent<ScrollRect>();fit.normalized=scrollY;}
        ReskinPcInventory(stage);
        Show("mapInventory");fit.Apply();
    }
    void BuildPcEquipmentPanel(RectTransform panel,SaveData save,string selectedUid)
    {
        var def=GameDatabase.GetSurvivor(invSurvivorId);
        var body=UIFactory.Img(panel,Color.white,"EquipmentCharacter");PcInvPlace(body.rectTransform,142,64,338,525);
        body.sprite=ArtLoader.Get("Art/Characters/"+invSurvivorId+"_full");
        if(!body.sprite)body.sprite=InventoryMannequin.Get(def);
        if(!body.sprite)body.sprite=SurvivorPortraitSprite(def);
        body.preserveAspect=true;body.raycastTarget=false;
        const float size=96f,left=28f,right=496f;
        CreateMapEquipmentSlot(panel,save,ItemSlot.Capacete,"CAPACETE",left,64,size,selectedUid);
        CreateMapEquipmentSlot(panel,save,ItemSlot.Mochila,"MOCHILA",left,198,size,selectedUid);
        CreateMapEquipmentSlot(panel,save,ItemSlot.Colete,"COLETE",left,332,size,selectedUid);
        CreateMapEquipmentSlot(panel,save,ItemSlot.Calca,"CALÇAS",left,466,size,selectedUid);
        CreateMapEquipmentSlot(panel,save,ItemSlot.Botas,"BOTAS",right,64,size,selectedUid);
        CreateMapEquipmentSlot(panel,save,ItemSlot.Arma,"MELHORIA",right,332,size,selectedUid);
        // Read-only display. No ItemInstance, Button, drag target or equipment callback exists here.
        var weapon=WeaponCatalog.ForSurvivor(def);
        PcInvText(panel,"FixedWeaponLabel","ARMA FIXA",476,198,136,20,16,UIKit.Col.Gold).alignment=TextAlignmentOptions.Center;
        var fixedTile=PcInvImage(panel,"ExclusiveWeaponDisplay","slot",496,217,96,96,true);fixedTile.color=UIKit.Col.Gold;
        var locked=PcInvImage(fixedTile.transform,"Lock","lock",33,9,30,30);locked.preserveAspect=true;
        PcInvText(fixedTile.transform,"WeaponName",weapon.displayName,5,44,86,25,18,UIKit.Col.Gold).alignment=TextAlignmentOptions.Center;
        PcInvText(fixedTile.transform,"Fixed","EXCLUSIVA",5,72,86,16,11,new Color(0.70f,0.70f,0.63f)).alignment=TextAlignmentOptions.Center;
        PcInvText(panel,"AccessoryLabel","ACESSÓRIO",476,466,136,20,16,new Color(0.36f,0.42f,0.45f)).alignment=TextAlignmentOptions.Center;
        var unavailable=PcInvImage(panel,"UnavailableAccessory","slot",496,485,96,96,true);unavailable.color=new Color(0.26f,0.30f,0.33f);
        PcInvText(unavailable.transform,"NoSlot","SEM SLOT",6,30,84,35,13,new Color(0.42f,0.48f,0.50f)).alignment=TextAlignmentOptions.Center;
        var strip=PcInvRect(panel,"SurvivorStrip",16,610,592,84);
        var layout=UIFactory.HLayout(strip,10,TextAnchor.MiddleCenter);layout.childForceExpandWidth=false;layout.childForceExpandHeight=false;
        float chipW=(592f-10f*Mathf.Max(0,GameDatabase.Survivors.Count-1))/Mathf.Max(1,GameDatabase.Survivors.Count);
        foreach(var sv in GameDatabase.Survivors)BuildSurvivorChip(strip,sv,save,chipW);
    }
    void ReflowPcInventoryStats(RectTransform panel,SaveData save)
    {
        var def=GameDatabase.GetSurvivor(invSurvivorId);
        var portrait=UIFactory.Img(panel,Color.white,"ProfilePortrait");PcInvPlace(portrait.rectTransform,24,68,128,128);portrait.sprite=SurvivorPortraitSprite(def);portrait.preserveAspect=true;portrait.raycastTarget=false;
        MovePcInv(panel,"ProfileName",168,65,440,50);
        MovePcInv(panel,"ProfileClass",168,119,550,28);
        MovePcInv(panel,"ProfileLevel",620,65,100,50);
        MovePcInv(panel,"ProfileXp",168,170,550,26);
        var bar=panel.Find("ProfileXpBar");if(bar)PcInvPlace((RectTransform)bar,168,151,550,12);
        else foreach(var image in panel.GetComponentsInChildren<Image>(true))
            if(image.type==Image.Type.Filled){PcInvPlace((RectTransform)image.transform.parent,168,151,550,12);break;}
        PcInvText(panel,"Description",def!=null?def.description:"",24,215,696,90,23,new Color(0.71f,0.79f,0.82f),true);
        var grid=(RectTransform)panel.Find("StatsGrid");if(!grid)return;
        PcInvPlace(grid,20,330,704,338);
        var blocks=new[]{"VIDA","DANO","DEFESA","PONTOS DE HABILIDADE"};
        int[] cols={0,1,0,1};int[] rows={0,0,1,2};
        for(int i=0;i<blocks.Length;i++)
        {
            var block=(RectTransform)grid.Find("Stat_"+blocks[i]);if(!block)continue;
            PcInvPlace(block,cols[i]*352+4,rows[i]*112+4,344,104);
            if(blocks[i]=="DANO")
            {
                var label=block.Find("Label");if(label)label.GetComponent<TMP_Text>().text="BÔNUS DE DANO";
            }
        }
        var w=WeaponCatalog.ForSurvivor(def);
        BuildPcInvSmallStat(grid,"Cadence","CADÊNCIA BASE",(w.shotsPerSecond*(def!=null?def.fireRateMult:1f)).ToString("0.#")+"/s","ARMA DA CLASSE",356,116,new Color(1f,0.40f,0.27f));
        BuildPcInvSmallStat(grid,"WeaponDamage","DANO BASE DA ARMA",(w.damage*(def!=null?def.damageMult:1f)).ToString("0.#"),w.displayName,4,228,UIKit.Col.Gold);
        PcInvText(panel,"StatScope","BASE/NÍVEL + ITENS • SEM BÔNUS DA ÁRVORE",24,674,696,24,13,new Color(0.45f,0.55f,0.59f));
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
