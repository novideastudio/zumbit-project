using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Presentation-only PC survivor selection. Confirmation stays in OnConfirmSurvivor().
public partial class MainMenuController
{
    sealed class PcSurvivorCard
    {
        public SurvivorDef def;
        public Image frame;
        public TMP_Text level,power,specialization,selection,name;
    }
    readonly List<PcSurvivorCard> pcSurvivorCards=new List<PcSurvivorCard>();
    static readonly Dictionary<string,Sprite> pcSurvivorSprites=new Dictionary<string,Sprite>();
    RectTransform pcSurvivorStage;
    Button pcSurvivorConfirm;
    const string PcSurvivorArt="Art/PCSurvivorUI/";

    Sprite PcSurvivorSprite(string key,bool sliced=false)
    {
        if(pcSurvivorSprites.TryGetValue(key,out var sp) && sp!=null)return sp;
        var tex=Resources.Load<Texture2D>(PcSurvivorArt+key);
        if(tex==null)throw new InvalidOperationException("Arte da selecao ausente: "+PcSurvivorArt+key);
        sp=Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),new Vector2(0.5f,0.5f),100f,0,SpriteMeshType.FullRect,sliced?new Vector4(18,18,18,18):Vector4.zero);
        pcSurvivorSprites[key]=sp;return sp;
    }
    static RectTransform PcSurvivorRect(Transform parent,string name,float x,float y,float w,float h)
    {
        var go=new GameObject(name,typeof(RectTransform));var rt=(RectTransform)go.transform;rt.SetParent(parent,false);
        rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0f,1f);rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(w,h);rt.localScale=Vector3.one;return rt;
    }
    Image PcSurvivorImage(Transform parent,string name,string texture,float x,float y,float w,float h,bool sliced=false)
    {
        var rt=PcSurvivorRect(parent,name,x,y,w,h);var image=rt.gameObject.AddComponent<Image>();
        image.sprite=PcSurvivorSprite(texture,sliced);image.type=sliced?Image.Type.Sliced:Image.Type.Simple;image.raycastTarget=false;return image;
    }
    TMP_Text PcSurvivorText(Transform parent,string name,string text,float x,float y,float w,float h,float size,Color color,bool wrap=false,TextAlignmentOptions align=TextAlignmentOptions.MidlineLeft)
    {
        var rt=PcSurvivorRect(parent,name,x,y,w,h);var t=rt.gameObject.AddComponent<TextMeshProUGUI>();
        if(UITheme.Current.displayFont)t.font=UITheme.Current.displayFont;else if(UITheme.Current.font)t.font=UITheme.Current.font;
        t.text=text;t.fontSize=size;t.fontSizeMin=Mathf.Min(size,14);t.fontSizeMax=size;t.enableAutoSizing=true;
        t.fontStyle=FontStyles.Bold;t.alignment=align;t.color=color;t.raycastTarget=false;t.richText=false;
        t.textWrappingMode=wrap?TextWrappingModes.Normal:TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Truncate;return t;
    }
    Button PcSurvivorButton(Transform parent,string name,string text,string texture,float x,float y,float w,float h,Action action)
    {
        var image=PcSurvivorImage(parent,name,texture,x,y,w,h,true);image.raycastTarget=true;
        var b=image.gameObject.AddComponent<Button>();b.targetGraphic=image;
        var c=b.colors;c.normalColor=Color.white;c.highlightedColor=new Color(1f,0.95f,0.83f);c.pressedColor=new Color(0.75f,0.73f,0.66f);c.selectedColor=Color.white;c.disabledColor=new Color(0.4f,0.4f,0.4f);b.colors=c;
        b.onClick.AddListener(()=>{ZombitAudio.PlayUI();action();});
        PcSurvivorText(image.transform,"Label",text,10,0,w-20,h,27,new Color(0.95f,0.91f,0.79f),false,TextAlignmentOptions.Center);return b;
    }
    void BuildPcSurvivorSelect()
    {
        PcSurvivorSprite("panel",true);
        var screen=NewScreen("select");pcSurvivorCards.Clear();cardBtns.Clear();cardImgs.Clear();cardFills.Clear();
        var black=UIFactory.Img(screen,Color.black,"PcSelectionLetterbox");UIFactory.Stretch(black.rectTransform);black.raycastTarget=false;
        pcSurvivorStage=PcSurvivorRect(screen,"PcSelectionStage",0,0,1920,1080);
        var fit=pcSurvivorStage.gameObject.AddComponent<PcSurvivorFit>();fit.viewport=screen;fit.content=pcSurvivorStage;fit.Apply();fit.escape=PcSurvivorBack;
        PcSurvivorImage(pcSurvivorStage,"Background","backdrop",0,0,1920,1080);
        var shade=UIFactory.Img(pcSurvivorStage,new Color(0.01f,0.02f,0.03f,0.88f),"Shade");UIFactory.Stretch(shade.rectTransform);shade.raycastTarget=false;
        PcSurvivorImage(pcSurvivorStage,"Header","panel",24,18,1872,100,true);
        PcSurvivorText(pcSurvivorStage,"Heading","SOBREVIVENTES",50,31,750,48,40,new Color(0.94f,0.90f,0.77f));
        playerIdentityInSelect=PcSurvivorText(pcSurvivorStage,"Player","",50,81,1700,26,19,new Color(0.70f,0.78f,0.81f));
        var list=GameDatabase.Survivors;
        float gap=16f;
        float cardW=(1808f-gap*Mathf.Max(0,list.Count-1))/Mathf.Max(1,list.Count);
        for(int i=0;i<list.Count;i++)BuildPcSurvivorCard(list[i],56f+i*(cardW+gap),cardW);
        selNote=PcSurvivorText(pcSurvivorStage,"Note","",56,905,1808,40,20,new Color(0.80f,0.75f,0.61f),true,TextAlignmentOptions.Center);
        PcSurvivorButton(pcSurvivorStage,"BackButton","VOLTAR","panel",520,963,330,72,PcSurvivorBack);
        pcSurvivorConfirm=PcSurvivorButton(pcSurvivorStage,"ConfirmButton","CONFIRMAR SOBREVIVENTE","primary",878,963,522,72,()=>{if(selected!=null)OnConfirmSurvivor();});
        confirmLabel=pcSurvivorConfirm.GetComponentInChildren<TMP_Text>();
        fit.Apply();
    }
    void BuildPcSurvivorCard(SurvivorDef def,float x,float w)
    {
        var im=PcSurvivorImage(pcSurvivorStage,"PcCard_"+def.id,"panel",x,146,w,738,true);
        im.raycastTarget=true;
        var button=im.gameObject.AddComponent<Button>();button.targetGraphic=im;button.transition=Selectable.Transition.None;
        button.onClick.AddListener(()=>{ZombitAudio.PlayUI();SelectPcSurvivor(def);});
        var portrait=PcSurvivorImage(im.transform,"CampCharacter",def.id,12,55,w-24,304);portrait.preserveAspect=true;
        var c=new PcSurvivorCard {def=def,frame=im};pcSurvivorCards.Add(c);
        c.name=PcSurvivorText(im.transform,"Name",def.name.ToUpperInvariant(),16,10,w-118,35,27,new Color(0.94f,0.90f,0.78f));
        c.level=PcSurvivorText(im.transform,"Level","",w-96,12,78,31,19,UIKit.Col.Gold,false,TextAlignmentOptions.MidlineRight);
        PcSurvivorText(im.transform,"Role",BlurbFor(def).title,16,365,w-32,30,24,new Color(0.78f,0.82f,0.80f));
        c.power=PcSurvivorText(im.transform,"Power","",16,400,w-32,28,19,UIKit.Col.Gold);
        var weapon=WeaponCatalog.Preview(def);
        PcSurvivorText(im.transform,"Weapon",weapon.displayName+"  |  ARMA BASE",16,440,w-32,28,23,new Color(0.94f,0.90f,0.78f));
        PcSurvivorText(im.transform,"WeaponStats","DANO "+(weapon.damage*def.damageMult).ToString("0.#")+"  |  "+(weapon.shotsPerSecond*def.fireRateMult).ToString("0.#")+"/s  |  PENTE "+weapon.magazineSize,16,472,w-32,38,16,new Color(0.68f,0.76f,0.79f),true);
        PcSurvivorText(im.transform,"AbilityName",(def.utilityName??"").ToUpperInvariant(),16,521,w-32,28,20,UIKit.Col.Gold);
        PcSurvivorText(im.transform,"AbilityDescription",def.utilityDesc??"",16,553,w-32,81,18,new Color(0.78f,0.83f,0.83f),true);
        c.specialization=PcSurvivorText(im.transform,"Specialization","",16,644,w-32,46,17,new Color(0.68f,0.76f,0.79f),true);
        c.selection=PcSurvivorText(im.transform,"Selection","",16,698,w-32,28,18,UIKit.Col.Gold,false,TextAlignmentOptions.Center);
    }
    void ShowPcSurvivorSelect()
    {
        if(GameDatabase.Survivors.Count==0){Toast("Nenhum sobrevivente disponível.");Show("main");return;}
        if(!newGameMode && GameSession.Save==null){Show("main");return;}
        if(!newGameMode)selected=GameDatabase.GetSurvivor(GameSession.Save.selectedSurvivor);
        if(selected==null)selected=GameDatabase.Survivors[0];
        string nick=newGameMode?GetPendingNickname("JOGADOR"):GetPlayerNickname(GameSession.Save);
        playerIdentityInSelect.text="JOGADOR: "+nick.ToUpperInvariant()+"  |  ESCOLHA SEU SOBREVIVENTE";
        confirmLabel.text=newGameMode?"INICIAR CAMPANHA":"CONFIRMAR SOBREVIVENTE";
        selNote.text=newGameMode && SaveSystem.FirstEmptySlot()<0?"Todos os slots estão cheios: o Slot 1 será sobrescrito.":newGameMode?"Escolha sua classe. O progresso será salvo automaticamente.":"Cada sobrevivente mantém seu nível, equipamento e habilidades. A mudança é aplicada ao confirmar.";
        foreach(var card in pcSurvivorCards)
        {
            int level=1;string power="PODER BASE";
            SurvivorProgress progress=null;
            if(!newGameMode && GameSession.Save!=null)
            {
                progress=GameSession.Save.GetProgress(card.def.id);level=progress.level;
                power="PODER "+PowerRating.Of(GameSession.Save,card.def.id);
            }
            card.level.text="NV. "+level;card.power.text=power;
            var spec=progress!=null?SkillTreeSystem.GetSpec(progress):null;
            card.specialization.text=spec!=null?"ESPECIALIZAÇÃO: "+spec.name:"ESPECIALIZAÇÃO DISPONÍVEL NO NV. 15";
        }
        SelectPcSurvivor(selected);Show("select");
        if(pcSurvivorStage)pcSurvivorStage.GetComponent<PcSurvivorFit>().Apply();
    }
    void SelectPcSurvivor(SurvivorDef def)
    {
        if(def==null)return;selected=def;
        foreach(var c in pcSurvivorCards)
        {
            bool on=c.def.id==def.id;
            c.frame.sprite=PcSurvivorSprite(on?"selected":"panel",true);
            c.frame.color=Color.white;c.frame.transform.localScale=Vector3.one;
            c.selection.text=on?"SELECIONADO":"CLIQUE PARA SELECIONAR";
            c.selection.color=on?UIKit.Col.Gold:new Color(0.43f,0.53f,0.58f);
            c.name.color=on?UIKit.Col.Gold:new Color(0.94f,0.90f,0.78f);
        }
        if(pcSurvivorConfirm)pcSurvivorConfirm.interactable=true;
    }
    void PcSurvivorBack()
    {
        if(newGameMode)Show("main");else ShowHome();
    }
}
