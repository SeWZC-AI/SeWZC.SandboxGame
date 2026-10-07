using SeWZC.WorldBox.Core.Runtime;
namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    /// <summary>返回居民死亡原因的中文说明。</summary>
    /// <param name="cause">居民的死亡原因。</param>
    public static string DeathCauseName(DeathCause cause)
    {
        return cause switch
        {
            DeathCause.Starvation => "长期缺粮导致饥饿致死",
            DeathCause.OldAge => "超过种族寿命后衰老致死",
            DeathCause.Fire => "在燃烧地块受到致命灼伤",
            DeathCause.Disease => "疫病造成致命损伤",
            DeathCause.Battle => "战斗中受到致命攻击",
            DeathCause.Magic => "被战斗法术击杀",
            DeathCause.Meteor => "被陨石撞击致死",
            DeathCause.Drowning => "地形变为水域后无处逃生，溺亡",
            DeathCause.TerrainChange => "地形变化造成致命损伤",
            DeathCause.Conflict => "资源冲突斗殴中受到致命伤",
            DeathCause.PlayerIntervention => "玩家将生命设为零",
            DeathCause.Dehydration => "长期缺水导致脱水致死",
            _ => "尚未死亡",
        };
    }

    private void DamageResident(ResidentCursor person, double damage, DeathCause cause)
    {
        if (person.Health <= 0)
            return;
        person.Health = Math.Max(0, person.Health - damage);
        if (person.Health <= 0)
        {
            person.Replace(person.Value with { DeathCause = cause, DeathTick = Current.Tick });
        }
    }
}
