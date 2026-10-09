using System.Text.Json;
using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>以固定字段顺序保存信息快照，避免每个居民的记忆重复保存字段名。</summary>
internal sealed class AgentFactJsonConverter : JsonConverter<AgentFact>
{
    /// <inheritdoc />
    public override AgentFact Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("信息快照须为紧凑数组。");
        var fact = new AgentFact
        {
            Id = Integer(ref reader), Kind = (AgentFactKind)Integer(ref reader),
            SubjectId = Integer(ref reader), X = Integer(ref reader), Y = Integer(ref reader),
            Value = Number(ref reader), ObservedTick = Long(ref reader), LearnedTick = Long(ref reader),
            OriginResidentId = Integer(ref reader), OriginProfession = (Profession)Integer(ref reader),
            SourceResidentId = Integer(ref reader), Confidence = Number(ref reader), Hops = Integer(ref reader),
            Text = Text(ref reader), EventId = Integer(ref reader), CampaignEventId = Integer(ref reader),
            WarObjective = (WarObjective)Integer(ref reader), TargetNationId = Integer(ref reader),
        };
        if (!reader.Read() || reader.TokenType != JsonTokenType.EndArray)
            throw new JsonException("信息快照字段数量无效。");
        return fact;
    }

    private static int Integer(ref Utf8JsonReader reader)
    {
        if (!reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out var value))
            throw new JsonException("信息快照整数字段无效。");
        return value;
    }

    private static long Long(ref Utf8JsonReader reader)
    {
        if (!reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetInt64(out var value))
            throw new JsonException("信息快照时间字段无效。");
        return value;
    }

    private static double Number(ref Utf8JsonReader reader)
    {
        if (!reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetDouble(out var value)
            || !double.IsFinite(value))
            throw new JsonException("信息快照数值字段无效。");
        return value;
    }

    private static string Text(ref Utf8JsonReader reader)
    {
        if (!reader.Read() || reader.TokenType != JsonTokenType.String)
            throw new JsonException("信息快照说明字段无效。");
        return reader.GetString()!;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, AgentFact value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.Id);
        writer.WriteNumberValue((int)value.Kind);
        writer.WriteNumberValue(value.SubjectId);
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteNumberValue(value.Value);
        writer.WriteNumberValue(value.ObservedTick);
        writer.WriteNumberValue(value.LearnedTick);
        writer.WriteNumberValue(value.OriginResidentId);
        writer.WriteNumberValue((int)value.OriginProfession);
        writer.WriteNumberValue(value.SourceResidentId);
        writer.WriteNumberValue(value.Confidence);
        writer.WriteNumberValue(value.Hops);
        writer.WriteStringValue(value.Text);
        writer.WriteNumberValue(value.EventId);
        writer.WriteNumberValue(value.CampaignEventId);
        writer.WriteNumberValue((int)value.WarObjective);
        writer.WriteNumberValue(value.TargetNationId);
        writer.WriteEndArray();
    }
}
