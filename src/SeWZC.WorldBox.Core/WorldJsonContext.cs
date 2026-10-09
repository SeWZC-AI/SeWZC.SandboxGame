using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>不可变存档与编辑输入的源生成 JSON 序列化上下文，支持裁剪后的 WebAssembly。</summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(Tile))]
[JsonSerializable(typeof(Settlement))]
[JsonSerializable(typeof(Nation))]
[JsonSerializable(typeof(Army))]
[JsonSerializable(typeof(DiplomaticRelation))]
[JsonSerializable(typeof(WorldEvent))]
[JsonSerializable(typeof(LocalConflict))]
[JsonSerializable(typeof(Building))]
[JsonSerializable(typeof(SettlementResearch))]
[JsonSerializable(typeof(LocalPolicy))]
[JsonSerializable(typeof(NationInstitution))]
[JsonSerializable(typeof(CulturalContact))]
[JsonSerializable(typeof(WorldState))]
[JsonSerializable(typeof(Resident))]
[JsonSerializable(typeof(AgentState))]
[JsonSerializable(typeof(AgentFact))]
[JsonSerializable(typeof(List<ResidentHistoryEntry>))]
internal partial class WorldJsonContext : JsonSerializerContext;
