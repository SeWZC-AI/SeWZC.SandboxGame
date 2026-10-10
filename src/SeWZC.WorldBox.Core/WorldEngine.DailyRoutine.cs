using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private bool FollowDailyRoutine(StateReference<Resident> person, StateReference<Settlement> town, bool emergency)
    {
        var agent = person.Value.Agent;
        var time = SimulationTime.TimeOfDay(SimulationTick);
        if ((agent.Goal.PlayerDirected && SimulationTick < agent.Goal.ReviewTick)
            || person.Value.ArmyId != 0 || agent.Goal.Kind == AgentGoalKind.Rescue)
            return false;
        var dwelling = ResidentHome(person.Value);
        var destination = RestDestination(person.Value);
        var journey = agent.DestinationSettlementId != 0 || agent.Goal.Kind == AgentGoalKind.Migrate || town.Value.FoundationPending;
        var evening = time >= SimulationTime.ReturnHomeTick || time < SimulationTime.WakeTick
                      || (!journey && agent.DailyPlan is { } plan && SimulationTick >= plan.ReturnHomeTick);
        // 已到水源且缺少随身饮水时，先完成眼前的补给，再返家。
        if (evening && time >= SimulationTime.WakeTick && time < SimulationTime.SleepTick
            && agent.Goal.Kind == AgentGoalKind.FetchWater && agent.Goal.TargetEntityId > 0
            && agent.Goal.TargetEntityId <= Tiles.Count && person.Value.Inventory.Water < WaterCollectionTarget(person)
            && Distance(person.Value.X, person.Value.Y, agent.Goal.TargetX, agent.Goal.TargetY) <= 1
            && AvailableWater((agent.Goal.TargetEntityId - 1) % Width, (agent.Goal.TargetEntityId - 1) / Width) > 0)
            return false;
        if (!evening || emergency)
        {
            if (agent.Goal.Kind == AgentGoalKind.Sleep)
            {
                if (!emergency && dwelling is not null && (person.Value.X != destination.X || person.Value.Y != destination.Y))
                {
                    MoveAgentTowards(person, destination.X, destination.Y, ResidentActivity.Wandering);
                    return true;
                }
                var resumed = (agent.DaytimeGoal ?? agent.DailyPlan?.WorkGoal ?? new AgentGoal()).ResetNavigation();
                ChangeWorkReservation(agent.Goal, resumed);
                person.Replace(person.Value.WithAction(agent with
                {
                    Goal = resumed, DaytimeGoal = null, NextThinkTick = SimulationTick,
                }, ResidentActivity.Resting) with { IsInsideHome = false });
            }
            else if (person.Value.Activity == ResidentActivity.Sleeping && agent.Goal.Kind != AgentGoalKind.Rest)
                person.Replace(person.Value.WithActivity(ResidentActivity.Resting));
            return false;
        }
        if (person.Value.FrozenUntilTick > SimulationTick)
        {
            person.Replace(person.Value.WithActivity(ResidentActivity.Resting));
            return true;
        }
        var sleeping = time >= SimulationTime.SleepTick || time < SimulationTime.WakeTick;
        if (journey)
        {
            if (!sleeping)
                return false;
            person.Replace(person.Value.WithActivity(ResidentActivity.Sleeping));
            return true;
        }
        if (agent.Goal.Kind != AgentGoalKind.Sleep)
        {
            var morning = SimulationTime.DayIndex(SimulationTick) * SimulationTime.TicksPerDay + SimulationTime.WakeTick;
            if (morning <= SimulationTick)
                morning += SimulationTime.TicksPerDay;
            var sleep = new AgentGoal
            {
                Kind = AgentGoalKind.Sleep, TargetX = destination.X, TargetY = destination.Y,
                TargetSettlementId = town.Value.Id, TargetEntityId = dwelling?.Value.Id ?? 0,
                StartedTick = SimulationTick, ReviewTick = morning,
                Reason = dwelling is null ? "暂无空余住所，按作息就地休息与露宿"
                    : "按当日安排提前沿实际道路返回住宅，夜间睡眠，晨起继续熟悉任务",
            };
            ChangeWorkReservation(agent.Goal, sleep);
            agent = agent with { Goal = sleep, DaytimeGoal = agent.Goal, NextThinkTick = morning };
        }
        else if (agent.Goal.TargetX != destination.X || agent.Goal.TargetY != destination.Y
                 || agent.Goal.TargetEntityId != (dwelling?.Value.Id ?? 0) || agent.Goal.PlayerDirected)
        {
            agent = agent with
            {
                Goal = (agent.Goal with
                {
                    TargetX = destination.X, TargetY = destination.Y, TargetEntityId = dwelling?.Value.Id ?? 0,
                    PlayerDirected = false, Reason = "指定安排已结束，按作息返回分配的住宅",
                }).ResetNavigation(),
            };
        }
        person.Replace(person.Value.WithAgent(agent));
        if (dwelling is not null && (person.Value.X != destination.X || person.Value.Y != destination.Y))
        {
            MoveAgentTowards(person, destination.X, destination.Y, ResidentActivity.Wandering);
            return true;
        }
        EnterResidentHome(person);
        person.Replace(person.Value.WithActivity(sleeping ? ResidentActivity.Sleeping : ResidentActivity.Resting));
        return true;
    }
}
