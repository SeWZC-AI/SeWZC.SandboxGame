namespace SeWZC.WorldBox.Core;

public sealed partial record Resident
{
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
    }
}
