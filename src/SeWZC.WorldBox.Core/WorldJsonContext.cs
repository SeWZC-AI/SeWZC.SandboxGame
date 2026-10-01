using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(WorldState))]
[JsonSerializable(typeof(Resident))]
[JsonSerializable(typeof(AgentState))]
[JsonSerializable(typeof(List<ResidentHistoryEntry>))]
internal partial class WorldJsonContext : JsonSerializerContext;
