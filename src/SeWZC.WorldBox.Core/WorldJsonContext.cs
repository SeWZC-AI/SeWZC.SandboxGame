using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>为存档及编辑副本生成序列化元数据，在裁剪后的 WebAssembly 构建中仍可使用。</summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(WorldState))]
[JsonSerializable(typeof(Resident))]
[JsonSerializable(typeof(AgentState))]
[JsonSerializable(typeof(List<ResidentHistoryEntry>))]
internal partial class WorldJsonContext : JsonSerializerContext;
