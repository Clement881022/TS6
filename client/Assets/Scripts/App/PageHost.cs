#nullable enable
using System;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    /// <summary>
    /// Main 場景裡唯一的外殼：建立 UIDocument 與 PanelSettings、載入樣式與字型，
    /// 之後所有頁面都是 Resources/Pages/&lt;Page&gt;.prefab，用 Show 切換（舊頁面連同 prefab 一起銷毀）。
    /// 命令列參數 -sanguoShot &lt;資料夾&gt; 會自動逐頁操作並截圖後離開（見 ShotRunner）。
    /// </summary>
    public sealed class PageHost : MonoBehaviour
    {
        [SerializeField] private Page startPage = Page.Home;

        public static PageHost? Current { get; private set; }

        public PageBase? ActivePage { get; private set; }

        private VisualElement _root = null!;
        private VisualElement? _container;

        private void Awake()
        {
            Current = this;
            Application.runInBackground = true;
            GameSession.EnsureInit();

            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            // Preserve the reference canvas width on tall displays so the three-column
            // roster and bottom battle hand retain their intended proportions.
            settings.screenMatchMode = PanelScreenMatchMode.Expand;
            // UI Toolkit 執行時要求有 Theme；這裡用空的，所有樣式都在 Resources/UI/*.uss。
            settings.themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();

            var doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = settings;

            _root = doc.rootVisualElement;
            _root.AddToClassList("page-root");
            AddSheet(_root, "UI/Battle");
            AddSheet(_root, "UI/Pages");
            AddSheet(_root, "UI/Theme");
            AddSheet(_root, "UI/BattleTheme");
            AddSheet(_root, "UI/BattleLayout");
            AddSheet(_root, "UI/Pages2");
            AddSheet(_root, "UI/Tutorial");
            AddSheet(_root, "UI/Polish");
            AddSheet(_root, "UI/Chibi");
            AddSheet(_root, "UI/Home");
            AddSheet(_root, "UI/BattlePolish");
            AddSheet(_root, "UI/Revision");
            ApplyCjkFont(_root);

            // 連伺服器但裝置上沒有登入 token：先到登入頁。
            Show(GameSession.Accounts is { HasSession: false } ? Page.Login : startPage);
            if (GameSession.ShotDir != null && ShotRunner.Instance == null)
                ShotRunner.Begin(GameSession.ShotDir);
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        /// <summary>切換頁面：銷毀目前的頁面 prefab 與它的 UI 容器，載入並開啟新的。</summary>
        public void Show(Page page)
        {
            if (ActivePage != null) Destroy(ActivePage.gameObject);
            ActivePage = null;
            _container?.RemoveFromHierarchy();

            var prefab = Resources.Load<GameObject>("Pages/" + page);
            if (prefab == null)
            {
                Debug.LogError($"找不到頁面 prefab：Resources/Pages/{page}.prefab（選單「三國 → 建立頁面 Prefab」可重建）");
                return;
            }
            var view = Instantiate(prefab, transform).GetComponent<PageBase>();
            if (view == null)
            {
                Debug.LogError($"頁面 prefab {page} 缺少 PageBase 元件");
                return;
            }

            _container = new VisualElement();
            _container.AddToClassList("page-container");
            _root.Add(_container);
            ActivePage = view;
            AudioManager.PlayBgm(page == Page.Battle ? Bgm.Battle : Bgm.Home);
            view.Open(_container);
        }

        private static void AddSheet(VisualElement root, string path)
        {
            var sheet = Resources.Load<StyleSheet>(path);
            if (sheet != null) root.styleSheets.Add(sheet);
            else Debug.LogError($"找不到 Resources/{path}.uss");
        }

        /// <summary>
        /// 中文字型：優先用 Resources/Fonts/CjkFont（.ttf / .otf，放進去即可，手機也能用）；
        /// 沒有就借用系統中文字型（只有 Windows 等桌面有，手機沒有，上線前必須內嵌字型）。
        /// </summary>
        private static void ApplyCjkFont(VisualElement root)
        {
            try
            {
                var font = Resources.Load<Font>("Fonts/CjkFontMedium") ?? Resources.Load<Font>("Fonts/CjkFont");
                if (font == null)
                {
                    Debug.LogWarning("沒有內嵌中文字型（Resources/Fonts/CjkFont），改用系統字型；手機上會顯示不出中文。");
                    font = Font.CreateDynamicFontFromOSFont(
                        new[] { "Microsoft JhengHei UI", "Microsoft JhengHei", "Microsoft YaHei UI", "Noto Sans CJK TC" }, 32);
                }
                // 取樣字級 80、padding 8、大圖集（多張自動擴充）：比預設值的 SDF 更銳利，中文筆畫多時也不糊。
                var asset = FontAsset.CreateFontAsset(font, 80, 8, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048,
                    UnityEngine.TextCore.Text.AtlasPopulationMode.Dynamic, true);
                root.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromSDFFont(asset));
            }
            catch (Exception e)
            {
                Debug.LogWarning("中文字型載入失敗：" + e.Message);
            }
        }
    }
}
