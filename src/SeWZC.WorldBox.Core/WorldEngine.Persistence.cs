using System.Text;
using System.Text.Json;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    public const int MaxSaveBytes = 32 * 1024 * 1024;

    public string ExportJson() => JsonSerializer.Serialize(State, WorldJsonContext.Default.WorldState);

    public static WorldEngine ImportJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaxSaveBytes || Encoding.UTF8.GetByteCount(json) > MaxSaveBytes)
            throw new ArgumentException("存档为空或超过 32 MiB。", nameof(json));
        WorldState state;
        try { state = JsonSerializer.Deserialize(json, WorldJsonContext.Default.WorldState) ?? throw new JsonException("存档内容为空。"); }
        catch (JsonException ex) { throw new ArgumentException("无法读取存档：JSON 格式无效。", nameof(json), ex); }
        ValidateState(state);
        return new WorldEngine(state);
    }

    private static void ValidateState(WorldState state)
    {
        static void Require(bool condition, string description)
        {
            if (!condition) throw new ArgumentException("无效存档：" + description);
        }
        static bool TextValid(string? value, int maximum = 120) => value is not null && value.Length <= maximum && !value.Any(c => char.IsControl(c) && c != '\n');
        static bool FiniteRange(double value, double max) => double.IsFinite(value) && value >= 0 && value <= max;
        static bool StockValid(ResourceStock? stock) => stock is not null && AdvancementRules.Resources.All(kind => FiniteRange(stock.Get(kind), 1_000_000_000));
        bool PositionValid(int x, int y) => x >= 0 && y >= 0 && x < state.Width && y < state.Height;
        int IndexFor(int x, int y) => y * state.Width + x;
        bool WalkablePosition(int x, int y) => PositionValid(x, y) && state.Tiles[y * state.Width + x].IsWalkable;

        Require(state.FormatVersion == 7, "不支持该存档版本。");
        Require(state.Width is >= 32 and <= 256 && state.Height is >= 32 and <= 256, "地图尺寸超出范围。");
        Require(state.Tick is >= 0 and <= 120_000_000 && state.RandomState != 0 && state.NextId is > 0 and < 2_000_000_000, "时间或随机数状态无效。");
        Require(state.Tiles is not null && state.Tiles.Length == state.Width * state.Height, "地图地格数量不匹配。");
        Require(state.Residents is not null && state.Residents.Count <= MaxPopulation && state.Settlements is not null && state.Settlements.Count <= 256 && state.Nations is not null && state.Nations.Count <= 64, "实体数量超出范围。");
        Require(state.Armies is not null && state.Armies.Count <= 64 && state.Diplomacies is not null && state.Diplomacies.Count <= 2016 && state.TradeRoutes is not null && state.TradeRoutes.Count <= 256 && state.Events is not null && state.Events.Count <= 400, "世界记录数量超出范围。");
        Require(state.SimulationVersion == 7 && state.PendingMessages is not null && state.PendingMessages.Count <= MaxPopulation * 2 && state.ArchivedResidents is not null && state.ArchivedResidents.Count <= 256 && state.Society is not null, "认知或社会记录无效。");
        var ids = new HashSet<int>();
        bool IdValid(int id) => id > 0 && id < state.NextId && ids.Add(id);
        foreach (var nation in state.Nations!) Require(nation is not null && IdValid(nation.Id) && TextValid(nation.Name, 40) && nation.Name.Length > 0 && Enum.IsDefined(nation.FoundingRace) && nation.Technology is >= 1 and <= 5 && TextValid(nation.Decision, 240) && StockValid(nation.Resources), "国家数据无效。");
        foreach (var town in state.Settlements!) Require(town is not null && IdValid(town.Id) && TextValid(town.Name, 80) && PositionValid(town.X, town.Y) && town.Housing is >= 0 and <= 20_000 && town.Level is >= 1 and <= 5 && StockValid(town.Resources), "聚落数据无效。");
        foreach (var army in state.Armies!) Require(army is not null && IdValid(army.Id) && PositionValid(army.X, army.Y) && army.Soldiers is >= 0 and <= MaxPopulation && FiniteRange(army.Morale, 100) && FiniteRange(army.Supplies, 1_000_000) && TextValid(army.Status), "军队数据无效。");
        foreach (var resident in state.Residents!) Require(resident is not null && IdValid(resident.Id) && TextValid(resident.Name, 80) && TextValid(resident.Trait, 80) && PositionValid(resident.X, resident.Y) && Enum.IsDefined(resident.Race) && Enum.IsDefined(resident.Profession) && Enum.IsDefined(resident.Activity) && FiniteRange(resident.Age, 1000) && FiniteRange(resident.Health, 100) && FiniteRange(resident.Hunger, 100) && resident.SicknessTicks is >= 0 and <= 10_000, "居民数据无效。");
        var nations = state.Nations!.ToDictionary(n => n.Id);
        var towns = state.Settlements!.ToDictionary(s => s.Id);
        var armies = state.Armies!.ToDictionary(a => a.Id);
        for (var i = 0; i < state.Tiles!.Length; i++)
        {
            var tile = state.Tiles[i];
            Require(tile is not null && Enum.IsDefined(tile.Terrain) && tile.Fertility <= 100 && tile.RoadLevel <= 3 && FiniteRange(tile.ResourceAmount, 1_000_000) && tile.FireTicks is >= 0 and <= 10_000 && tile.DroughtTicks is >= 0 and <= 10_000, "地格数据无效。");
            Require(Enum.IsDefined(tile!.Improvement) && FiniteRange(tile.Harvested, 1_000_000_000) && tile.LastHarvestTick >= 0 && tile.LastHarvestTick <= state.Tick
                && FiniteRange(tile.DepositAmount, 1_000_000) && (tile.Deposit.HasValue ? DepositResearch(tile.Deposit.Value).HasValue : tile.DepositAmount == 0 && !tile.DepositDiscovered)
                && (tile.Improvement != LandImprovement.MountainPass || tile.Terrain == TerrainType.Mountain)
                && (tile.Improvement != LandImprovement.Bridge || tile.Terrain is TerrainType.River or TerrainType.Water), "地块改造或矿藏状态无效。");
            Require(tile!.NationId == 0 || nations.ContainsKey(tile.NationId), "地格引用了不存在的国家。");
            Require(tile.SettlementId == 0 || towns.TryGetValue(tile.SettlementId, out var town) && town.X == i % state.Width && town.Y == i / state.Width, "聚落地格引用无效。");
        }
        foreach (var nation in state.Nations) Require(towns.TryGetValue(nation.CapitalId, out var capital) && capital.NationId == nation.Id, "国家首都引用无效。");
        foreach (var town in state.Settlements) Require(nations.ContainsKey(town.NationId) && WalkablePosition(town.X, town.Y) && state.Tiles[town.Y * state.Width + town.X].SettlementId == town.Id && state.Tiles[town.Y * state.Width + town.X].NationId == town.NationId, "聚落归属或位置无效。");
        foreach (var resident in state.Residents)
        {
            ValidateResidentV2(resident, state.Tick, state.Width, state.Height);
            Require(nations.ContainsKey(resident.NationId) && towns.TryGetValue(resident.SettlementId, out var home) && home.NationId == resident.NationId && CanTraverse(state.Tiles[IndexFor(resident.X, resident.Y)], resident.TravelMode), "居民归属或位置无效。");
            Require(resident.ArmyId == 0 || armies.TryGetValue(resident.ArmyId, out var army) && army.NationId == resident.NationId, "居民军队引用无效。");
        }
        foreach (var army in state.Armies) Require(Enum.IsDefined(army.KnownDiplomacy) && army.LastOrderTick >= 0 && army.LastOrderTick <= state.Tick
            && army.LastOrderFactId >= 0 && army.LastOrderFactId < state.NextId
            && PositionValid(army.TargetX, army.TargetY) && army.MoveDurationTicks is >= 1 and <= 100 && nations.ContainsKey(army.NationId) && army.TargetNationId > 0 && army.NationId != army.TargetNationId && WalkablePosition(army.X, army.Y), "军队国家、位置或军令记录无效。");
        var pairs = new HashSet<(int, int)>();
        foreach (var relation in state.Diplomacies!) Require(relation is not null && nations.ContainsKey(relation.FirstNationId) && nations.ContainsKey(relation.SecondNationId)
            && relation.FirstNationId != relation.SecondNationId && Enum.IsDefined(relation.Status)
            && relation.FirstOpinion is >= -100 and <= 100 && relation.SecondOpinion is >= -100 and <= 100
            && relation.Opinion == (int)Math.Round((relation.FirstOpinion + relation.SecondOpinion) / 2d, MidpointRounding.AwayFromZero)
            && pairs.Add((Math.Min(relation.FirstNationId, relation.SecondNationId), Math.Max(relation.FirstNationId, relation.SecondNationId))), "外交关系无效或重复。");
        foreach (var route in state.TradeRoutes!) Require(route is not null && towns.ContainsKey(route.FromSettlementId) && towns.ContainsKey(route.ToSettlementId) && route.FromSettlementId != route.ToSettlementId && route.TravelTicks is > 0 and <= 200_000 && route.RemainingTicks > 0 && route.RemainingTicks <= route.TravelTicks && FiniteRange(route.FoodCargo, 1_000_000), "贸易路线无效。");
        foreach (var entry in state.Events!) Require(entry is not null && Enum.IsDefined(entry.Kind) && Enum.IsDefined(entry.Importance) && entry.Tick >= 0 && entry.Tick <= state.Tick && TextValid(entry.Message, 1000) && (entry.X == -1 && entry.Y == -1 || PositionValid(entry.X, entry.Y)), "历史记录无效。");
        foreach (var archived in state.ArchivedResidents!)
        {
            Require(archived is not null && IdValid(archived.Id), "人物档案编号无效。");
            ValidateResidentV2(archived!, state.Tick, state.Width, state.Height);
        }
        foreach (var town in state.Settlements)
        {
            Require(town.PublicKnowledge is not null && town.PublicKnowledge.Count <= 24 && town.Petitions is not null && town.Petitions.Count <= 128 && town.FertilityBoostTicks is >= 0 and <= 100_000 && town.ShieldTicks is >= 0 and <= 100_000, "聚落认知记录无效。");
            foreach (var fact in town.PublicKnowledge!) ValidateFactV2(fact, state.Tick, state.Width, state.Height);
            foreach (var petition in town.Petitions!) Require(petition is not null && Enum.IsDefined(petition.Topic) && double.IsFinite(petition.Value) && double.IsFinite(petition.Weight) && petition.Weight >= 0 && petition.ObservedTick >= 0 && petition.ObservedTick <= petition.ReceivedTick && petition.ReceivedTick <= state.Tick, "递送意见无效。");
        }
        foreach (var pending in state.PendingMessages!)
        {
            Require(pending is not null && pending.SenderId > 0 && pending.RecipientId > 0 && pending.DeliverTick >= 0 && pending.DeliverTick <= state.Tick + 1000 && pending.Facts is not null && pending.Facts.Count <= 4, "待递送口信无效。");
            foreach (var fact in pending!.Facts!) ValidateFactV2(fact, state.Tick, state.Width, state.Height);
        }
        var snapshots = new Dictionary<int, AgentFact>();
        foreach (var fact in state.Residents.Concat(state.ArchivedResidents!).SelectMany(r => r.Agent.Memory.Concat(r.Agent.CarriedMessages))
            .Concat(state.Settlements.SelectMany(s => s.PublicKnowledge)).Concat(state.PendingMessages.SelectMany(p => p.Facts)))
        {
            Require(fact.Id > 0 && fact.Id < state.NextId, "信息快照编号无效。");
            if (snapshots.TryGetValue(fact.Id, out var prior)) Require(SameFactSnapshot(prior, fact), "相同信息编号包含冲突内容。");
            else snapshots[fact.Id] = fact;
        }
        ValidateWorldRules(state.Rules);
        foreach (var town in state.Settlements)
            Require(TextValid(town.DevelopmentGoal, 160) && TextValid(town.DevelopmentBlocker, 240)
                && FiniteRange(town.Unrest, 100) && town.LastDevelopmentTick >= 0 && town.LastDevelopmentTick <= state.Tick
                && town.LastPoliticalChangeTick >= 0 && town.LastPoliticalChangeTick <= state.Tick, "发展记录无效。");
        foreach (var relation in state.Diplomacies)
            Require(relation.LastChangedTick >= 0 && relation.LastChangedTick <= state.Tick && relation.LastContactTick >= 0
                && relation.LastContactTick <= state.Tick && relation.LastEvaluatedTick >= 0 && relation.LastEvaluatedTick <= state.Tick
                && relation.LastEventId >= 0 && relation.LastEventId < state.NextId && TextValid(relation.Reason, 240)
                && relation.AllianceOfferTick >= 0 && relation.AllianceOfferTick <= state.Tick
                && (relation.AllianceOfferNationId == 0 || relation.AllianceOfferNationId == relation.FirstNationId || relation.AllianceOfferNationId == relation.SecondNationId), "外交过程记录无效。");
        ValidateSocietyState(state);
        ValidateStories(state);
    }
}
