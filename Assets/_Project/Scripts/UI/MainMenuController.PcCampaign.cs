using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// PC campaign skin. All progression/mission actions remain in the original controllers.
public partial class MainMenuController
{
    const string PcCampaignArt="Art/PCCampaignUI/";
    static readonly Dictionary<string,Sprite> pcCampaignSprites=new Dictionary<string,Sprite>();
    readonly Dictionary<string,Image> pcCampaignNodes=new Dictionary<string,Image>();
    readonly Dictionary<string,GameObject> pcCampaignPins=new Dictionary<string,GameObject>();
    RectTransform pcCampaignStage,pcCampaignMap;
    GameObject pcCampaignDetails,pcCampaignRules;
    readonly List<GameObject> pcCampaignDiffLocks=new List<GameObject>();
    Button pcCampaignFormation;
    bool pcCampaignOpen;
    string pcCampaignVisualKey;

    static RectTransform PcCampRect(Transform parent,string name,float x,float y,float w,float h)
    {
        var go=new GameObject(name,typeof(RectTransform));var rt=(RectTransform)go.transform;rt.SetParent(parent,false);
        rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0f,1f);
        rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(w,h);rt.localScale=Vector3.one;
        return rt;
    }
    Sprite PcCampSprite(string name,bool sliced=true)
    {
        if(pcCampaignSprites.TryGetValue(name,out var sp) && sp!=null)return sp;
        var tex=Resources.Load<Texture2D>(PcCampaignArt+name);
        if(tex==null)throw new InvalidOperationException("Textura ausente: "+PcCampaignArt+name);
        var border=sliced?new Vector4(20,20,20,20):Vector4.zero;
        sp=Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),new Vector2(0.5f,0.5f),100f,0,SpriteMeshType.FullRect,border);
        pcCampaignSprites[name]=sp;return sp;
    }
    Image PcCampImage(Transform parent,string name,string texture,float x,float y,float w,float h,bool sliced=true)
    {
        var rt=PcCampRect(parent,name,x,y,w,h);var im=rt.gameObject.AddComponent<Image>();
        im.sprite=PcCampSprite(texture,sliced);im.type=sliced?Image.Type.Sliced:Image.Type.Simple;
        im.color=Color.white;im.raycastTarget=false;return im;
    }
    TMP_Text PcCampText(Transform parent,string name,string text,float x,float y,float w,float h,float size,Color color,bool wrap=false,TextAlignmentOptions alignment=TextAlignmentOptions.MidlineLeft)
    {
        var rt=PcCampRect(parent,name,x,y,w,h);var t=rt.gameObject.AddComponent<TextMeshProUGUI>();
        var theme=UITheme.Current;if(theme.displayFont!=null)t.font=theme.displayFont;else if(theme.font!=null)t.font=theme.font;
        t.text=text;t.fontSize=size;t.fontSizeMax=size;t.fontSizeMin=Mathf.Min(size,12f);t.enableAutoSizing=true;
        t.fontStyle=FontStyles.Bold;t.color=color;t.alignment=alignment;t.richText=true;t.raycastTarget=false;
        t.textWrappingMode=wrap?TextWrappingModes.Normal:TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Truncate;
        return t;
    }
    Button PcCampButton(Transform parent,string name,string text,string texture,float x,float y,float w,float h,Action action,float size=22f)
    {
        var im=PcCampImage(parent,name,texture,x,y,w,h);im.raycastTarget=true;
        var b=im.gameObject.AddComponent<Button>();b.targetGraphic=im;
        var c=b.colors;c.normalColor=Color.white;c.highlightedColor=new Color(1f,0.95f,0.84f);c.pressedColor=new Color(0.73f,0.73f,0.70f);c.selectedColor=Color.white;b.colors=c;
        if(action!=null)b.onClick.AddListener(()=>{ZombitAudio.PlayUI();action();});
        PcCampText(im.transform,"Label",text,10,0,w-20,h,size,new Color(0.94f,0.90f,0.78f),false,TextAlignmentOptions.Center);
        return b;
    }
    void PcCampIcon(Transform parent,string key,float x,float y,float size)
    {
        var image=PcCampImage(parent,"Icon_"+key,key,x,y,size,size,false);image.preserveAspect=true;
    }
    static Color PcCampCream => new Color(0.93f,0.89f,0.77f);

    // Replaces only the original panel builder; original SelectLocation populates these fields.
    void BuildPcCampaignPanel(RectTransform screen)
    {
        pcCampaignDiffLocks.Clear();pcCampaignVisualKey=null;
        var panel=PcCampImage(screen,"PcCampaignDetails","panel",1330,18,574,1044);
        pcCampaignDetails=panel.gameObject;
        var title=PcCampRect(panel.transform,"TitleBar",16,12,542,66);
        PcCampImage(title,"NumberBadge","gold",0,8,56,48);
        hubBadge=PcCampText(title,"Num","",0,8,56,48,27,UIKit.Col.Gold,false,TextAlignmentOptions.Center);
        dName=PcCampText(title,"DName","",70,0,408,66,30,PcCampCream);
        var close=PcCampButton(title,"CloseDetails","X","button",490,7,46,46,()=>SetPcCampaignDetails(false),25);

        var previewFrame=PcCampImage(panel.transform,"PreviewFrame","slot",20,88,534,206);
        var mask=PcCampRect(previewFrame.transform,"PreviewMask",6,6,522,194);mask.gameObject.AddComponent<RectMask2D>();
        var previewGo=PcCampRect(mask,"SelectedLocationPreview",0,0,522,194);
        selectedLocationPreview=previewGo.gameObject.AddComponent<Image>();selectedLocationPreview.color=Color.white;selectedLocationPreview.raycastTarget=false;
        hubHeroFitter=previewGo.gameObject.AddComponent<AspectRatioFitter>();hubHeroFitter.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;hubHeroFitter.aspectRatio=2.7f;
        dDesc=PcCampText(panel.transform,"Description","",24,306,526,67,18,new Color(0.70f,0.77f,0.78f),true);
        dInfo=PcCampText(panel.transform,"LocationNote","",24,376,526,27,16,UIKit.Col.Gold,true);
        hubStatA=BuildPcCampaignStat(panel.transform,"Level",20);
        hubStatB=BuildPcCampaignStat(panel.transform,"Waves",202);
        hubStatD=BuildPcCampaignStat(panel.transform,"Reward",384);hubStatC=null;
        PcCampText(panel.transform,"EnemiesTitle","INIMIGOS PRINCIPAIS",24,486,526,26,17,PcCampCream);
        hubEnemyRow=PcCampRect(panel.transform,"EnemyRow",20,520,534,80);
        var h=UIFactory.HLayout(hubEnemyRow,8,TextAnchor.MiddleCenter);h.childForceExpandWidth=true;h.childForceExpandHeight=true;
        PcCampText(panel.transform,"ItemsTitle","ITENS POSSÍVEIS",24,610,526,26,17,PcCampCream);
        var items=PcCampRect(panel.transform,"ItemRow",20,644,534,64);
        var ih=UIFactory.HLayout(items,6,TextAnchor.MiddleCenter);ih.childForceExpandWidth=true;ih.childForceExpandHeight=true;
        BuildHubItems(items);
        PcCampText(panel.transform,"DifficultyTitle","DIFICULDADE",24,719,526,26,17,PcCampCream);
        for(int i=0;i<4;i++)
        {
            int level=i+1;
            var b=PcCampButton(panel.transform,"Difficulty_"+level,Difficulties.Get(level).name,"button",20+i*136,754,126,61,()=>{SelectDifficulty(level);RefreshPcCampaignStyle();},19);
            diffButtons[i]=b;
            var lockIcon=PcCampImage(b.transform,"DifficultyLock","lock",101,35,15,18,false);lockIcon.preserveAspect=true;
            pcCampaignDiffLocks.Add(lockIcon.gameObject);
        }
        var rules=PcCampButton(panel.transform,"MissionRules","REGRAS DA MISSÃO","button",20,829,534,46,()=>{pcCampaignRules.SetActive(true);},18);
        PcCampIcon(rules.transform,"info",14,11,24);
        pcCampaignFormation=PcCampButton(panel.transform,"ChooseFormation","ESCOLHER FORMAÇÃO","button",20,888,534,59,OpenMissionFormation,23);
        PcCampIcon(pcCampaignFormation.transform,"formation",18,12,34);
        dStart=PcCampButton(panel.transform,"StartMission","INICIAR MISSÃO","red",20,962,534,62,OnStartMission,30);
        PcCampIcon(dStart.transform,"arrow",477,17,28);
    }
    TMP_Text BuildPcCampaignStat(Transform parent,string name,float x)
    {
        var f=PcCampImage(parent,"Stat_"+name,"slot",x,414,170,61);
        return PcCampText(f.transform,"Value","",6,4,158,53,18,PcCampCream,true,TextAlignmentOptions.Center);
    }

    void SetupPcCampaign(RectTransform screen,RectTransform mapArea)
    {
        // Fit the whole composition together: never scale the picture independently of its markers.
        var children=new List<Transform>();for(int i=0;i<screen.childCount;i++)children.Add(screen.GetChild(i));
        var black=UIFactory.Img(screen,Color.black,"PcCampaignLetterbox");UIFactory.Stretch(black.rectTransform);black.raycastTarget=false;black.transform.SetAsFirstSibling();
        pcCampaignStage=PcCampRect(screen,"PcCampaignStage",0,0,1920,1080);
        pcCampaignStage.anchorMin=pcCampaignStage.anchorMax=pcCampaignStage.pivot=new Vector2(0.5f,0.5f);pcCampaignStage.anchoredPosition=Vector2.zero;
        foreach(var c in children)c.SetParent(pcCampaignStage,false);
        var fit=pcCampaignStage.gameObject.AddComponent<CampaignPcFit>();fit.content=pcCampaignStage;fit.viewport=screen;fit.Apply();
        pcCampaignMap=mapArea;
        var backdrop=pcCampaignStage.Find("MapBackdrop");if(backdrop!=null)backdrop.SetAsFirstSibling();
        mapArea.SetSiblingIndex(backdrop!=null?backdrop.GetSiblingIndex()+1:0);
        var inner=mapArea.Find("MapInner") as RectTransform;if(inner)ExactMapRect(inner,Vector2.zero,Vector2.one);
        PcCampImage(pcCampaignStage,"CampaignHeader","panel",100,18,575,70);
        PcCampText(pcCampaignStage,"CampaignTitle","CAMPANHA",120,20,530,32,26,PcCampCream);
        PcCampText(pcCampaignStage,"CampaignChapter","CAPÍTULO 1 · REGIÃO URBANA",120,54,530,26,17,new Color(0.65f,0.74f,0.77f));
        var back=PcCampButton(pcCampaignStage,"CampaignBack","","button",18,18,68,68,()=>{ClosePcCampaignRules();ShowHome();});PcCampIcon(back.transform,"back",17,17,34);
        BuildPcCampaignRulesModal();
        SetPcCampaignDetails(true);
        var updater=pcCampaignStage.gameObject.AddComponent<CampaignPcRefresh>();updater.refresh=RefreshPcCampaignStyle;
        updater.escape=()=>{if(missionFormationCanvas!=null){CloseMissionFormation();return;}if(pcCampaignRules!=null && pcCampaignRules.activeSelf){ClosePcCampaignRules();return;}if(pcCampaignOpen)SetPcCampaignDetails(false);else ShowHome();};
        RefreshPcCampaignStyle();fit.Apply();
    }
    void SetPcCampaignDetails(bool visible)
    {
        pcCampaignOpen=visible;
        if(pcCampaignMap)ExactMapRect(pcCampaignMap,Vector2.zero,new Vector2(visible?0.69f:1f,1f));
        if(pcCampaignDetails) { pcCampaignDetails.SetActive(visible);pcCampaignDetails.transform.SetAsLastSibling(); }
        if(pcCampaignRules)pcCampaignRules.transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
    }
    void TogglePcCampaignLocation(LocationDef loc)
    {
        if(loc==null)return;
        if(pcCampaignOpen && selectedLoc!=null && selectedLoc.id==loc.id) {SetPcCampaignDetails(false);return;}
        SelectLocation(loc);SetPcCampaignDetails(true);pcCampaignVisualKey=null;RefreshPcCampaignStyle();
    }

    // Compact metal labels replacing the large picture cards. Positions belong to this skin only.
    void BuildPcCampaignNode(Transform parent,LocationDef loc,int index,SaveData save)
    {
        if(parent==null || loc==null)return;
        Vector2 p;
        switch(loc.id)
        {
            case "suburbio":p=new Vector2(0.155f,0.205f);break;
            case "residencial":p=new Vector2(0.365f,0.278f);break;
            case "shopping":p=new Vector2(0.525f,0.462f);break;
            case "hospital":p=new Vector2(0.175f,0.555f);break;
            case "delegacia":p=new Vector2(0.460f,0.715f);break;
            case "metro":p=new Vector2(0.705f,0.302f);break;
            case "centro":p=new Vector2(0.875f,0.535f);break;
            case "base":p=new Vector2(0.895f,0.205f);break;
            case "industrial":p=new Vector2(0.858f,0.795f);break;
            default:return;
        }
        var go=new GameObject("Node_"+loc.id,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Button));var rt=(RectTransform)go.transform;rt.SetParent(parent,false);
        // Normalised rectangle scales with map expansion. No layout groups or hover scaling.
        rt.anchorMin=new Vector2(p.x-0.094f,1f-p.y-0.034f);rt.anchorMax=new Vector2(p.x+0.094f,1f-p.y+0.034f);
        rt.offsetMin=rt.offsetMax=Vector2.zero;
        var image=go.GetComponent<Image>();image.sprite=PcCampSprite("button");image.type=Image.Type.Sliced;image.raycastTarget=true;
        var b=go.GetComponent<Button>();b.targetGraphic=image;b.transition=Selectable.Transition.None;var location=loc;b.onClick.AddListener(()=>{ZombitAudio.PlayUI();TogglePcCampaignLocation(location);});
        var num=PcCampText(rt,"Number",MapDisplayNumber(loc).ToString("00"),0,0,1,1,23,UIKit.Col.Gold,false,TextAlignmentOptions.Center);
        ExactMapRect(num.rectTransform,new Vector2(0.025f,0.13f),new Vector2(0.20f,0.89f));
        var name=PcCampText(rt,"Name",MapDisplayName(loc),0,0,1,1,17,PcCampCream);ExactMapRect(name.rectTransform,new Vector2(0.24f,0.43f),new Vector2(0.98f,0.91f));
        for(int i=0;i<4;i++)
        {
            var starGo=new GameObject("Star_"+i,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));var sr=(RectTransform)starGo.transform;sr.SetParent(rt,false);
            ExactMapRect(sr,new Vector2(0.25f+i*0.135f,0.14f),new Vector2(0.33f+i*0.135f,0.37f));
            var star=starGo.GetComponent<Image>();star.sprite=UIKit.Star;star.preserveAspect=true;star.raycastTarget=false;
            star.color=save.IsCompleted(loc.id,i+1)?UIKit.Col.Gold:new Color(0.26f,0.32f,0.33f);
        }
        bool unlocked=GameDatabase.IsUnlocked(loc,save);
        if(!unlocked)
        {
            var lockGo=new GameObject("Lock",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));var lr=(RectTransform)lockGo.transform;lr.SetParent(rt,false);
            ExactMapRect(lr,new Vector2(0.89f,0.10f),new Vector2(0.965f,0.38f));
            var li=lockGo.GetComponent<Image>();li.sprite=PcCampSprite("lock",false);li.preserveAspect=true;li.raycastTarget=false;
            name.color=new Color(0.62f,0.70f,0.72f);
        }
        var outline=go.AddComponent<Outline>();outline.effectColor=unlocked?UIKit.Col.Gold:new Color(0.26f,0.35f,0.40f);outline.effectDistance=new Vector2(1f,-1f);
        nodeOutlines[loc.id]=outline;nodeBorders[loc.id]=outline.effectColor;
        pcCampaignNodes[loc.id]=image;
        var pin=PcCampImage(rt,"SelectedPin","arrow",0,0,16,16,false);
        ExactMapRect(pin.rectTransform,new Vector2(0.005f,0.30f),new Vector2(0.035f,0.70f));pin.gameObject.SetActive(false);
        nodePins[loc.id]=pin.gameObject;pcCampaignPins[loc.id]=pin.gameObject;
    }

    void RefreshPcCampaignStyle()
    {
        var save=GameSession.Save;if(save==null || selectedLoc==null || dStart==null)return;
        string key=selectedLoc.id+"#"+selectedDiff+"#"+save.selectedSurvivor+"#"+save.selectedPartner+"#"+save.selectedThird+"#"+buyInsurance;
        if(pcCampaignVisualKey==key)return;pcCampaignVisualKey=key;
        foreach(var kv in pcCampaignNodes)if(kv.Value)kv.Value.sprite=PcCampSprite(kv.Key==selectedLoc.id?"gold":"button");
        for(int i=0;i<4;i++)
        {
            if(diffButtons[i]==null)continue;
            bool unlocked=GameDatabase.IsDifficultyUnlocked(selectedLoc,save,i+1);
            var im=diffButtons[i].GetComponent<Image>();if(im)im.sprite=PcCampSprite(i+1==selectedDiff?"gold":"button");
            var cb=diffButtons[i].colors;cb.normalColor=Color.white;cb.disabledColor=new Color(0.50f,0.56f,0.59f,1f);cb.selectedColor=Color.white;diffButtons[i].colors=cb;
            var t=diffButtons[i].GetComponentInChildren<TMP_Text>();if(t)t.text=Difficulties.Get(i+1).name;
            if(i<pcCampaignDiffLocks.Count && pcCampaignDiffLocks[i])pcCampaignDiffLocks[i].SetActive(!unlocked);
        }
        bool can=GameDatabase.IsDifficultyUnlocked(selectedLoc,save,selectedDiff);
        if(pcCampaignFormation)pcCampaignFormation.interactable=can;
        var startColors=dStart.colors;startColors.normalColor=Color.white;startColors.disabledColor=new Color(0.45f,0.45f,0.45f,1f);dStart.colors=startColors;
        var label=dStart.GetComponentInChildren<TMP_Text>();if(label)label.text="INICIAR MISSÃO";
    }

    void BuildPcCampaignRulesModal()
    {
        var modal=PcCampRect(pcCampaignStage,"PcCampaignRules",0,0,1920,1080);pcCampaignRules=modal.gameObject;
        var shade=modal.gameObject.AddComponent<Image>();shade.color=new Color(0f,0f,0f,0.80f);shade.raycastTarget=true;
        var p=PcCampImage(modal,"RulesPanel","panel",420,150,1080,780);
        PcCampText(p.transform,"Title","REGRAS DA MISSÃO / SEGURO",24,16,960,48,30,PcCampCream);
        PcCampButton(p.transform,"CloseRules","X","button",1006,18,48,48,ClosePcCampaignRules,25);
        var viewport=PcCampRect(p.transform,"Scroll",24,82,1032,562);viewport.gameObject.AddComponent<RectMask2D>();
        var capture=viewport.gameObject.AddComponent<Image>();capture.color=Color.clear;capture.raycastTarget=true;
        dDiffInfo=PcCampText(viewport,"Rules","",0,0,1000,560,24,PcCampCream,true,TextAlignmentOptions.TopLeft);
        dDiffInfo.enableAutoSizing=false;dDiffInfo.overflowMode=TextOverflowModes.Overflow;
        dDiffInfo.rectTransform.anchorMin=new Vector2(0f,1f);dDiffInfo.rectTransform.anchorMax=new Vector2(1f,1f);dDiffInfo.rectTransform.pivot=new Vector2(0.5f,1f);dDiffInfo.rectTransform.sizeDelta=new Vector2(-18f,560f);dDiffInfo.rectTransform.anchoredPosition=Vector2.zero;
        var fitter=dDiffInfo.gameObject.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=dDiffInfo.rectTransform;scroll.horizontal=false;scroll.vertical=true;scroll.scrollSensitivity=35;
        dInsurance=PcCampButton(p.transform,"Insurance","","button",24,670,1032,72,()=>{buyInsurance=!buyInsurance;RefreshDifficultyUI();pcCampaignVisualKey=null;RefreshPcCampaignStyle();},21);
        dInsuranceInfo=dInsurance.GetComponentInChildren<TMP_Text>();if(dInsuranceInfo)dInsuranceInfo.richText=true;
        pcCampaignRules.SetActive(false);
        // Bind rule/insurance texts for the location selected before this modal was created.
        RefreshDifficultyUI();
    }
    void ClosePcCampaignRules() {if(pcCampaignRules)pcCampaignRules.SetActive(false);}
}
