using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    // 作息只改变未来行动；返家沿真实路径移动，睡眠不推进驻留或劳动量。
    private bool FollowDailyRoutine(ResidentCursor person, SettlementCursor home, bool emergency)
    {
        var agent = person.Agent;
        var time = SimulationTime.TimeOfDay(Current.Tick);
        var evening = time >= SimulationTime.ReturnHomeTick || time < SimulationTime.WakeTick;
        if ((agent.Goal.PlayerDirected && Current.Tick < agent.Goal.ReviewTick) || person.ArmyId != 0)
            return false;

        if (!evening || emergency)
        {
            if (agent.Goal.Kind == AgentGoalKind.Sleep)
            {
                // 尚未走完返程时先到家；晨起不能把人再次拉回昨天的远处目标。
                if (!evening && !emergency && (Distance(person.X, person.Y, home.X, home.Y) > 1
                                               || !Walkable(person.X, person.Y, person.Race)))
                {
                    MoveAgentTowards(person, home.X, home.Y, ResidentActivity.Wandering);
                    return true;
                }

                var resumed = (agent.DaytimeGoal ?? new AgentGoal()).ResetNavigation();
                ChangeWorkReservation(agent.Goal, resumed);
                person.Agent = agent with { Goal = resumed, DaytimeGoal = null, NextThinkTick = Current.Tick };
                person.Activity = ResidentActivity.Resting;
            }

            return false;
        }

        if (person.FrozenUntilTick > Current.Tick)
        {
            person.Activity = ResidentActivity.Resting;
            return true;
        }

        var sleeping = time >= SimulationTime.SleepTick || time < SimulationTime.WakeTick;
        // 远处取水与劳动先取得实物再返仓；夜间宿营，避免每日往返耗尽白天。
        var journey =
            agent.DestinationSettlementId != 0 || agent.Goal.Kind is AgentGoalKind.Migrate or AgentGoalKind.Explore
                                               || (agent.Goal.Kind == AgentGoalKind.FetchWater &&
                                                   Distance(person.X, person.Y, home.X, home.Y) > 1)
                                               || (agent.Goal.Kind is AgentGoalKind.Gather or AgentGoalKind.Work
                                                       or AgentGoalKind.Study
                                                       or AgentGoalKind.TrainMagic or AgentGoalKind.Hunt
                                                       or AgentGoalKind.Fish
                                                   && Distance(agent.Goal.TargetX, agent.Goal.TargetY, home.X, home.Y) >
                                                   3
                                                   && Distance(person.X, person.Y, home.X, home.Y) > 1)
                                               || home.FoundationPending;
        var medicalRest = agent.Goal.Kind == AgentGoalKind.Rest && agent.Goal.TargetEntityId != 0;
        if (journey || medicalRest)
        {
            if (!sleeping)
                return false;
            if (agent.Fatigue > 0)
                person.Agent = agent with { Fatigue = Math.Max(0, agent.Fatigue - 2.2) };
            person.Activity = ResidentActivity.Sleeping;
            return true;
        }

        if (agent.Goal.Kind != AgentGoalKind.Sleep)
        {
            var morning = SimulationTime.DayIndex(Current.Tick) * SimulationTime.TicksPerDay
                          + SimulationTime.WakeTick;
            if (morning <= Current.Tick)
                morning += SimulationTime.TicksPerDay;
            var sleep = new AgentGoal
            {
                Kind = AgentGoalKind.Sleep,
                TargetX = home.X,
                TargetY = home.Y,
                TargetSettlementId = home.Id,
                StartedTick = Current.Tick,
                ReviewTick = morning,
                Reason = "傍晚沿实际道路返家，夜间睡眠，晨起继续白天活动",
            };
            ChangeWorkReservation(agent.Goal, sleep);
            agent = agent with { Goal = sleep, DaytimeGoal = agent.Goal, NextThinkTick = morning };
            person.Agent = agent;
        }

        if (Distance(person.X, person.Y, home.X, home.Y) > 1
            || !Walkable(person.X, person.Y, person.Race))
        {
            MoveAgentTowards(person, home.X, home.Y, ResidentActivity.Wandering);
            return true;
        }

        person.Activity = sleeping ? ResidentActivity.Sleeping : ResidentActivity.Resting;
        if (person.Agent.Fatigue > 0)
        {
            person.Agent = person.Agent with
            {
                Fatigue = Math.Max(0, person.Agent.Fatigue - (sleeping ? 2.2 : .8) * HomeRestMultiplier(person)),
            };
        }

        return true;
    }
}
