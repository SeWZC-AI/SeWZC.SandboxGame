using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>居民死亡时记录的直接原因。</summary>
public enum DeathCause
{
    /// <summary>尚未死亡。</summary>
    None,
    /// <summary>饥饿。</summary>
    Starvation,
    /// <summary>衰老。</summary>
    OldAge,
    /// <summary>火灾。</summary>
    Fire,
    /// <summary>疫病。</summary>
    Disease,
    /// <summary>战斗。</summary>
    Battle,
    /// <summary>战斗法术。</summary>
    Magic,
    /// <summary>陨石。</summary>
    Meteor,
    /// <summary>溺水。</summary>
    Drowning,
    /// <summary>地形变化。</summary>
    TerrainChange,
    /// <summary>局部冲突。</summary>
    Conflict,
    /// <summary>玩家干预。</summary>
    PlayerIntervention,
    /// <summary>脱水。</summary>
    Dehydration,
}

public sealed partial class Resident
{
    /// <summary>死亡直接原因，存活时为 <c>None</c>。</summary>
    [JsonRequired]
    public DeathCause DeathCause { get; set; }

    /// <summary>死亡时的模拟日序。</summary>
    [JsonRequired]
    public long DeathTick { get; set; }
}

public sealed partial class WorldEngine
{
    /// <summary>返回居民死亡原因的中文说明。</summary>
    /// <param name="cause">居民的死亡原因。</param>
    public static string DeathCauseName(DeathCause cause)
    {
        return cause switch
        {
            DeathCause.Dehydration => "长期缺水导致脱水致死", DeathCause.Starvation => "长期缺粮导致饥饿致死",
            DeathCause.OldAge => "超过种族寿命后衰老致死",
            DeathCause.Fire => "在燃烧地块受到致命灼伤", DeathCause.Disease => "疫病造成致命损伤",
            DeathCause.Battle => "战斗中受到致命攻击", DeathCause.Magic => "被战斗法术击杀",
            DeathCause.Meteor => "被陨石撞击致死", DeathCause.Drowning => "地形变为水域后无处逃生，溺亡",
            DeathCause.TerrainChange => "地形变化造成致命损伤", DeathCause.Conflict => "资源冲突斗殴中受到致命伤",
            DeathCause.PlayerIntervention => "玩家将生命设为零", _ => "尚未死亡",
        };
    }

    private void DamageResident(Resident person, double damage, DeathCause cause)
    {
        if (person.Health <= 0) return;
        person.Health = Math.Max(0, person.Health - damage);
        if (person.Health <= 0)
        {
            person.DeathCause = cause;
            person.DeathTick = State.Tick;
        }
    }
}
