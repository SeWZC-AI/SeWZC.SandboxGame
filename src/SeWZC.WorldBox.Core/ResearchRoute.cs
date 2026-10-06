namespace SeWZC.WorldBox.Core;

/// <summary>研究树的路线及节点筛选规则。</summary>
public abstract class ResearchRoute
{
    private protected ResearchRoute() { }
    /// <summary>研究路线的稳定标识。</summary>
    public abstract string Id { get; }
    /// <summary>研究路线的中文显示标题。</summary>
    public abstract string Title { get; }
    /// <summary>是否使用魔法路线的呈现与默认选项。</summary>
    public virtual bool IsMagic => false;
    /// <summary>路线默认选中的研究项目。</summary>
    public virtual ResearchKind DefaultResearch => ResearchKind.Agriculture;
    /// <summary>科技研究路线。</summary>
    public static ResearchRoute Technology { get; } = new TechnologyRoute();
    /// <summary>魔法研究路线。</summary>
    public static ResearchRoute Magic { get; } = new MagicRoute();
    /// <summary>两条路线共同使用的基础研究。</summary>
    public static ResearchRoute Common { get; } = new CommonRoute();
    /// <summary>判断研究节点是否应包含在此路线中。</summary>
    public abstract bool Includes(ResearchDefinition definition);

    private sealed class TechnologyRoute : ResearchRoute
    {
        public override string Id => "technology";
        public override string Title => "科技研究树";

        public override bool Includes(ResearchDefinition definition)
        {
            return !definition.Magic || definition.Shared;
        }
    }

    private sealed class MagicRoute : ResearchRoute
    {
        public override string Id => "magic";
        public override string Title => "魔法研究树";
        public override bool IsMagic => true;
        public override ResearchKind DefaultResearch => ResearchKind.ArcaneArts;

        public override bool Includes(ResearchDefinition definition)
        {
            return definition.Magic || definition.Shared;
        }
    }

    private sealed class CommonRoute : ResearchRoute
    {
        public override string Id => "common";
        public override string Title => "两条路线的共同基础";

        public override bool Includes(ResearchDefinition definition)
        {
            return definition.Shared;
        }
    }
}
