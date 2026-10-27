#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GameFlowSetup
{
    [MenuItem("Zombit/Setup Menu and Flow")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string gamePath = EditorSceneManager.GetActiveScene().path;
        if (string.IsNullOrEmpty(gamePath) || gamePath.EndsWith("MainMenu.unity"))
        {
            var g = AssetDatabase.FindAssets("SampleScene t:Scene");
            if (g.Length == 0)
            {
                EditorUtility.DisplayDialog("Zombit", "Abra a cena do jogo (SampleScene) e rode de novo.", "OK");
                return;
            }
            gamePath = AssetDatabase.GUIDToAssetPath(g[0]);
        }

        var gameScene = EditorSceneManager.OpenScene(gamePath, OpenSceneMode.Single);
        var stats = Object.FindFirstObjectByType<PlayerStats>();
        if (stats == null)
        {
            EditorUtility.DisplayDialog("Zombit", "A cena do jogo ainda não tem o herói. Rode antes: Zombit > Build Test Scene.", "OK");
            return;
        }

        var sys = GameObject.Find("Systems");
        if (sys == null) sys = new GameObject("Systems");
        if (sys.GetComponent<MissionSetup>() == null) sys.AddComponent<MissionSetup>();
        EditorSceneManager.MarkSceneDirty(gameScene);
        EditorSceneManager.SaveScene(gameScene);

        var menu = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.07f, 0.07f, 0.09f);
        camGo.AddComponent<AudioListener>();
        new GameObject("MainMenu").AddComponent<MainMenuController>();

        if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
        string menuPath = "Assets/Scenes/MainMenu.unity";
        EditorSceneManager.SaveScene(menu, menuPath);

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(menuPath, true),
            new EditorBuildSettingsScene(gamePath, true)
        };

        Debug.Log("Zombit: MainMenu criada e Build Settings configurado (0 = MainMenu, 1 = " + gamePath + "). Abra MainMenu e de Play.");
    }
}
#endif
