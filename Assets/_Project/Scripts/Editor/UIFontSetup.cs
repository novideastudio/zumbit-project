#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Gera os TMP Font Assets a partir de TTFs em Assets/_Project/Fonts e liga ao UITheme.
/// Passos: coloque "Display.ttf" (titulos/botoes, ex.: Rajdhani Bold) e "Body.ttf" (textos, ex.: Chakra Petch / Exo 2)
/// em Assets/_Project/Fonts e use o menu Zombit > UI > Gerar fontes do tema.
/// Opcional: "Symbols.ttf" (ex.: Noto Sans Symbols 2) vira fallback para glifos como setas e losangos.
/// </summary>
public static class UIFontSetup
{
    const string Dir = "Assets/_Project/Fonts";

    [MenuItem("Zombit/UI/Gerar fontes do tema")]
    static void Run()
    {
        var theme = Resources.Load<UITheme>("Config/UITheme");
        if (theme == null) { Debug.LogError("UITheme nao encontrado em Resources/Config."); return; }
        if (!Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); AssetDatabase.Refresh(); }

        var symbols = Make("Symbols");
        var display = Make("Display");
        var body = Make("Body");
        if (symbols != null)
        {
            if (display != null) AddFallback(display, symbols);
            if (body != null) AddFallback(body, symbols);
        }
        if (display != null) theme.displayFont = display;
        if (body != null) theme.font = body;
        EditorUtility.SetDirty(theme);
        AssetDatabase.SaveAssets();
        UITheme.Reload();
        Debug.Log("Zombit: fontes do tema atualizadas (Display=" + (display != null) + ", Body=" + (body != null) + ").");
    }

    static void AddFallback(TMP_FontAsset main, TMP_FontAsset fallback)
    {
        if (main.fallbackFontAssetTable == null) main.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
        if (!main.fallbackFontAssetTable.Contains(fallback)) main.fallbackFontAssetTable.Add(fallback);
        EditorUtility.SetDirty(main);
    }

    static TMP_FontAsset Make(string baseName)
    {
        string ttf = Dir + "/" + baseName + ".ttf";
        string otf = Dir + "/" + baseName + ".otf";
        string path = File.Exists(ttf) ? ttf : File.Exists(otf) ? otf : null;
        if (path == null) { Debug.LogWarning("Fonte nao encontrada: " + ttf); return null; }
        string assetPath = Dir + "/" + baseName + " SDF.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;

        var font = AssetDatabase.LoadAssetAtPath<Font>(path);
        var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        if (fa == null) { Debug.LogError("Falha ao criar font asset para " + path); return null; }
        AssetDatabase.CreateAsset(fa, assetPath);
        // Atlas e material precisam ser sub-assets para sobreviver a reimportacoes.
        if (fa.atlasTextures != null)
            foreach (var t in fa.atlasTextures) if (t != null) AssetDatabase.AddObjectToAsset(t, fa);
        if (fa.material != null) AssetDatabase.AddObjectToAsset(fa.material, fa);
        EditorUtility.SetDirty(fa);
        return fa;
    }
}
#endif
