namespace SeWZC.WorldBox.Core.Runtime;

internal sealed partial class ResidentCursor
{
    private BodyFields _body = new(value);
    private MotionFields _motion = new(value);
    private AgentState _agent = value.Agent;
    private ResourceStock _inventory = value.Inventory;
    private bool _draftChanged;

    // 日常字段以不可变值暂存，完整实体及世界快照在边界冻结。
    public new ref readonly Resident Value
    {
        get
        {
            FlushPending();
            return ref base.Value;
        }
    }

    internal override void FlushPending()
    {
        if (_draftChanged)
            ReplaceChanged(_body.Apply(base.Value, _motion, _agent, _inventory));
    }

    public override void Replace(in Resident value)
    {
        FlushPending();
        base.Replace(value);
    }

    internal void BeginMove(int x, int y, long tick, int duration, ResidentActivity activity, AgentState agent)
    {
        _motion = _motion with
        {
            FromX = _motion.X,
            FromY = _motion.Y,
            X = x,
            Y = y,
            MoveStartedTick = tick,
            MoveDurationTicks = duration,
        };
        if (_body.Activity != activity)
            _body = _body with { Activity = activity };
        _agent = agent;
        _draftChanged = true;
    }

    internal void ApplyDay(in Resident.DailyState value)
    {
        // 低频身份与效果变化单独提交；日常字段继续与动作累积到同一份草稿。
        var before = base.Value;
        if (before.Profession != value.Profession || before.DiseaseImmuneUntilTick != value.Immunity
                                                  || before.DeathCause != value.DeathCause ||
                                                  before.DeathTick != value.DeathTick)
        {
            FlushPending();
            ReplaceChanged(base.Value with
            {
                Profession = value.Profession,
                DiseaseImmuneUntilTick = value.Immunity,
                DeathCause = value.DeathCause,
                DeathTick = value.DeathTick,
            });
        }

        // 老化等常见变化一次标记草稿，避免每个输出字段再走独立设置器。
        _draftChanged |= !_body.Age.Equals(value.Age) || !_body.Health.Equals(value.Health)
                         || !_body.Hunger.Equals(value.Hunger) || !_body.Thirst.Equals(value.Thirst)
                         || _body.SicknessTicks != value.Sickness || _body.Activity != value.Activity
                         || !_body.Mana.Equals(value.Mana) || _inventory != value.Inventory;
        _draftChanged |= !ReferenceEquals(_agent, value.Agent);
        _body = _body with
        {
            Age = value.Age,
            Health = value.Health,
            Hunger = value.Hunger,
            Thirst = value.Thirst,
            SicknessTicks = value.Sickness,
            Activity = value.Activity,
            Mana = value.Mana,
        };
        _agent = value.Agent;
        _inventory = value.Inventory;
    }

    private readonly record struct BodyFields
    {
        internal double Age { get; init; }
        internal ResidentActivity Activity { get; init; }
        internal int SicknessTicks { get; init; }
        internal double Health { get; init; }
        internal double Hunger { get; init; }
        internal double Thirst { get; init; }
        internal double Mana { get; init; }

        internal BodyFields(Resident value)
        {
            Age = value.Age;
            Activity = value.Activity;
            Health = value.Health;
            Hunger = value.Hunger;
            SicknessTicks = value.SicknessTicks;
            Thirst = value.Thirst;
            Mana = value.Mana;
        }

        internal Resident Apply(Resident value, in MotionFields motion, AgentState agent, in ResourceStock inventory)
        {
            return value with
            {
                X = motion.X,
                Y = motion.Y,
                Age = Age,
                Activity = Activity,
                Health = Health,
                Hunger = Hunger,
                SicknessTicks = SicknessTicks,
                TravelMode = motion.TravelMode,
                Thirst = Thirst,
                Inventory = inventory,
                Mana = Mana,
                FromX = motion.FromX,
                FromY = motion.FromY,
                MoveStartedTick = motion.MoveStartedTick,
                MoveDurationTicks = motion.MoveDurationTicks,
                Agent = agent,
            };
        }
    }

    private readonly record struct MotionFields
    {
        internal int X { get; init; }
        internal int Y { get; init; }
        internal int FromX { get; init; }
        internal int FromY { get; init; }
        internal long MoveStartedTick { get; init; }
        internal int MoveDurationTicks { get; init; }
        internal TravelMode TravelMode { get; init; }

        internal MotionFields(Resident value)
        {
            X = value.X;
            Y = value.Y;
            TravelMode = value.TravelMode;
            FromX = value.FromX;
            FromY = value.FromY;
            MoveStartedTick = value.MoveStartedTick;
            MoveDurationTicks = value.MoveDurationTicks;
        }
    }
}
