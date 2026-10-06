using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SanGuo.Client;

namespace SanGuo.EditorTools
{
    /// <summary>專案建置工具：場景與打包都由程式產生，不需要手動拖拉。</summary>
    public static class SanGuoTools
    {
        private const string ScenePath = "Assets/Scenes/Battle.unity";

        [MenuItem("三國/建立戰鬥場景")]
        public static void CreateBattleScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.1f, 0.13f);

            var go = new GameObject("Battle");
            go.AddComponent<BattleBootstrap>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("已建立 " + ScenePath);
        }

        [MenuItem("三國/打包 Windows 測試版")]
        public static void BuildWindows()
        {
            ConfigurePlayer();
            if (!File.Exists(ScenePath)) CreateBattleScene();

            string outDir = CommandLineValue("-sanguoOut")
                ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../../build/win"));
            Directory.CreateDirectory(outDir);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(outDir, "SanGuo.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            Debug.Log("Build result: " + report.summary.result + " -> " + outDir);
            if (Application.isBatchMode)
                EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>批次模式入口：建立場景後離開。</summary>
        public static void CreateBattleSceneAndExit()
        {
            CreateBattleScene();
            EditorApplication.Exit(0);
        }

        private static string CommandLineValue(string key)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == key) return args[i + 1];
            return null;
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "SanGuo";
            PlayerSettings.productName = "三國將星傳";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
        }
    }
}
