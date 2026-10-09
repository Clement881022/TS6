#nullable enable
namespace SanGuo.Client
{
    public sealed class HeroGrowthPage : HeroesPage
    {
        protected override bool GrowthMode => true;
        protected override Page Id => Page.HeroGrowth;
        protected override string Title => "養成";
    }
}
