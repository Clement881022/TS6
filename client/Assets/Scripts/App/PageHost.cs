#nullable enable
using System;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
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
            settings.screenMatchMode = PanelScreenMatchMode.Expand;
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
            AddSheet(_root, "UI/Commercial");
            AddSheet(_root, "BattleArt/HandCompact");
            AddSheet(_root, "UI/BattleHud");
            AddSheet(_root, "UI/HomeLayout");
            AddSheet(_root, "UI/Unified");
            ApplyCjkFont(_root);

            Show(GameSession.Accounts is { HasSession: false } ? Page.Login : startPage);
            if (GameSession.ShotDir != null && ShotRunner.Instance == null)
            {
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "-sanguoRosterShot") >= 0) RosterUiCapture.Begin(GameSession.ShotDir);
                else if (Array.IndexOf(Environment.GetCommandLineArgs(), "-sanguoEquipmentShot") >= 0) EquipmentUiCapture.Begin(GameSession.ShotDir);
                else if (Array.IndexOf(Environment.GetCommandLineArgs(), "-sanguoGrowthShot") >= 0) GrowthUiCapture.Begin(GameSession.ShotDir);
                else if (Array.IndexOf(Environment.GetCommandLineArgs(), "-sanguoUnifiedShot") >= 0) UnifiedUiCapture.Begin(GameSession.ShotDir);
                else if (Array.IndexOf(Environment.GetCommandLineArgs(), "-sanguoCommercialShot") >= 0) CommercialUiCapture.Begin(GameSession.ShotDir);
                else ShotRunner.Begin(GameSession.ShotDir);
            }
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

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
