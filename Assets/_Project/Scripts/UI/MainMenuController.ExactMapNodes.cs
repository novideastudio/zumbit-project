using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Cartões de localização desenhados por cima do mapa limpo. As posições são
// normalizadas em relação à área esquerda, para preservar a composição do mockup.
public partial class MainMenuController
{
    static void ExactMapRect(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static int MapDisplayNumber(LocationDef loc)
    {
        if (loc == null) return 0;
        switch (loc.id)
        {
            case "suburbio": return 1;
            case "residencial": return 2;
            case "shopping": return 3;
            case "hospital": return 4;
            case "delegacia": return 5;
            case "metro": return 6;
            case "centro": return 7;
            case "base": return 8;
            case "industrial": return 9; // Zona Industrial aparece como DOCAS na navegação.
            default: return GameDatabase.Locations.IndexOf(loc) + 1;
        }
    }

    static string MapDisplayName(LocationDef loc)
    {
        if (loc == null) return string.Empty;
        switch (loc.id)
        {
            case "suburbio": return "SUBÚRBIO";
            case "residencial": return "BAIRRO RESIDENCIAL";
            case "shopping": return "SHOPPING CENTER";
            case "hospital": return "HOSPITAL";
            case "delegacia": return "DELEGACIA";
            case "metro": return "ESTAÇÃO DE METRÔ";
            case "centro": return "CENTRO DA CIDADE";
            case "base": return "BASE MILITAR";
            case "industrial": return "DOCAS";
            default: return loc.name.ToUpperInvariant();
        }
    }

    void BuildExactMapNode(Transform parent, LocationDef loc, int index, SaveData save)
    {
        if (parent == null || loc == null || save == null) return;

        // x, topo, largura e altura, todos relativos ao painel de mapa esquerdo.
        // Disposição inspirada diretamente no mapa de referência enviado.
        float x, top, w, h;
        switch (loc.id)
        {
            case "suburbio":   x = 0.043f; top = 0.128f; w = 0.174f; h = 0.164f; break;
            case "residencial":x = 0.253f; top = 0.193f; w = 0.189f; h = 0.186f; break;
            case "shopping":   x = 0.434f; top = 0.379f; w = 0.181f; h = 0.164f; break;
            case "hospital":   x = 0.071f; top = 0.475f; w = 0.185f; h = 0.156f; break;
            case "delegacia":  x = 0.346f; top = 0.630f; w = 0.191f; h = 0.162f; break;
            case "metro":      x = 0.613f; top = 0.225f; w = 0.175f; h = 0.155f; break;
            case "centro":     x = 0.796f; top = 0.444f; w = 0.179f; h = 0.156f; break;
            case "base":       x = 0.826f; top = 0.130f; w = 0.151f; h = 0.161f; break;
            case "industrial": x = 0.762f; top = 0.709f; w = 0.198f; h = 0.165f; break;
            default: return;
        }

        bool unlocked = GameDatabase.IsUnlocked(loc, save);
        int completed = Mathf.Clamp(save.CompletedLevelCount(loc.id), 0, 5);
        Color border = unlocked ? new Color(1f, 0.72f, 0.16f, 1f) : new Color(0.38f, 0.42f, 0.43f, 1f);
        Color fill = unlocked ? new Color(0.025f, 0.033f, 0.035f, 1f) : new Color(0.025f, 0.03f, 0.032f, 1f);
        LocationDef location = loc;

        var button = UIFactory.Btn(parent, string.Empty, 10f, () => ToggleCampaignLocation(location), fill, "Node_" + loc.id);
        var rt = (RectTransform)button.transform;
        ExactMapRect(rt, new Vector2(x, 1f - top - h), new Vector2(x + w, 1f - top));
        button.interactable = true;
        button.transition = Selectable.Transition.ColorTint;
        var background = button.GetComponent<Image>();
        if (background)
        {
            background.sprite = UIKit.Chamfer;
            background.type = Image.Type.Sliced;
            background.color = Color.white;
            background.raycastTarget = true;
            button.targetGraphic = background;
        }
        var colors = button.colors;
        colors.normalColor = fill;
        colors.highlightedColor = Color.Lerp(fill, new Color(1f, 0.78f, 0.25f, 1f), 0.18f);
        colors.pressedColor = Color.Lerp(fill, Color.black, 0.25f);
        colors.selectedColor = Color.Lerp(fill, UIKit.Col.Gold, 0.12f);
        colors.disabledColor = new Color(0.10f, 0.11f, 0.11f, 1f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        // Miniatura regional na metade superior do cartão.
        var art = UIFactory.Img(button.transform, Color.white, "LocationArt");
        art.sprite = ArtLoader.Get("Art/Map/Locations/" + loc.id);
        art.preserveAspect = false;
        UIFactory.Stretch(art.rectTransform, 0.025f, 0.345f, 0.975f, 0.975f);
        art.raycastTarget = false;
        if (!unlocked) art.color = new Color(0.55f, 0.58f, 0.57f, 1f);

        var artShade = UIFactory.Img(button.transform, new Color(0.008f, 0.012f, 0.014f, unlocked ? 0.12f : 0.35f), "ArtShade");
        UIFactory.Stretch(artShade.rectTransform, 0.025f, 0.345f, 0.975f, 0.975f);
        artShade.raycastTarget = false;

        // Rodapé escuro dedicado ao nome e às estrelas de progressão.
        var footer = UIFactory.Img(button.transform, new Color(0.012f, 0.018f, 0.022f, 0.98f), "Footer");
        UIFactory.Stretch(footer.rectTransform, 0.02f, 0.02f, 0.98f, 0.365f);
        footer.raycastTarget = false;
        var footerLine = UIFactory.Img(button.transform, unlocked ? border : new Color(0.30f, 0.34f, 0.35f, 1f), "FooterLine");
        UIFactory.Stretch(footerLine.rectTransform, 0.025f, 0.355f, 0.975f, 0.365f);
        footerLine.raycastTarget = false;

        var numPlate = UIKit.Frame(button.transform, "NumBox", new Color(0.045f, 0.052f, 0.052f, 0.98f),
            unlocked ? UIKit.Col.Gold : new Color(0.48f, 0.52f, 0.52f, 1f), 1.5f);
        float numSize = (Screen.width < 1100 || Screen.height < 650) ? 30f : 42f;
        UIFactory.Place(numPlate, new Vector2(0f, 1f), new Vector2(7f, -6f), new Vector2(numSize, numSize * 0.88f));
        var num = UIFactory.Txt(numPlate, MapDisplayNumber(loc).ToString("00"), numSize * 0.48f,
            TextAlignmentOptions.Center, unlocked ? UIKit.Col.Gold : UIKit.Col.TextDim, "Num");
        UIFactory.Stretch(num.rectTransform);
        num.fontStyle = FontStyles.Bold;
        num.raycastTarget = false;

        var nameText = UIFactory.Txt(button.transform, MapDisplayName(loc), 13f,
            TextAlignmentOptions.MidlineLeft, unlocked ? Color.white : new Color(0.68f, 0.72f, 0.72f), "LocationName");
        UIFactory.Stretch(nameText.rectTransform, 0.055f, 0.155f, 0.96f, 0.335f);
        nameText.fontStyle = FontStyles.Bold;
        nameText.characterSpacing = 0.25f;
        nameText.textWrappingMode = TextWrappingModes.NoWrap;
        nameText.overflowMode = TextOverflowModes.Truncate;
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 8f;
        nameText.fontSizeMax = 13f;
        nameText.raycastTarget = false;

        for (int i = 0; i < 5; i++)
        {
            var star = UIFactory.Img(button.transform, i < completed ? UIKit.Col.Gold : new Color(0.25f, 0.29f, 0.30f, 1f), "Star_" + i);
            star.sprite = UIKit.Star;
            star.type = Image.Type.Simple;
            star.preserveAspect = true;
            float starSize = (Screen.width < 1100 || Screen.height < 650) ? 12f : 17f;
            UIFactory.Place(star.rectTransform, new Vector2(0f, 0f), new Vector2(10f + i * (starSize + 4f), 4f), new Vector2(starSize, starSize));
            star.raycastTarget = false;
        }

        if (!unlocked)
        {
            var lockText = UIFactory.Txt(button.transform, "BLOQUEADO", 9f, TextAlignmentOptions.MidlineRight,
                new Color(1f, 0.72f, 0.45f, 1f), "LockLevel");
            UIFactory.Place(lockText.rectTransform, new Vector2(1f, 1f), new Vector2(-5f, -5f), new Vector2(72f, 18f));
            lockText.fontStyle = FontStyles.Bold;
            lockText.enableAutoSizing = true; lockText.fontSizeMin = 7f; lockText.fontSizeMax = 9f;
            lockText.textWrappingMode = TextWrappingModes.NoWrap;
            lockText.raycastTarget = false;
        }

        var outline = button.gameObject.AddComponent<Outline>();
        outline.effectColor = border;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        nodeOutlines[loc.id] = outline;
        nodeBorders[loc.id] = border;
    }
}
