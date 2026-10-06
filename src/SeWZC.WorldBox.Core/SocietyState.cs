namespace SeWZC.WorldBox.Core;

/// <summary>需要保存的文化、设施、本地研究、制度，以及机构实际收到的报告。</summary>
public sealed class SocietyState
{
    /// <summary>是否允许新的魔法发展和施法。</summary>
    public bool MagicEnabled { get; set; } = true;

    /// <summary>独立的文化定义集合。</summary>
    public List<CultureDefinition> Cultures { get; set; } = [];

    /// <summary>实际设施及其施工、生产和服务状态。</summary>
    public List<Building> Buildings { get; set; } = [];

    /// <summary>各聚落掌握的研究和进行中的项目。</summary>
    public List<SettlementResearch> Research { get; set; } = [];

    /// <summary>各聚落当前政策及决策依据。</summary>
    public List<LocalPolicy> Policies { get; set; } = [];

    /// <summary>各国家的制度和决策记录。</summary>
    public List<NationInstitution> Institutions { get; set; } = [];

    /// <summary>机构实际收到的报告，是制度决策使用的报告集合。</summary>
    public List<InstitutionReport> Reports { get; set; } = [];

    /// <summary>居民接触不同文化的记录。</summary>
    public List<CulturalContact> CulturalContacts { get; set; } = [];
}
