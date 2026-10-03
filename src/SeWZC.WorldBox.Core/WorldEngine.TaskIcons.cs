namespace SeWZC.WorldBox.Core;

public enum ResidentTaskIcon { Explore, Log, Mine, Gather, Farm, Build, Upgrade, Research, Magic, Heal, Pickup, Deliver, Message, Trade, Claim, Water, Hunt, Fish, Rest, Eat, Talk, Flee, March, Smelt, Power, Craft, Ship, Plane, Crystal, Runic, Aether, Extinguish }

public sealed partial class WorldEngine
{
    public ResidentTaskIcon GetResidentTaskIcon(Resident person, Building? facility = null)
    {
        var goal = person.Agent.Goal;
        // A source-tile ID used for fishing or water is not a building ID.
        if (goal.Kind is AgentGoalKind.Work or AgentGoalKind.Study or AgentGoalKind.TrainMagic)
            facility ??= FindBuilding(goal.TargetEntityId);
        else facility = null;
        return goal.Kind switch
        {
            AgentGoalKind.ClaimLand => ResidentTaskIcon.Claim,
            AgentGoalKind.ExtinguishFire => ResidentTaskIcon.Extinguish,
            AgentGoalKind.FetchWater => ResidentTaskIcon.Water,
            AgentGoalKind.Hunt => ResidentTaskIcon.Hunt,
            AgentGoalKind.Fish => ResidentTaskIcon.Fish,
            AgentGoalKind.Gather => ResidentTaskIcon.Gather,
            AgentGoalKind.Study => ResidentTaskIcon.Research,
            AgentGoalKind.TrainMagic => ResidentTaskIcon.Magic,
            AgentGoalKind.DeliverMessage or AgentGoalKind.Petition => ResidentTaskIcon.Message,
            AgentGoalKind.Trade => ResidentTaskIcon.Trade,
            AgentGoalKind.ReturnHome => ResidentTaskIcon.Deliver,
            AgentGoalKind.Eat => ResidentTaskIcon.Eat,
            AgentGoalKind.Rest => ResidentTaskIcon.Rest,
            AgentGoalKind.Socialize => ResidentTaskIcon.Talk,
            AgentGoalKind.Flee => ResidentTaskIcon.Flee,
            AgentGoalKind.March => ResidentTaskIcon.March,
            AgentGoalKind.Work when facility is { IsUpgrading: true } => ResidentTaskIcon.Upgrade,
            AgentGoalKind.Work when facility is { IsCompleted: false } => ResidentTaskIcon.Build,
            AgentGoalKind.Work when facility is not null && AdvancementRules.For(facility.Kind) is { } recipe
                => MissingResources(person.Inventory, recipe.Input) is not null ? ResidentTaskIcon.Pickup : facility.Kind switch
                {
                    BuildingKind.Foundry => ResidentTaskIcon.Smelt, BuildingKind.PowerPlant => ResidentTaskIcon.Power,
                    BuildingKind.Fabricator => ResidentTaskIcon.Craft, BuildingKind.Dock => ResidentTaskIcon.Ship,
                    BuildingKind.Airfield => ResidentTaskIcon.Plane, BuildingKind.AutomatedFarm => ResidentTaskIcon.Farm,
                    BuildingKind.Crystallizer => ResidentTaskIcon.Crystal, BuildingKind.RunicGarden => ResidentTaskIcon.Runic,
                    BuildingKind.AetherForge => ResidentTaskIcon.Aether, _ => ResidentTaskIcon.Craft
                },
            AgentGoalKind.Work when facility?.Kind == BuildingKind.Farm => ResidentTaskIcon.Farm,
            AgentGoalKind.Work when facility?.Kind == BuildingKind.Academy => ResidentTaskIcon.Research,
            AgentGoalKind.Work when facility?.Kind == BuildingKind.Infirmary => ResidentTaskIcon.Heal,
            AgentGoalKind.Work when person.Profession == Profession.Lumberjack => ResidentTaskIcon.Log,
            AgentGoalKind.Work when person.Profession == Profession.Miner => ResidentTaskIcon.Mine,
            AgentGoalKind.Work => ResidentTaskIcon.Build,
            _ => ResidentTaskIcon.Explore
        };
    }

    public static string TaskIconName(ResidentTaskIcon icon) => icon switch
    {
        ResidentTaskIcon.Log => "伐木", ResidentTaskIcon.Mine => "采矿", ResidentTaskIcon.Gather => "采食物", ResidentTaskIcon.Farm => "农场耕作",
        ResidentTaskIcon.Build => "建筑施工", ResidentTaskIcon.Upgrade => "升级或改造", ResidentTaskIcon.Research => "研究", ResidentTaskIcon.Magic => "魔法训练",
        ResidentTaskIcon.Heal => "治疗", ResidentTaskIcon.Pickup => "返仓取原料", ResidentTaskIcon.Deliver => "运回物资", ResidentTaskIcon.Message => "递送消息",
        ResidentTaskIcon.Trade => "贸易", ResidentTaskIcon.Claim => "占领地块", ResidentTaskIcon.Extinguish => "用水扑救火灾", ResidentTaskIcon.Water => "打水或寻水", ResidentTaskIcon.Hunt => "狩猎",
        ResidentTaskIcon.Fish => "捕鱼", ResidentTaskIcon.Rest => "休息", ResidentTaskIcon.Eat => "领取口粮", ResidentTaskIcon.Talk => "交谈",
        ResidentTaskIcon.Flee => "避险", ResidentTaskIcon.March => "行军", ResidentTaskIcon.Smelt => "冶炼合金",
        ResidentTaskIcon.Power => "制造动力单元", ResidentTaskIcon.Craft => "精密制造", ResidentTaskIcon.Ship => "造船",
        ResidentTaskIcon.Plane => "制造飞机", ResidentTaskIcon.Crystal => "凝炼魔晶", ResidentTaskIcon.Runic => "符文种植",
        ResidentTaskIcon.Aether => "以太转化", _ => "勘察或迁居"
    };
}
