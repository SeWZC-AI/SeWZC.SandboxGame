using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

public sealed partial record Resident
{
    /// <summary>当前移动区段经过的地格索引，包含起点与终点。</summary>
    [JsonRequired]
    public ImmutableArray<int> MovementRoute
    {
        get => _movement.Route;
        init => _movement = _movement with { Route = value };
    }

    /// <summary>连续移动中尚未用于下一格的刻数，停下行动后清除。</summary>
    public double MovementCredit
    {
        get => _movement.Credit;
        init => _movement = _movement with { Credit = value };
    }
    // 相应字段以不可变记录共享，一次动作或结算只复制发生变化的这一组。
    private readonly Movement _movement = Movement.Default;

    private Movement MovementState
    {
        init => _movement = value;
    }

    private sealed record Movement
    {
        internal static readonly Movement Default = new();

        public int X { get; init; }
        public int Y { get; init; }
        public int FromX { get; init; }
        public int FromY { get; init; }
        public long MoveStartedTick { get; init; }
        public int MoveDurationTicks { get; init; } = 1;
        public TravelMode TravelMode { get; init; }
        public ImmutableArray<int> Route { get; init; } = [];
        public double Credit { get; init; }
    }
}
