namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    /// <summary>根据居民目标、职业和设施状态选择当前具体任务图标。</summary>
    /// <param name="person">居民。</param>
    /// <param name="facility">已查到的目标设施，空值时按任务查找；非设施任务忽略此值。</param>
    public ResidentTaskIcon GetResidentTaskIcon(Resident person, Building? facility = null)
    {
        var goal = person.Agent.Goal;
        // 取水和捕鱼的目标编号表示资源地格，不能作为建筑 ID 查找。
        if (goal.Kind is AgentGoalKind.Work or AgentGoalKind.Study or AgentGoalKind.TrainMagic)
            facility ??= FindBuilding(goal.TargetEntityId)?.Value;
        else
            facility = null;
        return goal.Kind switch
        {
            AgentGoalKind.Eat => ResidentTaskIcon.Eat,
            AgentGoalKind.Gather => ResidentTaskIcon.Gather,
            AgentGoalKind.Work when facility is { IsUpgrading: true } => ResidentTaskIcon.Upgrade,
            AgentGoalKind.Rest or AgentGoalKind.Sleep => ResidentTaskIcon.Rest,
            AgentGoalKind.Flee => ResidentTaskIcon.Flee,
            AgentGoalKind.Socialize => ResidentTaskIcon.Talk,
            AgentGoalKind.DeliverMessage or AgentGoalKind.Petition => ResidentTaskIcon.Message,
            AgentGoalKind.Trade => ResidentTaskIcon.Trade,
            AgentGoalKind.Study => ResidentTaskIcon.Research,
            AgentGoalKind.TrainMagic => ResidentTaskIcon.Magic,
            AgentGoalKind.March => ResidentTaskIcon.March,
            AgentGoalKind.ReturnHome => ResidentTaskIcon.Deliver,
            AgentGoalKind.ClaimLand => ResidentTaskIcon.Claim,
            AgentGoalKind.FetchWater => ResidentTaskIcon.Water,
            AgentGoalKind.Hunt => ResidentTaskIcon.Hunt,
            AgentGoalKind.Fish => ResidentTaskIcon.Fish,
            AgentGoalKind.ExtinguishFire => ResidentTaskIcon.Extinguish,
            AgentGoalKind.Work when facility is { IsCompleted: false } => ResidentTaskIcon.Build,
            AgentGoalKind.Work when facility is not null && ProductionRules.For(facility.Kind) is { } recipe
                => MissingResources(person.Inventory, recipe.Input) is not null
                    ? ResidentTaskIcon.Pickup
                    : facility.Kind switch
                    {
                        BuildingKind.Foundry => ResidentTaskIcon.Smelt,
                        BuildingKind.PowerPlant => ResidentTaskIcon.Power,
                        BuildingKind.AutomatedFarm => ResidentTaskIcon.Farm,
                        BuildingKind.Fabricator => ResidentTaskIcon.Craft,
                        BuildingKind.Crystallizer => ResidentTaskIcon.Crystal,
                        BuildingKind.RunicGarden => ResidentTaskIcon.Runic,
                        BuildingKind.AetherForge => ResidentTaskIcon.Aether,
                        BuildingKind.Airfield => ResidentTaskIcon.Plane,
                        BuildingKind.Shipyard => ResidentTaskIcon.Ship,
                        _ => ResidentTaskIcon.Craft,
                    },
            AgentGoalKind.Work when facility?.Kind == BuildingKind.Well => ResidentTaskIcon.Water,
            AgentGoalKind.Work when facility?.Kind == BuildingKind.TownCenter => ResidentTaskIcon.Build,
            AgentGoalKind.Work when facility?.Kind == BuildingKind.Farm => ResidentTaskIcon.Farm,
            AgentGoalKind.Work when facility?.Kind == BuildingKind.Academy => ResidentTaskIcon.Research,
            AgentGoalKind.Work when facility?.Kind == BuildingKind.Infirmary => ResidentTaskIcon.Heal,
            AgentGoalKind.Work when person.Profession == Profession.Lumberjack => ResidentTaskIcon.Log,
            AgentGoalKind.Work when person.Profession == Profession.Miner => ResidentTaskIcon.Mine,
            AgentGoalKind.Work => ResidentTaskIcon.Build,
            _ => ResidentTaskIcon.Explore,
        };
    }

    /// <summary>返回任务图标对应的中文动作名称。</summary>
    /// <param name="icon">居民任务图标类别。</param>
    public static string TaskIconName(ResidentTaskIcon icon)
    {
        return icon switch
        {
            ResidentTaskIcon.Log => "伐木",
            ResidentTaskIcon.Mine => "采矿",
            ResidentTaskIcon.Gather => "采食物",
            ResidentTaskIcon.Farm => "农场耕作",
            ResidentTaskIcon.Build => "建筑施工",
            ResidentTaskIcon.Upgrade => "升级或改造",
            ResidentTaskIcon.Research => "研究",
            ResidentTaskIcon.Magic => "魔法训练",
            ResidentTaskIcon.Heal => "治疗",
            ResidentTaskIcon.Pickup => "返仓取原料",
            ResidentTaskIcon.Deliver => "运回物资",
            ResidentTaskIcon.Message => "递送消息",
            ResidentTaskIcon.Trade => "贸易",
            ResidentTaskIcon.Claim => "占领地块",
            ResidentTaskIcon.Water => "打水或寻水",
            ResidentTaskIcon.Hunt => "狩猎",
            ResidentTaskIcon.Fish => "捕鱼",
            ResidentTaskIcon.Rest => "休息",
            ResidentTaskIcon.Eat => "领取口粮",
            ResidentTaskIcon.Talk => "交谈",
            ResidentTaskIcon.Flee => "避险",
            ResidentTaskIcon.March => "行军",
            ResidentTaskIcon.Smelt => "冶炼合金",
            ResidentTaskIcon.Power => "制造动力单元",
            ResidentTaskIcon.Craft => "精密制造",
            ResidentTaskIcon.Ship => "造船",
            ResidentTaskIcon.Plane => "制造飞机",
            ResidentTaskIcon.Crystal => "凝炼魔晶",
            ResidentTaskIcon.Runic => "符文种植",
            ResidentTaskIcon.Aether => "以太转化",
            ResidentTaskIcon.Extinguish => "用水扑救火灾",
            _ => "勘察或迁居",
        };
    }
}
