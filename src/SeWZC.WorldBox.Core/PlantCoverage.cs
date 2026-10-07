using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>地格内各类自然植物的组成份额。</summary>
public readonly record struct PlantCoverage
{
    /// <summary>乔木份额。</summary>
    [JsonPropertyName("t")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Trees { get; init; }

    /// <summary>灌木份额。</summary>
    [JsonPropertyName("s")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Shrubs { get; init; }

    /// <summary>草本份额。</summary>
    [JsonPropertyName("g")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Grass { get; init; }

    /// <summary>芦苇份额。</summary>
    [JsonPropertyName("r")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Reeds { get; init; }

    /// <summary>四类自然植物组成份额的合计。</summary>
    [JsonIgnore]
    public readonly double Total => Trees + Shrubs + Grass + Reeds;

    /// <summary>读取自然植物的组成份额；作物不存储在此结构中，返回零。</summary>
    /// <param name="kind">植物类别。</param>
    public readonly double Get(PlantKind kind)
    {
        return kind switch
        {
            PlantKind.Trees => Trees,
            PlantKind.Shrubs => Shrubs,
            PlantKind.Grass => Grass,
            PlantKind.Reeds => Reeds,
            _ => 0,
        };
    }

    /// <summary>返回替换自然植物份额后的值；作物不存储在此结构中，返回原值。</summary>
    /// <param name="kind">植物类别。</param>
    /// <param name="cover">要设置的自然植物组成份额。</param>
    public PlantCoverage WithCoverage(PlantKind kind, double cover) => kind switch
    {
        PlantKind.Trees => this with { Trees = cover },
        PlantKind.Shrubs => this with { Shrubs = cover },
        PlantKind.Grass => this with { Grass = cover },
        PlantKind.Reeds => this with { Reeds = cover },
        _ => this,
    };
}
