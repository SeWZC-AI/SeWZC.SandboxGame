using System.Runtime.InteropServices;

namespace SeWZC.WorldBox.Core;

public sealed partial record AgentState
{
    // 只记录已经实际抵达的地格；近期重访刷新顺序，容量满时忘记最久未走过的地点。
    internal AgentState RememberRouteTile(int tile)
    {
        if (FamiliarTiles.Length > 0 && FamiliarTiles[^1] == tile) return this;
        var previous = FamiliarTiles.IndexOf(tile);
        var count = Math.Min(MaximumFamiliarTiles, FamiliarTiles.Length + (previous < 0 ? 1 : 0));
        var items = new int[count];
        var position = 0;
        for (var index = previous < 0 && FamiliarTiles.Length == MaximumFamiliarTiles ? 1 : 0;
             index < FamiliarTiles.Length; index++)
            if (index != previous) items[position++] = FamiliarTiles[index];
        items[position] = tile;
        return this with { FamiliarTiles = ImmutableCollectionsMarshal.AsImmutableArray(items) };
    }
}
