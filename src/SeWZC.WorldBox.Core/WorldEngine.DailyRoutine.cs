using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    // 作息只改变未来行动；返家沿真实路径移动，睡眠不推进驻留或劳动量。
    private bool FollowDailyRoutine(StateReference<Resident> person, StateReference<Settlement> home, bool emergency)
    {
        var agent = person.Value.Agent;
        var time = SimulationTime.TimeOfDay(SimulationTick);
        var evening = time >= SimulationTime.ReturnHomeTick || time < SimulationTime.WakeTick;
        if ((agent.Goal.PlayerDirected && SimulationTick < agent.Goal.ReviewTick) || person.Value.ArmyId != 0)
            return false;

        if (!evening || emergency)
        {
            if (agent.Goal.Kind == AgentGoalKind.Sleep)
            {
                // 尚未走完返程时先到家；晨起不能把人再次拉回昨天的远处目标。
                if (!evening && !emergency && (Distance(person.Value.X, person.Value.Y, home.Value.X, home.Value.Y) > 1
                                               || !Walkable(person.Value.X, person.Value.Y, person.Value.Race)))
                {
                    MoveAgentTowards(person, home.Value.X, home.Value.Y, ResidentActivity.Wandering);
                    return true;
                }

                var resumed = (agent.DaytimeGoal ?? new AgentGoal()).ResetNavigation();
                ChangeWorkReservation(agent.Goal, resumed);
                person.Replace(person.Value.WithAction(agent with { Goal = resumed, DaytimeGoal = null, NextThinkTick = SimulationTick }, ResidentActivity.Resting));
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
        // 远处取水与劳动先取得实物再返仓；夜间宿营，避免每日往返耗尽白天。
        var journey =
            agent.DestinationSettlementId != 0 || agent.Goal.Kind is AgentGoalKind.Migrate or AgentGoalKind.Explore
                                               || (agent.Goal.Kind == AgentGoalKind.FetchWater &&
                                                   Distance(person.Value.X, person.Value.Y, home.Value.X, home.Value.Y) > 1)
                                               || (agent.Goal.Kind is AgentGoalKind.Gather or AgentGoalKind.Work
                                                       or AgentGoalKind.Study
                                                       or AgentGoalKind.TrainMagic or AgentGoalKind.Hunt
                                                       or AgentGoalKind.Fish
                                                   && Distance(agent.Goal.TargetX, agent.Goal.TargetY, home.Value.X, home.Value.Y) >
                                                   3
                                                   && Distance(person.Value.X, person.Value.Y, home.Value.X, home.Value.Y) > 1)
                                               || home.Value.FoundationPending;
        var medicalRest = agent.Goal.Kind == AgentGoalKind.Rest && agent.Goal.TargetEntityId != 0;
        if (journey || medicalRest)
        {
            if (!sleeping)
                return false;
            person.Replace(person.Value.WithActivity(ResidentActivity.Sleeping));
            return true;
        }

        if (agent.Goal.Kind != AgentGoalKind.Sleep)
        {
            var morning = SimulationTime.DayIndex(SimulationTick) * SimulationTime.TicksPerDay
                          + SimulationTime.WakeTick;
            if (morning <= SimulationTick)
                morning += SimulationTime.TicksPerDay;
            var sleep = new AgentGoal
            {
                Kind = AgentGoalKind.Sleep,
                TargetX = home.Value.X,
                TargetY = home.Value.Y,
                TargetSettlementId = home.Value.Id,
                StartedTick = SimulationTick,
                ReviewTick = morning,
                Reason = "傍晚沿实际道路返家，夜间睡眠，晨起继续白天活动",
            };
            ChangeWorkReservation(agent.Goal, sleep);
            agent = agent with { Goal = sleep, DaytimeGoal = agent.Goal, NextThinkTick = morning };
        }

        if (Distance(person.Value.X, person.Value.Y, home.Value.X, home.Value.Y) > 1
            || !Walkable(person.Value.X, person.Value.Y, person.Value.Race))
        {
            person.Replace(person.Value.WithAgent(agent));
            MoveAgentTowards(person, home.Value.X, home.Value.Y, ResidentActivity.Wandering);
            return true;
        }

        person.Replace(person.Value.WithAction(agent, sleeping ? ResidentActivity.Sleeping : ResidentActivity.Resting));

        return true;
    }
}
