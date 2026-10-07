using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core;

/// <summary>一种生产技术的定义。</summary>
/// <param name="research">解锁该生产技术的研究项目。</param>
/// <param name="name">生产技术名称。</param>
/// <param name="stage">所属发展阶段名称。</param>
/// <param name="magic">该配方是否属于魔法路线。</param>
/// <param name="prerequisites">除本研究外须掌握的前置研究。</param>
/// <param name="researchCost">启动对应研究所需的资源成本。</param>
/// <param name="facility">执行该配方的设施类别。</param>
/// <param name="facilityName">生产设施的显示名称。</param>
/// <param name="buildingCost">建造该设施所需的资源成本。</param>
/// <param name="input">每批生产实际消耗的原料数量。</param>
/// <param name="output">生产得到的资源种类。</param>
/// <param name="yield">每批生产的基础产出数量。</param>
/// <param name="mana">每批生产需要消耗的魔力，0 表示不消耗魔力。</param>
public sealed class Advancement(
    ResearchKind research,
    string name,
    string stage,
    bool magic,
    IReadOnlyList<ResearchKind> prerequisites,
    ResourceAmounts researchCost,
    BuildingKind facility,
    string facilityName,
    ResourceAmounts buildingCost,
    ResourceAmounts input,
    ResourceKind output,
    double yield,
    double mana = 0)
{
    /// <summary>解锁该生产技术的研究项目。</summary>
    public ResearchKind Research { get; } = research;
    /// <summary>生产技术名称。</summary>
    public string Name { get; } = name;
    /// <summary>所属发展阶段名称。</summary>
    public string Stage { get; } = stage;
    /// <summary>该配方是否属于魔法路线。</summary>
    public bool Magic { get; } = magic;
    /// <summary>除本研究外须掌握的前置研究。</summary>
    public ImmutableArray<ResearchKind> Prerequisites { get; } = prerequisites.ToImmutableArray();
    /// <summary>启动对应研究所需的资源成本。</summary>
    public ResourceAmounts ResearchCost { get; } = researchCost;
    /// <summary>执行该配方的设施类别。</summary>
    public BuildingKind Facility { get; } = facility;
    /// <summary>生产设施的显示名称。</summary>
    public string FacilityName { get; } = facilityName;
    /// <summary>建造该设施所需的资源成本。</summary>
    public ResourceAmounts BuildingCost { get; } = buildingCost;
    /// <summary>每批生产实际消耗的原料数量。</summary>
    public ResourceAmounts Input { get; } = input;
    /// <summary>生产得到的资源种类。</summary>
    public ResourceKind Output { get; } = output;
    /// <summary>每批生产的基础产出数量。</summary>
    public double Yield { get; } = yield;
    /// <summary>每批生产需要消耗的魔力，0 表示不消耗魔力。</summary>
    public double Mana { get; } = mana;

    /// <summary>该配方实际消耗的原料种类。</summary>
    public ImmutableArray<ResourceKind> InputResources { get; } =
        Enum.GetValues<ResourceKind>().Where(k => input.Get(k) > 0).ToImmutableArray();
}
