namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    // 状态转换先产生不可变值，命令在这里明确提交；读取记录不再附带写入回调。
    private SettlementResearch PublishResearch(SettlementResearch research)
    {
        var index = _localWorkQueriesActive && _localResearch.TryGetValue(research.SettlementId, out var cached)
            ? cached
            : Current.Society.Research.FindIndex(r => r.SettlementId == research.SettlementId);
        Current.Society = Current.Society with { Research = Current.Society.Research.SetItem(index, research) };
        return research;
    }

    private LocalPolicy PublishPolicy(LocalPolicy policy)
    {
        var index = Current.Society.Policies.FindIndex(p => p.SettlementId == policy.SettlementId);
        Current.Society = Current.Society with { Policies = Current.Society.Policies.SetItem(index, policy) };
        return policy;
    }

    private NationInstitution PublishInstitution(NationInstitution institution)
    {
        var index = Current.Society.Institutions.FindIndex(i => i.NationId == institution.NationId);
        Current.Society = Current.Society with { Institutions = Current.Society.Institutions.SetItem(index, institution) };
        return institution;
    }

    private CulturalContact PublishContact(CulturalContact contact)
    {
        var index = Current.Society.CulturalContacts.FindIndex(c =>
            c.ResidentId == contact.ResidentId && c.CultureId == contact.CultureId);
        Current.Society = Current.Society with { CulturalContacts = Current.Society.CulturalContacts.SetItem(index, contact) };
        return contact;
    }

    private DiplomaticRelation PublishRelation(DiplomaticRelation relation)
    {
        var index = Current.Diplomacies.FindIndex(r =>
            r.FirstNationId == relation.FirstNationId && r.SecondNationId == relation.SecondNationId);
        Current.Diplomacies = Current.Diplomacies.SetItem(index, relation);
        return relation;
    }

    private LocalConflict PublishConflict(LocalConflict conflict)
    {
        var index = Current.Conflicts.FindIndex(c => c.Id == conflict.Id);
        Current.Conflicts = Current.Conflicts.SetItem(index, conflict);
        return conflict;
    }

    private WorldEvent PublishEvent(WorldEvent entry)
    {
        var index = Current.Events.FindIndex(e => e.Id == entry.Id);
        // 容量规则可能已淘汰刚创建的事件，人物经历仍可引用返回的完整值。
        if (index >= 0)
            Current.Events = Current.Events.SetItem(index, entry);
        return entry;
    }
}
