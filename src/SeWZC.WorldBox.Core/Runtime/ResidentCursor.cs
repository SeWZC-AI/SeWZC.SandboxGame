using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>Resident 的引擎内定位引用；连续日常字段变化在快照边界合并为不可变状态。</summary>
internal sealed partial class ResidentCursor(Resident value) : StateCursor<Resident>(value)
{
    public int Id => base.Value.Id;

    public string Name
    {
        get => base.Value.Name;
        set
        {
            if (!EqualityComparer<string>.Default.Equals(Value.Name, value))
                ReplaceChanged(Value with { Name = value });
        }
    }

    public RaceKind Race
    {
        get => base.Value.Race;
        set
        {
            if (!EqualityComparer<RaceKind>.Default.Equals(Value.Race, value))
                ReplaceChanged(Value with { Race = value });
        }
    }

    public int X
    {
        get => _draft.X;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(_draft.X, value))
            {
                _draft.X = value;
                _draftChanged = true;
            }
        }
    }

    public int Y
    {
        get => _draft.Y;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(_draft.Y, value))
            {
                _draft.Y = value;
                _draftChanged = true;
            }
        }
    }

    public double Age
    {
        get => _draft.Age;
        set
        {
            if (!EqualityComparer<double>.Default.Equals(_draft.Age, value))
            {
                _draft.Age = value;
                _draftChanged = true;
            }
        }
    }

    public int NationId
    {
        get => base.Value.NationId;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.NationId, value))
                ReplaceChanged(Value with { NationId = value });
        }
    }

    public int SettlementId
    {
        get => base.Value.SettlementId;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.SettlementId, value))
                ReplaceChanged(Value with { SettlementId = value });
        }
    }

    public Profession Profession
    {
        get => base.Value.Profession;
        set
        {
            if (!EqualityComparer<Profession>.Default.Equals(Value.Profession, value))
                ReplaceChanged(Value with { Profession = value });
        }
    }

    public ResidentActivity Activity
    {
        get => _draft.Activity;
        set
        {
            if (!EqualityComparer<ResidentActivity>.Default.Equals(_draft.Activity, value))
            {
                _draft.Activity = value;
                _draftChanged = true;
            }
        }
    }

    public double Health
    {
        get => _draft.Health;
        set
        {
            if (!EqualityComparer<double>.Default.Equals(_draft.Health, value))
            {
                _draft.Health = value;
                _draftChanged = true;
            }
        }
    }

    public double Hunger
    {
        get => _draft.Hunger;
        set
        {
            if (!EqualityComparer<double>.Default.Equals(_draft.Hunger, value))
            {
                _draft.Hunger = value;
                _draftChanged = true;
            }
        }
    }

    public int SicknessTicks
    {
        get => _draft.SicknessTicks;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(_draft.SicknessTicks, value))
            {
                _draft.SicknessTicks = value;
                _draftChanged = true;
            }
        }
    }

    public int ArmyId
    {
        get => base.Value.ArmyId;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.ArmyId, value))
                ReplaceChanged(Value with { ArmyId = value });
        }
    }

    public string Trait
    {
        get => base.Value.Trait;
        set
        {
            if (!EqualityComparer<string>.Default.Equals(Value.Trait, value))
                ReplaceChanged(Value with { Trait = value });
        }
    }

    public long DiseaseImmuneUntilTick => base.Value.DiseaseImmuneUntilTick;

    public DeathCause DeathCause => base.Value.DeathCause;

    public long DeathTick => base.Value.DeathTick;

    public double Armor
    {
        get => base.Value.Armor;
        set
        {
            if (!EqualityComparer<double>.Default.Equals(Value.Armor, value))
                ReplaceChanged(Value with { Armor = value });
        }
    }

    public double PersonalWard
    {
        get => base.Value.PersonalWard;
        set
        {
            if (!EqualityComparer<double>.Default.Equals(Value.PersonalWard, value))
                ReplaceChanged(Value with { PersonalWard = value });
        }
    }

    public long FrozenUntilTick
    {
        get => base.Value.FrozenUntilTick;
        set
        {
            if (!EqualityComparer<long>.Default.Equals(Value.FrozenUntilTick, value))
                ReplaceChanged(Value with { FrozenUntilTick = value });
        }
    }

    public long LastRangedAttackTick => base.Value.LastRangedAttackTick;

    public TravelMode TravelMode
    {
        get => _draft.TravelMode;
        set
        {
            if (!EqualityComparer<TravelMode>.Default.Equals(_draft.TravelMode, value))
            {
                _draft.TravelMode = value;
                _draftChanged = true;
            }
        }
    }

    public double Thirst
    {
        get => _draft.Thirst;
        set
        {
            if (!EqualityComparer<double>.Default.Equals(_draft.Thirst, value))
            {
                _draft.Thirst = value;
                _draftChanged = true;
            }
        }
    }

    public int CultureId
    {
        get => base.Value.CultureId;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.CultureId, value))
                ReplaceChanged(Value with { CultureId = value });
        }
    }

    public AgentState Agent
    {
        get => _draft.Agent;
        set
        {
            if (!ReferenceEquals(_draft.Agent, value))
            {
                _draft.Agent = value;
                _draftChanged = true;
            }
        }
    }

    public ResourceStock Inventory
    {
        get => _draft.Inventory;
        set
        {
            if (!EqualityComparer<ResourceStock>.Default.Equals(_draft.Inventory, value))
            {
                _draft.Inventory = value;
                _draftChanged = true;
            }
        }
    }

    public double Mana
    {
        get => _draft.Mana;
        set
        {
            if (!EqualityComparer<double>.Default.Equals(_draft.Mana, value))
            {
                _draft.Mana = value;
                _draftChanged = true;
            }
        }
    }

    public double MagicTalent
    {
        get => base.Value.MagicTalent;
        set
        {
            if (!EqualityComparer<double>.Default.Equals(Value.MagicTalent, value))
                ReplaceChanged(Value with { MagicTalent = value });
        }
    }

    public double MagicTraining
    {
        get => base.Value.MagicTraining;
        set
        {
            if (!EqualityComparer<double>.Default.Equals(Value.MagicTraining, value))
                ReplaceChanged(Value with { MagicTraining = value });
        }
    }

    public int FromX
    {
        get => _draft.FromX;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(_draft.FromX, value))
            {
                _draft.FromX = value;
                _draftChanged = true;
            }
        }
    }

    public int FromY
    {
        get => _draft.FromY;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(_draft.FromY, value))
            {
                _draft.FromY = value;
                _draftChanged = true;
            }
        }
    }

    public long MoveStartedTick
    {
        get => _draft.MoveStartedTick;
        set
        {
            if (!EqualityComparer<long>.Default.Equals(_draft.MoveStartedTick, value))
            {
                _draft.MoveStartedTick = value;
                _draftChanged = true;
            }
        }
    }

    public int MoveDurationTicks
    {
        get => _draft.MoveDurationTicks;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(_draft.MoveDurationTicks, value))
            {
                _draft.MoveDurationTicks = value;
                _draftChanged = true;
            }
        }
    }

    public ImmutableList<ResidentHistoryEntry> History
    {
        get => Value.History;
        set
        {
            if (!ReferenceEquals(Value.History, value))
                ReplaceChanged(Value with { History = value });
        }
    }

    public static implicit operator Resident(ResidentCursor cursor)
    {
        return cursor.Value;
    }

    public static implicit operator ResidentCursor(Resident value)
    {
        return new ResidentCursor(value);
    }

    protected override void OnReplace(in Resident before, in Resident after)
    {
        _draft = new DailyDraft(after);
        _draftChanged = false;
    }
}
