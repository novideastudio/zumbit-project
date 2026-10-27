#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Reorganiza o prefab Screen_main: arte inteira (sem cortar o logo) e menu menor, em coluna vertical.
// Depois de rodar, tudo continua editavel no prefab (arraste o objeto "Buttons" para mover o menu).
public static class MainScreenRelayout
{
    const string PrefabPath = "Assets/_Project/Resources/UI/Screens/Screen_main.prefab";

    // Ajuste aqui se quiser outro tamanho/posicao e rode o menu de novo.
    const float ButtonWidth = 320f;
    const float ButtonHeight = 54f;
    const float Spacing = 10f;
    static readonly Vector2 MenuAnchor = new Vector2(1f, 0.42f);   // lado direito, um pouco abaixo do meio
    static readonly Vector2 MenuOffset = new Vector2(-70f, 0f);
    static readonly Color BackdropTint = new Color(0.32f, 0.32f, 0.36f, 1f);   // quanto escurecer o fundo das laterais

    [MenuItem("ZomBit/UI/2 - Tela inicial: arte inteira + menu vertical")]
    static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("ZomBit UI", "Saia do modo Play antes.", "OK");
            return;
        }
        if (!System.IO.File.Exists(PrefabPath))
        {
            EditorUtility.DisplayDialog("ZomBit UI", "Nao achei " + PrefabPath, "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("ZomBit UI",
                "Isto altera o prefab Screen_main (menu vertical, arte inteira).\nFaca uma copia dele antes se quiser poder voltar atras.\n\nContinuar?",
                "Aplicar", "Cancelar"))
            return;

        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            // 1) Arte inteira: FitInParent mostra a imagem toda (logo incluso). Antes era EnvelopeParent (corta as bordas).
            var art = EditorUiBinding.FindDeep(root.transform, "MenuArt");
            if (art != null)
            {
                var fit = art.GetComponent<AspectRatioFitter>();
                if (fit != null)
                {
                    fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                    // Proporcao real da arte atual (evita faixa de 1 pixel se a imagem for trocada por outra de tamanho diferente).
                    var sp = art.GetComponent<Image>() != null ? art.GetComponent<Image>().sprite : null;
                    if (sp != null && sp.rect.height > 0f) fit.aspectRatio = sp.rect.width / sp.rect.height;
                }

                // 1b) Fundo das laterais: copia escurecida da MESMA arte, preenchendo a tela toda (cortando as bordas).
                // Sem isso, as faixas laterais mostram o cenario generico do MenuCanvas (outra imagem).
                var artImage = art.GetComponent<Image>();
                var existing = EditorUiBinding.FindDeep(root.transform, "MenuBackdrop");
                GameObject bd = existing != null
                    ? existing.gameObject
                    : new GameObject("MenuBackdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                if (existing == null) bd.transform.SetParent(root.transform, false);
                bd.transform.SetAsFirstSibling();
                var bdImage = bd.GetComponent<Image>();
                bdImage.sprite = artImage != null ? artImage.sprite : null;
                bdImage.color = bdImage.sprite != null ? BackdropTint : new Color(0.02f, 0.03f, 0.04f, 1f);
                bdImage.preserveAspect = false;
                bdImage.raycastTarget = false;
                var bdFit = bd.GetComponent<AspectRatioFitter>();
                if (bdFit == null) bdFit = bd.AddComponent<AspectRatioFitter>();
                bdFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                bdFit.aspectRatio = fit != null ? fit.aspectRatio : 1.7778f;
            }

            // 2) A faixa escura de baixo era o fundo da barra horizontal: nao e mais necessaria.
            foreach (var n in new[] { "MenuBar", "MenuBarFadeTop", "MenuBarFadeMid", "MenuBarLine" })
            {
                var t = EditorUiBinding.FindDeep(root.transform, n);
                if (t != null) t.gameObject.SetActive(false);
            }

            // 3) Menu em coluna.
            var buttons = EditorUiBinding.FindDeep(root.transform, "Buttons") as RectTransform;
            if (buttons == null)
            {
                EditorUtility.DisplayDialog("ZomBit UI", "Objeto 'Buttons' nao encontrado em Screen_main.", "OK");
                return;
            }

            var h = buttons.GetComponent<HorizontalLayoutGroup>();
            if (h != null) Object.DestroyImmediate(h);
            var v = buttons.GetComponent<VerticalLayoutGroup>();
            if (v == null) v = buttons.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(0, 0, 0, 0);
            v.spacing = Spacing;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            var fitter = buttons.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = buttons.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            buttons.anchorMin = MenuAnchor;
            buttons.anchorMax = MenuAnchor;
            buttons.pivot = new Vector2(1f, 0.5f);
            buttons.anchoredPosition = MenuOffset;
            buttons.sizeDelta = new Vector2(ButtonWidth, 0f);

            for (int i = 0; i < buttons.childCount; i++)
            {
                var child = buttons.GetChild(i);
                if (child.GetComponent<Button>() == null) continue;
                var le = child.GetComponent<LayoutElement>();
                if (le == null) le = child.gameObject.AddComponent<LayoutElement>();
                le.minWidth = -1f;
                le.preferredWidth = -1f;
                le.flexibleWidth = -1f;
                le.minHeight = ButtonHeight;
                le.preferredHeight = ButtonHeight;
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[ZomBit UI] Screen_main reorganizada: arte inteira + menu vertical.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif
