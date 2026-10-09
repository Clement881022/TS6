namespace SanGuo.Client
{
    public enum Page { Home, Map, Formation, Gacha, Heroes, Dungeons, Quests, Shop, Battle, WorldBoss, Login, Account, HeroGrowth }

    public static class Nav
    {
        public static void Go(Page page) => PageHost.Current?.Show(page);
    }
}
