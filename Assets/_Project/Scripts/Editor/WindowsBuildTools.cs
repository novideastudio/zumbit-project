#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ZombitSurvival.EditorTools
{
    public static class WindowsBuildTools
    {
        private const string MainMenuScene = "Assets/Scenes/MainMenu.unity";
        private const string MissionScene = "Assets/Scenes/SampleScene.unity";
        private const string OutputDirectory = "Builds/Windows";
        private const string OutputPath = OutputDirectory + "/Zombit Survival.exe";

        [MenuItem("Zombit/Build/1 - Prepare Windows Build")]
        public static void PrepareWindowsBuild()
        {
            if (!File.Exists(MainMenuScene) || !File.Exists(MissionScene))
            {
                EditorUtility.DisplayDialog("Zombit Survival", "Não encontrei as cenas esperadas:\n" + MainMenuScene + "\n" + MissionScene + "\n\nConfirme se os nomes e caminhos das cenas estão corretos.", "OK");
                return;
            }

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuScene, true),
                new EditorBuildSettingsScene(MissionScene, true)
            };

            PlayerSettings.productName = "Zombit Survival";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.companyName = string.IsNullOrWhiteSpace(PlayerSettings.companyName) ? "Zombit Survival" : PlayerSettings.companyName;

            string absoluteOutput = Path.GetFullPath(OutputDirectory);
            Directory.CreateDirectory(absoluteOutput);

            EditorUtility.DisplayDialog(
                "Zombit Survival — Windows",
                "Configuração preparada.\n\n" +
                "• MainMenu será a primeira cena.\n" +
                "• SampleScene estará incluída.\n" +
                "• Janela inicial: 1600 × 900, redimensionável.\n" +
                "• Saída prevista: " + OutputPath + "\n\n" +
                "Agora use Zombit > Build > 2 - Build Windows (Release).",
                "OK");
        }

        [MenuItem("Zombit/Build/2 - Build Windows (Release)")]
        public static void BuildWindowsRelease()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            {
                bool switchTarget = EditorUtility.DisplayDialog(
                    "Mudar plataforma para Windows",
                    "A plataforma ativa não é Windows 64-bit. Quer mudar agora? A Unity pode precisar recarregar os assets; depois execute este comando novamente.",
                    "Mudar plataforma", "Cancelar");
                if (switchTarget)
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
                return;
            }

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0 || scenes[0] != MainMenuScene || !scenes.Contains(MissionScene))
            {
                EditorUtility.DisplayDialog("Zombit Survival", "Execute primeiro Zombit > Build > 1 - Prepare Windows Build.", "OK");
                return;
            }

            Directory.CreateDirectory(Path.GetFullPath(OutputDirectory));
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                EditorUtility.RevealInFinder(Path.GetFullPath(OutputPath));
                EditorUtility.DisplayDialog("Build concluída", "Build Windows criada com sucesso.\n\n" + Path.GetFullPath(OutputPath) + "\n\nTeste o jogo fora do Editor antes de distribuir.", "OK");
            }
            else
            {
                Debug.LogError("Zombit Survival: build Windows falhou. Resultado: " + report.summary.result + ". Consulte os erros acima no Console.");
                EditorUtility.DisplayDialog("Build não concluída", "A Unity não conseguiu concluir a build. Abra o Console e envie a primeira mensagem vermelha completa.", "OK");
            }
        }
    }
}
#endif
