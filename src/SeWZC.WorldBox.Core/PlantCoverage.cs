using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>乔木、灌木、草本和芦苇占地格植物组成的份额。</summary>
public struct PlantCoverage : IEquatable<PlantCoverage>
{
    /// <summary>乔木在此格植物组成中的份额。</summary>
    [JsonPropertyName("t")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Trees { get; set; }

    /// <summary>灌木在此格植物组成中的份额。</summary>
    [JsonPropertyName("s")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Shrubs { get; set; }

    /// <summary>草本在此格植物组成中的份额。</summary>
    [JsonPropertyName("g")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Grass { get; set; }

    /// <summary>芦苇在此格植物组成中的份额。</summary>
    [JsonPropertyName("r")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Reeds { get; set; }

    /// <summary>比较四类自然植物的组成份额是否相等。</summary>
    /// <param name="other">用于比较的同类型值。</param>
    public readonly bool Equals(PlantCoverage other)
    {
        return Trees.Equals(other.Trees) && Shrubs.Equals(other.Shrubs)
                                         && Grass.Equals(other.Grass) && Reeds.Equals(other.Reeds);
    }

    /// <summary>比较四类自然植物的组成份额是否相等。</summary>
    /// <param name="obj">用于比较的对象，空值或其他类型均不相等。</param>
    public readonly override bool Equals(object? obj)
    {
        return obj is PlantCoverage other && Equals(other);
    }

    /// <summary>根据四类自然植物的份额计算哈希值。</summary>
    public readonly override int GetHashCode()
    {
        return HashCode.Combine(Trees, Shrubs, Grass, Reeds);
    }

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

    /// <summary>替换自然植物的组成份额；作物不存储在此结构中，不作修改。</summary>
    /// <param name="kind">植物类别。</param>
    /// <param name="cover">要设置的自然植物组成份额。</param>
    public void Set(PlantKind kind, double cover)
    {
        switch (kind)
        {
            case PlantKind.Trees: Trees = cover; break;
            case PlantKind.Shrubs: Shrubs = cover; break;
            case PlantKind.Grass: Grass = cover; break;
            case PlantKind.Reeds: Reeds = cover; break;
        }
    }
}
