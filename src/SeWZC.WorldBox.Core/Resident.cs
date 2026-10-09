using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>参与世界模拟的居民个体。</summary>
public sealed partial record Resident
{
    /// <summary>居民的稳定 ID。</summary>
    public int Id
    {
        get => _identity.Id;
        init
        {
            if (_identity.Id != value)
                _identity = _identity with { Id = value };
        }
    }

    /// <summary>居民的显示名称。</summary>
    public string Name
    {
        get => _identity.Name;
        init
        {
            if (_identity.Name != value)
                _identity = _identity with { Name = value };
        }
    }

    /// <summary>居民的种族。</summary>
    public RaceKind Race
    {
        get => _identity.Race;
        init
        {
            if (_identity.Race != value)
                _identity = _identity with { Race = value };
        }
    }

    /// <summary>所在地点的横向地格坐标。</summary>
    public int X { get; init; }

    /// <summary>所在地点的纵向地格坐标。</summary>
    public int Y { get; init; }

    /// <summary>年龄，以模拟年为单位，允许小数。</summary>
    public double Age { get; init; }

    /// <summary>居民所属国家的 ID。</summary>
    public int NationId
    {
        get => _identity.NationId;
        init
        {
            if (_identity.NationId != value)
                _identity = _identity with { NationId = value };
        }
    }

    /// <summary>居民所属聚落的 ID。</summary>
    public int SettlementId
    {
        get => _identity.SettlementId;
        init
        {
            if (_identity.SettlementId != value)
                _identity = _identity with { SettlementId = value };
        }
    }

    /// <summary>当前职业分工。</summary>
    public Profession Profession
    {
        get => _identity.Profession;
        init
        {
            if (_identity.Profession != value)
                _identity = _identity with { Profession = value };
        }
    }

    /// <summary>当前活动或身体状态。</summary>
    public ResidentActivity Activity { get; init; }

    /// <summary>当前生命值，归零时居民死亡。</summary>
    public double Health { get; init; } = 100;

    /// <summary>饥饿程度，越高表示越缺粮。</summary>
    public double Hunger { get; init; }

    /// <summary>疫病剩余模拟 tick 数，0 表示未患病。</summary>
    public int SicknessTicks { get; init; }

    /// <summary>所加入的军队 ID，0 表示未编入军队。</summary>
    public int ArmyId
    {
        get => _effects.ArmyId;
        init
        {
            if (_effects.ArmyId != value)
                _effects = _effects with { ArmyId = value };
        }
    }

    /// <summary>供档案显示的性格特征文字。</summary>
    public string Trait
    {
        get => _identity.Trait;
        init
        {
            if (_identity.Trait != value)
                _identity = _identity with { Trait = value };
        }
    }

    /// <summary>疫病康复后的暂时免疫截止 tick 序。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long DiseaseImmuneUntilTick
    {
        get => _effects.DiseaseImmuneUntilTick;
        init
        {
            if (_effects.DiseaseImmuneUntilTick != value)
                _effects = _effects with { DiseaseImmuneUntilTick = value };
        }
    }

    /// <summary>死亡直接原因，存活时为 <c>None</c>。</summary>
    [JsonRequired]
    public DeathCause DeathCause
    {
        get => _effects.DeathCause;
        init
        {
            if (_effects.DeathCause != value)
                _effects = _effects with { DeathCause = value };
        }
    }

    /// <summary>死亡时的模拟 tick 序。</summary>
    [JsonRequired]
    public long DeathTick
    {
        get => _effects.DeathTick;
        init
        {
            if (_effects.DeathTick != value)
                _effects = _effects with { DeathTick = value };
        }
    }

    /// <summary>当前个人护甲强度。</summary>
    [JsonRequired]
    public double Armor
    {
        get => _effects.Armor;
        init
        {
            if (BitConverter.DoubleToInt64Bits(_effects.Armor) != BitConverter.DoubleToInt64Bits(value))
                _effects = _effects with { Armor = value };
        }
    }

    /// <summary>当前可吸收伤害的个人符文护甲余量。</summary>
    [JsonRequired]
    public double PersonalWard
    {
        get => _effects.PersonalWard;
        init
        {
            if (BitConverter.DoubleToInt64Bits(_effects.PersonalWard) != BitConverter.DoubleToInt64Bits(value))
                _effects = _effects with { PersonalWard = value };
        }
    }

    /// <summary>冰霜减速效果的截止 tick 序。</summary>
    [JsonRequired]
    public long FrozenUntilTick
    {
        get => _effects.FrozenUntilTick;
        init
        {
            if (_effects.FrozenUntilTick != value)
                _effects = _effects with { FrozenUntilTick = value };
        }
    }

    /// <summary>最近一次远程攻击的模拟 tick 序。</summary>
    [JsonRequired]
    public long LastRangedAttackTick
    {
        get => _effects.LastRangedAttackTick;
        init
        {
            if (_effects.LastRangedAttackTick != value)
                _effects = _effects with { LastRangedAttackTick = value };
        }
    }

    /// <summary>当前移动采用的交通方式。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public TravelMode TravelMode { get; init; }

    /// <summary>口渴程度，越高表示越缺水。</summary>
    [JsonRequired]
    public double Thirst { get; init; }

    /// <summary>文化归属的稳定 ID。</summary>
    public int CultureId
    {
        get => _identity.CultureId;
        init
        {
            if (_identity.CultureId != value)
                _identity = _identity with { CultureId = value };
        }
    }

    /// <summary>居民的认知与自主行动状态。</summary>
    public AgentState Agent { get; init; } = new();

    /// <summary>居民实际随身携带的资源和物品。</summary>
    public ResourceStock Inventory { get; init; } = new();

    /// <summary>当前可消耗的魔力。</summary>
    public double Mana { get; init; } = 20;

    /// <summary>魔法天赋，影响学习和施法能力。</summary>
    public double MagicTalent
    {
        get => _identity.MagicTalent;
        init
        {
            if (BitConverter.DoubleToInt64Bits(_identity.MagicTalent) != BitConverter.DoubleToInt64Bits(value))
                _identity = _identity with { MagicTalent = value };
        }
    }

    /// <summary>累计魔法训练程度。</summary>
    public double MagicTraining
    {
        get => _identity.MagicTraining;
        init
        {
            if (BitConverter.DoubleToInt64Bits(_identity.MagicTraining) != BitConverter.DoubleToInt64Bits(value))
                _identity = _identity with { MagicTraining = value };
        }
    }

    /// <summary>当前移动区段起点的横向地格坐标。</summary>
    public int FromX { get; init; }

    /// <summary>当前移动区段起点的纵向地格坐标。</summary>
    public int FromY { get; init; }

    /// <summary>当前移动区段开始的模拟 tick 序。</summary>
    public long MoveStartedTick { get; init; }

    /// <summary>当前移动区段所需的模拟 tick 数。</summary>
    public int MoveDurationTicks { get; init; } = 1;

    /// <summary>容量受限的个人经历记录。</summary>
    public ImmutableList<ResidentHistoryEntry> History
    {
        get => _identity.History;
        init
        {
            if (!ReferenceEquals(_identity.History, value))
                _identity = _identity with { History = value };
        }
    }
}
