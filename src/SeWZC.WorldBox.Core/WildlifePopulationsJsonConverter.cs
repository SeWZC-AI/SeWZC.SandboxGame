using System.Text.Json;
using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

// Format 16 stores sparse [species ID, exact population] pairs. No rounding,
// thresholds or inferred species: even very small and invalid values survive
// writing so the normal input validator can reject invalid world states.
internal sealed class WildlifePopulationsJsonConverter : JsonConverter<WildlifePopulations>
{
    public override WildlifePopulations Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("动物种群须为稀疏数组。");
        var populations = new WildlifePopulations(); uint seen = 0;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out var species) || species <= 0 || species >= AnimalRules.SpeciesCount
                || (seen & (1u << species)) != 0) throw new JsonException("动物物种编号无效或重复。");
            seen |= 1u << species;
            if (!reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetDouble(out var count)
                || !double.IsFinite(count) || count < 0 || count > 1000) throw new JsonException("动物种群数量无效。");
            populations.Set((WildlifeKind)species, count);
        }
        if (reader.TokenType != JsonTokenType.EndArray) throw new JsonException("动物种群数组不完整。");
        return populations;
    }

    public override void Write(Utf8JsonWriter writer, WildlifePopulations value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        for (var species = 1; species < AnimalRules.SpeciesCount; species++)
        {
            var count = value.Get((WildlifeKind)species);
            if (count == 0) continue;
            writer.WriteNumberValue(species); writer.WriteNumberValue(count);
        }
        writer.WriteEndArray();
    }
}
