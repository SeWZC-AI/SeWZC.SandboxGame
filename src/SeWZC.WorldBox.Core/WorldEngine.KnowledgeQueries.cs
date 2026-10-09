namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private readonly Dictionary<(int Resident, int Culture), int> _cultureContactIndices = [];
    private readonly HashSet<(int Town, int Fact)> _institutionReports = [];
    private readonly Dictionary<int, ulong> _knowledgeByTown = [];
    private int _indexedCultureContactCount;

    // 研究索引只在当前模拟阶段有效；编辑后直接读取权威列表，阶段内收到知识时同步更新掩码。
    private bool _knowledgeQueriesActive;

    private void BeginKnowledgeQueries()
    {
        _knowledgeByTown.Clear();
        _institutionReports.Clear();
        IndexCultureContacts();
        foreach (var report in Society.Reports)
            _institutionReports.Add((report.RecipientSettlementId, report.FactId));
        foreach (var research in Society.Research)
        {
            var mask = 0UL;
            foreach (var kind in research.Completed)
                if ((uint)kind.Id < 64)
                    mask |= 1UL << kind.Id;
            _knowledgeByTown[research.SettlementId] = mask;
        }

        _knowledgeQueriesActive = true;
    }

    private void EndKnowledgeQueries()
    {
        Array.Clear(_conversationResidents, 0, _nearbyResidentCount);
        _nearbyResidentCount = 0;
        _knowledgeQueriesActive = false;
        _knowledgeByTown.Clear();
        _institutionReports.Clear();
        _cultureContactIndices.Clear();
    }

    private void IndexCultureContacts()
    {
        _cultureContactIndices.Clear();
        var contacts = Society.CulturalContacts;
        for (var index = 0; index < contacts.Count; index++)
            _cultureContactIndices.TryAdd((contacts[index].ResidentId, contacts[index].CultureId), index);
        _indexedCultureContactCount = contacts.Count;
    }

    private int FindCultureContactIndex(int residentId, int cultureId)
    {
        if (!_knowledgeQueriesActive)
            return Society.CulturalContacts.FindIndex(c =>
                c.ResidentId == residentId && c.CultureId == cultureId);
        // 新增接触同步登记；删除接触后重建位置，接触程度的改写仍从权威记录即时读取。
        if (_indexedCultureContactCount != Society.CulturalContacts.Count)
            IndexCultureContacts();
        return _cultureContactIndices.GetValueOrDefault((residentId, cultureId), -1);
    }
}
