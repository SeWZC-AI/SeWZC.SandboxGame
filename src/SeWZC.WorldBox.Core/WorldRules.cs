namespace SeWZC.WorldBox.Core;

/// <summary>控制自主模拟机制的开关、强度和速率参数。</summary>
public sealed record WorldRules
{
    /// <summary>是否允许自然资源恢复。</summary>
    public bool ResourceRegeneration { get; set; } = true;
    /// <summary>是否允许现有火灾向邻格蔓延。</summary>
    public bool FireSpread { get; set; } = true;
    /// <summary>资源采集速率倍率，范围为 0.25 至 3。</summary>
    public double GatheringRate { get; set; } = 1;
    /// <summary>战斗伤害倍率，范围为 0.25 至 3。</summary>
    public double CombatDamageRate { get; set; } = 1;
    /// <summary>是否允许自主出生。</summary>
    public bool Births { get; set; } = true;
    /// <summary>是否推进居民年龄及衰老。</summary>
    public bool Aging { get; set; } = true;
    /// <summary>是否启用居民饥饿与粮食需求。</summary>
    public bool Hunger { get; set; } = true;
    /// <summary>是否启用居民口渴与饮水需求。</summary>
    public bool Thirst { get; set; } = true;
    /// <summary>是否启用疫病传播和损伤。</summary>
    public bool Disease { get; set; } = true;
    /// <summary>是否允许自主安排建设。</summary>
    public bool Construction { get; set; } = true;
    /// <summary>是否允许自主安排研究。</summary>
    public bool Research { get; set; } = true;
    /// <summary>是否允许自主安排城镇扩充。</summary>
    public bool Expansion { get; set; } = true;
    /// <summary>是否允许自主贸易。</summary>
    public bool Trade { get; set; } = true;
    /// <summary>是否允许自主缔结联盟。</summary>
    public bool Alliances { get; set; } = true;
    /// <summary>是否允许自主宣战。</summary>
    public bool Wars { get; set; } = true;
    /// <summary>是否允许自主协商停战。</summary>
    public bool Peace { get; set; } = true;
    /// <summary>是否允许居民自主迁徙。</summary>
    public bool Migration { get; set; } = true;
    /// <summary>是否允许地方自主分裂建国。</summary>
    public bool Secession { get; set; } = true;
    /// <summary>局部资源冲突强度，0 表示关闭，最高为 3。</summary>
    public int Conflict { get; set; } = 1;
    /// <summary>自然灾害频率等级，0 表示关闭，最高为 3。</summary>
    public int DisasterFrequency { get; set; } = 1;
    /// <summary>自然灾害强度等级，范围为 1 至 3。</summary>
    public int DisasterStrength { get; set; } = 1;
    /// <summary>建设和研究的推进倍率，范围为 0.5 至 3。</summary>
    public double DevelopmentRate { get; set; } = 1;
    /// <summary>魔法发展速率倍率，范围为 0.5 至 3。</summary>
    public double MagicRate { get; set; } = 1;

    /// <summary>创建指定预设的独立世界规则对象。</summary>
    /// <param name="preset">要采用的世界规则预设。</param>
    public static WorldRules For(WorldPreset preset)
    {
        return preset switch
        {
            WorldPreset.Flourishing => new WorldRules
                { Wars = false, Secession = false, Conflict = 0, DisasterFrequency = 0 },
            WorldPreset.Turbulent => new WorldRules { Conflict = 3, DisasterFrequency = 2, DisasterStrength = 2 },
            _ => new WorldRules(),
        };
    }
}
