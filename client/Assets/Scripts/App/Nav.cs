namespace SanGuo.Client
{
    /// <summary>遊戲裡的頁面。每個頁面是一個 prefab：Resources/Pages/&lt;列舉名&gt;.prefab，由 PageHost 載入。</summary>
    public enum Page { Home, Map, Formation, Gacha, Heroes, Dungeons, Quests, Shop, Battle }

    public static class Nav
    {
        public static void Go(Page page) => PageHost.Current?.Show(page);
    }
}
