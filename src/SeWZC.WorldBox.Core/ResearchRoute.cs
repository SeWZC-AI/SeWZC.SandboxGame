namespace SeWZC.WorldBox.Core;

/// <summary>研究树的路线及节点筛选规则。</summary>
public abstract class ResearchRoute
{
    private protected ResearchRoute() { }
    public abstract string Id { get; }
    public abstract string Title { get; }
    public virtual bool IsMagic => false;
    public virtual ResearchKind DefaultResearch => ResearchKind.Agriculture;
    public abstract bool Includes(ResearchDefinition definition);
    public static ResearchRoute Technology { get; } = new TechnologyRoute();
    public static ResearchRoute Magic { get; } = new MagicRoute();
    public static ResearchRoute Common { get; } = new CommonRoute();

    private sealed class TechnologyRoute : ResearchRoute
    {
        public override string Id => "technology";
        public override string Title => "科技研究树";
        public override bool Includes(ResearchDefinition definition) => !definition.Magic || definition.Shared;
    }

    private sealed class MagicRoute : ResearchRoute
    {
        public override string Id => "magic";
        public override string Title => "魔法研究树";
        public override bool IsMagic => true;
        public override ResearchKind DefaultResearch => ResearchKind.ArcaneArts;
        public override bool Includes(ResearchDefinition definition) => definition.Magic || definition.Shared;
    }

    private sealed class CommonRoute : ResearchRoute
    {
        public override string Id => "common";
        public override string Title => "两条路线的共同基础";
        public override bool Includes(ResearchDefinition definition) => definition.Shared;
    }
}
