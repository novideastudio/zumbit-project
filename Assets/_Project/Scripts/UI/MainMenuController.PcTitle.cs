using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Entry menu only. Uses the approved title artwork and separate functional button layers.
public partial class MainMenuController
{
    const string PcTitleArt="Art/PCTitleUI/";
    static readonly Dictionary<string,Sprite> pcTitleSprites=new Dictionary<string,Sprite>();
    RectTransform pcTitleStage;
    GameObject pcTitlePopup;

    Sprite PcTitleSprite(string name)
    {
        if(pcTitleSprites.TryGetValue(name,out var sp) && sp!=null)return sp;
        var texture=Resources.Load<Texture2D>(PcTitleArt+name);
        if(texture==null)throw new InvalidOperationException("Arte do menu inicial ausente: "+PcTitleArt+name);
        sp=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(0.5f,0.5f),100f);
        pcTitleSprites[name]=sp;return sp;
    }
    static RectTransform PcTitleRect(Transform parent,string name,float x,float y,float w,float h)
    {
        var go=new GameObject(name,typeof(RectTransform));var rt=(RectTransform)go.transform;rt.SetParent(parent,false);
        rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0f,1f);rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(w,h);rt.localScale=Vector3.one;return rt;
    }
    Image PcTitleImage(Transform parent,string name,string texture,float x,float y,float w,float h)
    {
        var rt=PcTitleRect(parent,name,x,y,w,h);var image=rt.gameObject.AddComponent<Image>();
        image.sprite=PcTitleSprite(texture);image.type=Image.Type.Simple;image.color=Color.white;image.raycastTarget=false;return image;
    }
    Button PcTitleButton(string texture,string name,float x,float y,float w,float h,Action action)
    {
        var image=PcTitleImage(pcTitleStage,name,texture,x,y,w,h);image.raycastTarget=true;
        var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;
        var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1f,0.88f,0.66f,1f);
        colors.pressedColor=new Color(0.70f,0.60f,0.48f,1f);colors.disabledColor=new Color(0.36f,0.36f,0.36f,1f);colors.selectedColor=Color.white;colors.fadeDuration=0.10f;button.colors=colors;
        // Prevent keyboard focus from being captured at menu creation; mouse and touch both work.
        var nav=button.navigation;nav.mode=Navigation.Mode.None;button.navigation=nav;
        button.onClick.AddListener(()=>{ZombitAudio.PlayUI();action();});return button;
    }
    void BuildPcReferenceTitle()
    {
        // Fail clearly instead of rendering an invisible replacement.
        PcTitleSprite("title_scene");
        var s=NewScreen("main");
        var black=UIFactory.Img(s,Color.black,"PcTitleLetterbox");UIFactory.Stretch(black.rectTransform);black.raycastTarget=false;
        pcTitleStage=PcTitleRect(s,"PcTitleStage",0,0,1672,941);
        var fit=pcTitleStage.gameObject.AddComponent<PcTitleFit>();fit.viewport=s;fit.stage=pcTitleStage;fit.Apply();
        PcTitleImage(pcTitleStage,"PcTitleArtwork","title_scene",0,0,1672,941);
        PcTitleButton("new_game","BtnNewGame",39,278,305,68,OnNewGame);
        continueBtn=PcTitleButton("continue","BtnContinue",39,354,305,70,OnContinue);
        PcTitleButton("load","BtnSlots",39,431,305,70,ShowSlots);
        PcTitleButton("options","BtnOptions",39,507,305,71,OpenPcTitleOptions);
        PcTitleButton("credits","BtnCredits",39,584,305,70,()=>Show("credits"));
        PcTitleButton("quit","BtnQuit",39,660,305,72,PcTitleQuit);
        continueBtn.interactable=PcTitleHasSave();
        var refresh=pcTitleStage.gameObject.AddComponent<PcTitleAvailability>();
        refresh.refresh=()=>{if(continueBtn)continueBtn.interactable=PcTitleHasSave();};
        fit.Apply();
    }
    bool PcTitleHasSave()
    {
        // A menu availability check must NOT load slots or resolve interrupted runs.
        for(int i=1;i<=SaveSystem.SlotCount;i++)if(SaveSystem.Exists(i))return true;
        return false;
    }
    void HidePcTitleLegacyAccount()
    {
        if(!screens.TryGetValue("main",out var s) || !s)return;
        foreach(string name in new[]{"AccountLabel","SignOutButton"})
        {
            var child=s.Find(name);if(child)child.gameObject.SetActive(false);
        }
    }
    void ClosePcTitlePopup()
    {
        if(pcTitlePopup)Destroy(pcTitlePopup);pcTitlePopup=null;
    }
    RectTransform PcTitlePopupPanel(string title)
    {
        ClosePcTitlePopup();
        var dim=PcTitleRect(pcTitleStage,"PcTitleDialog",0,0,1672,941);pcTitlePopup=dim.gameObject;
        var image=dim.gameObject.AddComponent<Image>();image.color=new Color(0f,0f,0f,0.83f);image.raycastTarget=true;
        var panel=UIKit.Frame(dim,"Panel",new Color(0.04f,0.035f,0.03f,1f),new Color(0.88f,0.47f,0.14f,1f),2f);
        UIFactory.Place(panel,new Vector2(0.5f,0.5f),Vector2.zero,new Vector2(650f,530f));
        var caption=UIFactory.Txt(panel,title,34,TMPro.TextAlignmentOptions.Center,UIFactory.Gold,"Title");
        UIFactory.Stretch(caption.rectTransform,0.04f,0.80f,0.96f,0.96f);
        return panel;
    }
    void OpenPcTitleOptions()
    {
        // Account actions are preserved here instead of overlaying the approved entry artwork.
        var p=PcTitlePopupPanel("OPÇÕES");
        PcTitleDialogButton(p,"ÁUDIO E VÍDEO",0.62f,()=>{ClosePcTitlePopup();RefreshOptions();Show("options");});
        PcTitleDialogButton(p,"ALTERAR NICK",0.44f,()=>{ClosePcTitlePopup();OpenNicknameEditorFromMainMenu();});
        var logout=PcTitleDialogButton(p,"SAIR DA CONTA",0.26f,()=>{ClosePcTitlePopup();ZombitCloud.SignOut();});logout.interactable=ZombitCloud.IsReady;
        PcTitleDialogButton(p,"VOLTAR",0.08f,ClosePcTitlePopup);
    }
    Button PcTitleDialogButton(RectTransform parent,string text,float y,Action action)
    {
        var b=UIFactory.Btn(parent,text,24,action,new Color(0.15f,0.12f,0.09f,1f),"DialogButton");
        UIFactory.Stretch((RectTransform)b.transform,0.08f,y,0.92f,y+0.13f);return b;
    }
    void PcTitleQuit()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        var p=PcTitlePopupPanel("SAIR DO JOGO");
        var t=UIFactory.Txt(p,"Na versão Web, feche a aba do navegador para sair.",26,TMPro.TextAlignmentOptions.Center,Color.white,"Message");
        t.textWrappingMode=TMPro.TextWrappingModes.Normal;UIFactory.Stretch(t.rectTransform,0.08f,0.35f,0.92f,0.72f);
        PcTitleDialogButton(p,"VOLTAR",0.10f,ClosePcTitlePopup);
#else
        OnQuit();
#endif
    }
}
