using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core;

/// <summary>生产技术的研究要求、设施成本、每批原料与产出，以及可选的魔力消耗。</summary>
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
    ResearchKind research, string name, string stage, bool magic, IReadOnlyList<ResearchKind> prerequisites,
    ResourceAmounts researchCost, BuildingKind facility, string facilityName, ResourceAmounts buildingCost,
    ResourceAmounts input, ResourceKind output, double yield, double mana = 0)
{
    public ResearchKind Research { get; } = research;
    public string Name { get; } = name;
    public string Stage { get; } = stage;
    public bool Magic { get; } = magic;
    public ImmutableArray<ResearchKind> Prerequisites { get; } = prerequisites.ToImmutableArray();
    public ResourceAmounts ResearchCost { get; } = researchCost;
    public BuildingKind Facility { get; } = facility;
    public string FacilityName { get; } = facilityName;
    public ResourceAmounts BuildingCost { get; } = buildingCost;
    public ResourceAmounts Input { get; } = input;
    public ResourceKind Output { get; } = output;
    public double Yield { get; } = yield;
    public double Mana { get; } = mana;
    /// <summary>该配方实际消耗的原料种类。</summary>
    public ImmutableArray<ResourceKind> InputResources { get; } =
        Enum.GetValues<ResourceKind>().Where(k => input.Get(k) > 0).ToImmutableArray();
}
