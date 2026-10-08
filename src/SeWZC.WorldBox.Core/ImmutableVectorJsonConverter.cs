using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace SeWZC.WorldBox.Core;

/// <summary>以有序 JSON 数组读写持久化序列，复用源生成的对象序列化信息。</summary>
/// <typeparam name="T">不可变对象类型。</typeparam>
public sealed class ImmutableVectorJsonConverter<T> : JsonConverter<ImmutableVector<T>> where T : class
{
    /// <inheritdoc />
    public override ImmutableVector<T> Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("实体集合必须是数组。");
        var info = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
        var items = new List<T>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            items.Add(JsonSerializer.Deserialize(ref reader, info)!);
        if (reader.TokenType != JsonTokenType.EndArray)
            throw new JsonException("实体集合不完整。");
        return ImmutableVector<T>.CreateRange(items);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, ImmutableVector<T> value, JsonSerializerOptions options)
    {
        var info = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
        writer.WriteStartArray();
        foreach (var item in value)
            JsonSerializer.Serialize(writer, item, info);
        writer.WriteEndArray();
    }
}
