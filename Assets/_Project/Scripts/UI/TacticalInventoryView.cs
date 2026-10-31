using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shared visual renderer for menu and mission inventories. Contains no save/equip/sell logic.
public sealed class TacticalInventoryView
{
    const string Art="Art/PCInventoryUI/";
    static readonly Dictionary<string,Sprite> cache=new Dictionary<string,Sprite>();
    public RectTransform Stage,Equipment,Stats,Details,Bag;
    public PcInventoryFit Fit;
    TMP_Text currency;
    RectTransform statsGrid;
    static readonly Color Muted=new Color(0.68f,0.76f,0.79f);
    static readonly ItemSlot[] Categories={ItemSlot.Capacete,ItemSlot.Colete,ItemSlot.Calca,ItemSlot.Botas,ItemSlot.Mochila,ItemSlot.Arma};
    static readonly string[] CategoryNames={"CAPACETES","COLETES","CALÇAS","BOTAS","MOCHILAS","MELHORIAS"};

    static Sprite SpriteFor(string name,bool sliced=false)
    {
        string key=name+(sliced?"#slice":"#plain");if(cache.TryGetValue(key,out var found) && found)return found;
        var tex=Resources.Load<Texture2D>(Art+name);
        if(!tex)throw new InvalidOperationException("Inventário compartilhado: arte ausente "+Art+name);
        var sp=Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),new Vector2(0.5f,0.5f),100f,0,SpriteMeshType.FullRect,sliced?new Vector4(16,16,16,16):Vector4.zero);
        cache[key]=sp;return sp;
    }
    public static void Place(Transform target,float x,float y,float w,float h)
    {
        var rt=target as RectTransform;if(!rt)return;rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(w,h);rt.localScale=Vector3.one;
    }
    static RectTransform Node(Transform parent,string name,float x,float y,float w,float h)
    {
        var rt=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;rt.SetParent(parent,false);Place(rt,x,y,w,h);return rt;
    }
    static Image ImageAt(Transform parent,string name,string art,float x,float y,float w,float h,bool sliced=false)
    {
        var rt=Node(parent,name,x,y,w,h);var im=rt.gameObject.AddComponent<Image>();im.sprite=SpriteFor(art,sliced);im.type=sliced?Image.Type.Sliced:Image.Type.Simple;im.raycastTarget=false;return im;
    }
    static TMP_Text TextAt(Transform parent,string name,string value,float x,float y,float w,float h,float size,Color color,bool wrap=false)
    {
        var rt=Node(parent,name,x,y,w,h);var t=rt.gameObject.AddComponent<TextMeshProUGUI>();if(UITheme.Current.font)t.font=UITheme.Current.font;
        t.text=value;t.fontSize=size;t.enableAutoSizing=true;t.fontSizeMin=Mathf.Min(size,12);t.fontSizeMax=size;t.fontStyle=FontStyles.Bold;t.color=color;t.raycastTarget=false;
        t.alignment=TextAlignmentOptions.MidlineLeft;t.textWrappingMode=wrap?TextWrappingModes.Normal:TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Truncate;return t;
    }
    static RectTransform Panel(Transform parent,string name,string title,float x,float y,float w,float h)
    {
        var image=ImageAt(parent,name,"panel",x,y,w,h,true);
        var tick=UIFactory.Img(image.transform,new Color(0.8f,0.06f,0.09f),"Tick");Place(tick.transform,14,16,5,28);tick.raycastTarget=false;
        TextAt(image.transform,"PanelTitle",title,30,10,w-56,40,22,new Color(0.78f,0.86f,0.88f));return image.rectTransform;
    }
    public static TacticalInventoryView Create(RectTransform screen,string money,Action close,string closeLabel)
    {
        var v=new TacticalInventoryView();
        var blocker=UIFactory.Img(screen,Color.black,"InventoryInputBlocker");UIFactory.Stretch(blocker.rectTransform);blocker.raycastTarget=true;
        v.Stage=Node(screen,"TacticalInventoryStage",0,0,2048,1152);
        v.Fit=v.Stage.gameObject.AddComponent<PcInventoryFit>();v.Fit.viewport=screen;v.Fit.content=v.Stage;v.Fit.escape=close;v.Fit.Apply();
        var bg=UIFactory.Img(v.Stage,Color.white,"Background");bg.sprite=ArtLoader.Get(SceneBackgrounds.DefaultPath);if(!bg.sprite)bg.color=Color.black;UIFactory.Stretch(bg.rectTransform);bg.raycastTarget=false;
        var shade=UIFactory.Img(v.Stage,new Color(0.015f,0.02f,0.03f,0.72f),"Shade");UIFactory.Stretch(shade.rectTransform);shade.raycastTarget=false;
        ImageAt(v.Stage,"Header","panel",24,10,1160,86,true);
        var logo=ImageAt(v.Stage,"Logo","logo",34,13,220,79);logo.preserveAspect=true;
        TextAt(v.Stage,"Title","INVENTÁRIO TÁTICO",275,21,880,62,36,new Color(0.90f,0.86f,0.70f));
        ImageAt(v.Stage,"CurrencyPanel","panel",1200,10,400,86,true);
        v.currency=TextAt(v.Stage,"Currency",money,1220,24,360,54,25,UIKit.Col.Gold);
        var back=ImageAt(v.Stage,"BackButton","red",1616,10,408,86,true);back.raycastTarget=true;
        var button=back.gameObject.AddComponent<Button>();button.targetGraphic=back;button.onClick.AddListener(()=>{ZombitAudio.PlayUI();close?.Invoke();});
        TextAt(back.transform,"Label",closeLabel,28,0,352,86,28,new Color(0.95f,0.90f,0.77f)).alignment=TextAlignmentOptions.Center;
        v.Equipment=Panel(v.Stage,"MapEquipmentPanel","EQUIPAMENTO DO SOBREVIVENTE",24,112,624,710);
        v.Stats=Panel(v.Stage,"MapStatsPanel","ATRIBUTOS DO SOBREVIVENTE",664,112,744,710);
        v.Details=Panel(v.Stage,"MapItemDetailsPanel","DETALHES DO ITEM",1424,112,600,710);
        v.Bag=Panel(v.Stage,"MapBackpackPanel","MOCHILA DE CAMPO",24,844,2000,284);
        return v;
    }
    public void SetMoney(string value){if(currency && currency.text!=value)currency.text=value;}
    public static Transform Find(Transform parent,string name)
    {
        if(!parent)return null;for(int i=0;i<parent.childCount;i++){var c=parent.GetChild(i);if(c.name==name)return c;var found=Find(c,name);if(found)return found;}return null;
    }
    static void Move(Transform parent,string name,float x,float y,float w,float h){var t=Find(parent,name);if(t)Place(t,x,y,w,h);}
    static Sprite Portrait(SurvivorDef def)
    {
        if(def==null)return null;return def.portrait?def.portrait:ArtLoader.Get("Art/Characters/"+def.id+"_portrait");
    }
    public void BuildEquipment(SurvivorDef def,int chipCount,Action<RectTransform,ItemSlot,string,float,float,float> slot,Action<RectTransform,float> chips)
    {
        var body=UIFactory.Img(Equipment,Color.white,"EquipmentCharacter");Place(body.transform,142,64,338,525);
        body.sprite=def!=null?ArtLoader.Get("Art/Characters/"+def.id+"_full"):null;if(!body.sprite)body.sprite=InventoryMannequin.Get(def);if(!body.sprite)body.sprite=Portrait(def);body.preserveAspect=true;body.raycastTarget=false;
        slot(Equipment,ItemSlot.Capacete,"CAPACETE",28,64,96);slot(Equipment,ItemSlot.Mochila,"MOCHILA",28,198,96);
        slot(Equipment,ItemSlot.Colete,"COLETE",28,332,96);slot(Equipment,ItemSlot.Calca,"CALÇAS",28,466,96);
        slot(Equipment,ItemSlot.Botas,"BOTAS",496,64,96);slot(Equipment,ItemSlot.Arma,"MELHORIA",496,332,96);
        var w=WeaponCatalog.ForSurvivor(def);
        TextAt(Equipment,"FixedWeaponLabel","ARMA FIXA",476,198,136,20,16,UIKit.Col.Gold).alignment=TextAlignmentOptions.Center;
        var fixedTile=ImageAt(Equipment,"ExclusiveWeaponDisplay","slot",496,217,96,96,true);fixedTile.color=UIKit.Col.Gold;
        var locked=ImageAt(fixedTile.transform,"Lock","lock",33,9,30,30);locked.preserveAspect=true;
        TextAt(fixedTile.transform,"WeaponName",w.displayName,5,44,86,25,18,UIKit.Col.Gold).alignment=TextAlignmentOptions.Center;
        TextAt(fixedTile.transform,"Fixed","EXCLUSIVA",5,72,86,16,11,new Color(0.70f,0.70f,0.63f)).alignment=TextAlignmentOptions.Center;
        TextAt(Equipment,"AccessoryLabel","ACESSÓRIO",476,466,136,20,16,new Color(0.36f,0.42f,0.45f)).alignment=TextAlignmentOptions.Center;
        var unavailable=ImageAt(Equipment,"UnavailableAccessory","slot",496,485,96,96,true);unavailable.color=new Color(0.26f,0.30f,0.33f);
        TextAt(unavailable.transform,"NoSlot","SEM SLOT",6,30,84,35,13,new Color(0.42f,0.48f,0.50f)).alignment=TextAlignmentOptions.Center;
        var strip=Node(Equipment,"SurvivorStrip",16,610,592,84);var layout=UIFactory.HLayout(strip,10,TextAnchor.MiddleCenter);layout.childForceExpandWidth=false;layout.childForceExpandHeight=false;
        float width=Mathf.Min(110f,(592f-10*Mathf.Max(0,chipCount-1))/Mathf.Max(1,chipCount));chips(strip,width);
    }
    static TMP_Text ChildText(Transform parent,string name){var t=Find(parent,name);return t?t.GetComponent<TMP_Text>():null;}
    RectTransform Stat(string name){return Find(Stats,"Stat_"+name) as RectTransform;}
    RectTransform SmallStat(string name,string label,string value,string sub,Color color)
    {
        var root=ImageAt(statsGrid,"Stat_"+name,"panel",0,0,344,104,true).rectTransform;
        TextAt(root,"Label",label,18,5,308,25,15,Muted);TextAt(root,"Value",value,18,28,308,44,32,color);TextAt(root,"Sub",sub,18,75,308,22,14,Muted);return root;
    }
    public void ArrangeStatistics(SurvivorDef def,SaveData save,bool mission)
    {
        var portrait=UIFactory.Img(Stats,Color.white,"ProfilePortrait");Place(portrait.transform,24,68,128,128);portrait.sprite=Portrait(def);portrait.preserveAspect=true;portrait.raycastTarget=false;
        Move(Stats,"ProfileName",168,65,440,50);Move(Stats,"ProfileClass",168,119,550,28);Move(Stats,"ProfileLevel",620,65,100,50);Move(Stats,"ProfileXp",168,170,550,26);
        var bar=Find(Stats,"ProfileXpBar");if(bar)Place(bar,168,151,550,12);
        else foreach(var image in Stats.GetComponentsInChildren<Image>(true))if(image.type==Image.Type.Filled){Place(image.transform.parent,168,151,550,12);break;}
        TextAt(Stats,"Description",def!=null?def.description:"",24,215,696,90,23,new Color(0.71f,0.79f,0.82f),true);
        statsGrid=Find(Stats,"StatsGrid") as RectTransform;
        if(!statsGrid)
        {
            var hp=Stat("VIDA");if(hp)statsGrid=hp.parent as RectTransform;
            if(statsGrid==Stats)
            {
                var blocks=new List<Transform>();foreach(Transform child in Stats)if(child.name.StartsWith("Stat_",StringComparison.Ordinal))blocks.Add(child);
                statsGrid=Node(Stats,"SharedStatsGrid",20,330,704,338);foreach(var child in blocks)child.SetParent(statsGrid,false);
            }
        }
        if(!statsGrid)return;
        Place(statsGrid,20,330,704,338);
        var w=WeaponCatalog.ForSurvivor(def);
        if(!Stat("CADÊNCIA"))SmallStat("CADÊNCIA",mission?"CADÊNCIA":"CADÊNCIA BASE",(w.shotsPerSecond*(def!=null?def.fireRateMult:1f)).ToString("0.#")+"/s","ARMA DA CLASSE",new Color(1f,0.40f,0.27f));
        if(!Stat("DANO BASE DA ARMA"))SmallStat("DANO BASE DA ARMA","DANO BASE DA ARMA",(w.damage*(def!=null?def.damageMult:1f)).ToString("0.#"),w.displayName,UIKit.Col.Gold);
        if(!Stat("PONTOS DE HABILIDADE"))SmallStat("PONTOS DE HABILIDADE","PONTOS DE HABILIDADE",Points(save,def).ToString(),"PARA GASTAR NA ÁRVORE",UIKit.Col.Gold);
        string[] names={"VIDA","DANO","DEFESA","CADÊNCIA","DANO BASE DA ARMA","PONTOS DE HABILIDADE"};
        for(int i=0;i<names.Length;i++)
        {
            var block=Stat(names[i]);if(!block)continue;Place(block,(i%2)*352+4,(i/2)*112+4,344,104);
            var value=ChildText(block,"Value");if(value){value.enableAutoSizing=true;value.fontSizeMin=18;value.fontSizeMax=38;value.textWrappingMode=TextWrappingModes.NoWrap;}
        }
        if(!mission){var label=ChildText(Stat("DANO"),"Label");if(label)label.text="BÔNUS DE DANO";}
        TextAt(Stats,"StatScope",mission?"EM COMBATE • DANO SEM CRÍTICO • ATRIBUTOS ATUAIS":"BASE/NÍVEL + ITENS • SEM BÔNUS DA ÁRVORE",24,674,696,24,13,new Color(0.45f,0.55f,0.59f));
    }
    static int Points(SaveData save,SurvivorDef def)
    {
        if(save!=null && def!=null && save.progress!=null)foreach(var p in save.progress)if(p!=null && p.id==def.id)return p.skillPoints;return 0;
    }
    void Value(string stat,string value){var t=ChildText(Stat(stat),"Value");if(t && t.text!=value)t.text=value;}
    public void RefreshLiveStats(PlayerStats ps,SurvivorDef def,SaveData save)
    {
        if(!ps)return;
        var level=ChildText(Stats,"ProfileLevel");if(level)level.text="LV. "+ps.Level;
        var xp=ChildText(Stats,"ProfileXp");if(xp)xp.text="XP  "+ps.Xp.ToString("N0")+" / "+ps.XpToNext.ToString("N0");
        foreach(var image in Stats.GetComponentsInChildren<Image>(true))if(image.type==Image.Type.Filled){image.fillAmount=Mathf.Clamp01((float)ps.Xp/Mathf.Max(1,ps.XpToNext));break;}
        var hp=ps.GetComponent<Health>();if(hp)Value("VIDA",Mathf.CeilToInt(hp.Current)+" / "+Mathf.CeilToInt(hp.max));
        Value("DEFESA",ps.TotalDefense.ToString("0.#"));Value("PONTOS DE HABILIDADE",Points(save,def).ToString());
        var h=ps.GetComponent<Hero>();if(h && h.weapon)
        {
            float dmg=(h.weapon.damage+h.damageBonus*h.weapon.BonusScale)*h.damageMult*(1f+h.affixDamage);
            float rate=h.weapon.shotsPerSecond*h.fireRateMult*(1f+h.affixFireRate);
            Value("DANO",dmg.ToString("0.#"));Value("CADÊNCIA",rate.ToString("0.#")+"/s");
        }
    }
    public void Finish(int category,string activeId,float scroll)
    {
        var grid=Bag.GetComponentInChildren<GridLayoutGroup>(true);if(grid){grid.cellSize=new Vector2(120,120);grid.spacing=new Vector2(10,10);grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=15;}
        var vp=Find(Bag,"MapItemsViewport");if(vp){Fit.bag=vp.GetComponent<ScrollRect>();Fit.normalized=scroll;}
        foreach(var b in Stage.GetComponentsInChildren<Button>(true))
        {
            var im=b.targetGraphic as Image;if(!im)continue;
            bool tile=b.name.StartsWith("Item_",StringComparison.Ordinal)||b.name.StartsWith("EquipSlot_",StringComparison.Ordinal)||b.name.StartsWith("Survivor_",StringComparison.Ordinal);
            var text=b.GetComponentInChildren<TMP_Text>(true);string words=text?text.text:"";
            bool active=b.name=="InvCat_TODOS" && category==-1;
            for(int i=0;i<Categories.Length;i++)if(b.name=="InvCat_"+CategoryNames[i] && category==(int)Categories[i])active=true;
            string skin=tile?"slot":words.StartsWith("EQUIPAR",StringComparison.Ordinal)?"green":b.name=="BackButton"||words.StartsWith("VENDER",StringComparison.Ordinal)||words.StartsWith("MELHORAR",StringComparison.Ordinal)||active?"red":"panel";
            im.sprite=SpriteFor(skin,true);im.type=Image.Type.Sliced;im.color=Color.white;
            if(tile)
            {
                var fill=b.transform.Find("Fill");if(fill)fill.gameObject.SetActive(false);
                var c=b.colors.normalColor;
                if(b.name=="Survivor_"+activeId || (Mathf.Abs(c.r-UIKit.Col.Accent.r)<0.01f && Mathf.Abs(c.g-UIKit.Col.Accent.g)<0.01f && Mathf.Abs(c.b-UIKit.Col.Accent.b)<0.01f))UIFactory.SetButtonColor(b,UIKit.Col.Gold);
            }
            else{var colors=b.colors;colors.normalColor=Color.white;colors.selectedColor=Color.white;colors.highlightedColor=new Color(1f,0.93f,0.80f);colors.pressedColor=new Color(0.72f,0.70f,0.64f);b.colors=colors;}
            foreach(var label in b.GetComponentsInChildren<TMP_Text>(true)){label.enableAutoSizing=true;label.fontSizeMax=label.fontSize;label.fontSizeMin=Mathf.Min(12,label.fontSize);label.textWrappingMode=TextWrappingModes.NoWrap;}
        }
        foreach(var rt in Stage.GetComponentsInChildren<RectTransform>(true))if(rt.name.StartsWith("Empty_",StringComparison.Ordinal)){var im=rt.GetComponent<Image>();if(im){im.sprite=SpriteFor("slot",true);im.type=Image.Type.Sliced;im.color=new Color(0.20f,0.27f,0.31f);}}
        Fit.Apply();
    }
}
