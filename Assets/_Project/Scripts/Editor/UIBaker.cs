#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Converte a UI montada por codigo em PREFABS editaveis, com o visual identico.
/// Como funciona: roda os mesmos builders do jogo em modo Editor, troca os sprites gerados por
/// codigo por arquivos .png reais (com 9-slice) e salva cada tela como prefab em Resources/UI/Screens.
/// </summary>
public static class UIBaker
{
    const string SpritesDir = "Assets/_Project/Art/UI/Generated";
    const string ScreensDir = "Assets/_Project/Resources/UI/Screens";
    const string ThemePath = "Assets/_Project/Resources/Config/UITheme.asset";
    const string ToggleMenu = "ZomBit/UI/Usar UI do Editor (alternar)";

    [MenuItem("ZomBit/UI/1 - Gerar prefabs das telas")]
    public static void BakeScreens() => BakeInternal(false);

    // Seguro: so cria os prefabs que ainda NAO existem. Nunca sobrescreve o que voce ja editou.
    [MenuItem("ZomBit/UI/1b - Gerar so os prefabs que faltam (nao sobrescreve)")]
    public static void BakeMissing() => BakeInternal(true);

    static void BakeInternal(bool onlyMissing)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("ZomBit UI", "Saia do modo Play antes de gerar os prefabs.", "OK");
            return;
        }

        if (!onlyMissing && Directory.Exists(ScreensDir) && Directory.GetFiles(ScreensDir, "*.prefab").Length > 0 &&
            !EditorUtility.DisplayDialog("ZomBit UI",
                "Ja existem prefabs em\n" + ScreensDir + "\n\nGerar de novo SOBRESCREVE as suas edicoes nesses prefabs.\nContinuar?",
                "Sobrescrever", "Cancelar"))
            return;

        Directory.CreateDirectory(SpritesDir);
        Directory.CreateDirectory(ScreensDir);

        var temps = new List<GameObject>();
        int saved = 0;
        try
        {
            var screens = new List<KeyValuePair<string, GameObject>>();
            screens.AddRange(MainMenuController.EditorUiBake(out var menuHost));
            temps.Add(menuHost);
            var pause = MissionPauseMenu.EditorUiBake(out var pauseHost);
            temps.Add(pauseHost);
            foreach (var kv in pause) temps.Add(kv.Value);
            screens.AddRange(pause);

            // Modelos (Card_Survivor, Row_slot) primeiro: as telas recebem instancias deles como previa.
            screens.Sort((a, c) => TemplateOrder(a.Key).CompareTo(TemplateOrder(c.Key)));
            var saved_assets = new Dictionary<string, GameObject>();

            int skipped = 0;
            foreach (var kv in screens)
            {
                var go = kv.Value;
                string existingPath = IsPrefabOnlyKey(kv.Key) ? ScreensDir + "/" + kv.Key + ".prefab" : ScreensDir + "/Screen_" + kv.Key + ".prefab";
                if (onlyMissing && File.Exists(existingPath))
                {
                    // Ja existe (pode ter edicoes suas): mantem como esta, mas o modelo vale para as previas das outras telas.
                    saved_assets[kv.Key] = AssetDatabase.LoadAssetAtPath<GameObject>(existingPath);
                    skipped++;
                    continue;
                }
                go.SetActive(true);              // o prefab fica ativo; o jogo desativa ao instanciar
                RemapTransientSprites(go);
                // Telas completas precisam de Canvas/CanvasScaler para ter preview em escala real
                // no Prefab Mode. Pecas reutilizaveis (cards, slots, nodes, overlays) ficam sem Canvas.
                if (IsFullScreenScreenKey(kv.Key)) ConfigureLayoutPreviewCanvas(go);
                WarnTransientReferences(go, kv.Key);

                if (kv.Key == "select" && saved_assets.TryGetValue("Card_Survivor", out var cardAsset))
                    AddPreviewInstances(go, "Cards", cardAsset, GameDatabase.Survivors.Count);
                if (kv.Key == "slots" && saved_assets.TryGetValue("Row_slot", out var rowAsset))
                    AddPreviewInstances(go, "Rows", rowAsset, SaveSystem.SlotCount);

                string path = IsPrefabOnlyKey(kv.Key) ? ScreensDir + "/" + kv.Key + ".prefab" : ScreensDir + "/Screen_" + kv.Key + ".prefab";
                var savedAsset = PrefabUtility.SaveAsPrefabAsset(go, path, out bool ok);
                if (ok) saved_assets[kv.Key] = savedAsset;
                if (ok) { saved++; Debug.Log("[ZomBit UI] Prefab gerado: " + path); }
                else Debug.LogError("[ZomBit UI] Falha ao salvar " + path);
            }
        }
        finally
        {
            foreach (var t in temps) if (t != null) Object.DestroyImmediate(t);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        EditorUtility.DisplayDialog("ZomBit UI",
            saved + " prefab(s) gerado(s) em\n" + ScreensDir +
            (onlyMissing ? "\n\nOs que ja existiam foram mantidos (suas edicoes estao seguras)." : "") +
            "\n\nAgora use: ZomBit > UI > Usar UI do Editor (alternar) e de Play.", "OK");
    }

    static bool IsPrefabOnlyKey(string key) => key.StartsWith("Card_") || key.StartsWith("Row_") || key.StartsWith("Overlay_") ||
                                                    key.StartsWith("Node_") || key.StartsWith("Tile_") || key.StartsWith("Cell_");
    static int TemplateOrder(string key) => (key == "Card_Survivor" || key == "Row_slot") ? 0 : 1;

    // Instancias aninhadas do modelo dentro do container da tela (so para voce VER o layout no Editor).
    static void AddPreviewInstances(GameObject screen, string containerName, GameObject templateAsset, int count)
    {
        var container = EditorUiBinding.FindDeep(screen.transform, containerName);
        if (container == null || templateAsset == null) return;
        for (int i = 0; i < count; i++)
            PrefabUtility.InstantiatePrefab(templateAsset, container);
    }

    [MenuItem("ZomBit/UI/3 - Gerar layouts editaveis (Mapa/Inventario/Habilidades/Loja)")]
    static void BakeLayoutOverridesMissing() => BakeLayoutOverridesInternal(true);

    [MenuItem("ZomBit/UI/3b - Regerar layouts editaveis (sobrescrever)")]
    static void BakeLayoutOverridesReplace() => BakeLayoutOverridesInternal(false);

    // Corrige os previews sem reconstruir a hierarquia: layouts editaveis + telas completas.
    // O comando e seguro para executar mesmo se alguns assets ainda nao tiverem sido gerados.
    [MenuItem("ZomBit/UI/4 - Corrigir preview dos layouts existentes (preservar edicoes)")]
    static void RepairExistingLayoutPreviews()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("ZomBit UI", "Saia do modo Play antes de corrigir os prefabs.", "OK");
            return;
        }

        string[] layoutKeys = { "map", "mapInventory", "mapSkills", "shop" };
        string[] fullScreenPrefabs = FullScreenPrefabNames;
        int repairedLayouts = 0, repairedScreens = 0, missingLayouts = 0, missingScreens = 0, failed = 0;

        foreach (string key in layoutKeys)
        {
            string path = ScreensDir + "/Layout_" + key + ".prefab";
            if (!File.Exists(path)) { missingLayouts++; continue; }
            if (RepairPrefabPreviewCanvas(path)) repairedLayouts++; else failed++;
        }

        foreach (string prefabName in fullScreenPrefabs)
        {
            string path = ScreensDir + "/" + prefabName + ".prefab";
            if (!File.Exists(path)) { missingScreens++; continue; }
            if (RepairPrefabPreviewCanvas(path)) repairedScreens++; else failed++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ZomBit UI] Preview audit/reparo concluido. Layouts corrigidos: " + repairedLayouts +
                  "; telas completas corrigidas: " + repairedScreens + "; layouts nao gerados: " + missingLayouts +
                  "; telas ausentes: " + missingScreens + "; falhas: " + failed +
                  ". Pecas reutilizaveis foram intencionalmente ignoradas.");
        EditorUtility.DisplayDialog("ZomBit UI",
            "Correcao de preview concluida, sem reconstruir os elementos nem apagar edicoes.\n\n" +
            "Layouts corrigidos: " + repairedLayouts + "\n" +
            "Telas completas corrigidas: " + repairedScreens + "\n" +
            "Layouts ainda nao gerados: " + missingLayouts + "\n" +
            "Telas completas ausentes: " + missingScreens + "\n" +
            "Falhas: " + failed + "\n\n" +
            "Inclui Screen_main, Screen_select, login, nickname, options, credits e slots.\n" +
            "Pecas reutilizaveis como Card_Survivor, Node_Location e Row_slot nao recebem Canvas proprio.", "OK");
    }

    static readonly string[] FullScreenPrefabNames =
    {
        "Screen_main", "Screen_select", "Screen_login", "Screen_nickname",
        "Screen_options", "Screen_credits", "Screen_slots"
    };

    static bool IsFullScreenScreenKey(string key)
    {
        switch (key)
        {
            case "main": case "select": case "login": case "nickname":
            case "options": case "credits": case "slots": return true;
            default: return false;
        }
    }

    static bool RepairPrefabPreviewCanvas(string path)
    {
        GameObject contents = null;
        try
        {
            contents = PrefabUtility.LoadPrefabContents(path);
            if (contents == null)
            {
                Debug.LogError("[ZomBit UI] Nao foi possivel abrir para reparo: " + path);
                return false;
            }
            ConfigureLayoutPreviewCanvas(contents);
            PrefabUtility.SaveAsPrefabAsset(contents, path, out bool ok);
            if (!ok) Debug.LogError("[ZomBit UI] Nao foi possivel salvar o preview: " + path);
            else Debug.Log("[ZomBit UI] Preview corrigido sem reconstruir filhos: " + path);
            return ok;
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            return false;
        }
        finally
        {
            if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    // Auditoria somente de leitura: distingue telas completas de elementos reutilizaveis.
    [MenuItem("ZomBit/UI/5 - Auditar prefabs de UI (sem alterar)")]
    static void AuditUiPrefabs()
    {
        int fullScreenOk = 0, fullScreenNeedsFix = 0, fullScreenMissing = 0;
        var report = new List<string>();
        foreach (string prefabName in FullScreenPrefabNames)
        {
            string path = ScreensDir + "/" + prefabName + ".prefab";
            if (!File.Exists(path)) { fullScreenMissing++; report.Add("AUSENTE: " + prefabName); continue; }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            bool ok = prefab != null && prefab.GetComponent<Canvas>() != null && prefab.GetComponent<CanvasScaler>() != null && prefab.GetComponent<GraphicRaycaster>() != null;
            if (ok) { fullScreenOk++; report.Add("OK: " + prefabName); }
            else { fullScreenNeedsFix++; report.Add("PRECISA REPARO: " + prefabName); }
        }

        int layoutOk = 0, layoutNeedsFix = 0, layoutNotGenerated = 0;
        foreach (string key in new[] { "map", "mapInventory", "mapSkills", "shop" })
        {
            string path = ScreensDir + "/Layout_" + key + ".prefab";
            if (!File.Exists(path)) { layoutNotGenerated++; report.Add("LAYOUT AINDA NAO GERADO: Layout_" + key); continue; }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            bool ok = prefab != null && prefab.GetComponent<Canvas>() != null && prefab.GetComponent<CanvasScaler>() != null && prefab.GetComponent<GraphicRaycaster>() != null;
            if (ok) { layoutOk++; report.Add("OK: Layout_" + key); }
            else { layoutNeedsFix++; report.Add("LAYOUT PRECISA REPARO: Layout_" + key); }
        }

        string fullReport = string.Join("\n", report);
        Debug.Log("[ZomBit UI] Auditoria de UI\n" + fullReport);
        EditorUtility.DisplayDialog("Auditoria de prefabs UI",
            "Telas completas: " + fullScreenOk + " OK, " + fullScreenNeedsFix + " precisam reparo, " + fullScreenMissing + " ausentes.\n" +
            "Layouts editaveis: " + layoutOk + " OK, " + layoutNeedsFix + " precisam reparo, " + layoutNotGenerated + " ainda nao gerados.\n\n" +
            "Detalhes completos foram enviados ao Console.\n\nElementos reutilizaveis nao precisam de Canvas/CanvasScaler proprio.", "OK");
    }

    static void ConfigureLayoutPreviewCanvas(GameObject root)
    {
        if (root == null) return;

        // No Prefab Mode, o Canvas root fornece uma referencia de escala para o layout.
        // Em runtime as telas completas sao instanciadas sob o Canvas principal; layouts continuam
        // a ter Canvas/CanvasScaler de preview (o aplicador de layout nao os copia para a tela real).
        var canvas = root.GetComponent<Canvas>();
        if (canvas == null) canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        var scaler = root.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (root.GetComponent<GraphicRaycaster>() == null)
            root.AddComponent<GraphicRaycaster>();
    }

    static void BakeLayoutOverridesInternal(bool onlyMissing)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("ZomBit UI", "Saia do modo Play antes de gerar os layouts.", "OK");
            return;
        }

        if (!onlyMissing && Directory.Exists(ScreensDir) &&
            new[] { "map", "mapInventory", "mapSkills", "shop" }.Any(key => File.Exists(ScreensDir + "/Layout_" + key + ".prefab")) &&
            !EditorUtility.DisplayDialog("ZomBit UI",
                "Ja existem layouts editaveis em\n" + ScreensDir + "\n\nRegerar vai SOBRESCREVER as edicoes feitas nesses quatro layouts.\nContinuar?",
                "Sobrescrever", "Cancelar"))
            return;

        Directory.CreateDirectory(ScreensDir);
        GameObject host = null;
        int saved = 0, skipped = 0;
        try
        {
            var layouts = MainMenuController.EditorUiBakeLayoutOverrides(out host);
            foreach (var pair in layouts)
            {
                string path = ScreensDir + "/Layout_" + pair.Key + ".prefab";
                if (onlyMissing && File.Exists(path)) { skipped++; continue; }

                GameObject go = pair.Value;
                if (go == null) continue;
                go.SetActive(true);
                RemapTransientSprites(go);
                ConfigureLayoutPreviewCanvas(go);
                ConfigureLayoutOverrideFlags(go.transform, pair.Key);
                WarnTransientReferences(go, "Layout_" + pair.Key);
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, path, out bool ok);
                if (ok)
                {
                    saved++;
                    Debug.Log("[ZomBit UI] Layout editavel gerado: " + path);
                }
                else Debug.LogError("[ZomBit UI] Falha ao salvar layout: " + path);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("ZomBit UI", "Falha ao gerar os layouts. Veja o Console para detalhes.\n\n" + e.Message, "OK");
            return;
        }
        finally
        {
            if (host != null) Object.DestroyImmediate(host);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        EditorUtility.DisplayDialog("ZomBit UI",
            saved + " layout(s) editavel(is) gerado(s)." + (onlyMissing ? "\n" + skipped + " layout(s) existente(s) preservado(s)." : "") +
            "\n\nPasta: " + ScreensDir + "\n\nEdite Layout_map, Layout_mapInventory, Layout_mapSkills e Layout_shop no Inspector/Prefab Mode.\nAs edicoes de layout sao aplicadas ao abrir cada tela durante o jogo.", "OK");
    }

    static void ConfigureLayoutOverrideFlags(Transform node, string key)
    {
        string path = BuildRelativePath(node);
        var flags = node.GetComponent<UIPrefabLayoutOverride>();
        if (flags == null) flags = node.gameObject.AddComponent<UIPrefabLayoutOverride>();

        bool screenRoot = node.parent != null && node.parent.name == "__ZombitLayoutBake";
        bool dataPositioned = IsDataPositionedPath(path);
        // The prefab is a visual template, not the authority for the full-screen root rectangle.
        flags.overrideAnchorsAndPosition = !screenRoot && !dataPositioned;
        flags.overrideSize = !screenRoot;
        flags.overridePivot = !screenRoot && !dataPositioned;
        flags.overrideScaleAndRotation = !screenRoot;

        var text = node.GetComponent<TMP_Text>();
        flags.overrideTextContent = text != null;
        flags.overrideTextStyle = true;
        flags.overrideTextColor = text != null;
        if (text != null)
        {
            // Baselines let runtime-edited values (nickname, item stats, levels, prices, etc.) survive,
            // while a user can still edit fixed strings and colors directly on the prefab.
            flags.bakedTextContent = text.text;
            flags.bakedTextColor = text.color;
        }

        var image = node.GetComponent<Image>();
        flags.overrideImageSprite = image != null;
        flags.overrideImageColor = image != null;
        if (image != null)
        {
            flags.bakedImageSprite = image.sprite;
            flags.bakedImageColor = image.color;
        }

        var button = node.GetComponent<Button>();
        flags.overrideButtonColors = button != null;
        if (button != null) flags.bakedButtonColors = button.colors;
        flags.overrideButtonTransition = button != null;
        flags.overrideLayoutComponents = true;
        EditorUtility.SetDirty(flags);

        for (int i = 0; i < node.childCount; i++) ConfigureLayoutOverrideFlags(node.GetChild(i), key);
    }

    static string BuildRelativePath(Transform node)
    {
        var parts = new List<string>();
        Transform current = node;
        while (current != null)
        {
            parts.Add(current.name);
            current = current.parent;
        }
        parts.Reverse();
        return string.Join("/", parts);
    }

    static bool PathContains(string path, string token) => path.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0;

    static bool IsDataPositionedPath(string path)
    {
        // Map paths and marker positions are driven by LocationDef data, not the prefab layout.
        // Their size can still be edited via the override flag; anchors/positions remain data-owned.
        return PathContains(path, "MapMarkerLayer/");
    }

    static bool IsRuntimeBoundTextPath(string path)
    {
        string[] tokens = {
            "MapNicknameValue", "MapNicknameClass", "MapXpBar", "MapXpFill", "RegionTitle", "RegionCount", "RegionBar",
            "/DName", "/DDesc", "/DInfo", "DifficultyInfo", "InsuranceToggle", "PointsValue", "OfferPrice",
            "SlotInfo", "LockLevel", "/Num", "LocationName", "NicknameHelp", "MapItemDetailsPanel", 
            "SurvivorSwitcher", "SkillBranch_", "/Skill_", "SelectedSkillPanel", "StatValue", "LevelChip", "PlayerIdentity"
        };
        foreach (var token in tokens) if (PathContains(path, token)) return true;
        if (PathContains(path, "MapMarkerLayer/")) return true;
        return false;
    }

    static bool IsRuntimeBoundImagePath(string path)
    {
        string[] tokens = {
            "MapMarkerLayer/", "SurvivorSwitcher/", "InventoryCategoryTabs/", "DifficultyRow/", "SelectedLocationPreview",
            "MapNicknameHitArea", "MapXpFill", "PointsDot", "SkillBranch_", "/Skill_", "SelectedSkillPanel",
            "MapItemDetailsPanel", "LevelChip", "StatFill", "HealthFill", "XpFill", "AbilityFill", "BuyOfferButton"
        };
        foreach (var token in tokens) if (PathContains(path, token)) return true;
        string leaf = path.Substring(path.LastIndexOf('/') + 1);
        return leaf == "Icon" || leaf == "Art" || leaf == "Portrait" || leaf == "Fill" || leaf == "SelectedPin";
    }

    static bool IsRuntimeBoundButtonPath(string path)
    {
        string[] tokens = {
            "MapMarkerLayer/Node_", "Difficulty_", "SurvivorSwitcher/", "NavTab_", "InventoryCategoryTabs/",
            "Item_", "EquipSlot_", "BuyOfferButton", "SkillBranch_", "/Skill_"
        };
        foreach (var token in tokens) if (PathContains(path, token)) return true;
        return false;
    }

    static bool IsGeneratedBaseSprite(Sprite sprite)
    {
        if (sprite == null) return true;
        return sprite.name == "UI_White" || sprite.name == "UI_Rounded9Slice" ||
               sprite.name == "UI_Circle" || sprite.name == "UI_Star" || sprite.name.StartsWith("UI_Generated_");
    }

    [MenuItem("ZomBit/UI/2 - Mostrar pasta dos prefabs")]
    static void ShowFolder()
    {
        var obj = AssetDatabase.LoadAssetAtPath<Object>(ScreensDir);
        if (obj != null) { Selection.activeObject = obj; EditorGUIUtility.PingObject(obj); }
        else EditorUtility.DisplayDialog("ZomBit UI", "Os prefabs ainda nao foram gerados.", "OK");
    }

    [MenuItem(ToggleMenu)]
    static void ToggleEditorUI()
    {
        var theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
        if (theme == null) { EditorUtility.DisplayDialog("ZomBit UI", "UITheme.asset nao encontrado em\n" + ThemePath, "OK"); return; }
        Undo.RecordObject(theme, "Alternar UI do Editor");
        theme.useEditorUI = !theme.useEditorUI;
        EditorUtility.SetDirty(theme);
        AssetDatabase.SaveAssets();
        UITheme.Reload();
        Debug.Log("[ZomBit UI] useEditorUI = " + theme.useEditorUI + (theme.useEditorUI ? "  (prefabs)" : "  (codigo antigo)"));
    }

    [MenuItem(ToggleMenu, true)]
    static bool ToggleEditorUIValidate()
    {
        var theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
        Menu.SetChecked(ToggleMenu, theme != null && theme.useEditorUI);
        return true;
    }

    // ---------- sprites gerados por codigo -> arquivos .png ----------
    static void RemapTransientSprites(GameObject go)
    {
        var cache = new Dictionary<Sprite, Sprite>();
        int generic = 0;
        foreach (var img in go.GetComponentsInChildren<Image>(true))
        {
            var sp = img.sprite;
            if (sp == null || EditorUtility.IsPersistent(sp)) continue;
            if (!cache.TryGetValue(sp, out var saved))
            {
                string name;
                if (sp == UIKit.Chamfer) name = "UI_Rounded9Slice";
                else if (sp == UIKit.Circle) name = "UI_Circle";
                else if (sp == UIKit.Star) name = "UI_Star";
                else if (sp == UIFactory.White) name = "UI_White";
                else name = "UI_Generated_" + (++generic);
                saved = SaveSprite(sp, name);
                cache[sp] = saved;
            }
            if (saved != null) img.sprite = saved;
        }
    }

    static Sprite SaveSprite(Sprite sp, string name)
    {
        string path = SpritesDir + "/" + name + ".png";
        if (!File.Exists(path))   // nao sobrescreve: se voce trocar a arte, ela e mantida
        {
            var tex = sp.texture;
            if (Mathf.RoundToInt(sp.rect.width) != tex.width || Mathf.RoundToInt(sp.rect.height) != tex.height)
            {
                Debug.LogWarning("[ZomBit UI] Sprite '" + name + "' usa so parte da textura; nao foi convertido.");
                return null;
            }
            File.WriteAllBytes(path, EncodePng(tex));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = sp.pixelsPerUnit;
            ti.spritePivot = new Vector2(sp.pivot.x / sp.rect.width, sp.pivot.y / sp.rect.height);
            ti.spriteBorder = sp.border;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.filterMode = tex.filterMode;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            ti.SetTextureSettings(settings);
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // Texturas geradas por codigo sao "nao legiveis": copia via RenderTexture para poder gravar o PNG.
    static byte[] EncodePng(Texture src)
    {
        int w = src.width, h = src.height;
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = RenderTexture.active;
        Graphics.Blit(src, rt);
        RenderTexture.active = rt;
        var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        copy.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        var bytes = copy.EncodeToPNG();
        Object.DestroyImmediate(copy);
        return bytes;
    }

    // Rede de seguranca: avisa se sobrou algum asset temporario (nao salvo em disco) referenciado no prefab.
    static void WarnTransientReferences(GameObject go, string key)
    {
        foreach (var c in go.GetComponentsInChildren<Component>(true))
        {
            if (c == null) continue;
            var so = new SerializedObject(c);
            var it = so.GetIterator();
            while (it.NextVisible(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                var o = it.objectReferenceValue;
                if (o == null || o is Component || o is GameObject || EditorUtility.IsPersistent(o)) continue;
                Debug.LogWarning("[ZomBit UI] Tela '" + key + "': '" + c.name + "' (" + c.GetType().Name + "." + it.propertyPath +
                                 ") referencia um asset temporario (" + o.GetType().Name + "). Avise para eu converter.");
            }
        }
    }
}
#endif
