using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>存档与编辑副本的源生成 JSON 序列化上下文，支持裁剪后的 WebAssembly。</summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(WorldState))]
[JsonSerializable(typeof(Resident))]
[JsonSerializable(typeof(AgentState))]
[JsonSerializable(typeof(List<ResidentHistoryEntry>))]
internal partial class WorldJsonContext : JsonSerializerContext;
