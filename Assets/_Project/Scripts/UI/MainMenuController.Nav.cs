using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Cabecalho e navegacao unificados das telas abertas pelo mapa (parte de MainMenuController).
// Ordem das abas identica em Mapa, Mochila, Habilidades, Personagem e Loja.
public partial class MainMenuController
{
    enum NavTab { Map, Bag, Skills, Character, Shop }

    void NavGo(NavTab tab)
    {
        switch (tab)
        {
            case NavTab.Map: ShowMap(); break;
            case NavTab.Bag: ShowMapInventory(null, currentScreen != "mapSkills"); break;   // vindo de Habilidades, mantem o sobrevivente
            case NavTab.Skills: ShowMapSkills(null); break;
            case NavTab.Character: newGameMode = false; ShowSelect(); break;
            case NavTab.Shop: ShowShop(); break;
        }
    }

    RectTransform CreateNavHeader(RectTransform screen, NavTab active, bool withMenu,
        out RectTransform left, out RectTransform chips)
    {
        var header = UIFactory.Img(screen, UIKit.Col.Header, "NavHeader");
        UIFactory.Stretch(header.rectTransform, 0f, 0.915f, 1f, 1f);
        header.raycastTarget = false;

        var line = UIFactory.Img(header.transform, UIKit.Col.Accent, "NavHeaderLine");
        var lr = line.rectTransform;
        lr.anchorMin = new Vector2(0f, 0f); lr.anchorMax = new Vector2(1f, 0f); lr.pivot = new Vector2(0.5f, 0f);
        lr.offsetMin = Vector2.zero; lr.offsetMax = new Vector2(0f, 3f);
        line.raycastTarget = false;

        left = UIFactory.Node("NavLeft", header.transform);
        UIFactory.Stretch(left, 0f, 0f, 0.30f, 1f);
        left.offsetMin = new Vector2(28f, 8f); left.offsetMax = new Vector2(0f, -8f);

        chips = UIFactory.Node("NavChips", header.transform);
        UIFactory.Stretch(chips, 0.30f, 0f, 0.53f, 1f);
        chips.offsetMin = new Vector2(0f, 14f); chips.offsetMax = new Vector2(0f, -14f);
        UIFactory.HLayout(chips, 10f, TextAnchor.MiddleLeft);

        var tabs = UIFactory.Node("NavTabs", header.transform);
        UIFactory.Stretch(tabs, 0.53f, 0f, 1f, 1f);
        tabs.offsetMin = new Vector2(0f, 14f); tabs.offsetMax = new Vector2(-24f, -14f);
        var h = UIFactory.HLayout(tabs, 8f, TextAnchor.MiddleRight);
        h.childForceExpandWidth = true; h.childForceExpandHeight = true;

        // Navegacao fica na barra lateral do mapa: aqui so MENU (no mapa) ou X para voltar ao mundo (nas janelas).
        h.childForceExpandWidth = false;
        if (withMenu)
        {
            // Mapa: sem botao aqui. O lado direito recebe ouro/sucata (BuildHubCurrency) e o retorno
            // a tela inicial fica no fim da barra lateral (Side_Home).
        }
        else
        {
            var close = UIKit.Button(tabs, "X   VOLTAR", ReturnFromUtility, UIKit.BtnStyle.Danger, UIKit.Size.Body, "NavClose");
            UIFactory.Elem(close.gameObject, 290f, 60f);
        }
        return header.rectTransform;
    }

    void AddNavTab(RectTransform tabs, NavTab tab, string label, NavTab active)
    {
        var b = UIKit.Tab(tabs, label, tab == active, () => { if (tab != active) NavGo(tab); }, "NavTab_" + tab);
        UIFactory.Elem(b.gameObject, 140f, 60f);
    }

    // Cabecalho simples para Mochila / Habilidades / Loja.
    RectTransform CreateUtilityHeader(RectTransform screen, string title, string info, NavTab active)
    {
        var header = CreateNavHeader(screen, active, false, out var left, out var chips);
        var tick = UIFactory.Img(left, UIKit.Col.Accent, "TitleTick");
        UIFactory.Place(tick.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(6f, 44f));
        tick.raycastTarget = false;
        var t = UIFactory.Txt(left, title, UIKit.Size.Title, TextAlignmentOptions.MidlineLeft, UIKit.Col.Text, "UtilityTitle");
        t.fontStyle = FontStyles.Bold;
        t.characterSpacing = 1.5f;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        UIFactory.Stretch(t.rectTransform);
        t.rectTransform.offsetMin = new Vector2(20f, 0f);

        if (!string.IsNullOrEmpty(info))
        {
            var pill = UIKit.Frame(chips, "InfoPill", new Color(0.06f, 0.09f, 0.11f, 1f), UIKit.Col.Border, 1.5f);
            UIFactory.Elem(pill.gameObject, 280f, 56f);
            var it = UIFactory.Txt(pill, info, UIKit.Size.Label, TextAlignmentOptions.Center, UIKit.Col.Gold, "UtilityInfo");
            it.fontStyle = FontStyles.Bold;
            it.textWrappingMode = TextWrappingModes.NoWrap;
            // Ajusta o tamanho para o texto (OURO + SUCATA) nunca passar da moldura.
            it.enableAutoSizing = true;
            it.fontSizeMax = it.fontSize;
            it.fontSizeMin = 12f;
            UIFactory.Stretch(it.rectTransform);
            it.rectTransform.offsetMin = new Vector2(10f, 0f);
            it.rectTransform.offsetMax = new Vector2(-10f, 0f);
        }
        return header;
    }
}
