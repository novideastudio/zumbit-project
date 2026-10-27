using UnityEngine;
using UnityEngine.UI;
public partial class MainMenuController
{
    RectTransform cleanMapArea,cleanMissionPanel;
    bool cleanDetailsOpen;
    GameObject cleanCloseDetails;
    void SetupCleanCampaign(RectTransform screen,RectTransform mapArea)
    {
        cleanMapArea=mapArea;
        cleanMissionPanel=dStart!=null?dStart.transform.parent as RectTransform:null;
        ExactMapRect(mapArea,Vector2.zero,Vector2.one);
        mapArea.SetAsFirstSibling();
        var inner=mapArea.Find("MapInner") as RectTransform;
        if(inner) ExactMapRect(inner,Vector2.zero,Vector2.one);
        var back=UIFactory.Btn(screen,"< ACAMPAMENTO",22,ShowHome,new Color(0.03f,0.06f,0.07f,0.96f),"CampaignBack");
        UIFactory.Place((RectTransform)back.transform,new Vector2(0f,1f),new Vector2(12f,-10f),new Vector2(240f,58f));
        var close=UIFactory.Btn(screen,"FECHAR DETALHES >",20,()=>SetCleanDetails(false),new Color(0.12f,0.06f,0.06f,0.96f),"CampaignClose");
        UIFactory.Place((RectTransform)close.transform,new Vector2(1f,1f),new Vector2(-12f,-10f),new Vector2(260f,58f));
        cleanCloseDetails=close.gameObject;
        SetCleanDetails(false);
    }
    void SetCleanDetails(bool visible)
    {
        cleanDetailsOpen=visible;
        if(cleanMapArea) ExactMapRect(cleanMapArea,Vector2.zero,Vector2.one);
        if(cleanMissionPanel)
        {
            ExactMapRect(cleanMissionPanel,new Vector2(0.68f,0.015f),new Vector2(0.995f,0.91f));
            cleanMissionPanel.SetAsLastSibling();cleanMissionPanel.gameObject.SetActive(visible);
        }
        if(cleanCloseDetails) { cleanCloseDetails.transform.SetAsLastSibling();cleanCloseDetails.SetActive(visible); }
        Canvas.ForceUpdateCanvases();
    }
    void ToggleCampaignLocation(LocationDef loc)
    {
        if(loc==null) return;
        if(cleanDetailsOpen && selectedLoc!=null && selectedLoc.id==loc.id) { SetCleanDetails(false);return; }
        SetCleanDetails(true);SelectLocation(loc);
    }
}
