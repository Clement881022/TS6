using System.Collections.Generic;

namespace SanGuo.Core.Data
{
    public static class CampaignStory
    {
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

    internal static class Cast
    {
        public static StoryLine N(string text) => new StoryLine(DemoStory.Narrator, "", text);
        public static StoryLine Me(string text) => new StoryLine(DemoStory.Protagonist, "", text);
        public static StoryLine Lb(string text) => new StoryLine("劉備", "liubei", text);
        public static StoryLine Zf(string text) => new StoryLine("張飛", "zhangfei", text);
        public static StoryLine Gy(string text) => new StoryLine("關羽", "guanyu", text);
        public static StoryLine S(string speaker, string portrait, string text) => new StoryLine(speaker, portrait, text);
    }
}
