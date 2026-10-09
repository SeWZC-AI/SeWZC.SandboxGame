using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

public sealed partial record Tile
{
    /// <summary>当前地块改良，影响通行和资源生产。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public LandImprovement Improvement { get; init; }

    /// <summary>地下矿藏种类，空值表示没有矿藏。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResourceKind? Deposit { get; init; }

    /// <summary>尚未开采的矿藏资源数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double DepositAmount { get; init; }

    /// <summary>观察者地图上的矿藏发现标记；居民仍须依据本地知识勘探。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool DepositDiscovered { get; init; }

    /// <summary>最近记录采收量的模拟 tick 序。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long LastHarvestTick { get; init; }

    /// <summary>最近记录的模拟日内，累计采收的资源数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Harvested { get; init; }
    /// <summary>根据种子和位置重新生成矿藏，不修改原地格。</summary>
    /// <param name="seed">地图种子。</param>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    internal Tile WithGeneratedDeposit(int seed, int x, int y)
    {
        ResourceKind? deposit = null;
        double amount = 0;
        if (!WorldEngine.IsWaterTerrain(Terrain))
        {
            var hash = unchecked((uint)(x * 374761393 + y * 668265263 + seed * 31 + 937));
            hash = (hash ^ (hash >> 13)) * 1274126177;
            deposit = (hash % 43) switch
            {
                0 or 1 => ResourceKind.Coal,
                2 => ResourceKind.Oil,
                3 => ResourceKind.RareEarth,
                _ => null,
            };
            if (deposit.HasValue)
                amount = 120 + hash % 181;
        }

        return Deposit == deposit && DepositAmount == amount && !DepositDiscovered
            ? this : this with { Deposit = deposit, DepositAmount = amount, DepositDiscovered = false };
    }
}
