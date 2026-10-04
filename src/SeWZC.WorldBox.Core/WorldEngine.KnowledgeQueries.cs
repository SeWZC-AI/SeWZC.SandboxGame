namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    // Derived, tick-scoped index. Direct editor/state mutations between steps still
    // read authoritative research lists; receiving knowledge updates the active mask.
    private bool _knowledgeQueriesActive;
    private readonly Dictionary<int, ulong> _knowledgeByTown = [];
    private void BeginKnowledgeQueries()
    {
        _knowledgeByTown.Clear();
        foreach (var research in State.Society.Research)
        {
            var mask = 0UL;
            foreach (var kind in research.Completed)
                if ((uint)kind < 64) mask |= 1UL << (int)kind;
            _knowledgeByTown[research.SettlementId] = mask;
        }
        _knowledgeQueriesActive = true;
    }
    private void EndKnowledgeQueries()
    { _knowledgeQueriesActive = false; _knowledgeByTown.Clear(); }
}
