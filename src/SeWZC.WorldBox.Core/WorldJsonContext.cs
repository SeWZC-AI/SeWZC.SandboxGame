using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(WorldState))]
internal partial class WorldJsonContext : JsonSerializerContext;
