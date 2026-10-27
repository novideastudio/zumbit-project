#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class SampleDataCreator
{
    const string Root = "Assets/_Project/Data";

    [MenuItem("Zombit/Create Sample Data")]
    public static void Create()
    {
        EnsureFolder("Weapons"); EnsureFolder("Enemies"); EnsureFolder("Waves"); EnsureFolder("Items");

        var w = ScriptableObject.CreateInstance<WeaponData>();
        w.displayName = "M4A1 Tatica"; w.damage = 12; w.shotsPerSecond = 4; w.accuracy = 0.8f;
        w.magazineSize = 30; w.reloadTime = 1.5f; w.range = 14.5f;
        Save(w, "Weapons/M4A1.asset");

        var z = ScriptableObject.CreateInstance<EnemyData>();
        z.displayName = "Zumbi Comum"; z.maxHealth = 30; z.moveSpeed = 1.2f; z.damage = 5;
        z.xp = 25; z.gold = 2; z.dropChance = 0.2f;
        Save(z, "Enemies/ZumbiComum.asset");

        for (int i = 1; i <= 3; i++)
        {
            var wave = ScriptableObject.CreateInstance<WaveData>();
            wave.groups = new[] { new WaveData.Group { count = 8 + i * 6, interval = Mathf.Max(0.3f, 0.8f - i * 0.1f) } };
            Save(wave, $"Waves/Wave{i}.asset");
        }

        CreateItem("Capacete Tatico", ItemSlot.Capacete, Rarity.Comum, 0, 10, 2, 15);
        CreateItem("Colete Leve", ItemSlot.Colete, Rarity.Incomum, 0, 30, 3, 40);
        CreateItem("Colete Pesado", ItemSlot.Colete, Rarity.Raro, 0, 60, 6, 120);
        CreateItem("Calca Reforcada", ItemSlot.Calca, Rarity.Comum, 0, 10, 1, 12);
        CreateItem("Botas de Combate", ItemSlot.Botas, Rarity.Incomum, 0, 15, 2, 35);
        CreateItem("Mochila Militar", ItemSlot.Mochila, Rarity.Raro, 0, 25, 1, 90);
        CreateItem("Kit de Mira", ItemSlot.Arma, Rarity.Epico, 8, 0, 0, 250);
        CreateItem("Mira Lendaria", ItemSlot.Arma, Rarity.Lendario, 15, 0, 0, 800);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Zombit: dados de exemplo criados em " + Root);
    }

    static void CreateItem(string name, ItemSlot slot, Rarity r, float dmg, float hp, float def, int sell)
    {
        var it = ScriptableObject.CreateInstance<ItemData>();
        it.displayName = name; it.slot = slot; it.rarity = r;
        it.bonusDamage = dmg; it.bonusHealth = hp; it.bonusDefense = def; it.sellValue = sell;
        it.description = "Item de exemplo.";
        Save(it, "Items/" + name.Replace(" ", "") + ".asset");
    }

    static void Save(Object o, string rel) => AssetDatabase.CreateAsset(o, AssetDatabase.GenerateUniqueAssetPath($"{Root}/{rel}"));

    static void EnsureFolder(string sub)
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Project")) AssetDatabase.CreateFolder("Assets", "_Project");
        if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets/_Project", "Data");
        if (!AssetDatabase.IsValidFolder($"{Root}/{sub}")) AssetDatabase.CreateFolder(Root, sub);
    }
}
#endif
