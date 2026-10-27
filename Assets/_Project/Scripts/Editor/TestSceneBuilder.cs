#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class TestSceneBuilder
{
    const string SpriteDir = "Assets/_Project/Sprites";
    const string PrefabDir = "Assets/_Project/Prefabs";
    static readonly Vector2 TL = new Vector2(0, 1), TR = new Vector2(1, 1), BL = new Vector2(0, 0), BR = new Vector2(1, 0), C = new Vector2(0.5f, 0.5f);

    [MenuItem("Zombit/Build Test Scene")]
    public static void Build()
    {
        EnsureFolder("Assets", "_Project");
        EnsureFolder("Assets/_Project", "Sprites");
        EnsureFolder("Assets/_Project", "Prefabs");

        if (AssetDatabase.FindAssets("t:WeaponData").Length == 0) SampleDataCreator.Create();

        Sprite sq = MakeSquare();
        var weapon = LoadFirst<WeaponData>();
        var enemyData = LoadFirst<EnemyData>();
        var waves = LoadAll<WaveData>();
        var items = LoadAll<ItemData>();
        System.Array.Sort(waves, (a, b) => string.CompareOrdinal(a.name, b.name));

        foreach (var it in items) { if (!it.icon) { it.icon = sq; EditorUtility.SetDirty(it); } }

        foreach (var n in new[] { "Hero", "Systems", "Spawn_A", "Spawn_B", "HUD Canvas", "EventSystem" })
        {
            var old = GameObject.Find(n);
            if (old) Object.DestroyImmediate(old);
        }

        var bullet = new GameObject("Bullet");
        var bsr = bullet.AddComponent<SpriteRenderer>();
        bsr.sprite = sq; bsr.color = Color.yellow; bsr.sortingOrder = 5;
        bullet.transform.localScale = new Vector3(0.3f, 0.12f, 1f);
        bullet.AddComponent<Projectile>();
        var bulletPrefab = PrefabUtility.SaveAsPrefabAsset(bullet, PrefabDir + "/Bullet.prefab");
        Object.DestroyImmediate(bullet);

        var z = new GameObject("Zombie");
        var zsr = z.AddComponent<SpriteRenderer>();
        zsr.sprite = sq; zsr.color = new Color(0.3f, 0.8f, 0.3f); zsr.sortingOrder = 2;
        z.transform.localScale = Vector3.one * 0.9f;
        z.AddComponent<Health>();
        var en = z.AddComponent<Enemy>();
        en.data = enemyData;
        var zombiePrefab = PrefabUtility.SaveAsPrefabAsset(z, PrefabDir + "/Zombie.prefab");
        Object.DestroyImmediate(z);

        weapon.projectilePrefab = bulletPrefab;
        EditorUtility.SetDirty(weapon);
        foreach (var w in waves)
        {
            foreach (var g in w.groups) g.enemyPrefab = zombiePrefab;
            EditorUtility.SetDirty(w);
        }

        var cam = Camera.main;
        if (cam)
        {
            cam.orthographic = true; cam.orthographicSize = 5f;
            cam.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
            cam.clearFlags = CameraClearFlags.SolidColor;
        }

        var heroGo = new GameObject("Hero");
        heroGo.transform.position = new Vector3(-4, 0, 0);
        var hsr = heroGo.AddComponent<SpriteRenderer>();
        hsr.sprite = sq; hsr.color = new Color(0.3f, 0.5f, 1f); hsr.sortingOrder = 3;
        var hero = heroGo.AddComponent<Hero>();
        hero.weapon = weapon;
        var stats = heroGo.AddComponent<PlayerStats>();
        var heroHp = heroGo.GetComponent<Health>();

        var sa = new GameObject("Spawn_A"); sa.transform.position = new Vector3(8, 2, 0);
        var sb = new GameObject("Spawn_B"); sb.transform.position = new Vector3(8, -2, 0);

        var sys = new GameObject("Systems");
        var inv = sys.AddComponent<Inventory>(); inv.stats = stats;
        var loot = sys.AddComponent<LootDropper>(); loot.pool = items; loot.inventory = inv;
        var ws = sys.AddComponent<WaveSpawner>();
        ws.waves = waves; ws.hero = heroGo.transform; ws.spawnPoints = new[] { sa.transform, sb.transform };
        var mc = sys.AddComponent<MissionController>(); mc.heroHealth = heroHp;
        var si = sys.AddComponent<StartingItems>();
        si.inventory = inv;
        si.items = items.Length > 4 ? new[] { items[0], items[1], items[2], items[3] } : items;

        var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
#if ENABLE_INPUT_SYSTEM
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif

        var cgo = new GameObject("HUD Canvas", typeof(RectTransform));
        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();
        var ct = cgo.transform;

        var hud = cgo.AddComponent<HudController>();
        hud.heroHealth = heroHp; hud.hero = hero; hud.stats = stats; hud.loot = loot;

        hud.levelText = Txt("Level", ct, "LV. 1", 30, TL, new Vector2(20, -15), new Vector2(300, 36), TextAlignmentOptions.Left);
        Img("HpBg", ct, sq, new Color(0.15f, 0.05f, 0.05f), TL, new Vector2(20, -58), new Vector2(320, 26));
        hud.hpFill = Fill("HpFill", ct, sq, new Color(0.85f, 0.15f, 0.15f), TL, new Vector2(20, -58), new Vector2(320, 26));
        hud.hpText = Txt("HpText", ct, "100/100", 18, TL, new Vector2(20, -58), new Vector2(320, 26), TextAlignmentOptions.Center);
        Img("XpBg", ct, sq, new Color(0.1f, 0.1f, 0.1f), TL, new Vector2(20, -90), new Vector2(320, 10));
        hud.xpFill = Fill("XpFill", ct, sq, new Color(0.95f, 0.8f, 0.2f), TL, new Vector2(20, -90), new Vector2(320, 10));

        hud.waveText = Txt("Wave", ct, "WAVE 1/3", 34, TR, new Vector2(-20, -15), new Vector2(300, 40), TextAlignmentOptions.Right);
        Txt("EnemiesLbl", ct, "ZUMBIS:", 22, TR, new Vector2(-80, -60), new Vector2(200, 30), TextAlignmentOptions.Right);
        hud.enemiesText = Txt("Enemies", ct, "0", 22, TR, new Vector2(-20, -60), new Vector2(60, 30), TextAlignmentOptions.Right);

        hud.ammoText = Txt("Ammo", ct, "30/30", 32, BL, new Vector2(20, 20), new Vector2(400, 40), TextAlignmentOptions.Left);
        hud.goldText = Txt("Gold", ct, "0", 28, BR, new Vector2(-20, 60), new Vector2(300, 34), TextAlignmentOptions.Right);
        hud.killsText = Txt("Kills", ct, "0", 24, BR, new Vector2(-20, 20), new Vector2(300, 30), TextAlignmentOptions.Right);

        hud.levelUpBanner = Txt("LevelUp", ct, "LEVEL UP!", 64, C, new Vector2(0, 200), new Vector2(600, 90), TextAlignmentOptions.Center).gameObject;
        hud.gameOverPanel = Txt("GameOver", ct, "VOCE MORREU", 90, C, Vector2.zero, new Vector2(900, 140), TextAlignmentOptions.Center).gameObject;
        hud.victoryPanel = Txt("Victory", ct, "MISSAO CONCLUIDA", 90, C, Vector2.zero, new Vector2(1100, 140), TextAlignmentOptions.Center).gameObject;

        var slotGo = MakeSlot("Slot", null, sq);
        var slotPrefabGo = PrefabUtility.SaveAsPrefabAsset(slotGo.gameObject, PrefabDir + "/Slot.prefab");
        Object.DestroyImmediate(slotGo.gameObject);
        var slotPrefab = slotPrefabGo.GetComponent<SlotUI>();

        var panel = Img("InventoryPanel", ct, sq, new Color(0.08f, 0.08f, 0.1f, 0.96f), C, Vector2.zero, new Vector2(1000, 600));
        Txt("Title", panel.transform, "INVENTARIO", 26, TR, new Vector2(-280, -12), new Vector2(400, 34), TextAlignmentOptions.Right);

        var inventoryUI = panel.gameObject.AddComponent<InventoryUI>();
        inventoryUI.inventory = inv; inventoryUI.stats = stats; inventoryUI.slotPrefab = slotPrefab;

        ItemSlot[] types = { ItemSlot.Arma, ItemSlot.Capacete, ItemSlot.Colete, ItemSlot.Calca, ItemSlot.Botas, ItemSlot.Mochila };
        var eq = new SlotUI[types.Length];
        for (int i = 0; i < types.Length; i++)
        {
            var s = MakeSlot("Equip_" + types[i], panel.transform, sq);
            var rt = (RectTransform)s.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = TL;
            rt.anchoredPosition = new Vector2(20 + i * 74, -20);
            s.EquipType = types[i];
            eq[i] = s;
            Txt("Lbl_" + types[i], panel.transform, types[i].ToString(), 12, TL, new Vector2(20 + i * 74, -86), new Vector2(64, 16), TextAlignmentOptions.Center);
        }
        inventoryUI.equipSlots = eq;

        var grid = R("Grid", panel.transform, TL, new Vector2(20, -112), new Vector2(694, 420));
        var glg = grid.gameObject.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(64, 64); glg.spacing = new Vector2(6, 6);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount; glg.constraintCount = 10;
        glg.startCorner = GridLayoutGroup.Corner.UpperLeft;
        inventoryUI.gridParent = grid;

        inventoryUI.goldText = Txt("InvGold", panel.transform, "0", 22, BL, new Vector2(20, 14), new Vector2(200, 30), TextAlignmentOptions.Left);
        inventoryUI.capacityText = Txt("InvCap", panel.transform, "0/60", 22, BL, new Vector2(240, 14), new Vector2(200, 30), TextAlignmentOptions.Left);

        var dp = Img("Detail", panel.transform, sq, new Color(0.14f, 0.14f, 0.18f), TR, new Vector2(-20, -60), new Vector2(240, 480));
        var detail = dp.gameObject.AddComponent<ItemDetailPanel>();
        detail.inventory = inv;
        var dIcon = Img("Icon", dp.transform, sq, Color.white, TL, new Vector2(20, -20), new Vector2(64, 64));
        detail.icon = dIcon;
        detail.nameText = Txt("Name", dp.transform, "Item", 18, TL, new Vector2(94, -20), new Vector2(140, 30), TextAlignmentOptions.Left);
        detail.rarityText = Txt("Rarity", dp.transform, "Raridade", 13, TL, new Vector2(94, -52), new Vector2(140, 24), TextAlignmentOptions.Left);
        detail.statsText = Txt("Stats", dp.transform, "", 15, TL, new Vector2(20, -100), new Vector2(200, 80), TextAlignmentOptions.TopLeft);
        detail.descriptionText = Txt("Desc", dp.transform, "", 13, TL, new Vector2(20, -190), new Vector2(200, 120), TextAlignmentOptions.TopLeft);
        var eb = Btn("EquipBtn", dp.transform, sq, new Color(0.9f, 0.7f, 0.1f), "Equipar", TL, new Vector2(20, -340), new Vector2(200, 42), out var ebl);
        ebl.color = Color.black;
        detail.equipButton = eb; detail.equipButtonLabel = ebl;
        var sbn = Btn("SellBtn", dp.transform, sq, new Color(0.7f, 0.15f, 0.15f), "Vender", TL, new Vector2(20, -392), new Vector2(200, 42), out var sbl);
        detail.sellButton = sbn; detail.sellLabel = sbl;
        inventoryUI.detail = detail;

        var toggle = cgo.AddComponent<PanelToggle>();
        toggle.panel = panel.gameObject; toggle.startHidden = true;

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Zombit: cena de teste criada. Salve (Ctrl+S) e de Play. Tecla I abre o inventario.");
    }

    static RectTransform R(string n, Transform p, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(n, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(p, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    static Image Img(string n, Transform p, Sprite s, Color c, Vector2 a, Vector2 pos, Vector2 size)
    {
        var rt = R(n, p, a, pos, size);
        var im = rt.gameObject.AddComponent<Image>();
        im.sprite = s; im.color = c;
        return im;
    }

    static Image Fill(string n, Transform p, Sprite s, Color c, Vector2 a, Vector2 pos, Vector2 size)
    {
        var im = Img(n, p, s, c, a, pos, size);
        im.type = Image.Type.Filled;
        im.fillMethod = Image.FillMethod.Horizontal;
        im.fillOrigin = (int)Image.OriginHorizontal.Left;
        im.fillAmount = 1f;
        im.raycastTarget = false;
        return im;
    }

    static TMP_Text Txt(string n, Transform p, string t, float size, Vector2 a, Vector2 pos, Vector2 sz, TextAlignmentOptions al)
    {
        var rt = R(n, p, a, pos, sz);
        var tx = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tx.text = t; tx.fontSize = size; tx.color = Color.white; tx.alignment = al;
        tx.raycastTarget = false;
        return tx;
    }

    static Button Btn(string n, Transform p, Sprite s, Color c, string label, Vector2 a, Vector2 pos, Vector2 sz, out TMP_Text lbl)
    {
        var im = Img(n, p, s, c, a, pos, sz);
        var b = im.gameObject.AddComponent<Button>();
        b.targetGraphic = im;
        lbl = Txt("Label", im.transform, label, 20, C, Vector2.zero, sz, TextAlignmentOptions.Center);
        return b;
    }

    static SlotUI MakeSlot(string n, Transform p, Sprite sq)
    {
        var border = Img(n, p, sq, new Color(0.25f, 0.25f, 0.25f), C, Vector2.zero, new Vector2(64, 64));
        var iconRt = R("Icon", border.transform, C, Vector2.zero, Vector2.zero);
        iconRt.anchorMin = Vector2.zero; iconRt.anchorMax = Vector2.one;
        iconRt.offsetMin = new Vector2(10, 10); iconRt.offsetMax = new Vector2(-10, -10);
        var icon = iconRt.gameObject.AddComponent<Image>();
        icon.raycastTarget = false;
        var s = border.gameObject.AddComponent<SlotUI>();
        s.border = border; s.icon = icon;
        icon.enabled = false;
        return s;
    }

    static Sprite MakeSquare()
    {
        string path = SpriteDir + "/Square.png";
        if (!File.Exists(path))
        {
            var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            var px = new Color[256];
            for (int i = 0; i < 256; i++) px[i] = Color.white;
            tex.SetPixels(px); tex.Apply();
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), path), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spritePixelsPerUnit = 16;
            ti.filterMode = FilterMode.Point;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static T LoadFirst<T>() where T : Object
    {
        var all = LoadAll<T>();
        return all.Length > 0 ? all[0] : null;
    }

    static T[] LoadAll<T>() where T : Object
    {
        var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
        var arr = new T[guids.Length];
        for (int i = 0; i < guids.Length; i++)
            arr[i] = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));
        return arr;
    }

    static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
