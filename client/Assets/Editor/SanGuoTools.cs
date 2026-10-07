using System.IO;
using System.Linq;
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
        private const string SceneDir = "Assets/Scenes";
        private const string PrefabDir = "Assets/Resources/Pages";
        private static readonly string InitScenePath = $"{SceneDir}/Init.unity";
        private static readonly string MainScenePath = $"{SceneDir}/Main.unity";

        /// <summary>Init（一次性初始化，啟動場景）與 Main（常駐 UI 外殼，頁面以 prefab 載入）。</summary>
        [MenuItem("三國/建立 Init 與 Main 場景")]
        public static void CreateScenes()
        {
            Directory.CreateDirectory(SceneDir);
            CreateScene(InitScenePath, "Init", go => go.AddComponent<InitScene>());
            CreateScene(MainScenePath, "Main", go => go.AddComponent<PageHost>());
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(InitScenePath, true),
                new EditorBuildSettingsScene(MainScenePath, true),
            };
            Debug.Log("已建立 Init / Main 場景並設定 Build Settings");
        }

        private static void CreateScene(string path, string name, System.Action<GameObject> addComponent)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.1f, 0.13f);
            addComponent(new GameObject(name));
            EditorSceneManager.SaveScene(scene, path);
        }

        /// <summary>每個頁面一個 prefab：Resources/Pages/&lt;Page&gt;.prefab，上面掛對應的 &lt;Page&gt;Page 元件。</summary>
        [MenuItem("三國/建立頁面 Prefab")]
        public static void CreatePagePrefabs()
        {
            Directory.CreateDirectory(PrefabDir);
            foreach (Page page in System.Enum.GetValues(typeof(Page)))
            {
                var type = System.Type.GetType($"SanGuo.Client.{page}Page, Assembly-CSharp");
                if (type == null) { Debug.LogError($"找不到頁面類別 {page}Page"); continue; }
                var go = new GameObject(page + "Page");
                go.AddComponent(type);
                PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{page}.prefab");
                Object.DestroyImmediate(go);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("已建立頁面 prefab 於 " + PrefabDir);
        }

        [MenuItem("三國/打包 Windows 測試版")]
        public static void BuildWindows()
        {
            ConfigurePlayer();
            if (!File.Exists(MainScenePath)) CreateScenes();

            string outDir = CommandLineValue("-sanguoOut")
                ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../../build/win"));
            Directory.CreateDirectory(outDir);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(x => x.enabled).Select(x => x.path).ToArray(),
                locationPathName = Path.Combine(outDir, "SanGuo.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            Debug.Log("Build result: " + report.summary.result + " -> " + outDir);
            if (Application.isBatchMode)
                EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>批次模式入口：建立場景後離開。</summary>
        public static void CreateAllScenesAndExit()
        {
            CreateScenes();
            CreatePagePrefabs();
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
