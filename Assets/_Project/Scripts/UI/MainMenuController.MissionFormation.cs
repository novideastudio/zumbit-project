using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public partial class MainMenuController
{
    Canvas missionFormationCanvas;
    readonly List<string> missionFormationIds = new List<string>();
    readonly Dictionary<string, Button> missionFormationButtons = new Dictionary<string, Button>();
    TMP_Text missionFormationSummary;
    Button missionFormationConfirm;
    int MissionFormationLimit => selectedDiff >= HeroSquad.TrioDifficulty ? 3 : 2;

    void AddMissionFormationButton()
    {
        if(dStart == null) return;
        var content=dStart.transform.parent.Find("MissionScroll/MissionContent");
        if(content == null) { Debug.LogWarning("Zombit: MissionContent ausente; botao de formacao nao instalado."); return; }
        var b=UIFactory.Btn(content,"ESCOLHER FORMAÇÃO",20,OpenMissionFormation,null,"MissionFormationButton");
        var le=b.gameObject.AddComponent<LayoutElement>();le.preferredHeight=60f;
        b.transform.SetAsFirstSibling();
    }
    void CloseMissionFormation()
    {
        if(missionFormationCanvas) Destroy(missionFormationCanvas.gameObject);
        missionFormationCanvas=null;
    }
    void OpenMissionFormation()
    {
        var save=GameSession.Save;
        if(save==null || selectedLoc==null || missionFormationCanvas!=null) return;
        if(!GameDatabase.IsDifficultyUnlocked(selectedLoc,save,selectedDiff)) { Toast("Local ou dificuldade bloqueados.");return; }
        missionFormationIds.Clear();missionFormationButtons.Clear();
        missionFormationIds.Add(save.selectedSurvivor);
        if(save.HasPartner && save.selectedPartner!=save.selectedSurvivor) missionFormationIds.Add(save.selectedPartner);
        if(MissionFormationLimit==3 && save.HasThird && !missionFormationIds.Contains(save.selectedThird)) missionFormationIds.Add(save.selectedThird);
        missionFormationCanvas=UIFactory.MakeCanvas("MissionFormationPicker",200);
        var dim=UIFactory.Img(missionFormationCanvas.transform,new Color(0f,0f,0f,0.82f),"Dim");UIFactory.Stretch(dim.rectTransform);
        var panel=UIKit.Frame(missionFormationCanvas.transform,"Panel",new Color(0.025f,0.04f,0.055f,1f),UIKit.Col.Gold,2f);
        UIFactory.Stretch(panel,0.10f,0.15f,0.90f,0.85f);
        var title=UIFactory.Txt(panel,"FORMAÇÃO DA MISSÃO",32,TextAlignmentOptions.Center,UIKit.Col.Gold,"Title");
        UIFactory.Stretch(title.rectTransform,0.03f,0.85f,0.97f,0.97f);
        var hint=UIFactory.Txt(panel,selectedLoc.name+" · "+Difficulties.Get(selectedDiff).name+" · Máximo "+MissionFormationLimit+" sobreviventes\nToque para adicionar/remover. A ordem dos cliques define frente, meio e atrás.",20,TextAlignmentOptions.Center,Color.white,"Hint");
        UIFactory.Stretch(hint.rectTransform,0.03f,0.69f,0.97f,0.85f);
        int n=GameDatabase.Survivors.Count;
        for(int i=0;i<n;i++)
        {
            var def=GameDatabase.Survivors[i];string id=def.id;
            var b=UIFactory.Btn(panel,"",18,()=>ToggleMissionFormation(id),null,"Choose_"+id);
            float x=0.025f+i*0.95f/n;
            UIFactory.Stretch((RectTransform)b.transform,x,0.30f,x+0.95f/n-0.012f,0.68f);
            var image=UIFactory.Img(b.transform,Color.white,"Portrait");image.sprite=HubPortrait(def);image.preserveAspect=true;image.raycastTarget=false;
            UIFactory.Stretch(image.rectTransform,0.08f,0.25f,0.92f,0.95f);
            var text=b.GetComponentInChildren<TMP_Text>();
            text.text=def.name.ToUpperInvariant()+"\nNV. "+save.GetProgress(id).level;
            UIFactory.Stretch(text.rectTransform,0.02f,0.02f,0.98f,0.24f);text.raycastTarget=false;
            text.enableAutoSizing=true;text.fontSizeMin=12;text.fontSizeMax=20;
            missionFormationButtons[id]=b;
        }
        missionFormationSummary=UIFactory.Txt(panel,"",20,TextAlignmentOptions.Center,UIKit.Col.Gold,"Summary");
        UIFactory.Stretch(missionFormationSummary.rectTransform,0.03f,0.19f,0.97f,0.29f);
        missionFormationConfirm=UIFactory.Btn(panel,"CONFIRMAR",22,ConfirmMissionFormation,UIFactory.Red,"Confirm");
        UIFactory.Stretch((RectTransform)missionFormationConfirm.transform,0.15f,0.04f,0.48f,0.16f);
        var cancel=UIFactory.Btn(panel,"CANCELAR",22,CloseMissionFormation,null,"Cancel");
        UIFactory.Stretch((RectTransform)cancel.transform,0.52f,0.04f,0.85f,0.16f);
        RefreshMissionFormation();
    }
    void ToggleMissionFormation(string id)
    {
        if(missionFormationIds.Contains(id)) missionFormationIds.Remove(id);
        else if(missionFormationIds.Count<MissionFormationLimit) missionFormationIds.Add(id);
        else { Toast("Máximo de "+MissionFormationLimit+" nesta dificuldade.");return; }
        RefreshMissionFormation();
    }
    void RefreshMissionFormation()
    {
        foreach(var kv in missionFormationButtons)
            UIFactory.SetButtonColor(kv.Value,missionFormationIds.Contains(kv.Key)?new Color(0.12f,0.35f,0.22f):new Color(0.06f,0.09f,0.12f));
        var names=new List<string>();
        foreach(var id in missionFormationIds) names.Add(GameDatabase.GetSurvivor(id).name);
        missionFormationSummary.text=names.Count==0?"Selecione pelo menos um sobrevivente":string.Join(" > ",names)+" · "+names.Count+"/"+MissionFormationLimit;
        missionFormationConfirm.interactable=names.Count>0;
    }
    void ConfirmMissionFormation()
    {
        var save=GameSession.Save;
        if(save==null || selectedLoc==null || missionFormationIds.Count<1 || missionFormationIds.Count>MissionFormationLimit) return;
        if(!GameDatabase.IsDifficultyUnlocked(selectedLoc,save,selectedDiff)) { CloseMissionFormation();Toast("Missão indisponível.");return; }
        save.selectedSurvivor=missionFormationIds[0];
        save.selectedPartner=missionFormationIds.Count>1?missionFormationIds[1]:"";
        save.selectedThird=missionFormationIds.Count>2?missionFormationIds[2]:"";
        save.NormalizeSquad();TrySaveSquad(save);
        int keep=selectedDiff;
        CloseMissionFormation();
        SelectLocation(selectedLoc);SelectDifficulty(keep);
        Toast("Formação confirmada.");
    }
}
