using System.Text.Json;
using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>以物种编号和精确数量组成的稀疏数组读写动物种群。</summary>
internal sealed class WildlifePopulationsJsonConverter : JsonConverter<WildlifePopulations>
{
    /// <inheritdoc />
    public override WildlifePopulations Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("动物种群须为稀疏数组。");
        var populations = new WildlifePopulations();
        uint seen = 0;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out var species) || species <= 0 ||
                species >= AnimalRules.SpeciesCount
                || (seen & (1u << species)) != 0) throw new JsonException("动物物种编号无效或重复。");
            seen |= 1u << species;
            if (!reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetDouble(out var count)
                || !double.IsFinite(count) || count < 0 || count > 1000) throw new JsonException("动物种群数量无效。");
            populations.Set((WildlifeKind)species, count);
        }

        if (reader.TokenType != JsonTokenType.EndArray) throw new JsonException("动物种群数组不完整。");
        return populations;
    }

    // 写入时保留非零小量和异常值，避免过滤后掩盖无效状态；读取时再由校验器拒绝。
    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, WildlifePopulations value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        for (var species = 1; species < AnimalRules.SpeciesCount; species++)
        {
            var count = value.Get((WildlifeKind)species);
            if (count == 0) continue;
            writer.WriteNumberValue(species);
            writer.WriteNumberValue(count);
        }

        writer.WriteEndArray();
    }
}
