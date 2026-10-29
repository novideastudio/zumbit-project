using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public partial class MainMenuController
{
    const string PcSkPath="Art/PCSkillsUI/";
    readonly Dictionary<string,Sprite> pcSkSprites=new Dictionary<string,Sprite>();
    PcSkillsTheme pcSkTheme;
    bool pcSkSpecOpen;
    Sprite PcSkSprite(string name)
    {
        if(pcSkSprites.TryGetValue(name,out var sp) && sp)return sp;
        sp=Resources.Load<Sprite>(PcSkPath+name);
        if(!sp)
        {
            var t=Resources.Load<Texture2D>(PcSkPath+name);if(!t)return null;
            sp=Sprite.Create(t,new Rect(0,0,t.width,t.height),new Vector2(0.5f,0.5f),100,0,SpriteMeshType.FullRect,name.StartsWith("frame")?new Vector4(16,16,16,16):Vector4.zero);
        }
        pcSkSprites[name]=sp;return sp;
    }
    static void PcSkPlace(Transform t,float x,float y,float w,float h)
    {
        var r=t as RectTransform;if(!r)return;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);r.localScale=Vector3.one;
    }
    static RectTransform PcSkNode(Transform parent,string name,float x,float y,float w,float h)
    {
        var r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;r.SetParent(parent,false);PcSkPlace(r,x,y,w,h);return r;
    }
    Image PcSkImage(Transform p,string name,Sprite sprite,float x,float y,float w,float h,bool sliced=false)
    {
        var r=PcSkNode(p,name,x,y,w,h);var i=r.gameObject.AddComponent<Image>();i.sprite=sprite;i.type=sliced?Image.Type.Sliced:Image.Type.Simple;i.raycastTarget=false;return i;
    }
    TMP_Text PcSkText(Transform p,string name,string value,float x,float y,float w,float h,float size,Color color,bool wrap=false)
    {
        var r=PcSkNode(p,name,x,y,w,h);var t=r.gameObject.AddComponent<TextMeshProUGUI>();if(UITheme.Current.font)t.font=UITheme.Current.font;
        t.text=value;t.color=color;t.fontSize=size;t.enableAutoSizing=true;t.fontSizeMin=Mathf.Min(size,12);t.fontSizeMax=size;t.fontStyle=FontStyles.Bold;t.raycastTarget=false;
        t.alignment=TextAlignmentOptions.MidlineLeft;t.textWrappingMode=wrap?TextWrappingModes.Normal:TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Truncate;return t;
    }
    Button PcSkButton(Transform p,string name,string label,float x,float y,float w,float h,Action action,Sprite sprite=null)
    {
        var i=PcSkImage(p,name,sprite?sprite:PcSkButtonFrame(),x,y,w,h,true);i.raycastTarget=true;var b=i.gameObject.AddComponent<Button>();b.targetGraphic=i;
        b.onClick.AddListener(()=>{ZombitAudio.PlayUI();action();});
        var t=PcSkText(i.transform,"Label",label,8,0,w-16,h,22,new Color(0.91f,0.89f,0.80f));t.alignment=TextAlignmentOptions.Center;return b;
    }
    Sprite PcSkPanel()=>pcSkTheme && pcSkTheme.panelFrame?pcSkTheme.panelFrame:PcSkSprite("frame");
    Sprite PcSkCard()=>pcSkTheme && pcSkTheme.cardFrame?pcSkTheme.cardFrame:PcSkSprite("frame");
    Sprite PcSkSelected()=>pcSkTheme && pcSkTheme.selectedFrame?pcSkTheme.selectedFrame:PcSkSprite("frame_selected");
    Sprite PcSkButtonFrame()=>pcSkTheme && pcSkTheme.buttonFrame?pcSkTheme.buttonFrame:PcSkSprite("frame_button");
    Sprite PcSkIcon(SurvivorDef survivor,SkillTreeNode node,bool banner)
    {
        if(pcSkTheme && pcSkTheme.skills!=null)foreach(var a in pcSkTheme.skills)
            if(a!=null && a.nodeId==node.id){var sp=banner?a.banner:a.icon;if(sp)return sp;}
        if(banner)return pcSkTheme && pcSkTheme.defaultDetailBanner?pcSkTheme.defaultDetailBanner:PcSkSprite("detail_banner");
        var icon=ArtLoader.Get(SkArt+"icon_"+SkIconKey(survivor.id,node));
        return icon?icon:pcSkTheme && pcSkTheme.defaultIcon?pcSkTheme.defaultIcon:PcSkSprite("icon_generic");
    }
    Sprite PcSkBanner(string survivorId,int branch)
    {
        if(pcSkTheme)
        {
            if(pcSkTheme.branches!=null)foreach(var b in pcSkTheme.branches)if(b!=null && b.survivorId==survivorId && b.branchIndex==branch && b.banner)return b.banner;
            if(pcSkTheme.branchBanners!=null && branch<pcSkTheme.branchBanners.Length && pcSkTheme.branchBanners[branch])return pcSkTheme.branchBanners[branch];
        }
        return PcSkSprite(branch==0?"banner_red":branch==1?"banner_gold":"banner_green");
    }
    static bool PcSkPreserve(Transform image,Transform root,string[] prefixes)
    {
        for(var t=image;t && t!=root;t=t.parent)foreach(var prefix in prefixes)if(t.name.StartsWith(prefix,StringComparison.Ordinal))return true;
        return false;
    }
    void PcSkFrame(RectTransform root,Sprite sprite,Color color,params string[] preserve)
    {
        foreach(var im in root.GetComponentsInChildren<Image>(true))
        {
            if(im.transform==root)
            {
                if(root.GetComponent<Button>()){im.color=Color.clear;im.raycastTarget=true;}else im.enabled=false;
            }
            else if(!PcSkPreserve(im.transform,root,preserve))im.enabled=false;
        }
        var skin=PcSkImage(root,"PcSkin",sprite,0,0,root.sizeDelta.x,root.sizeDelta.y,true);
        skin.color=color;skin.transform.SetAsFirstSibling();
    }
    static void PcSkMove(Transform root,string name,float x,float y,float w,float h)
    {
        var t=SkillUiChild(root,name);if(t)PcSkPlace(t,x,y,w,h);
    }
    void ApplyPcSkillTheme(RectTransform screen,SurvivorDef survivor,SurvivorProgress progress,IReadOnlyList<SkillTreeNode> nodes,Color[] accents)
    {
        pcSkTheme=Resources.Load<PcSkillsTheme>("Config/PcSkillsTheme");
        var oldHeader=screen.Find("NavHeader");if(oldHeader)oldHeader.gameObject.SetActive(false);
        var legend=screen.Find("SkillLegend");if(legend)legend.gameObject.SetActive(false);
        for(int i=0;i<screen.childCount;i++)
        {
            var child=screen.GetChild(i);if(!child.name.StartsWith("SkillBranch_",StringComparison.Ordinal) && child.name!="SelectedSkillPanel")child.gameObject.SetActive(false);
        }
        var matte=UIFactory.Img(screen,Color.black,"PcSkillsLetterbox");UIFactory.Stretch(matte.rectTransform);matte.raycastTarget=false;
        var stage=PcSkNode(screen,"PcSkillsStage",0,0,1920,1080);
        var backdrop=UIFactory.Img(stage,new Color(0.20f,0.24f,0.27f),"Backdrop");backdrop.sprite=ArtLoader.Get(SceneBackgrounds.DefaultPath);backdrop.raycastTarget=false;UIFactory.Stretch(backdrop.rectTransform);
        var fit=stage.gameObject.AddComponent<PcSkillsFit>();fit.viewport=screen;fit.content=stage;
        PcSkImage(stage,"Header",PcSkPanel(),12,8,1896,102,true).color=new Color(0.55f,0.64f,0.68f);
        var logo=PcSkImage(stage,"Logo",pcSkTheme && pcSkTheme.logo?pcSkTheme.logo:PcSkSprite("logo"),18,8,230,96);logo.preserveAspect=true;
        PcSkText(stage,"Heading","ÁRVORE DE HABILIDADES",260,22,490,66,32,new Color(0.91f,0.87f,0.74f));
        PcSkImage(stage,"PointPanel",PcSkSelected(),758,16,188,80,true);
        PcSkText(stage,"PointLabel","PONTOS DISPONÍVEIS",771,21,162,24,14,SkGoldCol);
        PcSkText(stage,"PointCount",progress.skillPoints.ToString(),771,45,162,46,36,SkGoldCol).alignment=TextAlignmentOptions.Center;
        var strip=PcSkNode(stage,"SurvivorSwitcher",962,20,740,76);var hl=UIFactory.HLayout(strip,8,TextAnchor.MiddleCenter);hl.childForceExpandWidth=false;hl.childForceExpandHeight=false;
        float chipW=(740f-8*(GameDatabase.Survivors.Count-1))/Mathf.Max(1,GameDatabase.Survivors.Count);
        foreach(var def in GameDatabase.Survivors)
        {
            SkBuildSurvivorChip(strip,def,GameSession.Save,def.id==survivor.id,chipW);
            var chip=strip.GetChild(strip.childCount-1) as RectTransform;
            var le=chip.GetComponent<LayoutElement>();if(le)le.preferredHeight=76;
            // Layout groups own final dimensions, so supply a nominal rect before framing.
            chip.sizeDelta=new Vector2(chipW,76);
            PcSkFrame(chip,def.id==survivor.id?PcSkSelected():PcSkPanel(),Color.white,"Portrait","PointsDot");
        }
        PcSkButton(stage,"Back","VOLTAR",1718,18,178,78,ReturnFromUtility);
        for(int branch=0;branch<3;branch++)
        {
            var panel=screen.Find("SkillBranch_"+branch) as RectTransform;if(!panel)continue;
            panel.SetParent(stage,false);PcSkPlace(panel,24+branch*434,128,420,928);
            PcSkFrame(panel,PcSkPanel(),accents[branch],"Skill_","BranchBar","EmblemIcon");
            PcSkImage(panel,"BranchBanner",PcSkBanner(survivor.id,branch),8,8,404,100);
            PcSkMove(panel,"EmblemIcon",18,90,82,82);
            foreach(var first in nodes)if(first.branchIndex==branch)
            {
                var emblem=SkillUiChild(panel,"EmblemIcon");if(emblem){var image=emblem.GetComponent<Image>();image.sprite=PcSkIcon(survivor,first,false);image.enabled=true;}break;
            }
            PcSkMove(panel,"BranchTitle",110,98,294,52);
            PcSkMove(panel,"BranchProgress",16,178,146,24);
            PcSkMove(panel,"BranchBarTrack",164,186,238,8);
            int done=0,total=0;foreach(var n in nodes)if(n.branchIndex==branch){done+=SkillTreeSystem.GetRank(progress,n.id);total+=SkillTreeSystem.MaxRank;}
            PcSkMove(panel,"BranchBarFill",164,186,total>0?238f*done/total:0,8);
            foreach(var node in nodes)
            {
                if(node.branchIndex!=branch)continue;
                var card=panel.Find("Skill_"+node.id) as RectTransform;if(!card)continue;
                PcSkPlace(card,12,222+(node.tier-1)*114,396,106);
                var state=SkStateOf(node,progress,out int rank,out bool prereq,out bool level);
                bool selected=node.id==selectedSkillId;
                PcSkFrame(card,selected?PcSkSelected():PcSkCard(),selected?Color.white:state==SkState.Locked?new Color(0.40f,0.48f,0.52f):accents[branch],"IconFrame","RankPip_","StateGlyph");
                var fx=card.GetComponent<UIButtonFx>();if(fx)fx.enabled=false;
                PcSkMove(card,"IconFrame",12,12,82,82);PcSkMove(card,"SkillName",106,6,240,36);PcSkMove(card,"SkillRank",106,44,240,24);
                for(int r=0;r<SkillTreeSystem.MaxRank;r++)PcSkMove(card,"RankPip_"+r,106+r*39,81,33,8);
                PcSkMove(card,"StateGlyph",354,40,28,28);
                var icon=SkillUiChild(card,"SkillIcon");if(icon){var image=icon.GetComponent<Image>();image.sprite=PcSkIcon(survivor,node,false);image.enabled=true;image.preserveAspect=true;}
                if(state==SkState.Available)
                {
                    var old=SkillUiChild(card,"StateGlyph");if(old)old.gameObject.SetActive(false);
                    var plus=PcSkImage(card,"BuyRank",pcSkTheme && pcSkTheme.plusIcon?pcSkTheme.plusIcon:PcSkSprite("icon_plus"),350,38,34,34);
                    plus.raycastTarget=true;var buy=plus.gameObject.AddComponent<Button>();buy.targetGraphic=plus;string id=node.id;
                    buy.onClick.AddListener(()=>UnlockMapSkill(id));
                }
                else if(state==SkState.Locked)
                {
                    var old=SkillUiChild(card,"StateGlyph");if(old){var image=old.GetComponent<Image>();image.sprite=pcSkTheme && pcSkTheme.lockedIcon?pcSkTheme.lockedIcon:PcSkSprite("icon_lock");image.enabled=true;}
                }
            }
        }
        var detail=screen.Find("SelectedSkillPanel") as RectTransform;
        GameObject specOverlay=null;
        if(detail)
        {
            detail.SetParent(stage,false);PcSkPlace(detail,1326,128,570,928);
            PcSkFrame(detail,PcSkPanel(),new Color(0.55f,0.64f,0.68f),"DetailIconFrame","RankRow_","ReqLevelMark","ReqPreviousMark","ReqPointsMark","UnlockSkillButton","SkillLockedStatus","SkillMaxedStatus","ResetSkillsButton","SpecBox");
            var node=SkillTreeSystem.GetNode(survivor.id,selectedSkillId);
            PcSkMove(detail,"SkillDetailName",106,12,448,45);PcSkMove(detail,"SkillDetailMeta",106,60,448,22);PcSkMove(detail,"SkillCurrentRank",106,83,448,22);
            PcSkMove(detail,"DetailIconFrame",16,14,80,80);
            if(node!=null)
            {
                var icon=SkillUiChild(detail,"DetailIcon");if(icon){var image=icon.GetComponent<Image>();image.sprite=PcSkIcon(survivor,node,false);image.enabled=true;}
                PcSkImage(detail,"SelectedSkillBanner",PcSkIcon(survivor,node,true),16,114,538,144);
                PcSkText(detail,"Description","Confira os bônus e requisitos desta habilidade.",20,264,530,30,17,SkDim);
                int rank=SkillTreeSystem.GetRank(progress,node.id);
                var next=PcSkImage(detail,"NextRank",PcSkButtonFrame(),16,304,538,64,true);
                PcSkText(next.transform,"NextTitle",rank<SkillTreeSystem.MaxRank?"PRÓXIMO GRAU ("+(rank+1)+"/"+SkillTreeSystem.MaxRank+")":"GRAU MÁXIMO",12,3,514,22,17,SkGoldCol);
                string line=SkillTreeSystem.LineFor(node.stat,node.bonuses.Get(node.stat)*rank);
                if(rank<SkillTreeSystem.MaxRank)line+="  →  "+SkillTreeSystem.LineFor(node.stat,node.bonuses.Get(node.stat)*(rank+1));
                PcSkText(next.transform,"NextValue",line,12,28,514,32,17,SkGreenText,true);
            }
            PcSkMove(detail,"RanksLabel",20,380,180,26);PcSkMove(detail,"RanksBonusLabel",202,380,260,26);PcSkMove(detail,"RanksLevelLabel",472,380,78,26);
            for(int r=1;r<=SkillTreeSystem.MaxRank;r++)
            {
                float y=410+(r-1)*35;PcSkMove(detail,"RankRow_"+r,16,y,538,32);PcSkMove(detail,"RankName_"+r,24,y,174,32);PcSkMove(detail,"RankBonus_"+r,202,y,264,32);PcSkMove(detail,"RankLevel_"+r,472,y,78,32);
            }
            PcSkText(detail,"RequirementsTitle","REQUISITOS",20,634,530,24,19,new Color(0.73f,0.83f,0.85f));
            string[] req={"ReqLevel","ReqPrevious","ReqPoints"};for(int i=0;i<3;i++){PcSkMove(detail,req[i]+"Mark",20,665+i*30,22,22);PcSkMove(detail,req[i],51,660+i*30,499,28);}
            PcSkMove(detail,"MaxedText",20,670,530,54);
            foreach(var name in new[]{"UnlockSkillButton","SkillLockedStatus","SkillMaxedStatus","ResetSkillsButton"})
            {
                var target=SkillUiChild(detail,name) as RectTransform;if(!target)continue;
                PcSkPlace(target,16,name=="ResetSkillsButton"?822:756,538,54);
                PcSkFrame(target,PcSkButtonFrame(),Color.white,"StatusIcon");
                foreach(var text in target.GetComponentsInChildren<TMP_Text>(true))
                {
                    UIFactory.Stretch(text.rectTransform);text.rectTransform.offsetMin=new Vector2(12,2);text.rectTransform.offsetMax=new Vector2(-12,-2);
                    text.enableAutoSizing=true;text.fontSizeMin=12;text.fontSizeMax=20;text.color=new Color(0.94f,0.90f,0.78f);text.textWrappingMode=TextWrappingModes.NoWrap;text.alignment=TextAlignmentOptions.Center;
                }
            }
            PcSkMove(detail,"ResetHint",20,884,530,28);
            var spec=SkillUiChild(detail,"SpecBox") as RectTransform;
            if(spec)
            {
                var overlay=PcSkNode(stage,"SpecializationOverlay",0,0,1920,1080);specOverlay=overlay.gameObject;
                var dim=overlay.gameObject.AddComponent<Image>();dim.color=new Color(0,0,0,0.94f);dim.raycastTarget=true;
                PcSkImage(overlay,"SpecWindow",PcSkPanel(),440,240,1040,590,true);
                PcSkText(overlay,"Title","ESPECIALIZAÇÃO",476,258,968,55,34,SkGoldCol);
                spec.SetParent(overlay,false);PcSkPlace(spec,476,330,968,340);
                PcSkButton(overlay,"CloseSpec","VOLTAR À ÁRVORE",790,724,340,64,()=>{pcSkSpecOpen=false;overlay.gameObject.SetActive(false);});
                overlay.gameObject.SetActive(pcSkSpecOpen);
                PcSkButton(detail,"OpenSpec","ESPECIALIZAÇÃO",350,220,202,34,()=>{pcSkSpecOpen=true;overlay.gameObject.SetActive(true);overlay.SetAsLastSibling();});
            }
        }
        fit.escape=()=>{if(specOverlay && specOverlay.activeSelf){pcSkSpecOpen=false;specOverlay.SetActive(false);}else ReturnFromUtility();};
        foreach(var fx in stage.GetComponentsInChildren<UIButtonFx>(true))fx.enabled=false;
        fit.Apply();Canvas.ForceUpdateCanvases();
    }
}
