#nullable enable
namespace SanGuo.Client
{
    /// <summary>獨立養成頁；共用角色展示，所有修改操作只在此頁建立。</summary>
    public sealed class HeroGrowthPage : HeroesPage
    {
        protected override bool GrowthMode => true;
        protected override Page Id => Page.HeroGrowth;
        protected override string Title => "養成";
    }
}
