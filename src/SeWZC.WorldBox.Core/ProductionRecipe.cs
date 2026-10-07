using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core;

/// <summary>一种生产技术的定义。</summary>
/// <param name="research">解锁该生产技术的研究项目。</param>
/// <param name="facility">执行该配方的设施类别。</param>
/// <param name="facilityName">生产设施的显示名称。</param>
/// <param name="buildingCost">建造该设施所需的资源成本。</param>
/// <param name="input">每批生产实际消耗的原料数量。</param>
/// <param name="output">生产得到的资源种类。</param>
/// <param name="yield">每批生产的基础产出数量。</param>
/// <param name="mana">每批生产需要消耗的魔力，0 表示不消耗魔力。</param>
public sealed class ProductionRecipe(
    Advancement research,
    BuildingKind facility,
    string facilityName,
    ResourceAmounts buildingCost,
    ResourceAmounts input,
    ResourceKind output,
    double yield,
    double mana = 0)
{
    /// <summary>解锁该生产技术的研究项目。</summary>
    public Advancement Research { get; } = research;

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

    /// <summary>实际投入、产出和运输过程的说明。</summary>
    public string Description =>
        $"每批原料：{ResourceStock.Format(Input)}{(Mana > 0 ? $"\n施作者魔力消耗：{Mana:0}" : "")}\n每批基础产出：{ResourceStock.Name(Output)} {Yield:0.##}\n工人领料、现场加工，再将产物运回仓库。"
        + (Output == ResourceKind.Food ? "粮食产量受肥力与干旱影响。" : "");
}
