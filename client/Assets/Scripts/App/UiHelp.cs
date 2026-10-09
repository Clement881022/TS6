#nullable enable
using System;
using UnityEngine.UIElements;

namespace SanGuo.Client
{
    public static class UiHelp
    {
        public static Button Button(VisualElement host, Page page) =>
            UiKit.Btn("?", () => Show(host, "操作提示", Text(page))).WithClass("ui-help-button");

        public static string Text(Page page) => page switch
        {
            Page.Home => "武將：查看狀態、牌組與已穿裝備。\n養成：升級、突破及調整裝備。\n出征：前往下一個主線關卡。\n任務：查看目標與領取獎勵。",
            Page.Heroes => "選擇武將，查看目前狀態、突破後的牌組及已穿裝備。\n升級、突破與換裝請由主城的「養成」進入。",
            Page.HeroGrowth => "選擇武將後切換升級、突破或裝備。\n升級受帳號等級上限限制；突破需要重複武將與金幣。\n裝備頁可穿戴、卸下及分解；碎片集滿後自動合成。",
            Page.Gacha => "左側選擇卡池，下方進行單抽或十連。\n「機率詳情」提供當前卡池的機率及保底規則。\n重複武將轉為突破材料，滿突後轉為將魂。",
            Page.Quests => "切換每日、每週及七日目標。\n一鍵領取只處理目前分頁；七日目標會接著領取達標里程碑。\n點擊進度軌道的獎勵查看內容，可領取時直接領取。",
            Page.Battle => "選擇手牌，再點選高亮目標或格子出牌。\n移動牌先選我方角色，再選目的地。\n卡牌詳情顯示完整效果與射程；結束回合後敵軍行動。\n拖曳戰場旋轉視角，滾輪縮放；重置視角可恢復。",
            Page.Formation => "選擇武將並安排出戰位置。\n點擊出戰確認隊伍；鎖定編隊的關卡使用固定隊伍。",
            Page.Map => "選擇關卡查看敵軍、消耗與獎勵。\n完成前置關卡後開放後續關卡；首次通關獎勵只發放一次。",
            Page.Dungeons => "選擇副本查看消耗、開放條件及可取得的素材。",
            Page.Shop => "切換商店分類查看商品、將魂兌換、月卡及通行證。\n購買前請確認費用與持有量。",
            Page.WorldBoss => "選擇世界 Boss 參與挑戰，查看目前傷害與排行榜。",
            Page.Account => "查看或修改帳號資料，管理綁定與登入狀態。",
            _ => "選擇可用的操作繼續。"
        };

        public static VisualElement Dialog(VisualElement host, string title, Action<VisualElement> content)
        {
            host.Q(className: "ui-help-overlay")?.RemoveFromHierarchy();
            var overlay = new VisualElement().WithClass("ui-help-overlay");
            var panel = new VisualElement().WithClass("ui-help-panel");
            var header = UiKit.Row("ui-help-heading");
            header.Add(UiKit.Text(title, "ui-help-title"));
            header.Add(UiKit.Btn("×", () => overlay.RemoveFromHierarchy()).WithClass("ui-help-close"));
            panel.Add(header);
            var scroll = new ScrollView(ScrollViewMode.Vertical).WithClass("ui-help-content");
            content(scroll);
            panel.Add(scroll);
            overlay.Add(panel);
            host.Add(overlay);
            header.Q<Button>()?.Focus();
            overlay.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == UnityEngine.KeyCode.Escape) { overlay.RemoveFromHierarchy(); e.StopPropagation(); } });
            return overlay;
        }

        public static void Show(VisualElement host, string title, string text) =>
            Dialog(host, title, body => body.Add(UiKit.Text(text, "ui-help-text")));
    }
}
