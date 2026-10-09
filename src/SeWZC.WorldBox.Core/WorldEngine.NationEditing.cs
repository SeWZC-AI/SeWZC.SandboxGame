using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    /// <summary>修改国家的旗帜和领土显示颜色，并记录玩家编辑。</summary>
    /// <param name="nationId">国家 ID。</param>
    /// <param name="colorArgb">新的 ARGB 编码颜色。</param>
    public void SetNationColor(int nationId, uint colorArgb)
    {
        if (!_nations.TryGetValue(nationId, out var nation))
            throw new ArgumentException("国家不存在。", nameof(nationId));
        nation.Replace(nation.Value with { ColorArgb = colorArgb | 0xFF000000 });
    }

    /// <summary>将国家的古代工具等级设为 1 至 5，独立于聚落研究的完成情况。</summary>
    /// <param name="nationId">国家 ID。</param>
    /// <param name="level">古代工具等级，范围为 1 至 5。</param>
    public void SetNationTechnology(int nationId, int level)
    {
        if (!_nations.TryGetValue(nationId, out var nation))
            throw new ArgumentException("国家不存在。", nameof(nationId));
        if (level is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(level), "古代工具技术等级须在 1 到 5 之间。");
        nation.Replace(nation.Value with { Technology = level });
        AddEvent(WorldEventKind.Editor, $"{nation.Value.Name}的工具技术调整至 {level} 级。");
    }

    /// <summary>绘制陆地归属；聚落中心被覆盖时，一并转移整处聚落及相关实体。</summary>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    /// <param name="nationId">接收领土及笔刷覆盖聚落的国家 ID。</param>
    /// <param name="radius">领土笔刷作用半径，以地格为单位。</param>
    public void TransferTerritory(int x, int y, int nationId, int radius = 3)
    {
        if (!_nations.TryGetValue(nationId, out var nation))
            throw new ArgumentException("请选择存在的目标国家。", nameof(nationId));
        if (!InBounds(x, y))
            return;
        radius = Math.Clamp(radius, 0, 32);
        var indexes = Circle(x, y, radius).ToHashSet();
        foreach (var town in Current.Settlements.Where(s => s.Value.NationId != nationId && indexes.Contains(Index(s.Value.X, s.Value.Y)))
                     .ToArray())
            TransferSettlementOwnership(town, nationId);
        foreach (var index in indexes)
            if (Current.Tiles[index].Value.NationId != nationId && Current.Tiles[index].Value.SettlementId == 0)
            {
                Current.Tiles[index].Replace(Current.Tiles[index].Value.WithNationId(0));
                Current.Tiles[index].Replace(Current.Tiles[index].Value.WithClaimedSettlementId(0));
            }

        ReconcileConnectedClaims();
        // 领土笔刷可延伸已有连通地盘，但不能为城镇创建远处悬空飞地。
        foreach (var town in Current.Settlements.Where(t => t.Value.NationId == nationId && !t.Value.FoundationPending)
                     .OrderBy(t => t.Value.Id))
        {
            var queue = new Queue<int>();
            var seen = new HashSet<int>();
            var root = Index(town.Value.X, town.Value.Y);
            queue.Enqueue(root);
            seen.Add(root);
            while (queue.TryDequeue(out var current))
                foreach (var (dx, dy) in Directions)
                {
                    var xx = current % Current.Width + dx;
                    var yy = current / Current.Width + dy;
                    if (!InBounds(xx, yy))
                        continue;
                    var next = Index(xx, yy);
                    var tile = Current.Tiles[next];
                    if (seen.Contains(next) || !tile.Value.IsWalkable || IsWaterTerrain(tile.Value.Terrain)
                        || (tile.Value.ClaimedSettlementId != 0 && tile.Value.ClaimedSettlementId != town.Value.Id))
                        continue;
                    if (tile.Value.ClaimedSettlementId != town.Value.Id && !indexes.Contains(next))
                        continue;
                    tile.Replace(tile.Value with { NationId = nationId, ClaimedSettlementId = town.Value.Id });
                    seen.Add(next);
                    queue.Enqueue(next);
                }
        }

        Reindex();
        RemoveEmptyNations();
        InitializeSociety();
        RefreshTotals();
        AddEvent(WorldEventKind.Editor, $"{nation.Value.Name}的领土边界已调整，圈内聚落随领土转属。", x, y);
    }

    /// <summary>将至少拥有两处聚落的国家中的一处聚落独立为新国家，并返回国家 ID。</summary>
    /// <param name="settlementId">聚落 ID。</param>
    /// <param name="name">新的名称。</param>
    public int SplitSettlement(int settlementId, string name)
    {
        if (!_settlements.TryGetValue(settlementId, out var town))
            throw new ArgumentException("聚落不存在。", nameof(settlementId));
        name = (name ?? "").Trim();
        if (name.Length is < 1 or > 40 || name.Any(char.IsControl))
            throw new ArgumentException("国名须为 1–40 个可见字符。", nameof(name));
        if (Current.Nations.Count >= 64)
            throw new InvalidOperationException("国家数量已达上限。");
        var parent = _nations[town.Value.NationId];
        if (Current.Settlements.Count(s => s.Value.NationId == parent.Value.Id) < 2)
            throw new InvalidOperationException("拆分需要原国家至少拥有两个聚落。");
        var nation = new StateReference<Nation>(new Nation
        {
            Id = NewId(),
            Name = name,
            CapitalId = town.Value.Id,
            Technology = parent.Value.Technology,
            DevelopmentFocus = parent.Value.DevelopmentFocus,
            ColorArgb = NationColors[Current.Nations.Count % NationColors.Length],
            FoundingRace = parent.Value.FoundingRace,
            Decision = "独立建国：储备资源，建立外交关系",
        });
        foreach (var other in Current.Nations)
            Current.Diplomacies =
                Current.Diplomacies.Add(new DiplomaticRelation
                {
                    FirstNationId = other.Value.Id,
                    SecondNationId = nation.Value.Id,
                });
        Current.Nations.Add(nation);
        _nations[nation.Value.Id] = nation;
        TransferSettlementOwnership(town, nation.Value.Id);
        Current.Tiles[Index(town.Value.X, town.Value.Y)].Replace(Current.Tiles[Index(town.Value.X, town.Value.Y)].Value.WithNationId(nation.Value.Id));
        Reindex();
        InitializeSociety();
        RefreshTotals();
        AddEvent(WorldEventKind.Editor, $"{town.Value.Name}从{parent.Value.Name}独立，成立{nation.Value.Name}。", town.Value.X, town.Value.Y);
        return nation.Value.Id;
    }

    private void TransferSettlementOwnership(StateReference<Settlement> town, int targetNationId)
    {
        var previousId = town.Value.NationId;
        if (previousId == targetNationId)
            return;
        var previousNation = _nations[previousId];
        town.Replace(town.Value with { NationId = targetNationId });
        foreach (var ground in Current.Tiles)
            if (ground.Value.ClaimedSettlementId == town.Value.Id)
                ground.Replace(ground.Value.WithNationId(targetNationId));
        Current.Tiles[Index(town.Value.X, town.Value.Y)].Replace(Current.Tiles[Index(town.Value.X, town.Value.Y)].Value.WithNationId(targetNationId));
        var remainingHome = Current.Settlements.FirstOrDefault(s => s.Value.NationId == previousId);
        if (previousNation.Value.CapitalId == town.Value.Id)
            previousNation.Replace(previousNation.Value with { CapitalId = remainingHome?.Value.Id ?? 0 });
        if (remainingHome is not null)
        {
            foreach (var resident in Current.Residents.Where(r => r.SettlementId == town.Value.Id))
                if (resident.ArmyId != 0)
                    resident.Replace(resident.Value with { SettlementId = remainingHome.Value.Id });
                else
                    resident.Replace(resident.Value with { NationId = targetNationId });
        }
        else
        {
            foreach (var army in Current.Armies.Where(a => a.Value.NationId == previousId).ToArray())
                DisbandArmy(army);
            foreach (var resident in Current.Residents.Where(r => r.SettlementId == town.Value.Id))
                resident.Replace(resident.Value with { NationId = targetNationId });
        }
    }
}
