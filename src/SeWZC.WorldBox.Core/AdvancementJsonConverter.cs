using System.Text.Json;
using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>将研究保存为编号，读取时恢复目录中的共享节点。</summary>
internal sealed class AdvancementJsonConverter : JsonConverter<Advancement>
{
    public override Advancement Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out var id)
                                                     || Advancement.Find(id) is not { } research)
            throw new JsonException("研究编号无效。");
        return research;
    }

    public override void Write(Utf8JsonWriter writer, Advancement value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.Id);
    }
}
