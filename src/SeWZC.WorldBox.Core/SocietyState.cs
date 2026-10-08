using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>世界的社会发展状态。</summary>
public sealed record SocietyState
{
    /// <summary>是否允许新的魔法发展；已有施法能力不受此开关影响。</summary>
    public bool MagicEnabled { get; init; } = true;

    /// <summary>独立的文化定义集合。</summary>
    public ImmutableList<CultureDefinition> Cultures { get; init; } = [];

    /// <summary>世界中实际存在的设施。</summary>
    [JsonConverter(typeof(ImmutableVectorJsonConverter<Building>))]
    public ImmutableVector<Building> Buildings { get; init; } = [];

    /// <summary>各聚落掌握的研究和进行中的项目。</summary>
    [JsonConverter(typeof(ImmutableVectorJsonConverter<SettlementResearch>))]
    public ImmutableVector<SettlementResearch> Research { get; init; } = [];

    /// <summary>各聚落当前政策及决策依据。</summary>
    [JsonConverter(typeof(ImmutableVectorJsonConverter<LocalPolicy>))]
    public ImmutableVector<LocalPolicy> Policies { get; init; } = [];

    /// <summary>各国家的制度和决策记录。</summary>
    [JsonConverter(typeof(ImmutableVectorJsonConverter<NationInstitution>))]
    public ImmutableVector<NationInstitution> Institutions { get; init; } = [];

    /// <summary>机构实际收到的报告，用于制度决策。</summary>
    public ImmutableList<InstitutionReport> Reports { get; init; } = [];

    /// <summary>居民接触不同文化的记录。</summary>
    [JsonConverter(typeof(ImmutableVectorJsonConverter<CulturalContact>))]
    public ImmutableVector<CulturalContact> CulturalContacts { get; init; } = [];
}
