using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>按资源类别记录的不可变库存。</summary>
public readonly partial record struct ResourceStock
{
    // 粮水每天变化；其他资源共享不可变记录，居民快照无需反复复制全部库存金额。
    private readonly Materials? _materials;

    internal bool HasMaterials => _materials is not null;

    /// <summary>饮水数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Water { get; init; }

    /// <summary>合金数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Alloy
    {
        get => _materials?.Alloy ?? 0;
        init => _materials = ReplaceMaterial(ResourceKind.Alloy, value);
    }

    /// <summary>动力单元数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double EnergyCells
    {
        get => _materials?.EnergyCells ?? 0;
        init => _materials = ReplaceMaterial(ResourceKind.EnergyCells, value);
    }

    /// <summary>魔晶数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Crystals
    {
        get => _materials?.Crystals ?? 0;
        init => _materials = ReplaceMaterial(ResourceKind.Crystals, value);
    }

    /// <summary>煤数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Coal
    {
        get => _materials?.Coal ?? 0;
        init => _materials = ReplaceMaterial(ResourceKind.Coal, value);
    }

    /// <summary>石油数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Oil
    {
        get => _materials?.Oil ?? 0;
        init => _materials = ReplaceMaterial(ResourceKind.Oil, value);
    }

    /// <summary>稀土数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double RareEarth
    {
        get => _materials?.RareEarth ?? 0;
        init => _materials = ReplaceMaterial(ResourceKind.RareEarth, value);
    }

    /// <summary>舟船数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Boats
    {
        get => _materials?.Boats ?? 0;
        init => _materials = ReplaceMaterial(ResourceKind.Boats, value);
    }

    /// <summary>运输机数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Aircraft
    {
        get => _materials?.Aircraft ?? 0;
        init => _materials = ReplaceMaterial(ResourceKind.Aircraft, value);
    }

    /// <summary>工具数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Tools
    {
        get => _materials?.Tools ?? 0;
        init => _materials = ReplaceMaterial(ResourceKind.Tools, value);
    }

    /// <summary>药品数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Medicine
    {
        get => _materials?.Medicine ?? 0;
        init => _materials = ReplaceMaterial(ResourceKind.Medicine, value);
    }

    /// <summary>弹药数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Ammunition
    {
        get => _materials?.Ammunition ?? 0;
        init => _materials = ReplaceMaterial(ResourceKind.Ammunition, value);
    }

    // 省略零数量后，缺失字段必须还原为零，因此不能使用非零属性初值。
    /// <summary>粮食数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Food { get; init; }

    /// <summary>木材数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Wood
    {
        get => _materials?.Wood ?? 0;
        init => _materials = ReplaceMaterial(ResourceKind.Wood, value);
    }

    /// <summary>石材数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Stone
    {
        get => _materials?.Stone ?? 0;
        init => _materials = ReplaceMaterial(ResourceKind.Stone, value);
    }

    /// <summary>矿石数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Ore
    {
        get => _materials?.Ore ?? 0;
        init => _materials = ReplaceMaterial(ResourceKind.Ore, value);
    }
}
