#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class ConfigCreator
{
    const string Root = "Assets/_Project/Resources/Config";

    [MenuItem("Zombit/Create Config Assets")]
    public static void Create()
    {
        Folder("Assets/_Project", "Resources");
        Folder("Assets/_Project/Resources", "Config");
        Folder(Root, "Survivors");
        Folder(Root, "Enemies");

        int created = 0;

        string themePath = Root + "/UITheme.asset";
        if (AssetDatabase.LoadAssetAtPath<UITheme>(themePath) == null)
        {
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<UITheme>(), themePath);
            created++;
        }

        string tuningPath = Root + "/GameTuning.asset";
        if (AssetDatabase.LoadAssetAtPath<GameTuning>(tuningPath) == null)
        {
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameTuning>(), tuningPath);
            created++;
        }

        var defs = GameDatabase.DefaultSurvivors();
        for (int i = 0; i < defs.Count; i++)
        {
            string p = $"{Root}/Survivors/{defs[i].id}.asset";
            if (AssetDatabase.LoadAssetAtPath<SurvivorAsset>(p) != null) continue;
            var a = SurvivorAsset.FromDef(defs[i], i);
            a.portrait = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Project/Resources/Art/Characters/{defs[i].id}_portrait.png");
            a.fullBody = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Project/Resources/Art/Characters/{defs[i].id}_full.png");
            AssetDatabase.CreateAsset(a, p);
            created++;
        }

        foreach (var theme in EnemyRoster.Themes)
        {
            Folder(Root + "/Enemies", theme);
            var r = EnemyRoster.BuildDefault(theme);
            for (int i = 0; i < r.commons.Count; i++) created += SaveEnemy(theme, "common", i + 1, r.commons[i].data);
            for (int i = 0; i < r.elites.Count; i++) created += SaveEnemy(theme, "elite", i + 1, r.elites[i].data);
            if (r.boss != null) created += SaveEnemy(theme, "boss", 1, r.boss.data);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Zombit: {created} assets de configuracao criados em {Root}. Edite-os no Inspector (F5 no menu recarrega).");
    }

    static int SaveEnemy(string theme, string tier, int idx, EnemyData src)
    {
        string p = $"{Root}/Enemies/{theme}/{tier}_{idx}.asset";
        if (AssetDatabase.LoadAssetAtPath<EnemyData>(p) != null) return 0;
        var copy = Object.Instantiate(src);
        copy.name = $"{tier}_{idx}";
        AssetDatabase.CreateAsset(copy, p);
        return 1;
    }

    static void Folder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
