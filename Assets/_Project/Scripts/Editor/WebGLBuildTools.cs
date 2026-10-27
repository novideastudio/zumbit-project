#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ZombitSurvival.EditorTools
{
    public static class WebGLBuildTools
    {
        private const string MainMenuScene = "Assets/Scenes/MainMenu.unity";
        private const string MissionScene = "Assets/Scenes/SampleScene.unity";
        private const string OutputDirectory = "Builds/WebGL";

        [MenuItem("Zombit/Build/3 - Build WebGL (Release)")]
        public static void BuildWebGLRelease()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                bool switchTarget = EditorUtility.DisplayDialog(
                    "Mudar plataforma para WebGL",
                    "A plataforma ativa não é WebGL. Quer mudar agora? Depois execute este comando de novo.",
                    "Mudar plataforma", "Cancelar");
                if (switchTarget)
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
                return;
            }

            if (!File.Exists(MainMenuScene) || !File.Exists(MissionScene))
            {
                EditorUtility.DisplayDialog("Zombit Survival", "Não encontrei as cenas esperadas:\n" + MainMenuScene + "\n" + MissionScene, "OK");
                return;
            }

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuScene, true),
                new EditorBuildSettingsScene(MissionScene, true)
            };

            // Gzip + fallback de descompressao funciona em qualquer hospedagem estatica, mesmo sem configurar cabecalhos.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.runInBackground = false;

            Directory.CreateDirectory(Path.GetFullPath(OutputDirectory));
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = OutputDirectory,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                EditorUtility.RevealInFinder(Path.GetFullPath(OutputDirectory));
                EditorUtility.DisplayDialog("Build WebGL concluída",
                    "Pasta: " + Path.GetFullPath(OutputDirectory) +
                    "\n\nSuba o conteúdo dessa pasta para a hospedagem (HTTPS). Lembre de adicionar o endereço do jogo em Redirect URLs no Supabase (Authentication > URL Configuration).", "OK");
            }
            else
            {
                Debug.LogError("Zombit Survival: build WebGL falhou. Resultado: " + report.summary.result + ". Veja os erros acima no Console.");
                EditorUtility.DisplayDialog("Build não concluída", "Abra o Console e veja a primeira mensagem vermelha.", "OK");
            }
        }
    }
}
#endif
