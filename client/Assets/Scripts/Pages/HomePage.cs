#nullable enable
using SanGuo.Core;
using SanGuo.Core.Meta;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    /// <summary>主城：章節橫幅、帳號概況、各功能入口。</summary>
    public sealed class HomePage : PageBase
    {
        protected override Page Id => Page.Home;
        protected override string Title => "三國將星傳";

        protected override void OnReady()
        {
            if (GameSession.View.ClearedStages.Count > 0) return;
            Tutorial.Show(Host, "home", "歡迎，主公！", new[]
            {
                "三國亂世開始了。你要帶領劉備、關羽、張飛等武將，從黃巾之亂一路打下去。",
                "「征戰」推進主線關卡；「招募」抽取新武將；「武將」升級與強化；「副本」和「任務」能取得養成素材。",
                "先從第一關開始，教學會一步步帶你熟悉出牌。",
            }, "前往征戰", () => Nav.Go(Page.Map));
        }

        protected override void BuildBody(VisualElement body)
        {
            var v = GameSession.View;

            // 章節橫幅
            int total = DemoContent.ChapterLevelCount;
            int cleared = 0;
            for (int i = 1; i <= total; i++)
                if (v.ClearedStages.Contains(GameSession.StageIdOf(i))) cleared++;
            var banner = UiKit.Panel("banner");
            banner.Add(UiKit.Text("目前章節", "banner-eyebrow"));
            banner.Add(UiKit.Text("第一章　黃巾之亂", "banner-title"));
            var meta = new VisualElement();
            meta.AddToClassList("banner-meta");
            meta.Add(UiKit.Text($"章節進度 {cleared}/{total}", "txt-gold"));
            meta.Add(UiKit.Text(cleared >= total ? "章節已通關" : $"下一關：{DemoContent.LevelNames[System.Math.Min(cleared, total - 1)]}", "txt-dim"));
            banner.Add(meta);
            banner.Add(UiKit.Bar(total == 0 ? 0 : 100f * cleared / total, "bar-gold"));
            var orb = new Button(() => Nav.Go(Page.Map));
            orb.AddToClassList("orb-battle");
            banner.Add(orb);
            body.Add(banner);

            // 帳號經驗
            var account = UiKit.Panel();
            var head = UiKit.Row();
            head.AddToClassList("panel-row");
            head.Add(UiKit.Text($"帳號 Lv.{v.Level}", "txt-sub"));
            head.Add(UiKit.Text($"經驗 {v.Exp}/{v.ExpToNext}", "txt-dim"));
            account.Add(head);
            account.Add(UiKit.Bar(v.ExpToNext <= 0 ? 0 : 100f * v.Exp / v.ExpToNext));
            account.Add(UiKit.Text($"擁有武將 {v.Heroes.Count} 名　連線：{GameSession.Backend.Name}", "txt-dim"));
            body.Add(account);

            // 功能入口
            body.Add(UiKit.Section("功能"));
            var grid = new VisualElement();
            grid.AddToClassList("tile-grid");
            grid.Add(Tile("heroes", "武將", "升級、突破、強化卡牌", Page.Heroes));
            grid.Add(Tile("gacha", "招募", "抽取新武將", Page.Gacha, hot: true));
            grid.Add(Tile("dungeons", "副本", "每日資源副本與掃蕩", Page.Dungeons));
            grid.Add(Tile("quests", "任務", "每日任務與七日目標", Page.Quests));
            grid.Add(Tile("shop", "商店", "月卡、成長基金", Page.Shop));
            grid.Add(Tile("map", "征戰", "章節地圖與關卡", Page.Map));
            body.Add(grid);
        }

        private static VisualElement Tile(string iconName, string title, string desc, Page target, bool hot = false)
        {
            var tile = new Button(() => Nav.Go(target));
            tile.AddToClassList("tile");
            if (hot) tile.AddToClassList("tile-hot");
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("tile-icon");
            icon.AddToClassList("tile-ico-" + iconName);
            tile.Add(icon);
            var text = new VisualElement { pickingMode = PickingMode.Ignore };
            text.AddToClassList("tile-text");
            text.Add(new Label(title) { pickingMode = PickingMode.Ignore }.WithClass("tile-title"));
            text.Add(new Label(desc) { pickingMode = PickingMode.Ignore }.WithClass("tile-desc"));
            tile.Add(text);
            return tile;
        }
    }
}
