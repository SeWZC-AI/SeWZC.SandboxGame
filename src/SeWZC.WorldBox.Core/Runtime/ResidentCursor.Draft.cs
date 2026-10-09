namespace SeWZC.WorldBox.Core.Runtime;

internal sealed partial class ResidentCursor
{
    private DailyDraft _draft = new(value);
    private bool _draftChanged;

    // 内部字段立即供后续行为读取，完整实体及世界快照在边界冻结。
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
            ReplaceChanged(_draft.Apply(base.Value));
    }

    public override void Replace(in Resident value)
    {
        FlushPending();
        base.Replace(value);
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
        _draftChanged |= !_draft.Age.Equals(value.Age) || !_draft.Health.Equals(value.Health)
                                                       || !_draft.Hunger.Equals(value.Hunger) ||
                                                       !_draft.Thirst.Equals(value.Thirst)
                                                       || _draft.SicknessTicks != value.Sickness ||
                                                       _draft.Activity != value.Activity
                                                       || !_draft.Mana.Equals(value.Mana) ||
                                                       _draft.Inventory != value.Inventory;
        _draft.Age = value.Age;
        _draft.Health = value.Health;
        _draft.Hunger = value.Hunger;
        _draft.Thirst = value.Thirst;
        _draft.SicknessTicks = value.Sickness;
        _draft.Activity = value.Activity;
        _draft.Mana = value.Mana;
        _draft.Inventory = value.Inventory;
        if (!ReferenceEquals(_draft.Agent, value.Agent))
        {
            _draft.Agent = value.Agent;
            _draftChanged = true;
        }
    }

    private struct DailyDraft
    {
        internal int X;
        internal int Y;
        internal double Age;
        internal ResidentActivity Activity;
        internal double Health;
        internal double Hunger;
        internal int SicknessTicks;
        internal TravelMode TravelMode;
        internal double Thirst;
        internal ResourceStock Inventory;
        internal double Mana;
        internal int FromX;
        internal int FromY;
        internal long MoveStartedTick;
        internal int MoveDurationTicks;
        internal AgentState Agent;

        internal DailyDraft(Resident value)
        {
            X = value.X;
            Y = value.Y;
            Age = value.Age;
            Activity = value.Activity;
            Health = value.Health;
            Hunger = value.Hunger;
            SicknessTicks = value.SicknessTicks;
            TravelMode = value.TravelMode;
            Thirst = value.Thirst;
            Inventory = value.Inventory;
            Mana = value.Mana;
            FromX = value.FromX;
            FromY = value.FromY;
            MoveStartedTick = value.MoveStartedTick;
            MoveDurationTicks = value.MoveDurationTicks;
            Agent = value.Agent;
        }

        internal readonly Resident Apply(Resident value)
        {
            return value with
            {
                X = X,
                Y = Y,
                Age = Age,
                Activity = Activity,
                Health = Health,
                Hunger = Hunger,
                SicknessTicks = SicknessTicks,
                TravelMode = TravelMode,
                Thirst = Thirst,
                Inventory = Inventory,
                Mana = Mana,
                FromX = FromX,
                FromY = FromY,
                MoveStartedTick = MoveStartedTick,
                MoveDurationTicks = MoveDurationTicks,
                Agent = Agent,
            };
        }
    }
}
