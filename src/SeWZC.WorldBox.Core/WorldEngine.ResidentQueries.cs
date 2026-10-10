using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private int _nearbyResidentCount;
    private long _nearbyResidentTick = -1, _nearbyResidentRevision = -1;
    private bool _nearbyResidentsActive;

    // 仅在通信之后、无人移动的社会阶段复用位置索引；其他阶段和外部命令读取实时位置。
    private IEnumerable<StateReference<Resident>> NearbyResidents(int x, int y, int radius)
    {
        if (!_nearbyResidentsActive || !_knowledgeQueriesActive || _nearbyResidentTick != SimulationTick
            || _nearbyResidentRevision != Residents.MembershipRevision)
        {
            foreach (var person in Residents)
                if (Distance(person.Value.X, person.Value.Y, x, y) <= radius)
                    yield return person;
            yield break;
        }

        foreach (var tile in Circle(x, y, radius))
        {
            if (Distance(tile % Width, tile / Width, x, y) > radius)
                continue;
            var first = _conversationStarts[tile];
            var last = first + _conversationCounts[tile];
            for (var index = first; index < last; index++)
                yield return _conversationResidents[index];
        }
    }
}
