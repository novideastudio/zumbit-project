using UnityEngine;
using UnityEngine.UI;
public partial class MainMenuController
{
    RectTransform cleanMapArea, cleanMissionPanel;
    bool cleanDetailsOpen;
    GameObject cleanCloseDetails;
    void SetupCleanCampaign(RectTransform screen, RectTransform mapArea)
    {
        cleanMapArea = mapArea;
        cleanMissionPanel = dStart != null ? dStart.transform.parent as RectTransform : null;
        var back = UIFactory.Btn(screen, "< ACAMPAMENTO", 22, ShowHome, new Color(0.05f,0.07f,0.09f,0.95f), "CampaignBack");
        UIFactory.Place((RectTransform)back.transform, new Vector2(0f,1f), new Vector2(14f,-10f), new Vector2(240f,58f));
        var close = UIFactory.Btn(screen, "FECHAR DETALHES >", 20, () => SetCleanDetails(false), new Color(0.12f,0.07f,0.08f,1f), "CampaignClose");
        UIFactory.Place((RectTransform)close.transform, new Vector2(1f,1f), new Vector2(-14f,-10f), new Vector2(260f,58f));
        cleanCloseDetails = close.gameObject;
        SetCleanDetails(false);
    }
    void SetCleanDetails(bool visible)
    {
        cleanDetailsOpen = visible;
        if (cleanMissionPanel != null)
        {
            UIFactory.Stretch(cleanMissionPanel,0.675f,0.015f,0.995f,0.91f);
            cleanMissionPanel.gameObject.SetActive(visible);
        }
        if (cleanCloseDetails != null) cleanCloseDetails.SetActive(visible);
        if (cleanMapArea != null) UIFactory.Stretch(cleanMapArea,0.005f,0.015f,visible ? 0.665f : 0.995f,0.91f);
        Canvas.ForceUpdateCanvases();
    }
    void ToggleCampaignLocation(LocationDef location)
    {
        if (location == null) return;
        if (cleanDetailsOpen && selectedLoc != null && selectedLoc.id == location.id) { SetCleanDetails(false); return; }
        SetCleanDetails(true);
        SelectLocation(location);
    }
}
