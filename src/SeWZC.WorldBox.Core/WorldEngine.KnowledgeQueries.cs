namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private readonly Dictionary<int, ulong> _knowledgeByTown = [];

    // 研究索引只在当前模拟阶段有效；编辑后直接读取权威列表，阶段内收到知识时同步更新掩码。
    private bool _knowledgeQueriesActive;

    private void BeginKnowledgeQueries()
    {
        _knowledgeByTown.Clear();
        foreach (var research in State.Society.Research)
        {
            var mask = 0UL;
            foreach (var kind in research.Completed)
                if ((uint)kind < 64)
                    mask |= 1UL << (int)kind;
            _knowledgeByTown[research.SettlementId] = mask;
        }

        _knowledgeQueriesActive = true;
    }

    private void EndKnowledgeQueries()
    {
        _knowledgeQueriesActive = false;
        _knowledgeByTown.Clear();
    }
}
