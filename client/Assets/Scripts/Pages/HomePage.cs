#nullable enable
using SanGuo.Core;
using SanGuo.Core.Meta;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    /// <summary>主城：帳號概況、繼續征戰、各功能入口。</summary>
    public sealed class HomePage : PageBase
    {
        protected override Page Id => Page.Home;
        protected override string Title => "三國將星傳";

        protected override void BuildBody(VisualElement body)
        {
            var v = GameSession.View;

            // 章節橫幅
            int total = DemoContent.ChapterLevelCount;
            int cleared = 0;
            for (int i = 1; i <= total; i++)
                if (v.ClearedStages.Contains(GameSession.StageIdOf(i))) cleared++;
            var banner = UiKit.Panel("banner");
            banner.Add(UiKit.Text("第一章　黃巾之亂", "banner-title"));
            banner.Add(UiKit.Text($"章節進度 {cleared}/{total}", "txt-dim"));
            var bar = new VisualElement();
            bar.AddToClassList("bar-bg");
            var fill = new VisualElement();
            fill.AddToClassList("bar-fill");
            fill.style.width = Length.Percent(total == 0 ? 0 : 100f * cleared / total);
            bar.Add(fill);
            banner.Add(bar);
            banner.Add(UiKit.Btn("繼續征戰", () => Nav.Go(Page.Map), primary: true));
            body.Add(banner);

            // 帳號經驗
            var account = UiKit.Panel();
            account.Add(UiKit.Text($"帳號 Lv.{v.Level}　經驗 {v.Exp}/{v.ExpToNext}", "txt"));
            var expBar = new VisualElement();
            expBar.AddToClassList("bar-bg");
            var expFill = new VisualElement();
            expFill.AddToClassList("bar-fill");
            expFill.style.width = Length.Percent(v.ExpToNext <= 0 ? 0 : 100f * v.Exp / v.ExpToNext);
            expBar.Add(expFill);
            account.Add(expBar);
            account.Add(UiKit.Text($"目前連線：{GameSession.Backend.Name}　擁有武將 {v.Heroes.Count} 名", "txt-dim"));
            body.Add(account);

            // 功能入口
            var grid = new VisualElement();
            grid.AddToClassList("tile-grid");
            grid.Add(Tile("武將", "升級、突破、強化卡牌", Page.Heroes));
            grid.Add(Tile("招募", "抽取新武將", Page.Gacha));
            grid.Add(Tile("副本", "每日資源副本與掃蕩", Page.Dungeons));
            grid.Add(Tile("任務", "每日任務與七日目標", Page.Quests));
            grid.Add(Tile("商店", "月卡、成長基金", Page.Shop));
            body.Add(grid);
        }

        private static VisualElement Tile(string title, string desc, Page target)
        {
            var tile = new Button(() => Nav.Go(target));
            tile.AddToClassList("tile");
            tile.Add(UiKit.Text(title, "tile-title"));
            tile.Add(UiKit.Text(desc, "txt-dim"));
            return tile;
        }
    }
}
