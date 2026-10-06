#nullable enable
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    /// <summary>
    /// 戰鬥場景入口：在執行時建立 UIDocument 與 PanelSettings（場景裡只需要這個元件）。
    /// 命令列參數 -sanguoShot &lt;資料夾&gt; 會自動操作並截圖後離開（供自動驗證畫面）。
    /// </summary>
    public sealed class BattleBootstrap : MonoBehaviour
    {
        private BattleScreen _screen = null!;

        private void Awake()
        {
            Application.runInBackground = true;
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.Shrink;
            // UI Toolkit 執行時要求有 Theme；這裡用空的，所有樣式都在 Resources/UI/Battle.uss。
            settings.themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();

            var doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = settings;

            var root = doc.rootVisualElement;
            var sheet = Resources.Load<StyleSheet>("UI/Battle");
            if (sheet != null) root.styleSheets.Add(sheet);
            else Debug.LogError("找不到 Resources/UI/Battle.uss");

            ApplyCjkFont(root);

            var stage = gameObject.AddComponent<BattleStage>();
            _screen = new BattleScreen(root, stage, 1);

            string? levelArg = CommandLineValue("-sanguoLevel");
            if (levelArg != null && int.TryParse(levelArg, out int level)) _screen.DebugSetLevel(level);

            string? shotDir = CommandLineValue("-sanguoShot");
            if (shotDir != null) StartCoroutine(ScreenshotRoutine(shotDir));
            else _screen.OpenMap();
        }

        /// <summary>原型階段先借用系統中文字型；正式版需內嵌字型（手機沒有 Windows 字型）。</summary>
        private static void ApplyCjkFont(VisualElement root)
        {
            try
            {
                var font = Font.CreateDynamicFontFromOSFont(
                    new[] { "Microsoft JhengHei UI", "Microsoft JhengHei", "Microsoft YaHei UI", "Noto Sans CJK TC" }, 32);
                var asset = FontAsset.CreateFontAsset(font);
                root.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromSDFFont(asset));
            }
            catch (Exception e)
            {
                Debug.LogWarning("中文字型載入失敗：" + e.Message);
            }
        }

        private static string? CommandLineValue(string key)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == key) return args[i + 1];
            return null;
        }

        private IEnumerator ScreenshotRoutine(string dir)
        {
            Debug.Log("[shot] routine start -> " + dir);
            Directory.CreateDirectory(dir);
            yield return new WaitForSecondsRealtime(1.0f);
            Debug.Log("[shot] after wait");
            Shot(Path.Combine(dir, "1-start.png"));
            yield return new WaitForSecondsRealtime(0.5f);

            _screen.DebugPreviewFirstCard();
            yield return new WaitForSecondsRealtime(0.3f);
            Shot(Path.Combine(dir, "2-preview.png"));
            yield return new WaitForSecondsRealtime(0.5f);

            _screen.DebugPlayFirstPlayable();
            _screen.DebugPlayFirstPlayable();
            yield return new WaitForSecondsRealtime(0.3f);
            Shot(Path.Combine(dir, "3-played.png"));
            yield return new WaitForSecondsRealtime(0.5f);

            _screen.DebugOpenFormation();
            yield return new WaitForSecondsRealtime(0.3f);
            Shot(Path.Combine(dir, "4-formation.png"));
            yield return new WaitForSecondsRealtime(0.5f);

            _screen.DebugOpenMap();
            yield return new WaitForSecondsRealtime(0.3f);
            Shot(Path.Combine(dir, "5-map.png"));
            yield return new WaitForSecondsRealtime(0.8f);
            Application.Quit();
        }

        private static void Shot(string path)
        {
            Debug.Log("[shot] capture " + path);
            ScreenCapture.CaptureScreenshot(path);
        }
    }
}
