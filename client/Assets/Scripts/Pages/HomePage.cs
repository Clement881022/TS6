#nullable enable
using System;
using SanGuo.Core;
using SanGuo.Core.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    /// <summary>
    /// 策略國風主城：城景、左側功能列、右側章節卡與出征，文字與資源由即時資料呈現。
    /// </summary>
    public sealed class HomePage : PageBase
    {
        // Q 版場景圖以 16:9 填滿畫面；互動 HUD 獨立於城景裁切。
        private const float SceneAspect = 16f / 9f;

        private VisualElement? _scene;
        private bool _geometryHooked;

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
                "嗨嗨，主公！我是巴豆妖，負責教你遊戲怎麼玩。左側功能列可以管理你的隊伍。",
                "「征戰」推進主線關卡；「招募」抽取新武將；「武將」升級與強化；「副本」和「任務」能取得養成素材。",
                "先從第一關開始，戰鬥裡我會再教你出牌。",
            }, "前往征戰", () => Nav.Go(Page.Map), speaker: "巴豆妖", model: "badou");
        }

        protected override void BuildBody(VisualElement root)
        {
            var v = GameSession.View;
            // 首頁的主線面板顯示目前該打的那一章。
            var (chapter, _) = GameSession.Frontier();
            int total = Campaign.LevelsPerChapter;
            int cleared = 0;
            for (int i = 1; i <= total; i++)
                if (v.ClearedStages.Contains(GameSession.StageIdOf(chapter, i))) cleared++;

            // ---- 新城景與全螢幕 HUD ----
            _scene = new VisualElement();
            _scene.AddToClassList("home-scene");
            var tex = Resources.Load<Texture2D>("ChibiSkin/home");
            if (tex != null) _scene.style.backgroundImage = new StyleBackground(tex);
            root.Add(_scene);
            if (!_geometryHooked)
            {
                _geometryHooked = true;
                root.RegisterCallback<GeometryChangedEvent>(_ => FitScene(root));
            }
            root.schedule.Execute(() => FitScene(root)).ExecuteLater(0);

            root.Add(new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("strategy-home-shade"));
            var topBar = new VisualElement().WithClass("strategy-home-top");
            var brand = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("strategy-brand");
            brand.Add(UiKit.Text("三國將星傳", "strategy-brand-title"));
            topBar.Add(brand);

            // ---- 左上：玩家卡 + 章節進度 ----
            topBar.Add(BuildPlayerCard(v));

            // ---- 右上：資源 ----
            var res = new VisualElement { pickingMode = PickingMode.Ignore };
            res.AddToClassList("home-res");
            res.Add(UiKit.ResPill("item_stamina", $"{v.Stamina}/{v.StaminaCap}"));
            res.Add(UiKit.ResPill("item_gold", v.Gold.ToString("N0")));
            res.Add(UiKit.ResPill("item_yuanbao", v.Yuanbao.ToString("N0")));
            topBar.Add(res);
            root.Add(topBar);

            // 主線進度、下一關與主操作放在同一個面板，直接引導當前目標。
            root.Add(BuildFunctionBar());
            root.Add(BuildCampaign(v, chapter, cleared, total));
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

        private VisualElement BuildPlayerCard(ProfileView v)
        {
            // 點玩家卡進帳號頁（暱稱、綁定、登出）。
            var card = new VisualElement();
            card.AddToClassList("home-player");
            card.tooltip = "帳號";
            card.RegisterCallback<ClickEvent>(_ => Nav.Go(Page.Account));
            var hero = GameSession.DefOf("liubei");
            card.Add(UiKit.Avatar(hero?.Name ?? "主", hero?.Rarity ?? Rarity.UR, true, "liubei"));
            var info = new VisualElement { pickingMode = PickingMode.Ignore };
            info.AddToClassList("home-player-info");
            var top = new VisualElement { pickingMode = PickingMode.Ignore };
            top.AddToClassList("home-player-top");
            top.Add(new Label(GameSession.DisplayName) { pickingMode = PickingMode.Ignore }.WithClass("home-player-name"));
            top.Add(new Label($"Lv.{v.Level}") { pickingMode = PickingMode.Ignore }.WithClass("home-player-level"));
            info.Add(top);
            var bar = UiKit.Bar(v.ExpToNext <= 0 ? 0 : 100f * v.Exp / v.ExpToNext, "bar-gold bar-slim");
            bar.pickingMode = PickingMode.Ignore;
            info.Add(bar);
            info.Add(new Label($"經驗 {v.Exp}/{v.ExpToNext}") { pickingMode = PickingMode.Ignore }.WithClass("home-player-exp"));
            card.Add(info);
            return card;
        }

        private static VisualElement BuildCampaign(ProfileView v, int chapter, int cleared, int total)
        {
            var plate = new VisualElement().WithClass("home-campaign");
            var heading = new VisualElement().WithClass("home-campaign-heading");
            heading.Add(UiKit.Text("主線征戰", "home-campaign-kicker"));
            // Campaign.Title 為「第一章　黃巾烽火（上）」：章號與標題分兩行。
            var title = Campaign.Title(chapter).Split('　');
            heading.Add(UiKit.Text(title[0], "home-campaign-chapter"));
            plate.Add(heading);
            plate.Add(UiKit.Text(title.Length > 1 ? title[1] : "", "home-campaign-title"));
            var (_, nextLevel) = GameSession.Frontier();
            plate.Add(UiKit.Text(cleared >= total ? "主線已全數通關 · 可重返關卡" : $"{chapter}-{nextLevel} · {Campaign.LevelName(chapter, nextLevel)}", "home-campaign-next"));
            var status = new VisualElement().WithClass("home-campaign-status");
            status.Add(UiKit.Text("章節進度", "home-campaign-progress-label"));
            status.Add(UiKit.Text($"{cleared} / {total}", "home-campaign-progress-value"));
            plate.Add(status);
            var progress = UiKit.Bar(total <= 0 ? 0 : 100f * cleared / total, "bar-gold bar-slim");
            progress.AddToClassList("home-campaign-progress");
            plate.Add(progress);
            plate.Add(UiKit.Text(HintFor(v, chapter, cleared, total), "home-campaign-hint"));
            var expedition = UiKit.Btn("", () => { GameSession.Select(chapter, nextLevel); GameSession.OpenSelectedStageOnMap = true; Nav.Go(Page.Map); }, primary: true).WithClass("home-primary");
            expedition.tooltip = $"前往 {chapter}-{nextLevel}，查看敵軍與出戰條件";
            bool fresh = v.ClearedStages.Count == 0;
            expedition.Add(UiKit.Text(fresh ? "開始出征" : cleared >= total ? "重返戰場" : "繼續出征", "home-primary-title"));
            expedition.Add(UiKit.Text("›", "home-primary-arrow"));
            plate.Add(expedition);
            bool formationUnlocked = GameSession.IsUnlocked(0, DemoMeta.FirstOpenFormationLevel);
            var formation = UiKit.Btn("排兵布陣", () => { GameSession.FormationStageId = GameSession.StageIdOf(0, DemoMeta.FirstOpenFormationLevel); Nav.Go(Page.Formation); }).WithClass("home-formation");
            formation.SetEnabled(formationUnlocked);
            formation.tooltip = formationUnlocked ? "編輯主線出戰陣容" : $"通關第 {DemoMeta.FirstOpenFormationLevel - 1} 關後開放";
            var preparation = new VisualElement().WithClass("home-preparation");
            preparation.Add(formation);
            preparation.Add(UiKit.Text(formationUnlocked ? "調整出戰隊伍" : $"第 {DemoMeta.FirstOpenFormationLevel} 關開放", "home-preparation-note"));
            plate.Add(preparation);
            return plate;
        }

        private static VisualElement BuildFunctionBar()
        {
            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.AddToClassList("home-fn-bar");
            bar.Add(UiKit.Text("軍務", "home-nav-heading"));
            bar.Add(FunctionButton("武將", "培養與裝備", "heroes", Page.Heroes));
            bar.Add(FunctionButton("招募", "招募新將", "gacha", Page.Gacha));
            bar.Add(UiKit.Text("日常", "home-nav-heading home-nav-divider"));
            bar.Add(FunctionButton("副本", "取得養成素材", "dungeons", Page.Dungeons));
            bar.Add(FunctionButton("世界 Boss", "群雄榜排名", "map", Page.WorldBoss));
            bar.Add(FunctionButton("任務", "領取目標獎勵", "quests", Page.Quests));
            bar.Add(FunctionButton("商店", "補給與將魂", "shop", Page.Shop));
            return bar;
        }

        private static VisualElement FunctionButton(string label, string description, string icon, Page target)
        {
            var b = new Button(() => Nav.Go(target));
            b.AddToClassList("home-fn");
            var circle = new VisualElement { pickingMode = PickingMode.Ignore };
            circle.AddToClassList("home-fn-circle");
            circle.Add(new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("home-fn-icon").WithClass("tile-ico-" + icon));
            b.Add(circle);
            var text = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("home-nav-text");
            text.Add(UiKit.Text(label, "home-fn-label"));
            text.Add(UiKit.Text(description, "home-nav-description"));
            b.Add(text);
            b.tooltip = description;
            return b;
        }

        private static string HintFor(ProfileView v, int chapter, int cleared, int total)
        {
            if (v.ClearedStages.Count == 0) return "主公，先從第一關開始吧！";
            if (cleared < total && v.Stamina >= v.StaminaCap) return "體力滿了，快去征戰！";
            if (v.Heroes.Count < 5) return "多招募幾位武將，隊伍才強！";
            if (cleared >= total) return "討董之戰告一段落，天書第三卷仍下落不明……";
            return cleared == 0 && chapter > 0 ? $"{Campaign.Title(chapter).Split('　')[0]}開始了，敵人更強了，記得養成武將！" : "繼續推進主線吧，主公！";
        }
    }
}
