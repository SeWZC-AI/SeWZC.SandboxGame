using System.Text;
using System.Text.Json;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    public const int MaxSaveBytes = 64 * 1024 * 1024;

    /// <summary>同步把当前世界序列化为 JSON，用于存档或编辑恢复点。</summary>
    public string ExportJson() => JsonSerializer.Serialize(State, WorldJsonContext.Default.WorldState);

    private static readonly WorldJsonContext StreamingJson = new(new JsonSerializerOptions(WorldJsonContext.Default.Options)
        { DefaultBufferSize = 16 * 1024 });

    /// <summary>通过有界且可取消的分块捕获，将当前世界序列化为 JSON 字符串。</summary>
    /// <remarks>调用方须暂停模拟，并在修改或替换世界前取消仍在进行的捕获。</remarks>
    /// <param name="yield">在缓冲写入之间让界面有机会处理事件的回调。</param>
    /// <param name="cancellationToken">取消捕获，不返回不完整的存档。</param>
    public async Task<string> ExportJsonAsync(Func<CancellationToken, ValueTask> yield, CancellationToken cancellationToken = default)
        => string.Concat(await ExportJsonChunksAsync(yield, cancellationToken));

    /// <summary>将当前世界捕获为不可变的 JSON 文本块，在序列化期间让出执行权并检查存档大小上限。</summary>
    /// <remarks>调用方须暂停模拟，并在修改或替换世界前取消仍在进行的捕获。</remarks>
    public async Task<string[]> ExportJsonChunksAsync(Func<CancellationToken, ValueTask> yield, CancellationToken cancellationToken = default)
    {
        using var stream = new YieldingSaveStream(yield);
        await JsonSerializer.SerializeAsync(stream, State, StreamingJson.WorldState, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return stream.Complete();
    }

    /// <summary>将 UTF-8 写入数据解码为文本块，检查大小并在分块间让出执行权。</summary>
    private sealed class YieldingSaveStream(Func<CancellationToken, ValueTask> yield) : Stream
    {
        private static readonly UTF8Encoding Utf8 = new(false, true);
        private readonly byte[] _pending = new byte[4];
        private int _pendingCount;
        private readonly List<string> _chunks = [];
        private long _bytes;
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _bytes;
        public override long Position { get => _bytes; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_bytes + buffer.Length > MaxSaveBytes) throw new ArgumentException("存档超过 64 MiB。");
            _bytes += buffer.Length;
            if (_pendingCount > 0)
            {
                var expected = SequenceLength(_pending[0]);
                var take = Math.Min(expected - _pendingCount, buffer.Length);
                buffer.Span[..take].CopyTo(_pending.AsSpan(_pendingCount));
                _pendingCount += take; buffer = buffer[take..];
                if (_pendingCount == expected)
                { _chunks.Add(Utf8.GetString(_pending, 0, expected)); _pendingCount = 0; }
            }
            while (!buffer.IsEmpty)
            {
                var count = Math.Min(buffer.Length, 16 * 1024);
                var last = count - 1;
                while (last > 0 && (buffer.Span[last] & 0xc0) == 0x80) last--;
                var trailing = SequenceLength(buffer.Span[last]) > count - last ? count - last : 0;
                var complete = count - trailing;
                if (complete > 0) _chunks.Add(Utf8.GetString(buffer.Span[..complete]));
                if (trailing > 0)
                { buffer.Span.Slice(complete, trailing).CopyTo(_pending); _pendingCount = trailing; }
                buffer = buffer[count..];
                // Complete a code point split by our own chunk boundary before
                // processing any later bytes from the same serializer write.
                if (_pendingCount > 0 && !buffer.IsEmpty)
                {
                    var expected = SequenceLength(_pending[0]);
                    var take = Math.Min(expected - _pendingCount, buffer.Length);
                    buffer.Span[..take].CopyTo(_pending.AsSpan(_pendingCount));
                    _pendingCount += take; buffer = buffer[take..];
                    if (_pendingCount == expected)
                    { _chunks.Add(Utf8.GetString(_pending, 0, expected)); _pendingCount = 0; }
                }
                await yield(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        private static int SequenceLength(byte first) => first < 0x80 ? 1 : first < 0xe0 ? 2 : first < 0xf0 ? 3 : 4;

        public string[] Complete()
        {
            if (_pendingCount != 0) throw new InvalidOperationException("存档包含不完整的 UTF-8 文本。");
            return _chunks.ToArray();
        }
    }

    /// <summary>解析并校验当前格式的存档，再构造引擎并重建运行时索引。</summary>
    /// <exception cref="ArgumentException">存档为空、超出大小上限、格式错误或不满足世界数据约束。</exception>
    public static WorldEngine ImportJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaxSaveBytes || Encoding.UTF8.GetByteCount(json) > MaxSaveBytes)
            throw new ArgumentException("存档为空或超过 64 MiB。", nameof(json));
        WorldState state;
        try { state = JsonSerializer.Deserialize(json, WorldJsonContext.Default.WorldState) ?? throw new JsonException("存档内容为空。"); }
        catch (JsonException ex) { throw new ArgumentException("无法读取存档：JSON 格式无效。", nameof(json), ex); }
        // 构造引擎时会重建索引，因此须在初始化前拒绝无效的实体引用。
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

        Require(state.FormatVersion == 16, "不支持该存档版本，请为本版新建世界。");
        Require(state.Width is >= 32 and <= 256 && state.Height is >= 32 and <= 256, "地图尺寸超出范围。");
        Require(state.Tick is >= 0 and <= 120_000_000 && state.RandomState != 0 && state.NextId is > 0 and < 2_000_000_000, "时间或随机数状态无效。");
        Require(state.Tiles is not null && state.Tiles.Length == state.Width * state.Height, "地图地格数量不匹配。");
        Require(state.Residents is not null && state.Residents.Count <= MaxPopulation && state.Settlements is not null && state.Settlements.Count <= 256 && state.Nations is not null && state.Nations.Count <= 64, "实体数量超出范围。");
        Require(state.Armies is not null && state.Armies.Count <= 64 && state.Diplomacies is not null && state.Diplomacies.Count <= 2016 && state.TradeRoutes is not null && state.TradeRoutes.Count <= 256 && state.Events is not null && state.Events.Count <= 400, "世界记录数量超出范围。");
        Require(state.SimulationVersion == 15 && state.PendingMessages is not null && state.PendingMessages.Count <= MaxPopulation * 2 && state.ArchivedResidents is not null && state.ArchivedResidents.Count <= 256 && state.Society is not null, "认知或社会记录无效。");
        var ids = new HashSet<int>();
        bool IdValid(int id) => id > 0 && id < state.NextId && ids.Add(id);
        foreach (var nation in state.Nations!) Require(nation is not null && IdValid(nation.Id) && TextValid(nation.Name, 40) && nation.Name.Length > 0 && Enum.IsDefined(nation.FoundingRace) && Enum.IsDefined(nation.DevelopmentFocus) && nation.Technology is >= 1 and <= 5 && TextValid(nation.Decision, 240) && StockValid(nation.Resources), "国家数据无效。");
        foreach (var town in state.Settlements!) Require(town is not null && IdValid(town.Id) && TextValid(town.Name, 80) && PositionValid(town.X, town.Y) && town.Housing is >= 0 and <= 20_000 && town.Level is >= 1 and <= 5 && town.MaxClaimRadius is >= 1 and <= 17 && StockValid(town.Resources), "聚落数据无效。");
        foreach (var town in state.Settlements!) Require(Enum.IsDefined(town.Tier) && double.IsFinite(town.ExpansionRequired)
            && town.ExpansionRequired is >= 0 and <= 120 && double.IsFinite(town.ExpansionProgress)
            && town.ExpansionProgress >= 0 && town.ExpansionProgress <= town.ExpansionRequired
            && (!town.IsExpanding || town.Tier != SettlementTier.City), "城镇扩充状态无效。");
        foreach (var army in state.Armies!) Require(army is not null && IdValid(army.Id) && PositionValid(army.X, army.Y) && army.Soldiers is >= 0 and <= MaxPopulation && FiniteRange(army.Morale, 100) && FiniteRange(army.Supplies, 1_000_000) && FiniteRange(army.WaterSupplies, 1_000_000) && TextValid(army.Status), "军队数据无效。");
        foreach (var resident in state.Residents!) Require(resident is not null && IdValid(resident.Id) && TextValid(resident.Name, 80) && TextValid(resident.Trait, 80) && PositionValid(resident.X, resident.Y) && Enum.IsDefined(resident.Race) && Enum.IsDefined(resident.Profession) && Enum.IsDefined(resident.Activity) && FiniteRange(resident.Age, 1000) && FiniteRange(resident.Health, 100) && FiniteRange(resident.Hunger, 100) && resident.SicknessTicks is >= 0 and <= 10_000 && resident.DiseaseImmuneUntilTick >= 0 && resident.DiseaseImmuneUntilTick <= state.Tick + 180, "居民数据无效。");
        var nations = state.Nations!.ToDictionary(n => n.Id);
        var towns = state.Settlements!.ToDictionary(s => s.Id);
        var armies = state.Armies!.ToDictionary(a => a.Id);
        for (var i = 0; i < state.Tiles!.Length; i++)
        {
            var tile = state.Tiles[i];
            Require(tile is not null && Enum.IsDefined(tile.Terrain) && tile.Fertility <= 100 && tile.RoadLevel <= 3 && FiniteRange(tile.ResourceAmount, 1_000_000) && tile.FireTicks is >= 0 and <= 10_000 && tile.DroughtTicks is >= 0 and <= 10_000, "地格数据无效。");
            Require(FiniteRange(tile!.Rainfall, 4) && (tile.RiverWidth == 0 || (tile.Terrain == TerrainType.Stream ? tile.RiverWidth == 1 : tile.Terrain == TerrainType.River ? tile.RiverWidth is 2 or 3 : tile.Terrain == TerrainType.LargeRiver && tile.RiverWidth is 4 or 5)) && FiniteRange(tile.NaturalWaterYield, 4) && tile.WaterDrawTick >= 0 && tile.WaterDrawTick <= state.Tick
                && tile.FireSuppressionTick >= 0 && tile.FireSuppressionTick <= state.Tick && tile.FireSuppressed is >= 0 and <= 2
                && FiniteRange(tile.WaterDrawn, 124) && Enum.IsDefined(tile.BridgeDirection)
                && (tile.Improvement == LandImprovement.Bridge ? tile.BridgeLevel is >= 1 and <= 3 : tile.BridgeLevel == 0), "地块供水或桥梁方向状态无效。");
            for (var plant = 0; plant < 4; plant++) Require(FiniteRange(tile.Plants.Get((PlantKind)plant), 1), "植物覆盖无效。");
            Require(tile.Plants.Total <= 1.000001, "植物总覆盖超出上限。");
            Require(tile.ClaimedSettlementId == 0 || towns.TryGetValue(tile.ClaimedSettlementId, out var owner) && owner.NationId == tile.NationId, "地盘归属无效。");
            Require(Enum.IsDefined(tile!.Improvement) && FiniteRange(tile.Harvested, 1_000_000_000) && tile.LastHarvestTick >= 0 && tile.LastHarvestTick <= state.Tick
                && FiniteRange(tile.DepositAmount, 1_000_000) && (tile.Deposit.HasValue ? DepositResearch(tile.Deposit.Value).HasValue : tile.DepositAmount == 0 && !tile.DepositDiscovered)
                && (tile.Improvement != LandImprovement.MountainPass || tile.Terrain == TerrainType.Mountain)
                && (tile.Improvement != LandImprovement.Bridge || tile.Terrain is TerrainType.River or TerrainType.Stream or TerrainType.LargeRiver or TerrainType.Water or TerrainType.Lake), "地块改造或矿藏状态无效。");
            Require(Enum.IsDefined(tile.Wildlife) && FiniteRange(tile.WildlifePopulation, 1000)
                && (tile.Wildlife != WildlifeKind.None || tile.WildlifePopulation == 0), "野生动物状态无效。");
            for (var species = 1; species < AnimalRules.SpeciesCount; species++)
                Require(FiniteRange(tile.OtherWildlife.Get((WildlifeKind)species), 1000)
                    && ((WildlifeKind)species != tile.Wildlife || tile.OtherWildlife.Get((WildlifeKind)species) == 0), "共存动物状态无效。");
            Require(tile!.NationId == 0 || nations.ContainsKey(tile.NationId), "地格引用了不存在的国家。");
            Require(tile.SettlementId == 0 || towns.TryGetValue(tile.SettlementId, out var town) && town.X == i % state.Width && town.Y == i / state.Width, "聚落地格引用无效。");
        }
        foreach (var nation in state.Nations) Require(towns.TryGetValue(nation.CapitalId, out var capital) && capital.NationId == nation.Id, "国家首都引用无效。");
        foreach (var town in state.Settlements) Require(nations.ContainsKey(town.NationId) && PositionValid(town.X, town.Y) && !IsWaterTerrain(state.Tiles[town.Y * state.Width + town.X].Terrain) && state.Tiles[town.Y * state.Width + town.X].SettlementId == town.Id && (state.Tiles[town.Y * state.Width + town.X].NationId == town.NationId || town.FoundationPending && state.Tiles[town.Y * state.Width + town.X].NationId == 0), "聚落归属或位置无效。");
        foreach (var resident in state.Residents)
        {
            ValidateResidentV2(resident, state.Tick, state.Width, state.Height);
            Require(nations.ContainsKey(resident.NationId) && towns.TryGetValue(resident.SettlementId, out var home) && home.NationId == resident.NationId && CanTraverse(state.Tiles[IndexFor(resident.X, resident.Y)], resident.TravelMode, resident.Race), "居民归属或位置无效。");
            Require(resident.ArmyId == 0 || armies.TryGetValue(resident.ArmyId, out var army) && army.NationId == resident.NationId, "居民军队引用无效。");
        }
        foreach (var army in state.Armies) Require(Enum.IsDefined(army.KnownDiplomacy) && army.LastOrderTick >= 0 && army.LastOrderTick <= state.Tick
            && army.LastOrderFactId >= 0 && army.LastOrderFactId < state.NextId
            && PositionValid(army.TargetX, army.TargetY) && army.MoveDurationTicks is >= 1 and <= 100 && nations.ContainsKey(army.NationId) && army.TargetNationId > 0 && army.NationId != army.TargetNationId && WalkablePosition(army.X, army.Y), "军队国家、位置或军令记录无效。");
        var pairs = new HashSet<(int, int)>();
        foreach (var relation in state.Diplomacies!) Require(relation is not null && nations.ContainsKey(relation.FirstNationId) && nations.ContainsKey(relation.SecondNationId)
            && relation.FirstNationId != relation.SecondNationId && Enum.IsDefined(relation.Status)
            && relation.FirstEscalationTick >= 0 && relation.FirstEscalationTick <= state.Tick
            && relation.SecondEscalationTick >= 0 && relation.SecondEscalationTick <= state.Tick
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
            Require(pending is not null && pending.SenderId > 0 && pending.RecipientId > 0 && pending.DeliverTick >= 0 && pending.DeliverTick <= state.Tick + 1000 && pending.Facts is not null && pending.Facts.Count <= 8, "待递送口信无效。");
            foreach (var fact in pending!.Facts!) ValidateFactV2(fact, state.Tick, state.Width, state.Height);
        }
        ValidateSocietyState(state);
        ValidateConflicts(state);
        foreach (var town in state.Settlements)
            Require(state.Society!.Buildings.Count(b => b.SettlementId == town.Id && b.Kind == BuildingKind.TownCenter && b.X == town.X && b.Y == town.Y) == 1, "城镇中心缺失或重复。");
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
        ValidateStories(state);
        ValidateConnectedClaims(state);
    }
}
