namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    public void SetNationColor(int nationId, uint colorArgb)
    {
        if (!_nations.TryGetValue(nationId, out var nation)) throw new ArgumentException("国家不存在。", nameof(nationId));
        nation.ColorArgb = colorArgb | 0xFF000000;
    }

    public void SetNationTechnology(int nationId, int level)
    {
        if (!_nations.TryGetValue(nationId, out var nation)) throw new ArgumentException("国家不存在。", nameof(nationId));
        if (level is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(level), "古代工具技术等级须在 1 到 5 之间。");
        nation.Technology = level;
        AddEvent(WorldEventKind.Editor, $"{nation.Name}的工具技术调整至 {level} 级。");
    }

    /// <summary>Paints ownership on land. Towns whose centers are painted transfer as complete entities.</summary>
    public void TransferTerritory(int x, int y, int nationId, int radius = 3)
    {
        if (!_nations.TryGetValue(nationId, out var nation)) throw new ArgumentException("请选择存在的目标国家。", nameof(nationId));
        if (!InBounds(x, y)) return;
        radius = Math.Clamp(radius, 0, 32);
        var indexes = Circle(x, y, radius).ToHashSet();
        foreach (var index in indexes)
            if (State.Tiles[index].IsWalkable) State.Tiles[index].NationId = nationId;
        foreach (var town in State.Settlements.Where(s => s.NationId != nationId && indexes.Contains(Index(s.X, s.Y))).ToArray()) TransferSettlementOwnership(town, nationId);
        _armyPaths.Clear(); _armyTargets.Clear();
        Reindex(); RemoveEmptyNations(); InitializeSociety(); RefreshTotals();
        AddEvent(WorldEventKind.Editor, $"{nation.Name}的领土边界已调整，圈内聚落随领土转属。", x, y);
    }

    /// <summary>Creates a sovereign nation from one town of a country with at least two towns.</summary>
    public int SplitSettlement(int settlementId, string name)
    {
        if (!_settlements.TryGetValue(settlementId, out var town)) throw new ArgumentException("聚落不存在。", nameof(settlementId));
        name = (name ?? "").Trim();
        if (name.Length is < 1 or > 40 || name.Any(char.IsControl)) throw new ArgumentException("国名须为 1–40 个可见字符。", nameof(name));
        if (State.Nations.Count >= 64) throw new InvalidOperationException("国家数量已达上限。");
        var parent = _nations[town.NationId];
        if (State.Settlements.Count(s => s.NationId == parent.Id) < 2) throw new InvalidOperationException("拆分需要原国家至少拥有两个聚落。");
        var nation = new Nation { Id = NewId(), Name = name, CapitalId = town.Id, Technology = parent.Technology, ColorArgb = NationColors[State.Nations.Count % NationColors.Length], FoundingRace = parent.FoundingRace, Decision = "独立建国：储备资源，建立外交关系" };
        foreach (var other in State.Nations) State.Diplomacies.Add(new DiplomaticRelation { FirstNationId = other.Id, SecondNationId = nation.Id });
        State.Nations.Add(nation); _nations[nation.Id] = nation;
        TransferSettlementOwnership(town, nation.Id);
        foreach (var index in Circle(town.X, town.Y, 12))
        {
            var tile = State.Tiles[index];
            if (tile.IsWalkable && tile.NationId == parent.Id && (tile.SettlementId == 0 || tile.SettlementId == town.Id)) tile.NationId = nation.Id;
        }
        State.Tiles[Index(town.X, town.Y)].NationId = nation.Id;
        _armyPaths.Clear(); _armyTargets.Clear();
        Reindex(); InitializeSociety(); RefreshTotals();
        AddEvent(WorldEventKind.Editor, $"{town.Name}从{parent.Name}独立，成立{nation.Name}。", town.X, town.Y);
        return nation.Id;
    }

    private void TransferSettlementOwnership(Settlement town, int targetNationId)
    {
        var previousId = town.NationId;
        if (previousId == targetNationId) return;
        var previousNation = _nations[previousId];
        town.NationId = targetNationId;
        State.Tiles[Index(town.X, town.Y)].NationId = targetNationId;
        var remainingHome = State.Settlements.FirstOrDefault(s => s.NationId == previousId);
        if (previousNation.CapitalId == town.Id) previousNation.CapitalId = remainingHome?.Id ?? 0;
        if (remainingHome is not null)
        {
            foreach (var resident in State.Residents.Where(r => r.SettlementId == town.Id))
            {
                if (resident.ArmyId != 0) resident.SettlementId = remainingHome.Id;
                else resident.NationId = targetNationId;
            }
        }
        else
        {
            foreach (var army in State.Armies.Where(a => a.NationId == previousId).ToArray()) DisbandArmy(army);
            foreach (var resident in State.Residents.Where(r => r.SettlementId == town.Id)) resident.NationId = targetNationId;
        }
    }
}
