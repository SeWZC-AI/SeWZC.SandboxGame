using SeWZC.WorldBox.Core.Runtime;
namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private readonly Dictionary<int, ulong> _knowledgeByTown = [];

    // 研究索引只在当前模拟阶段有效；编辑后直接读取权威列表，阶段内收到知识时同步更新掩码。
    private bool _knowledgeQueriesActive;

    private void BeginKnowledgeQueries()
    {
        _knowledgeByTown.Clear();
        foreach (var research in Current.Society.Research)
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
        _knowledgeQueriesActive = false;
        _knowledgeByTown.Clear();
    }
}
