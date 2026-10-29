using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// PC reference camp: artwork matches the approved 1672x941 composition.
// This is a new home renderer only; existing navigation and campaign data remain owned by the game.
public partial class MainMenuController
{
    const string RefPcArt = "Art/PCReferenceUI/";
    static readonly Dictionary<string,Sprite> refPcSprites = new Dictionary<string,Sprite>();
    readonly Dictionary<string,Image> refPcPlates = new Dictionary<string,Image>();
    readonly Dictionary<string,TMP_Text> refPcLevels = new Dictionary<string,TMP_Text>();
    readonly Dictionary<string,GameObject> refPcTags = new Dictionary<string,GameObject>();
    readonly Dictionary<string,Outline> refPcBorders = new Dictionary<string,Outline>();
    TMP_Text refPcNick,refPcClass,refPcPower,refPcGold,refPcScrap;
    Image refPcPortrait,refPcXp;
    GameObject refPcSkillBadge;
    string refPcLastId;
    float refPcLastClick;

    Sprite RefPcSprite(string key)
    {
        if(refPcSprites.TryGetValue(key,out var sprite) && sprite!=null) return sprite;
        var texture=Resources.Load<Texture2D>(RefPcArt+key);
        if(texture==null) throw new InvalidOperationException("Arte PC ausente: "+RefPcArt+key);
        sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(0.5f,0.5f),100f);
        sprite.name="RefPc_"+key;refPcSprites[key]=sprite;
        return sprite;
    }

    static RectTransform RefPcRect(Transform parent,string name,float x,float y,float w,float h)
    {
        var go=new GameObject(name,typeof(RectTransform));
        var rt=(RectTransform)go.transform;rt.SetParent(parent,false);
        rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0f,1f);
        rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(w,h);
        rt.localScale=Vector3.one;
        return rt;
    }

    Image RefPcImage(Transform parent,string name,string art,float x,float y,float w,float h)
    {
        var rt=RefPcRect(parent,name,x,y,w,h);
        var image=rt.gameObject.AddComponent<Image>();
        image.sprite=RefPcSprite(art);image.color=Color.white;
        image.type=Image.Type.Simple;image.raycastTarget=false;
        return image;
    }

    static Image RefPcSolid(Transform parent,string name,Color color,float x,float y,float w,float h)
    {
        var rt=RefPcRect(parent,name,x,y,w,h);
        var image=rt.gameObject.AddComponent<Image>();
        image.sprite=UIFactory.White;image.color=color;image.raycastTarget=false;
        return image;
    }

    TMP_Text RefPcText(Transform parent,string name,string text,float x,float y,float w,float h,float size,Color color,TextAlignmentOptions align=TextAlignmentOptions.MidlineLeft)
    {
        var rt=RefPcRect(parent,name,x,y,w,h);
        var t=rt.gameObject.AddComponent<TextMeshProUGUI>();
        var theme=UITheme.Current;
        if(theme.displayFont!=null) t.font=theme.displayFont;
        else if(theme.font!=null) t.font=theme.font;
        t.text=text;t.fontSize=size;t.fontSizeMax=size;t.fontSizeMin=Mathf.Min(11f,size);
        t.enableAutoSizing=true;t.fontStyle=FontStyles.Bold;t.color=color;t.alignment=align;
        t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Truncate;
        t.raycastTarget=false;t.richText=false;
        return t;
    }

    Button RefPcHit(RectTransform rt,Action action,bool artwork)
    {
        var image=rt.GetComponent<Image>();
        if(image==null) { image=rt.gameObject.AddComponent<Image>();image.color=Color.clear; }
        image.raycastTarget=true;
        var button=rt.gameObject.AddComponent<Button>();button.targetGraphic=image;
        button.transition=artwork?Selectable.Transition.ColorTint:Selectable.Transition.None;
        var c=button.colors;c.normalColor=Color.white;c.highlightedColor=new Color(1f,0.95f,0.82f,1f);
        c.pressedColor=new Color(0.78f,0.75f,0.69f,1f);c.selectedColor=Color.white;c.fadeDuration=0.08f;button.colors=c;
        var navigation=button.navigation;navigation.mode=Navigation.Mode.None;button.navigation=navigation;
        button.onClick.AddListener(()=> { ZombitAudio.PlayUI();action(); });
        return button;
    }

    void RefPcButton(Transform parent,string art,float x,float y,float w,float h,Action action)
    {
        var image=RefPcImage(parent,"ReferenceButton_"+art,art,x,y,w,h);
        RefPcHit(image.rectTransform,action,true);
    }

    void ShowPcReferenceHome()
    {
        var save=GameSession.Save;
        if(save==null) { Show("main");return; }
        save.MigrateFromPreviousVersions();
        // Verify the main asset before replacing the existing screen.
        if(Resources.Load<Texture2D>(RefPcArt+"scene")==null)
        {
            Debug.LogError("Zombit: faltam as artes PCReferenceUI. Instale o ZIP completo.");
            Toast("Artes da tela PC não instaladas.");return;
        }
        homeOrigin=false;homeFocusId=null;homeLastTapId=null;refPcLastId=null;
        homeSeatRects.Clear();homePlates.Clear();homePlateOutlines.Clear();
        refPcPlates.Clear();refPcLevels.Clear();refPcTags.Clear();refPcBorders.Clear();
        var s=Rebuild("home");
        var black=RefPcSolid(s,"PC_Letterbox",Color.black,0,0,1,1);
        UIFactory.Stretch(black.rectTransform);
        var stage=RefPcRect(s,"PC_ReferenceStage",0,0,1672,941);
        stage.anchorMin=stage.anchorMax=stage.pivot=new Vector2(0.5f,0.5f);stage.anchoredPosition=Vector2.zero;
        var fit=stage.gameObject.AddComponent<ReferenceHomeFit>();fit.content=stage;fit.viewport=s;
        fit.Apply();
        RefPcImage(stage,"PC_Scene","scene",0,0,1672,941);

        // All interaction regions are separate GameObjects, aligned to the artwork.
        string[] ids={"assaulter","sniper","medic","engineer","explorer"};
        Rect[] bodies={new Rect(272,385,225,339),new Rect(526,383,233,314),new Rect(797,387,203,297),new Rect(1025,402,241,301),new Rect(1284,399,270,318)};
        Rect[] labels={new Rect(294,276,182,67),new Rect(556,290,167,61),new Rect(813,297,164,63),new Rect(1074,311,161,64),new Rect(1324,311,164,65)};
        for(int i=0;i<ids.Length;i++)
        {
            string id=ids[i];var body=bodies[i];var box=labels[i];
            var area=RefPcRect(stage,"PC_Survivor_"+id,body.x,body.y,body.width,body.height);
            RefPcHit(area,()=>RefPcChooseSurvivor(id),false);
            var plate=RefPcImage(stage,"PC_Plate_"+id,"plate_"+id,box.x,box.y,box.width,box.height);
            RefPcHit(plate.rectTransform,()=>RefPcChooseSurvivor(id),true);
            var def=GameDatabase.GetSurvivor(id);
            RefPcText(plate.transform,"Class",def.name.ToUpperInvariant(),8,6,box.width-16,25,20,new Color(0.95f,0.94f,0.85f),TextAlignmentOptions.Center);
            var level=RefPcText(plate.transform,"Level","",8,30,box.width-16,23,18,new Color(0.62f,0.76f,0.82f),TextAlignmentOptions.Center);
            var border=plate.gameObject.AddComponent<Outline>();border.effectDistance=new Vector2(1f,-1f);
            refPcPlates[id]=plate;refPcLevels[id]=level;refPcBorders[id]=border;
            var tag=RefPcSolid(stage,"PC_Selected_"+id,new Color(0.93f,0.62f,0.13f,1f),box.x+box.width*0.18f,box.y+box.height-1f,box.width*0.64f,20);
            RefPcText(tag.transform,"Label","SELECIONADO",0,0,box.width*0.64f,20,12,Color.black,TextAlignmentOptions.Center);
            refPcTags[id]=tag.gameObject;
        }

        var profile=RefPcImage(stage,"PC_Profile","profile",16,16,430,130);
        RefPcHit(profile.rectTransform,()=> { OpenFromHome(()=> {newGameMode=false;ShowSelect();}); },false);
        refPcPortrait=RefPcImage(profile.transform,"Portrait","portrait_assaulter",10,10,110,110);refPcPortrait.preserveAspect=true;
        refPcNick=RefPcText(profile.transform,"Nick","",145,10,267,33,27,new Color(0.94f,0.88f,0.69f));
        refPcClass=RefPcText(profile.transform,"Class","",145,45,267,25,19,new Color(0.65f,0.77f,0.83f));
        RefPcSolid(profile.transform,"XpTrack",new Color(0.15f,0.20f,0.23f),146,76,259,11);
        refPcXp=RefPcSolid(profile.transform,"XpFill",new Color(0.91f,0.62f,0.17f),146,76,259,11);
        refPcPower=RefPcText(profile.transform,"Power","",146,91,259,29,22,new Color(0.93f,0.83f,0.61f));
        var resources=RefPcImage(stage,"PC_Resources","resources",1265,16,302,74);
        refPcGold=RefPcText(resources.transform,"Gold","",70,15,79,45,26,new Color(0.94f,0.87f,0.68f));
        refPcScrap=RefPcText(resources.transform,"Scrap","",220,15,66,45,26,new Color(0.94f,0.87f,0.68f));
        RefPcButton(stage,"settings",1578,16,77,74,RefPcOpenOptions);
        RefPcButton(stage,"missions",15,177,189,74,()=>Toast("Missões: em breve."));
        RefPcButton(stage,"gifts",15,259,189,75,()=>Toast("Presentes: em breve."));
        RefPcButton(stage,"events",15,342,189,74,()=>Toast("Eventos: em breve."));
        RefPcButton(stage,"shop",15,764,181,146,()=>OpenFromHome(ShowShop));
        RefPcButton(stage,"survivors",205,764,181,146,()=>OpenFromHome(()=> { newGameMode=false;ShowSelect(); }));
        RefPcButton(stage,"skills",393,764,182,146,()=>OpenFromHome(()=>ShowMapSkills(null)));
        RefPcButton(stage,"inventory",582,764,182,146,()=>OpenFromHome(()=>ShowMapInventory(null,true)));
        RefPcButton(stage,"training",770,764,183,146,OpenRangePicker);
        RefPcButton(stage,"campaign",1251,770,404,138,()=> { homeOrigin=false;ShowMap(); });
        var badge=RefPcSolid(stage,"PC_SkillPointsBadge",new Color(0.92f,0.63f,0.14f),549,755,25,25);
        RefPcText(badge.transform,"Mark","!",0,0,25,25,19,Color.black,TextAlignmentOptions.Center);refPcSkillBadge=badge.gameObject;
        var tick=stage.gameObject.AddComponent<ReferenceHomeRefresh>();
        tick.refresh=RefreshPcReferenceHome;
        tick.escape=GoToTitleScreenKeepingHome;
        Show("home");fit.Apply();RefreshPcReferenceHome();
    }

    void RefreshPcReferenceHome()
    {
        var save=GameSession.Save;if(save==null || refPcNick==null) return;
        var def=GameDatabase.GetSurvivor(save.selectedSurvivor);if(def==null)return;
        var p=save.GetProgress(def.id);
        string nick=TruncateNick(GetPlayerNickname(save),18).ToUpperInvariant();
        if(refPcNick.text!=nick) refPcNick.text=nick;
        string identity=def.name.ToUpperInvariant()+" · NV. "+p.level;
        if(refPcClass.text!=identity)refPcClass.text=identity;
        refPcPower.text="PODER "+PowerRating.Of(save,def.id);
        refPcGold.text=save.gold.ToString("N0");refPcScrap.text=save.scrap.ToString("N0");
        float ratio=Mathf.Clamp01((float)p.xp/Mathf.Max(1,PlayerStats.XpForLevel(p.level)));
        refPcXp.rectTransform.sizeDelta=new Vector2(259f*ratio,11f);
        var portrait=def.id=="assaulter"?RefPcSprite("portrait_assaulter"):HubPortrait(def);
        if(portrait!=null)refPcPortrait.sprite=portrait;
        foreach(var kv in refPcPlates)
        {
            bool selected=save.selectedSurvivor==kv.Key;
            refPcLevels[kv.Key].text="NV. "+HomeLevel(save,kv.Key);
            refPcLevels[kv.Key].color=selected?new Color(1f,0.77f,0.30f):new Color(0.62f,0.76f,0.82f);
            refPcTags[kv.Key].SetActive(selected);
            refPcBorders[kv.Key].enabled=selected;
            refPcBorders[kv.Key].effectColor=new Color(1f,0.70f,0.18f,1f);
        }
        if(refPcSkillBadge)refPcSkillBadge.SetActive(HomeAnySkillPoints(save));
        RefPcApplyStrongSelection();
    }

    void RefPcChooseSurvivor(string id)
    {
        float now=Time.unscaledTime;
        if(refPcLastId==id && now-refPcLastClick<=HomeDoubleTapSeconds)
        {
            refPcLastId=null;SelectHomeSurvivor(id);return;
        }
        refPcLastId=id;refPcLastClick=now;
        var d=GameDatabase.GetSurvivor(id);
        Toast(d.name+" · NV. "+HomeLevel(GameSession.Save,id)+" · PODER "+PowerRating.Of(GameSession.Save,id)+" — dois cliques para selecionar");
    }

    void RefPcOpenOptions()
    {
        RefreshOptions();Show("options");
        // Preserve the correct return target for this entry, with restoration on close.
        if(screens.TryGetValue("options",out var options))
        {
            foreach(var button in options.GetComponentsInChildren<Button>(true))
            {
                if(button.name!="BtnBack")continue;
                var original=button.onClick;
                button.onClick=new Button.ButtonClickedEvent();
                button.onClick.AddListener(()=> {button.onClick=original;ShowHome();});
                break;
            }
        }
    }
}
