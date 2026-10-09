#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using SanGuo.Core;
using SanGuo.Core.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    public sealed class HomePage : PageBase
    {
        private const float SceneAspect = 16f / 9f;

        private const string HomeHeroKey = "home_hero";

        private VisualElement? _scene;
        private VisualElement? _heroArt;
        private bool _geometryHooked;

        protected override Page Id => Page.Home;
        protected override string Title => "三國將星傳";
        protected override bool UseFrame => false;

        protected override void OnReady()
        {
            if (GameSession.View.ClearedStages.Count > 0) return;
            StoryPlayer.ShowIntro(Host, ShowSystemTutorial);
            if (GameSession.View.ClearedStages.Count == 0 && Tutorial.Seen("story_intro")) ShowSystemTutorial();
        }

        private void ShowSystemTutorial()
        {
            Tutorial.Show(Host, "home", "主城功能", new[]
            {
                "嗨嗨，主公！我是巴豆妖，負責教你遊戲怎麼玩。下方功能列可以管理你的隊伍。",
                "「征戰」推進主線關卡；「招募」抽取新武將；「武將」查看狀態與牌組；「養成」升級、突破與換裝；「副本」和「任務」能取得養成素材。",
                "先從第一關開始，戰鬥裡我會再教你出牌。",
            }, "前往征戰", () => Nav.Go(Page.Map), speaker: "巴豆妖", model: "badou");
        }

        protected override void BuildBody(VisualElement root)
        {
            var v = GameSession.View;
            var (chapter, _) = GameSession.Frontier();
            int total = Campaign.LevelsPerChapter;
            int cleared = 0;
            for (int i = 1; i <= total; i++)
                if (v.ClearedStages.Contains(GameSession.StageIdOf(chapter, i))) cleared++;

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
            root.Add(BuildHero());
            var topBar = new VisualElement().WithClass("strategy-home-top");
            topBar.Add(BuildPlayerCard(v));

            var res = new VisualElement { pickingMode = PickingMode.Ignore };
            res.AddToClassList("home-res");
            res.Add(UiKit.ResPill("item_stamina", $"{v.Stamina}/{v.StaminaCap}"));
            res.Add(UiKit.ResPill("item_gold", v.Gold.ToString("N0")));
            res.Add(UiKit.ResPill("item_yuanbao", v.Yuanbao.ToString("N0")));
            topBar.Add(res);

            root.Add(topBar);
            root.Add(BuildFeatured());
            root.Add(BuildCampaign(v, chapter, cleared, total));
            root.Add(BuildFunctionBar());
        }

        private static string CurrentHomeHero()
        {
            var owned = GameSession.OwnedHeroes();
            string saved = PlayerPrefs.GetString(HomeHeroKey, "liubei");
            if (owned.Any(d => d.Id == saved)) return saved;
            return owned.Count > 0 ? owned[0].Id : saved;
        }

        private static void ApplyHeroArt(VisualElement art, string id)
        {
            var image = HeroArt.Full(id) ?? HeroArt.Bust(id) ?? HeroArt.Face(id);
            art.style.backgroundImage = image != null ? new StyleBackground(image) : new StyleBackground();
        }

        private VisualElement BuildHero()
        {
            var layer = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("home-hero-layer");
            _heroArt = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("home-hero");
            ApplyHeroArt(_heroArt, CurrentHomeHero());
            layer.Add(_heroArt);
            var switchButton = UiKit.Btn("切換角色", SwitchHero).WithClass("home-hero-switch");
            switchButton.name = "home-hero-switch";
            layer.Add(switchButton);
            return layer;
        }

        private void SwitchHero()
        {
            var owned = GameSession.OwnedHeroes();
            if (owned.Count == 0 || _heroArt == null) return;
            int index = owned.FindIndex(d => d.Id == CurrentHomeHero());
            string next = owned[(index + 1) % owned.Count].Id;
            PlayerPrefs.SetString(HomeHeroKey, next);
            ApplyHeroArt(_heroArt, next);
        }

        private static VisualElement BuildFeatured()
        {
            var pools = DemoMeta.Pools();
            var pool = pools.FirstOrDefault(p => p.UpUrs.Count > 0) ?? pools[0];
            var box = new VisualElement().WithClass("home-featured");
            box.name = "home-featured";
            var texture = Resources.Load<Texture2D>("RecruitBanners/" + pool.Id);
            if (texture != null) box.style.backgroundImage = new StyleBackground(texture);
            string heroId = pool.UpUrs.Count > 0 ? pool.UpUrs[0] : pool.UrHeroes.FirstOrDefault() ?? "";
            var art = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("home-featured-art");
            ApplyHeroArt(art, heroId);
            box.Add(art);
            box.Add(UiKit.Text("當期主打", "home-featured-tag"));
            box.Add(UiKit.Text(pool.Name, "home-featured-name"));
            box.RegisterCallback<ClickEvent>(_ => Nav.Go(Page.Gacha));
            return box;
        }

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
            card.Add(info);
            return card;
        }

        private static VisualElement BuildCampaign(ProfileView v, int chapter, int cleared, int total)
        {
            var plate = new VisualElement().WithClass("home-departure");
            var (_, nextLevel) = GameSession.Frontier();
            var expedition = UiKit.Btn("", () => { GameSession.Select(chapter, nextLevel); GameSession.OpenSelectedStageOnMap = true; Nav.Go(Page.Map); }, primary: true).WithClass("home-primary");
            var flag = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("home-sortie-art");
            flag.style.backgroundImage = new StyleBackground(Resources.Load<Texture2D>("HomeArt/Sortie"));
            expedition.Add(flag);
            expedition.Add(UiKit.Text("出征", "home-primary-title"));
            plate.Add(expedition);
            return plate;
        }

        private static VisualElement BuildFunctionBar()
        {
            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.AddToClassList("home-fn-bar");

            bar.Add(FunctionButton("武將", "查看武將", "heroes", Page.Heroes));
            bar.Add(FunctionButton("養成", "升級、突破與裝備", "shop", Page.HeroGrowth));
            bar.Add(FunctionButton("招募", "招募新將", "gacha", Page.Gacha));

            bar.Add(FunctionButton("挑戰", "素材副本與世界 Boss", "dungeons", Page.Dungeons));
            bar.Add(FunctionButton("任務", "領取目標獎勵", "quests", Page.Quests));
            bar.Add(FunctionButton("商店", "補給與將魂", "shop", Page.Shop));
            return bar;
        }

        private static VisualElement FunctionButton(string label, string description, string icon, Page target)
        {
            var b = new Button(() => Nav.Go(target));
            b.AddToClassList("home-fn");
            b.name = "home-" + target.ToString().ToLowerInvariant();
            int index = target == Page.Heroes ? 0 : target == Page.HeroGrowth ? 1 : target == Page.Gacha ? 2 : target == Page.Dungeons ? 3 : target == Page.Quests ? 4 : 5;
            b.Add(HomeArtwork.Icon(index));
            if (target != Page.Heroes) b.Add(new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("home-divider"));
            var text = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("home-nav-text");
            text.Add(UiKit.Text(label, "home-fn-label"));

            b.Add(text);
            b.tooltip = description;
            return b;
        }

    }
}
