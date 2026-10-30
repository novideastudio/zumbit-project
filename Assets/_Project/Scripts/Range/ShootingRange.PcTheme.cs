using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public partial class ShootingRange
{
    const string PcRangeArt="Art/PCTrainingUI/";
    PcRangeTheme pcRangeTheme;
    readonly Dictionary<string,Sprite> pcRangeSprites=new Dictionary<string,Sprite>();
    RectTransform pcRangeStage,pcRangeFormationStage;
    CanvasGroup pcRangeGroup,pcRangeFormationGroup;
    TMP_Text pcRangeCashGold,pcRangeCashScrap,pcRangeKills,pcRangeTargets;
    readonly List<Button> pcRangeTierButtons=new List<Button>();
    SpriteRenderer pcRangeBackdrop;

    Sprite PcRangeSprite(string key)
    {
        Sprite themed=null;
        if(pcRangeTheme)
        {
            switch(key)
            {
                case "panel":themed=pcRangeTheme.panel;break;case "red":themed=pcRangeTheme.red;break;
                case "green":themed=pcRangeTheme.green;break;case "gold":themed=pcRangeTheme.gold;break;
                case "selected":themed=pcRangeTheme.selected;break;case "logo":themed=pcRangeTheme.logo;break;
                case "coin":themed=pcRangeTheme.coin;break;case "scrap":themed=pcRangeTheme.scrap;break;
                case "target":themed=pcRangeTheme.targetIcon;break;case "target_preview":themed=pcRangeTheme.targetPreview;break;
            }
        }
        if(themed)return themed;
        if(pcRangeSprites.TryGetValue(key,out var cached) && cached)return cached;
        var sp=Resources.Load<Sprite>(PcRangeArt+key);
        if(!sp)
        {
            var tex=Resources.Load<Texture2D>(PcRangeArt+key);if(!tex)return null;
            bool frame=key=="panel" || key=="red" || key=="green" || key=="gold" || key=="selected";
            sp=Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),new Vector2(0.5f,0.5f),100,0,SpriteMeshType.FullRect,frame?new Vector4(16,16,16,16):Vector4.zero);
        }
        pcRangeSprites[key]=sp;return sp;
    }
    static void PcRangePlace(Transform t,float x,float y,float w,float h)
    {
        var rt=t as RectTransform;if(!rt)return;rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,1);
        rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(w,h);rt.localScale=Vector3.one;
    }
    static RectTransform PcRangeNode(Transform parent,string name,float x,float y,float w,float h)
    {
        var rt=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;rt.SetParent(parent,false);PcRangePlace(rt,x,y,w,h);return rt;
    }
    static Transform PcRangeFind(Transform root,string name)
    {
        if(!root)return null;for(int i=0;i<root.childCount;i++){var c=root.GetChild(i);if(c.name==name)return c;var found=PcRangeFind(c,name);if(found)return found;}return null;
    }
    Image PcRangeImage(Transform parent,string name,string art,float x,float y,float w,float h,bool slice=false)
    {
        var rt=PcRangeNode(parent,name,x,y,w,h);var im=rt.gameObject.AddComponent<Image>();im.sprite=PcRangeSprite(art);im.type=slice?Image.Type.Sliced:Image.Type.Simple;im.raycastTarget=false;return im;
    }
    TMP_Text PcRangeText(Transform parent,string name,string value,float x,float y,float w,float h,float size,Color color,bool wrap=false)
    {
        var rt=PcRangeNode(parent,name,x,y,w,h);var t=rt.gameObject.AddComponent<TextMeshProUGUI>();
        if(pcRangeTheme && pcRangeTheme.font)t.font=pcRangeTheme.font;else if(UITheme.Current.font)t.font=UITheme.Current.font;
        t.text=value;t.fontSize=size;t.fontSizeMax=size;t.fontSizeMin=Mathf.Min(12,size);t.enableAutoSizing=true;t.fontStyle=FontStyles.Bold;
        t.color=color;t.alignment=TextAlignmentOptions.MidlineLeft;t.raycastTarget=false;t.textWrappingMode=wrap?TextWrappingModes.Normal:TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Truncate;return t;
    }
    Button PcRangeButton(Transform parent,string name,string label,string skin,float x,float y,float w,float h,Action action)
    {
        var im=PcRangeImage(parent,name,skin,x,y,w,h,true);im.raycastTarget=true;var button=im.gameObject.AddComponent<Button>();button.targetGraphic=im;
        button.onClick.AddListener(()=>{ZombitAudio.PlayUI();action();});
        PcRangeText(im.transform,"Label",label,8,0,w-16,h,21,new Color(0.94f,0.91f,0.81f)).alignment=TextAlignmentOptions.Center;return button;
    }
    void PcRangeStyleButton(Button b,string skin)
    {
        if(!b)return;var im=b.targetGraphic as Image;if(!im)im=b.GetComponent<Image>();if(!im)return;
        foreach(var other in b.GetComponentsInChildren<Image>(true))if(other!=im && other.name!="Portrait")other.enabled=false;
        im.sprite=PcRangeSprite(skin);im.type=Image.Type.Sliced;im.color=Color.white;im.enabled=true;
        var c=b.colors;c.normalColor=Color.white;c.selectedColor=Color.white;c.highlightedColor=new Color(1f,0.93f,0.80f);c.pressedColor=new Color(0.74f,0.74f,0.70f);b.colors=c;
        foreach(var text in b.GetComponentsInChildren<TMP_Text>(true))
        {
            text.color=new Color(0.92f,0.91f,0.83f);text.enableAutoSizing=true;text.fontSizeMin=12;text.fontSizeMax=21;text.textWrappingMode=TextWrappingModes.NoWrap;
        }
        var fx=b.GetComponent<UIButtonFx>();if(fx)fx.enabled=false;
    }
    void PcRangeFrame(RectTransform root,string skin,params string[] preserve)
    {
        foreach(var im in root.GetComponentsInChildren<Image>(true))
        {
            bool keep=false;
            for(var t=im.transform;t && t!=root;t=t.parent)foreach(var prefix in preserve)if(t.name.StartsWith(prefix,StringComparison.Ordinal))keep=true;
            if(!keep)im.enabled=false;
        }
        var back=PcRangeImage(root,"PcSkin",skin,0,0,root.sizeDelta.x,root.sizeDelta.y,true);back.transform.SetAsFirstSibling();
    }
    void ApplyPcRangeUi()
    {
        pcRangeTheme=Resources.Load<PcRangeTheme>("Config/PcRangeTheme");
        pcRangeStage=PcRangeNode(canvas.transform,"PcTrainingStage",0,0,1920,1080);
        pcRangeGroup=pcRangeStage.gameObject.AddComponent<CanvasGroup>();
        var layout=pcRangeStage.gameObject.AddComponent<PcRangeLayout>();layout.viewport=(RectTransform)canvas.transform;layout.content=pcRangeStage;layout.tick=PcRangePresentationTick;
        PcRangeImage(pcRangeStage,"TitleFrame","panel",24,12,694,90,true);
        var logo=PcRangeImage(pcRangeStage,"Logo","logo",32,14,230,84);logo.preserveAspect=true;
        PcRangeText(pcRangeStage,"Title","ESTANDE DE TIRO",274,23,420,66,35,new Color(0.94f,0.88f,0.72f));
        PcRangeImage(pcRangeStage,"Currency","panel",1198,12,394,90,true);
        var coin=PcRangeImage(pcRangeStage,"Coin","coin",1212,34,44,44);coin.preserveAspect=true;
        pcRangeCashGold=PcRangeText(pcRangeStage,"Gold","",1268,26,110,62,32,Color.white);
        var scrap=PcRangeImage(pcRangeStage,"ScrapIcon","scrap",1404,34,44,44);scrap.preserveAspect=true;
        pcRangeCashScrap=PcRangeText(pcRangeStage,"Scrap","",1460,26,114,62,32,Color.white);
        PcRangeButton(pcRangeStage,"Back","VOLTAR","red",1606,12,290,90,()=>{var flow=FindFirstObjectByType<MissionFlow>();if(flow)flow.ExitTrainingToMap();});
        var left=PcRangeImage(pcRangeStage,"TargetPanel","red",24,120,472,310,true).rectTransform;
        var target=PcRangeImage(left,"TargetIcon","target",18,14,72,72);target.preserveAspect=true;
        PcRangeText(left,"Heading","ESTANDE DE TIRO",102,14,350,40,30,new Color(0.98f,0.91f,0.73f));
        PcRangeText(left,"Subheading","TREINAMENTO LIVRE",102,57,350,26,18,new Color(0.87f,0.56f,0.37f));
        PcRangeText(left,"TierTitle","RESISTÊNCIA DO ALVO",20,104,432,28,21,new Color(0.99f,0.78f,0.35f));
        pcRangeTierButtons.Clear();int[] types={0,1,3};string[] names={"NORMAL","ELITE","CHEFE"};
        for(int i=0;i<3;i++){int idx=types[i];pcRangeTierButtons.Add(PcRangeButton(left,"Tier_"+idx,names[i],"panel",20+i*145,148,132,44,()=>SelectPreset(idx)));}
        PcRangeText(left,"TierMeaning","ELITE: PRESET BLINDADO",20,196,432,20,12,new Color(0.72f,0.62f,0.56f));
        PcRangeText(left,"KillsLabel","ABATES",28,231,180,26,19,new Color(0.76f,0.72f,0.69f));
        PcRangeText(left,"TargetsLabel","ALVOS",258,231,180,26,19,new Color(0.76f,0.72f,0.69f));
        pcRangeKills=PcRangeText(left,"Kills","0",28,256,180,44,39,Color.white);
        pcRangeTargets=PcRangeText(left,"Targets","0",258,256,180,44,39,Color.white);
        var frame=panel.transform as RectTransform;frame.SetParent(pcRangeStage,false);PcRangePlace(frame,520,120,870,390);
        PcRangeFrame(frame,"panel","RangeStatsColumn","Preset_","DistMinus","DistPlus","RangeLife","RangePause","RangeReset");
        MovePcRange(frame,"RangeTitle",16,6,838,27);
        var heading=PcRangeFind(frame,"RangeTitle");if(heading){var text=heading.GetComponent<TMP_Text>();if(text)text.fontSize=21;}
        var lc=PcRangeFind(frame,"RangeStatsColumnLeft") as RectTransform;
        var rc=PcRangeFind(frame,"RangeStatsColumnRight") as RectTransform;
        PcRangePlace(lc,16,36,408,180);PcRangePlace(rc,438,36,416,180);
        PcRangeFrame(lc,"panel");PcRangeFrame(rc,"panel");
        MovePcRange(lc,"RangeStatsLeftTitle",12,4,384,24);MovePcRange(rc,"RangeStatsRightTitle",12,4,392,24);
        foreach(var titleName in new[]{"RangeStatsLeftTitle","RangeStatsRightTitle"}){var t=PcRangeFind(frame,titleName);if(t){var text=t.GetComponent<TMP_Text>();if(text)text.fontSize=19;}}
        PcRangePlace(statsLeftText.transform,12,32,384,140);PcRangePlace(statsRightText.transform,12,32,290,140);
        statsLeftText.fontSize=18;statsLeftText.enableAutoSizing=true;statsLeftText.fontSizeMin=12;statsLeftText.fontSizeMax=18;
        statsRightText.fontSize=17;statsRightText.enableAutoSizing=true;statsRightText.fontSizeMin=11;statsRightText.fontSizeMax=17;
        var targetPreview=PcRangeImage(rc,"TargetPreview","target_preview",310,32,94,136);targetPreview.preserveAspect=true;
        for(int i=0;i<presetButtons.Count;i++)PcRangePlace(presetButtons[i].transform,20+i*212,230,198,44);
        if(presetButtons.Count>1){var label=presetButtons[1].GetComponentInChildren<TMP_Text>();if(label)label.text="BLINDADO";}
        MovePcRange(frame,"DistMinus",230,284,58,42);MovePcRange(frame,"DistPlus",584,284,58,42);MovePcRange(frame,"DistLabel",300,284,272,42);
        if(distLabel)distLabel.fontSize=22;
        PcRangePlace(lifeButton.transform,130,338,196,44);PcRangePlace(pauseButton.transform,338,338,196,44);MovePcRange(frame,"RangeReset",546,338,196,44);
        var hint=PcRangeFind(frame,"RangeHint");if(hint)hint.gameObject.SetActive(false);
        var tips=PcRangeImage(pcRangeStage,"TestTips","panel",1414,120,482,290,true).rectTransform;
        PcRangeText(tips,"TipsTitle","DICAS DE TESTE",18,12,446,34,23,new Color(1f,0.75f,0.30f));
        PcRangeText(tips,"Tips","• Compare armas trocando o sobrevivente.\n• Equipe itens pela mochila.\n• Veja o DPS medido em tempo real.\n• Varie distância e resistência do alvo.\n• A formação de teste não muda a campanha.",18,55,446,148,19,new Color(0.70f,0.79f,0.82f),true);
        PcRangeButton(tips,"Inventory","MOCHILA [I]","panel",16,222,216,46,()=>{var toggle=PanelToggle.Instance;if(toggle)toggle.Toggle();});
        PcRangeButton(tips,"Shock","EMPURRAR [ESPAÇO]","panel",246,222,220,46,PcRangeShock);
        RefreshPcRangeButtons();HidePcLegacyRangeHud();layout.Apply();
    }
    static void MovePcRange(Transform root,string name,float x,float y,float w,float h)
    {
        var t=PcRangeFind(root,name);if(t)PcRangePlace(t,x,y,w,h);
    }
    void HidePcLegacyRangeHud()
    {
        if(!GameSession.IsRange)return;var hud=FindFirstObjectByType<HudController>();if(!hud)return;
        foreach(var name in new[]{"HudPlate_Wave","HudPlate_Backpack","HudPlate_Shock"}){var t=hud.transform.Find(name);if(t)t.gameObject.SetActive(false);}
    }
    void PcRangeShock()
    {
        foreach(var m in HeroSquad.Members)if(m!=null && m.stats && m.Alive)
        {
            var skills=m.stats.GetComponent<ActiveSkills>();if(skills && skills.TryShockWave())ZombitAudio.PlayUI();return;
        }
    }
    void RefreshPcRangeButtons()
    {
        if(!pcRangeStage)return;
        for(int i=0;i<presetButtons.Count;i++)PcRangeStyleButton(presetButtons[i],i==presetIndex?"red":"panel");
        int[] idx={0,1,3};for(int i=0;i<pcRangeTierButtons.Count;i++)PcRangeStyleButton(pcRangeTierButtons[i],presetIndex==idx[i]?"gold":"panel");
        PcRangeStyleButton(lifeButton,infiniteLife?"green":"panel");PcRangeStyleButton(pauseButton,Tracker.Paused?"green":"panel");
        foreach(var name in new[]{"DistMinus","DistPlus","RangeReset"}){var t=PcRangeFind(panel.transform,name);if(t)PcRangeStyleButton(t.GetComponent<Button>(),name=="RangeReset"?"red":"panel");}
    }
    void RefreshPcRangeStats()
    {
        if(!statsLeftText || !statsRightText || !hero)return;
        var p=Presets[presetIndex];float burst=0,sustained=0,avg=0;
        if(HeroSquad.Count>0)
        {
            foreach(var m in HeroSquad.Members)
            {
                if(m==null)continue;var h=m.hero?m.hero:m.stats?m.stats.GetComponent<Hero>():null;
                DpsTracker.Theoretical(h,out float b,out float s,out float a);burst+=b;sustained+=s;if(a>avg)avg=a;
            }
        }
        else DpsTracker.Theoretical(hero.GetComponent<Hero>(),out burst,out sustained,out avg);
        leftSb.Clear();leftSb.Append("DANO TOTAL:  <color=#FFC24B>").Append(N0(Tracker.Total)).Append("</color>\nDPS ATUAL (5s):  ").Append(N0(Tracker.DpsWindow()));
        leftSb.Append("\nDPS MÉDIO:  ").Append(N0(Tracker.DpsSession)).Append("\nPICO DE DPS:  ").Append(N0(Tracker.PeakDps)).Append("\nTEMPO:  ").Append(Clock(Tracker.Elapsed));
        leftSb.Append("\nTEÓRICO / SUST.:  ").Append(N0(burst)).Append(" / ").Append(N0(sustained)).Append("\n").Append(Tracker.Paused?"PAUSADO":Tracker.Started?"MEDINDO":"AGUARDANDO PRIMEIRO DANO");statsLeftText.text=leftSb.ToString();
        rightSb.Clear();rightSb.Append("DISTÂNCIA:  ").Append(ND(distance)).Append(" m\nACERTOS:  ").Append(Tracker.Hits).Append("\nCRÍTICOS:  ").Append(Tracker.Crits).Append(" (").Append(Mathf.RoundToInt(Tracker.CritRate*100)).Append("%)");
        rightSb.Append("\nMAIOR DANO:  ").Append(N0(Tracker.MaxHit)).Append("\nDANO MÉDIO:  ").Append(N0(Tracker.AvgHit)).Append("\nALVO:  ").Append(p.name);if(p.count>1)rightSb.Append(" x").Append(p.count);
        rightSb.Append("\nVIDA:  ").Append(infiniteLife?"INFINITA":N0(p.hp)+" HP");
        if(!infiniteLife)rightSb.Append("\nTTK:  ").Append(Tracker.Kills>0?N1(Tracker.LastTtk)+"s":"—");
        if(distance>ReachX()+0.05f)rightSb.Append("\n<color=#FF5050>FORA DE ALCANCE</color>");statsRightText.text=rightSb.ToString();
        if(distLabel)distLabel.text="DISTÂNCIA "+ND(distance)+" m";
    }
    void ApplyPcRangeFormation()
    {
        if(!trainingCanvas)return;
        pcRangeFormationStage=PcRangeNode(trainingCanvas.transform,"PcTrainingFormationStage",0,0,1920,1080);
        pcRangeFormationGroup=pcRangeFormationStage.gameObject.AddComponent<CanvasGroup>();
        var fit=pcRangeFormationStage.gameObject.AddComponent<PcRangeLayout>();fit.viewport=(RectTransform)trainingCanvas.transform;fit.content=pcRangeFormationStage;
        var strip=trainingCanvas.transform.Find("TrainingFormation") as RectTransform;if(!strip)return;
        strip.SetParent(pcRangeFormationStage,false);PcRangePlace(strip,24,922,1872,144);
        PcRangeFrame(strip,"panel","Choose_");MovePcRange(strip,"Title",16,4,1840,26);MovePcRange(strip,"Hint",16,124,1840,17);
        int count=GameDatabase.Survivors.Count;float width=(1836f-12f*Mathf.Max(0,count-1))/Mathf.Max(1,count);
        for(int i=0;i<count;i++)
        {
            var id=GameDatabase.Survivors[i].id;if(!trainingChoices.TryGetValue(id,out var b) || !b)continue;
            PcRangePlace(b.transform,18+i*(width+12),34,width,86);MovePcRange(b.transform,"Portrait",8,5,80,76);
            var text=b.GetComponentInChildren<TMP_Text>();if(text){PcRangePlace(text.transform,100,5,width-110,76);text.fontSizeMax=20;text.fontSizeMin=12;}
        }
        RefreshPcRangeFormation();fit.Apply();
    }
    void RefreshPcRangeFormation()
    {
        if(!pcRangeFormationStage)return;
        foreach(var pair in trainingChoices)PcRangeStyleButton(pair.Value,trainingOrder.Contains(pair.Key)?"selected":"panel");
        var title=PcRangeFind(pcRangeFormationStage,"Title");if(title){var text=title.GetComponent<TMP_Text>();if(text)text.text="SOBREVIVENTES: TOQUE PARA ALTERAR ("+trainingOrder.Count+"/3)";}
    }
    void ApplyPcRangeCamera()
    {
        if(!GameSession.IsRange || !rangeCameraAdjusted || !rangeCamera || !hero)return;
        if(pcRangeTheme && pcRangeTheme.composeCamera)
        {
            float aspect=Mathf.Max(0.1f,rangeCamera.aspect);rangeCamera.orthographicSize=Mathf.Max(0.1f,pcRangeTheme.cameraWidth)/(2f*aspect);
            float hh=rangeCamera.orthographicSize,hw=hh*aspect;
            rangeCamera.transform.position=new Vector3(hero.position.x+hw*(1-2*pcRangeTheme.heroScreenX),laneY+hh*(1-2*pcRangeTheme.feetScreenY),rangeCamera.transform.position.z);
        }
        if(pcRangeTheme && pcRangeTheme.worldBackground)
        {
            if(!pcRangeBackdrop){var go=new GameObject("PcTrainingBackdrop");go.transform.SetParent(transform,false);pcRangeBackdrop=go.AddComponent<SpriteRenderer>();}
            pcRangeBackdrop.sprite=pcRangeTheme.worldBackground;pcRangeBackdrop.sortingOrder=pcRangeTheme.backgroundSortingOrder;
            var bounds=pcRangeBackdrop.sprite.bounds;float hh=rangeCamera.orthographicSize;float hw=hh*rangeCamera.aspect;
            float scale=Mathf.Max(2*hw/Mathf.Max(0.01f,bounds.size.x),2*hh/Mathf.Max(0.01f,bounds.size.y));
            pcRangeBackdrop.transform.localScale=Vector3.one*scale;pcRangeBackdrop.transform.position=new Vector3(rangeCamera.transform.position.x-bounds.center.x*scale,rangeCamera.transform.position.y-bounds.center.y*scale,0);
        }
    }
    void PcRangePresentationTick(bool resized)
    {
        HidePcLegacyRangeHud();
        if(resized && rangeCameraAdjusted){ApplyPcRangeCamera();RebuildRuler(true);}
        var toggle=PanelToggle.Instance;bool inventoryOpen=toggle && toggle.panel && toggle.panel.activeInHierarchy;
        if(pcRangeGroup){pcRangeGroup.alpha=inventoryOpen?0:1;pcRangeGroup.blocksRaycasts=!inventoryOpen;}
        if(pcRangeFormationGroup){pcRangeFormationGroup.alpha=inventoryOpen?0:1;pcRangeFormationGroup.blocksRaycasts=!inventoryOpen;}
        if(pcRangeCashGold)
        {
            var inventory=FindFirstObjectByType<Inventory>();var save=GameSession.Save;
            pcRangeCashGold.text=(inventory?inventory.Gold:save!=null?save.gold:0).ToString("N0");pcRangeCashScrap.text=(save!=null?save.scrap:0).ToString("N0");
            pcRangeKills.text=Tracker.Kills.ToString();pcRangeTargets.text=Enemy.Alive.Count.ToString();
        }
    }
}
