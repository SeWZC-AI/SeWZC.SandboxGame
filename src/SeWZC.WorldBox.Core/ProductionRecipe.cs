using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core;

/// <summary>一种生产技术的定义。</summary>
/// <param name="Research">解锁该生产技术的研究项目。</param>
/// <param name="Facility">执行该配方的设施类别。</param>
/// <param name="FacilityName">生产设施的显示名称。</param>
/// <param name="BuildingCost">建造该设施所需的资源成本。</param>
/// <param name="Input">每批生产实际消耗的原料数量。</param>
/// <param name="Output">生产得到的资源种类。</param>
/// <param name="Yield">每批生产的基础产出数量。</param>
/// <param name="Mana">每批生产需要消耗的魔力，0 表示不消耗魔力。</param>
public sealed record ProductionRecipe(
    Advancement Research,
    BuildingKind Facility,
    string FacilityName,
    ResourceAmounts BuildingCost,
    ResourceAmounts Input,
    ResourceKind Output,
    double Yield,
    double Mana = 0)
{
    /// <summary>解锁该生产技术的研究项目。</summary>
    public Advancement Research { get; } = Research;

    /// <summary>执行该配方的设施类别。</summary>
    public BuildingKind Facility { get; } = Facility;

    /// <summary>生产设施的显示名称。</summary>
    public string FacilityName { get; } = FacilityName;

    /// <summary>建造该设施所需的资源成本。</summary>
    public ResourceAmounts BuildingCost { get; } = BuildingCost;

    /// <summary>每批生产实际消耗的原料数量。</summary>
    public ResourceAmounts Input { get; } = Input;

    /// <summary>生产得到的资源种类。</summary>
    public ResourceKind Output { get; } = Output;

    /// <summary>每批生产的基础产出数量。</summary>
    public double Yield { get; } = Yield;

    /// <summary>每批生产需要消耗的魔力，0 表示不消耗魔力。</summary>
    public double Mana { get; } = Mana;

    /// <summary>该配方实际消耗的原料种类。</summary>
    public ImmutableArray<ResourceKind> InputResources { get; } =
        Enum.GetValues<ResourceKind>().Where(k => Input.Get(k) > 0).ToImmutableArray();

    /// <summary>实际投入、产出和运输过程的说明。</summary>
    public string Description =>
        $"每批原料：{ResourceStock.Format(Input)}{(Mana > 0 ? $"\n施作者魔力消耗：{Mana:0}" : "")}\n每批基础产出：{ResourceStock.Name(Output)} {Yield:0.##}\n工人领料、现场加工，再将产物运回仓库。"
        + (Output == ResourceKind.Food ? "粮食产量受肥力与干旱影响。" : "");
}
