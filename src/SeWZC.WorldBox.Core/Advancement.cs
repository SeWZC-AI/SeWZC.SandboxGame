namespace SeWZC.WorldBox.Core;

/// <summary>生产技术的研究要求、设施成本、每批原料与产出，以及可选的魔力消耗。</summary>
/// <param name="Research">解锁该生产技术的研究项目。</param>
/// <param name="Name">生产技术名称。</param>
/// <param name="Stage">所属发展阶段名称。</param>
/// <param name="Magic">该配方是否属于魔法路线。</param>
/// <param name="Prerequisites">除本研究外须掌握的前置研究。</param>
/// <param name="ResearchCost">启动对应研究所需的资源成本。</param>
/// <param name="Facility">执行该配方的设施类别。</param>
/// <param name="FacilityName">生产设施的显示名称。</param>
/// <param name="BuildingCost">建造该设施所需的资源成本。</param>
/// <param name="Input">每批生产实际消耗的原料数量。</param>
/// <param name="Output">生产得到的资源种类。</param>
/// <param name="Yield">每批生产的基础产出数量。</param>
/// <param name="Mana">每批生产需要消耗的魔力，0 表示不消耗魔力。</param>
public sealed record Advancement(
    ResearchKind Research,
    string Name,
    string Stage,
    bool Magic,
    ResearchKind[] Prerequisites,
    ResourceStock ResearchCost,
    BuildingKind Facility,
    string FacilityName,
    ResourceStock BuildingCost,
    ResourceStock Input,
    ResourceKind Output,
    double Yield,
    double Mana = 0)
{
    /// <summary>该配方需要数量大于零的原料种类。</summary>
    public IReadOnlyList<ResourceKind> InputResources { get; } =
        Array.AsReadOnly(Enum.GetValues<ResourceKind>().Where(k => Input.Get(k) > 0).ToArray());
}
