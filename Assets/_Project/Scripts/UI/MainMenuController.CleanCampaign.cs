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

        ExactMapRect(mapArea, Vector2.zero, new Vector2(0.70f, 1f));
        var backdrop = screen.Find("MapBackdrop");
        if (backdrop != null) backdrop.SetAsFirstSibling();
        mapArea.SetSiblingIndex(backdrop != null ? backdrop.GetSiblingIndex() + 1 : 0);
        var inner = mapArea.Find("MapInner") as RectTransform;
        if (inner) ExactMapRect(inner, Vector2.zero, Vector2.one);

        var back = UIFactory.Btn(screen, "‹", 50f, ShowHome,
            new Color(0.025f, 0.035f, 0.045f, 0.98f), "CampaignBack");
        UIFactory.Place((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(42f, -42f), new Vector2(68f, 66f));
        var backText = back.GetComponentInChildren<TMPro.TMP_Text>(true);
        if (backText) { backText.color = UIKit.Col.Gold; backText.fontStyle = TMPro.FontStyles.Bold; backText.fontSize = 46f; }
        var backOutline = back.gameObject.AddComponent<Outline>();
        backOutline.effectColor = new Color(1f, 0.68f, 0.12f, 0.95f);
        backOutline.effectDistance = new Vector2(1.5f, -1.5f);

        // O X fica dentro do cabeçalho do painel, como na referência.
        Transform closeParent = cleanMissionPanel != null ? (Transform)cleanMissionPanel : screen;
        var close = UIFactory.Btn(closeParent, "×", 34f, () => SetCleanDetails(false),
            new Color(0.075f, 0.085f, 0.095f, 0.98f), "CampaignClose");
        UIFactory.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(50f, 50f));
        var closeText = close.GetComponentInChildren<TMPro.TMP_Text>(true);
        if (closeText) { closeText.color = new Color(0.92f, 0.94f, 0.96f, 1f); closeText.fontStyle = TMPro.FontStyles.Bold; }
        var closeOutline = close.gameObject.AddComponent<Outline>();
        closeOutline.effectColor = new Color(0.38f, 0.43f, 0.48f, 1f);
        closeOutline.effectDistance = new Vector2(1f, -1f);
        cleanCloseDetails = close.gameObject;

        // A tela abre como no mockup: primeira localização selecionada e detalhes visíveis.
        SetCleanDetails(true);
    }

    void SetCleanDetails(bool visible)
    {
        cleanDetailsOpen = visible;
        if (cleanMapArea)
            ExactMapRect(cleanMapArea, Vector2.zero, new Vector2(visible ? 0.70f : 1f, 1f));

        if (cleanMissionPanel)
        {
            // Margem superior de ~7,5%, igual à referência; painel quase até ao fundo.
            ExactMapRect(cleanMissionPanel, new Vector2(0.70f, 0.025f), new Vector2(0.995f, 0.925f));
            cleanMissionPanel.SetAsLastSibling();
            cleanMissionPanel.gameObject.SetActive(visible);
        }
        if (cleanCloseDetails)
        {
            cleanCloseDetails.transform.SetAsLastSibling();
            cleanCloseDetails.SetActive(visible);
        }
        Canvas.ForceUpdateCanvases();
    }

    void ToggleCampaignLocation(LocationDef loc)
    {
        if (loc == null) return;
        if (!cleanDetailsOpen) SetCleanDetails(true);
        SelectLocation(loc);
    }
}
