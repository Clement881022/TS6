#nullable enable
using System;
using SanGuo.Core;
using SanGuo.Core.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    /// <summary>
    /// 主城：整張主城場景當底，功能入口是建築上的名牌；左上玩家卡與章節進度、右上資源、
    /// 底部圓形功能鍵、右下「戰鬥」圓章，左下 3D 巴豆妖會依狀況提示下一步。版型參考 TS6Client 的主城介面。
    /// </summary>
    public sealed class HomePage : PageBase
    {
        // 場景圖是 16:9（Resources/UiBg/home），以「填滿」方式鋪滿畫面，名牌用圖上的百分比座標定位。
        private const float SceneAspect = 16f / 9f;

        private VisualElement? _scene;
        private bool _geometryHooked;
        private ModelStage? _mascot;

        protected override Page Id => Page.Home;
        protected override string Title => "三國將星傳";
        protected override bool UseFrame => false;

        protected override void OnReady()
        {
            if (GameSession.View.ClearedStages.Count > 0) return;
            // 先看序章（劇情）；再由巴豆妖講系統性教學（主城功能），最後進大地圖。
            StoryPlayer.ShowIntro(Host, ShowSystemTutorial);
            if (GameSession.View.ClearedStages.Count == 0 && Tutorial.Seen("story_intro")) ShowSystemTutorial();
        }

        /// <summary>巴豆妖負責系統性教學，不參與劇情。</summary>
        private void ShowSystemTutorial()
        {
            Tutorial.Show(Host, "home", "主城功能", new[]
            {
                "嗨嗨，主公！我是巴豆妖，負責教你遊戲怎麼玩。主城裡每棟建築都是一個功能。",
                "「征戰」推進主線關卡；「招募」抽取新武將；「武將」升級與強化；「副本」和「任務」能取得養成素材。",
                "先從第一關開始，戰鬥裡我會再教你出牌。",
            }, "前往征戰", () => Nav.Go(Page.Map), speaker: "巴豆妖", model: "badou");
        }

        private void OnDestroy()
        {
            _mascot?.Dispose();
            _mascot = null;
        }

        protected override void BuildBody(VisualElement root)
        {
            var v = GameSession.View;
            int total = DemoContent.ChapterLevelCount;
            int cleared = 0;
            for (int i = 1; i <= total; i++)
                if (v.ClearedStages.Contains(GameSession.StageIdOf(i))) cleared++;

            // ---- 場景 + 建築名牌 ----
            _scene = new VisualElement();
            _scene.AddToClassList("home-scene");
            var tex = Resources.Load<Texture2D>("UiBg/home");
            if (tex != null) _scene.style.backgroundImage = new StyleBackground(tex);
            root.Add(_scene);
            if (!_geometryHooked)
            {
                _geometryHooked = true;
                root.RegisterCallback<GeometryChangedEvent>(_ => FitScene(root));
            }
            root.schedule.Execute(() => FitScene(root)).ExecuteLater(0);

            AddSpot("招募", Page.Gacha, 30.7f, 40f);
            AddSpot("武將", Page.Heroes, 20.3f, 55f);
            AddSpot("征戰", Page.Map, 56.5f, 46f);
            AddSpot("副本", Page.Dungeons, 43.4f, 71f);
            AddSpot("任務", Page.Quests, 90.4f, 52f);
            AddSpot("商店", Page.Shop, 71.8f, 76f);

            // ---- 左上：玩家卡 + 章節進度 ----
            root.Add(BuildPlayerCard(v));
            root.Add(BuildChapterPlate(cleared, total));

            // ---- 右上：資源 ----
            var res = new VisualElement { pickingMode = PickingMode.Ignore };
            res.AddToClassList("home-res");
            res.Add(UiKit.Pill($"體力 {v.Stamina}/{v.StaminaCap}", "stamina"));
            res.Add(UiKit.Pill($"{v.Gold:N0}", "gold"));
            res.Add(UiKit.Pill($"{v.Yuanbao:N0}", "yuanbao"));
            root.Add(res);

            // ---- 底部：圓形功能鍵 + 戰鬥圓章 ----
            root.Add(BuildFunctionBar());
            var orb = new Button(() => Nav.Go(Page.Map));
            orb.AddToClassList("home-orb");
            root.Add(orb);

            // ---- 左下：3D 巴豆妖 + 提示氣泡 ----
            root.Add(BuildMascot(v, cleared, total));
        }

        // ------------------------------------------------------------ 版面

        private void FitScene(VisualElement root)
        {
            if (_scene == null) return;
            float w = root.resolvedStyle.width, h = root.resolvedStyle.height;
            if (float.IsNaN(w) || float.IsNaN(h) || w < 1f || h < 1f) return;
            float sh = Mathf.Max(h, w / SceneAspect);
            float sw = sh * SceneAspect;
            _scene.style.width = sw;
            _scene.style.height = sh;
            _scene.style.left = (w - sw) * 0.5f;
            _scene.style.top = (h - sh) * 0.5f;
        }

        /// <summary>建築上的名牌：藍底菱形框，點下去前往對應頁面。x / y 是場景圖上的百分比。</summary>
        private void AddSpot(string label, Page target, float x, float y)
        {
            var spot = new Button(() => Nav.Go(target));
            spot.AddToClassList("home-spot");
            spot.style.left = Length.Percent(x);
            spot.style.top = Length.Percent(y);
            spot.Add(new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("home-diamond"));
            spot.Add(new Label(label) { pickingMode = PickingMode.Ignore }.WithClass("home-spot-text"));
            spot.Add(new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("home-diamond"));
            _scene!.Add(spot);
        }

        private VisualElement BuildPlayerCard(ProfileView v)
        {
            var card = new VisualElement { pickingMode = PickingMode.Ignore };
            card.AddToClassList("home-player");
            var hero = GameSession.DefOf("liubei");
            card.Add(UiKit.Avatar(hero?.Name ?? "主", hero?.Rarity ?? Rarity.UR, true, "liubei"));
            var info = new VisualElement { pickingMode = PickingMode.Ignore };
            info.AddToClassList("home-player-info");
            var top = new VisualElement { pickingMode = PickingMode.Ignore };
            top.AddToClassList("home-player-top");
            top.Add(new Label("主公") { pickingMode = PickingMode.Ignore }.WithClass("home-player-name"));
            top.Add(new Label($"Lv.{v.Level}") { pickingMode = PickingMode.Ignore }.WithClass("home-player-level"));
            info.Add(top);
            var bar = UiKit.Bar(v.ExpToNext <= 0 ? 0 : 100f * v.Exp / v.ExpToNext, "bar-gold bar-slim");
            bar.pickingMode = PickingMode.Ignore;
            info.Add(bar);
            info.Add(new Label($"經驗 {v.Exp}/{v.ExpToNext}") { pickingMode = PickingMode.Ignore }.WithClass("home-player-exp"));
            card.Add(info);
            return card;
        }

        /// <summary>章節進度牌：點下去繼續征戰。</summary>
        private static VisualElement BuildChapterPlate(int cleared, int total)
        {
            var plate = new Button(() => Nav.Go(Page.Map));
            plate.AddToClassList("home-chapter");
            string next = cleared >= total ? "章節已通關" : $"{cleared + 1}　{DemoContent.LevelNames[Math.Min(cleared, total - 1)]}";
            plate.Add(new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("home-diamond"));
            var text = new VisualElement { pickingMode = PickingMode.Ignore };
            text.AddToClassList("home-chapter-text");
            text.Add(new Label("第零章　涿縣盜匪") { pickingMode = PickingMode.Ignore }.WithClass("home-chapter-title"));
            text.Add(new Label($"{next}　{cleared}/{total}") { pickingMode = PickingMode.Ignore }.WithClass("home-chapter-sub"));
            plate.Add(text);
            plate.Add(new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("home-diamond"));
            return plate;
        }

        private static VisualElement BuildFunctionBar()
        {
            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.AddToClassList("home-fn-bar");
            bar.Add(FunctionButton("武將", "heroes", Page.Heroes));
            bar.Add(FunctionButton("招募", "gacha", Page.Gacha));
            bar.Add(FunctionButton("副本", "dungeons", Page.Dungeons));
            bar.Add(FunctionButton("任務", "quests", Page.Quests));
            bar.Add(FunctionButton("商店", "shop", Page.Shop));
            return bar;
        }

        private static VisualElement FunctionButton(string label, string icon, Page target)
        {
            var b = new Button(() => Nav.Go(target));
            b.AddToClassList("home-fn");
            var circle = new VisualElement { pickingMode = PickingMode.Ignore };
            circle.AddToClassList("home-fn-circle");
            circle.Add(new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("home-fn-icon").WithClass("tile-ico-" + icon));
            b.Add(circle);
            b.Add(new Label(label) { pickingMode = PickingMode.Ignore }.WithClass("home-fn-label"));
            return b;
        }

        private VisualElement BuildMascot(ProfileView v, int cleared, int total)
        {
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.AddToClassList("home-mascot-box");

            string hint = HintFor(v, cleared, total);
            var bubble = new Label(hint) { pickingMode = PickingMode.Ignore };
            bubble.AddToClassList("home-bubble");
            box.Add(bubble);

            _mascot ??= ModelStage.Create("badou", 480, 600);
            if (_mascot != null)
            {
                var m = new Button(() => { bubble.text = HintFor(GameSession.View, cleared, total); _mascot?.Cheer(); });
                m.AddToClassList("home-mascot");
                m.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(_mascot.Texture));
                box.Add(m);
            }
            return box;
        }

        private static string HintFor(ProfileView v, int cleared, int total)
        {
            if (cleared == 0) return "主公，先從第一關開始吧！";
            if (cleared < total && v.Stamina >= v.StaminaCap) return "體力滿了，快去征戰！";
            if (v.Heroes.Count < 5) return "多招募幾位武將，隊伍才強！";
            return cleared >= total ? "第零章通關了，黃巾之亂就要來了！" : "繼續推進主線吧，主公！";
        }
    }
}
