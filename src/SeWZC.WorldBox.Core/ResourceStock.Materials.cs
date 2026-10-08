namespace SeWZC.WorldBox.Core;

public readonly partial record struct ResourceStock
{
    /// <summary>从日用粮水及共享的其他资源构造库存。</summary>
    /// <param name="food">粮食数量。</param>
    /// <param name="water">饮水数量。</param>
    /// <param name="materials">其他资源；空引用表示全零。</param>
    private ResourceStock(double food, double water, Materials? materials)
    {
        Food = food;
        Water = water;
        _materials = materials;
    }

    /// <inheritdoc />
    public bool Equals(ResourceStock other)
    {
        return Food.Equals(other.Food) && Water.Equals(other.Water)
                                       && (ReferenceEquals(_materials, other._materials)
                                           || (_materials ?? Materials.Empty).Equals(
                                               other._materials ?? Materials.Empty));
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(Food, Water, _materials ?? Materials.Empty);
    }

    private Materials? ReplaceMaterial(ResourceKind kind, double value)
    {
        // 数值相同的负零或不同 NaN 仍可能具有不同位模式，保留原有字段读写语义。
        if (BitConverter.DoubleToInt64Bits(Get(kind)) == BitConverter.DoubleToInt64Bits(value))
            return _materials;
        var materials = _materials ?? Materials.Empty;
        var changed = kind switch
        {
            ResourceKind.Wood => materials with { Wood = value },
            ResourceKind.Stone => materials with { Stone = value },
            ResourceKind.Ore => materials with { Ore = value },
            ResourceKind.Alloy => materials with { Alloy = value },
            ResourceKind.EnergyCells => materials with { EnergyCells = value },
            ResourceKind.Crystals => materials with { Crystals = value },
            ResourceKind.Coal => materials with { Coal = value },
            ResourceKind.Oil => materials with { Oil = value },
            ResourceKind.RareEarth => materials with { RareEarth = value },
            ResourceKind.Boats => materials with { Boats = value },
            ResourceKind.Aircraft => materials with { Aircraft = value },
            ResourceKind.Tools => materials with { Tools = value },
            ResourceKind.Medicine => materials with { Medicine = value },
            ResourceKind.Ammunition => materials with { Ammunition = value },
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        return value != 0 ? changed : NormalizeMaterials(changed);
    }

    private static Materials? NormalizeMaterials(Materials materials)
    {
        return materials.IsEmpty ? null : materials;
    }

    private sealed record Materials
    {
        internal static readonly Materials Empty = new();

        public double Wood { get; init; }
        public double Stone { get; init; }
        public double Ore { get; init; }
        public double Alloy { get; init; }
        public double EnergyCells { get; init; }
        public double Crystals { get; init; }
        public double Coal { get; init; }
        public double Oil { get; init; }
        public double RareEarth { get; init; }
        public double Boats { get; init; }
        public double Aircraft { get; init; }
        public double Tools { get; init; }
        public double Medicine { get; init; }
        public double Ammunition { get; init; }

        // 仅折叠正零，负零仍由实际记录保存；值相等由资源金额判断。
        internal bool IsEmpty =>
            BitConverter.DoubleToInt64Bits(Wood) == 0
            && BitConverter.DoubleToInt64Bits(Stone) == 0
            && BitConverter.DoubleToInt64Bits(Ore) == 0
            && BitConverter.DoubleToInt64Bits(Alloy) == 0
            && BitConverter.DoubleToInt64Bits(EnergyCells) == 0
            && BitConverter.DoubleToInt64Bits(Crystals) == 0
            && BitConverter.DoubleToInt64Bits(Coal) == 0
            && BitConverter.DoubleToInt64Bits(Oil) == 0
            && BitConverter.DoubleToInt64Bits(RareEarth) == 0
            && BitConverter.DoubleToInt64Bits(Boats) == 0
            && BitConverter.DoubleToInt64Bits(Aircraft) == 0
            && BitConverter.DoubleToInt64Bits(Tools) == 0
            && BitConverter.DoubleToInt64Bits(Medicine) == 0
            && BitConverter.DoubleToInt64Bits(Ammunition) == 0;

        internal bool Within(double maximum)
        {
            return Wood >= 0 && Wood <= maximum
                             && Stone >= 0 && Stone <= maximum
                             && Ore >= 0 && Ore <= maximum
                             && Alloy >= 0 && Alloy <= maximum
                             && EnergyCells >= 0 && EnergyCells <= maximum
                             && Crystals >= 0 && Crystals <= maximum
                             && Coal >= 0 && Coal <= maximum
                             && Oil >= 0 && Oil <= maximum
                             && RareEarth >= 0 && RareEarth <= maximum
                             && Boats >= 0 && Boats <= maximum
                             && Aircraft >= 0 && Aircraft <= maximum
                             && Tools >= 0 && Tools <= maximum
                             && Medicine >= 0 && Medicine <= maximum
                             && Ammunition >= 0 && Ammunition <= maximum;
        }
    }
}
