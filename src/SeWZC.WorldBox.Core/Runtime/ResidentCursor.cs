using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>Resident 的引擎内定位引用；连续日常字段变化在快照边界合并为不可变状态。</summary>
internal sealed partial class ResidentCursor(Resident value) : StateReference<Resident>(value)
{
    public int Id => base.Value.Id;

    public string Name => base.Value.Name;

    public RaceKind Race => base.Value.Race;

    public int X
    {
        get => _motion.X;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(_motion.X, value))
            {
                _motion = _motion with { X = value };
                MarkDraftChanged();
            }
        }
    }

    public int Y
    {
        get => _motion.Y;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(_motion.Y, value))
            {
                _motion = _motion with { Y = value };
                MarkDraftChanged();
            }
        }
    }

    public double Age
    {
        get => _body.Age;
        set
        {
            if (!EqualityComparer<double>.Default.Equals(_body.Age, value))
            {
                _body = _body with { Age = value };
                MarkDraftChanged();
            }
        }
    }

    public int NationId => base.Value.NationId;

    public int SettlementId => base.Value.SettlementId;

    public Profession Profession => base.Value.Profession;

    public ResidentActivity Activity
    {
        get => _body.Activity;
        set
        {
            if (!EqualityComparer<ResidentActivity>.Default.Equals(_body.Activity, value))
            {
                _body = _body with { Activity = value };
                MarkDraftChanged();
            }
        }
    }

    public double Health
    {
        get => _body.Health;
        set
        {
            if (!EqualityComparer<double>.Default.Equals(_body.Health, value))
            {
                _body = _body with { Health = value };
                MarkDraftChanged();
            }
        }
    }

    public double Hunger
    {
        get => _body.Hunger;
        set
        {
            if (!EqualityComparer<double>.Default.Equals(_body.Hunger, value))
            {
                _body = _body with { Hunger = value };
                MarkDraftChanged();
            }
        }
    }

    public int SicknessTicks
    {
        get => _body.SicknessTicks;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(_body.SicknessTicks, value))
            {
                _body = _body with { SicknessTicks = value };
                MarkDraftChanged();
            }
        }
    }

    public int ArmyId => base.Value.ArmyId;

    public string Trait => base.Value.Trait;

    public long DiseaseImmuneUntilTick => base.Value.DiseaseImmuneUntilTick;

    public DeathCause DeathCause => base.Value.DeathCause;

    public long DeathTick => base.Value.DeathTick;

    public double Armor => base.Value.Armor;

    public double PersonalWard => base.Value.PersonalWard;

    public long FrozenUntilTick => base.Value.FrozenUntilTick;

    public long LastRangedAttackTick => base.Value.LastRangedAttackTick;

    public TravelMode TravelMode
    {
        get => _motion.TravelMode;
        set
        {
            if (!EqualityComparer<TravelMode>.Default.Equals(_motion.TravelMode, value))
            {
                _motion = _motion with { TravelMode = value };
                MarkDraftChanged();
            }
        }
    }

    public double Thirst
    {
        get => _body.Thirst;
        set
        {
            if (!EqualityComparer<double>.Default.Equals(_body.Thirst, value))
            {
                _body = _body with { Thirst = value };
                MarkDraftChanged();
            }
        }
    }

    public int CultureId => base.Value.CultureId;

    public AgentState Agent
    {
        get => _agent;
        set
        {
            if (!ReferenceEquals(_agent, value))
            {
                _agent = value;
                MarkDraftChanged();
            }
        }
    }

    public ResourceStock Inventory
    {
        get => _inventory;
        set
        {
            if (!EqualityComparer<ResourceStock>.Default.Equals(_inventory, value))
            {
                _inventory = value;
                MarkDraftChanged();
            }
        }
    }

    public double Mana
    {
        get => _body.Mana;
        set
        {
            if (!EqualityComparer<double>.Default.Equals(_body.Mana, value))
            {
                _body = _body with { Mana = value };
                MarkDraftChanged();
            }
        }
    }

    public double MagicTalent => base.Value.MagicTalent;

    public double MagicTraining => base.Value.MagicTraining;

    public int FromX
    {
        get => _motion.FromX;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(_motion.FromX, value))
            {
                _motion = _motion with { FromX = value };
                MarkDraftChanged();
            }
        }
    }

    public int FromY
    {
        get => _motion.FromY;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(_motion.FromY, value))
            {
                _motion = _motion with { FromY = value };
                MarkDraftChanged();
            }
        }
    }

    public long MoveStartedTick
    {
        get => _motion.MoveStartedTick;
        set
        {
            if (!EqualityComparer<long>.Default.Equals(_motion.MoveStartedTick, value))
            {
                _motion = _motion with { MoveStartedTick = value };
                MarkDraftChanged();
            }
        }
    }

    public int MoveDurationTicks
    {
        get => _motion.MoveDurationTicks;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(_motion.MoveDurationTicks, value))
            {
                _motion = _motion with { MoveDurationTicks = value };
                MarkDraftChanged();
            }
        }
    }

    public ImmutableList<ResidentHistoryEntry> History => base.Value.History;

    protected override void OnReplace(in Resident before, in Resident after)
    {
        _body = new BodyFields(after);
        _motion = new MotionFields(after);
        _agent = after.Agent;
        _inventory = after.Inventory;
        _draftChanged = false;
        _snapshot = after;
        _snapshotCurrent = true;
    }
}
