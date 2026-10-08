using System.Collections.Generic;

namespace SanGuo.Core.Data
{
    /// <summary>
    /// 主線劇情的統一入口：第零章見 <see cref="DemoStory"/>，第 1–6 章見 Chapter1Story…Chapter6Story。
    /// 貫穿 1.0 的線索：①命牌每章歸位一枚；②黃布符紋與帶著暗光的「仙物」，從黃巾一路延伸到董卓；天書共三卷，張角、董卓各持一卷，第三卷下落不明（為架空章節的魔化埋伏筆）。
    /// </summary>
    public static class CampaignStory
    {
        /// <summary>戰前劇情；沒有則回傳空清單。</summary>
        public static List<StoryLine> Before(int chapter, int level)
        {
            switch (chapter)
            {
                case 0: return DemoStory.Before(level);
                case 1: return Chapter1Story.Before(level);
                case 2: return Chapter2Story.Before(level);
                case 3: return Chapter3Story.Before(level);
                case 4: return Chapter4Story.Before(level);
                case 5: return Chapter5Story.Before(level);
                case 6: return Chapter6Story.Before(level);
                default: return new List<StoryLine>();
            }
        }

        /// <summary>首通後劇情（失敗不播）；沒有則回傳空清單。</summary>
        public static List<StoryLine> After(int chapter, int level)
        {
            switch (chapter)
            {
                case 0: return DemoStory.After(level);
                case 1: return Chapter1Story.After(level);
                case 2: return Chapter2Story.After(level);
                case 3: return Chapter3Story.After(level);
                case 4: return Chapter4Story.After(level);
                case 5: return Chapter5Story.After(level);
                case 6: return Chapter6Story.After(level);
                default: return new List<StoryLine>();
            }
        }
    }

    /// <summary>劇情對白的說話者。Portrait 用既有的武將 / 敵人 id（客戶端找模型，找不到則依職業借用）；沒有合適模型的人物留空。</summary>
    internal static class Cast
    {
        public static StoryLine N(string text) => new StoryLine(DemoStory.Narrator, "", text);
        public static StoryLine Me(string text) => new StoryLine(DemoStory.Protagonist, "", text);
        public static StoryLine Lb(string text) => new StoryLine("劉備", "liubei", text);
        public static StoryLine Zf(string text) => new StoryLine("張飛", "zhangfei", text);
        public static StoryLine Gy(string text) => new StoryLine("關羽", "guanyu", text);
        /// <summary>其他人物：名字與模型 id（空字串 = 不顯示立繪）。</summary>
        public static StoryLine S(string speaker, string portrait, string text) => new StoryLine(speaker, portrait, text);
    }
}
