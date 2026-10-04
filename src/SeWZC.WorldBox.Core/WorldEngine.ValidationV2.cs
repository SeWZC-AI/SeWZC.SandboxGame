using System.Diagnostics.CodeAnalysis;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private static void CheckV2([DoesNotReturnIf(false)] bool valid, string reason)
    {
        if (!valid) throw new ArgumentException("无效角色或存档：" + reason);
    }
    private static bool BoundedText(string? value, int length) => value is not null && value.Length <= length && !value.Any(c => char.IsControl(c) && c is not '\n' and not '\t');
    private static bool IdentityText(string? value, int length) => BoundedText(value, length) && !value!.Contains('\t');
    private static bool Number(double value, double min, double max) => double.IsFinite(value) && value >= min && value <= max;
    private static bool Coordinates(int x, int y, int width, int height) => x >= 0 && y >= 0 && x < width && y < height;

    private static void ValidateFactV2(AgentFact? fact, long tick, int width, int height)
    {
        CheckV2(fact is not null, "记忆不能为空。");
        CheckV2(fact!.EventId is >= 0 and < 2_000_000_000 && fact.CampaignEventId is >= 0 and < 2_000_000_000 && Enum.IsDefined(fact.WarObjective), "消息事件引用或战争目标无效。");
        CheckV2(fact!.TargetNationId is >= 0 and < 2_000_000_000, "外交消息目标无效。");
        CheckV2(fact!.Id is >= 0 and < 2_000_000_000 && Enum.IsDefined(fact.Kind) && Enum.IsDefined(fact.OriginProfession) && fact.SubjectId is >= 0 and < 2_000_000_000 && fact.OriginResidentId is >= 0 and < 2_000_000_000 && fact.SourceResidentId is >= 0 and < 2_000_000_000, "记忆编号或类型无效。");
        CheckV2(Coordinates(fact.X, fact.Y, width, height) || fact.X == -1 && fact.Y == -1, "记忆位置超出地图。");
        CheckV2(Number(fact.Value, -1_000_000_000, 1_000_000_000) && Number(fact.Confidence, 0, 1) && fact.Hops is >= 0 and <= 1000, "记忆数值无效。");
        CheckV2(fact.ObservedTick >= 0 && fact.ObservedTick <= fact.LearnedTick && fact.LearnedTick <= tick && BoundedText(fact.Text, 400), "记忆时间或文字无效。");
    }

    private static void ValidateResidentV2(Resident person, long tick, int width, int height)
    {
        CheckV2(IdentityText(person.Name, 80) && person.Name.Length > 0 && IdentityText(person.Trait, 80) && Enum.IsDefined(person.Race) && Enum.IsDefined(person.Profession) && Enum.IsDefined(person.Activity), "身份信息无效。");
        CheckV2(person.Agent is not null && person.Agent.ExplorationHeading is >= 0 and < 8, "资源勘察方向无效。");
        CheckV2(person.Agent.MaterialPriority is null or ResourceKind.Ore or ResourceKind.Stone or ResourceKind.Coal or ResourceKind.Oil or ResourceKind.RareEarth, "采矿材料目标无效。");
        CheckV2(Enum.IsDefined(person.DeathCause) && person.DeathTick >= 0 && person.DeathTick <= tick, "死亡记录无效。");
        CheckV2(person.DiseaseImmuneUntilTick >= 0 && person.DiseaseImmuneUntilTick <= tick + 180, "疾病恢复免疫时间无效。");
        CheckV2(Number(person.Age, 0, 1000) && Number(person.Health, 0, 100) && Number(person.Hunger, 0, 100) && Number(person.Thirst, 0, 100) && Number(person.Mana, 0, 1000) && Number(person.MagicTalent, 0, 100) && Number(person.MagicTraining, 0, 100) && person.SicknessTicks is >= 0 and <= 10_000, "生命或魔法数值无效。");
        CheckV2(Coordinates(person.X, person.Y, width, height) && Coordinates(person.FromX, person.FromY, width, height) && person.MoveStartedTick >= 0 && person.MoveStartedTick <= tick && person.MoveDurationTicks is >= 1 and <= 100, "移动位置或时间无效。");
        CheckV2(person.Inventory is not null && AdvancementRules.Resources.All(kind => Number(person.Inventory.Get(kind), 0, 1_000_000)), "背包数值无效。");
        CheckV2(Enum.IsDefined(person.TravelMode) && (person.TravelMode != TravelMode.Aircraft || person.Inventory.Aircraft >= 1)
            && (person.TravelMode != TravelMode.Boat || person.Inventory.Boats >= 1), "运输工具状态无效。");
        var agent = person.Agent;
        CheckV2(agent is not null && agent.Personality is not null && agent.Goal is not null && agent.Memory is not null && agent.Memory.Count <= 16 && agent.Decisions is not null && agent.Decisions.Count <= 6 && agent.CarriedMessages is not null && agent.CarriedMessages.Count <= 8, "心智结构或容量无效。");
        var personality = agent!.Personality;
        CheckV2(Number(personality.Courage, 0, 1) && Number(personality.Diligence, 0, 1) && Number(personality.Sociability, 0, 1) && Number(personality.Ambition, 0, 1) && Number(agent.Fatigue, 0, 100) && Number(agent.SocialNeed, 0, 100), "性格或需求超出范围。");
        var goal = agent.Goal;
        CheckV2(goal.NavigationTarget >= -1 && goal.NavigationTarget < width * height
            && goal.NavigationVisited is not null && goal.NavigationVisited.Count <= 256
            && goal.NavigationVisited.All(i => i >= 0 && i < width * height)
            && goal.NavigationBestDistance is >= 0 and <= 512 && goal.NavigationWithoutProgress is >= 0 and <= 64
            && goal.NavigationRetryTick >= 0 && goal.NavigationRetryTick <= tick + 100_000, "寻路记录无效。");
        CheckV2(Enum.IsDefined(goal.Kind) && Coordinates(goal.TargetX, goal.TargetY, width, height) && goal.TargetEntityId >= 0 && goal.TargetSettlementId >= 0 && goal.StartedTick >= 0 && goal.StartedTick <= tick && goal.ReviewTick is >= 0 && goal.ReviewTick <= tick + 100_000 && goal.WorkTicks is >= 0 and <= 1_000_000 && BoundedText(goal.Reason, 400), "目标位置、时间或内容无效。");
        CheckV2(agent.JobChangedTick >= -120 && agent.JobChangedTick <= tick && agent.NextThinkTick >= 0 && agent.NextThinkTick <= tick + 100_000 && agent.LastConversationTick >= 0 && agent.LastConversationTick <= tick && agent.MissionStartedTick >= 0 && agent.MissionStartedTick <= tick && agent.MissionRetryTick >= 0 && agent.MissionRetryTick <= tick + 100_000, "行动调度时间无效。");
        foreach (var fact in agent.Memory!) ValidateFactV2(fact, tick, width, height);
        foreach (var fact in agent.CarriedMessages!) ValidateFactV2(fact, tick, width, height);
        foreach (var decision in agent.Decisions!)
            CheckV2(decision is not null && Enum.IsDefined(decision.Goal) && decision.Tick >= 0 && decision.Tick <= tick && decision.KnowledgeObservedTick >= 0 && decision.KnowledgeObservedTick <= decision.Tick && Number(decision.Score, -1_000_000, 1_000_000) && BoundedText(decision.Reason, 500), "决策记录无效。");
        CheckV2(person.History is not null && person.History.Count <= 24, "个人历史最多保留24条。");
        foreach (var entry in person.History!)
            CheckV2(entry is not null && entry.Tick >= 0 && entry.Tick <= tick && Enum.IsDefined(entry.Importance) && Enum.IsDefined(entry.Experience) && Number(entry.Impact, -1, 1) && BoundedText(entry.Text, 500), "历史记录无效。");
    }
}
