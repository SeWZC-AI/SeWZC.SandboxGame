using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private static long RoutineDayStart(long tick)
    {
        var start = SimulationTime.DayIndex(tick) * SimulationTime.TicksPerDay + SimulationTime.WakeTick;
        return Math.Max(0, start > tick ? start - SimulationTime.TicksPerDay : start);
    }

    private static bool DailyWorkGoal(AgentGoalKind kind) => kind is AgentGoalKind.Gather or AgentGoalKind.Work
        or AgentGoalKind.Study or AgentGoalKind.TrainMagic or AgentGoalKind.Hunt or AgentGoalKind.Fish;

    private void PlanResidentDay(StateReference<Resident> reference, bool refresh = false)
    {
        var person = reference.Value;
        if (person.Agent.Goal.PlayerDirected && SimulationTick < person.Agent.Goal.ReviewTick)
            return;
        var previous = person.Agent.DailyPlan;
        if (!refresh && previous is not null && RoutineDayStart(previous.PlannedTick) == RoutineDayStart(SimulationTick))
            return;
        var day = SimulationTime.DayIndex(RoutineDayStart(SimulationTick)) * SimulationTime.TicksPerDay;
        var nextMorning = day + SimulationTime.TicksPerDay + SimulationTime.WakeTick;
        var work = DailyWorkGoal(person.Agent.Goal.Kind) ? person.Agent.Goal
            : person.Agent.DaytimeGoal is { } daytime && DailyWorkGoal(daytime.Kind) ? daytime : previous?.WorkGoal;
        var home = ResidentHome(person);
        var distance = home is null ? 0 : Math.Max(Distance(person.X, person.Y, home.Value.X, home.Value.Y),
            work is null ? 0 : Distance(work.TargetX, work.TargetY, home.Value.X, home.Value.Y));
        var travel = (int)Math.Ceiling(distance * AgentMoveCost(reference, Index(person.X, person.Y)))
                     + Math.Max(0, person.MoveDurationTicks - (int)(SimulationTick - person.MoveStartedTick));
        var returnTick = Math.Clamp(day + SimulationTime.SleepTick - travel - DailyRoutineRules.ReturnMarginTicks,
            day + SimulationTime.WakeTick, day + SimulationTime.ReturnHomeTick);
        // 已在家时，无法留出白天活动时间的旧工作点应重选，不能每天晨起就开始返程。
        if (person.IsInsideHome && work is not null && returnTick == day + SimulationTime.WakeTick)
        {
            work = null;
            var goal = DailyWorkGoal(person.Agent.Goal.Kind)
                ? new AgentGoal { TargetX = person.X, TargetY = person.Y, Reason = "原工作点过远，重新安排当天工作" }
                : person.Agent.Goal;
            ChangeWorkReservation(person.Agent.Goal, goal);
            person = person.WithAgent(person.Agent with { Goal = goal, DaytimeGoal = null, NextThinkTick = SimulationTick });
            returnTick = day + SimulationTime.ReturnHomeTick;
        }
        var fatigueCost = ResidentNeedsRules.PhysicalWorkCostPerTick;
        if (work is not null && FindBuilding(work.TargetEntityId) is { } facility && IsMentalWork(facility.Value))
            fatigueCost *= ResidentNeedsRules.MentalWorkCostRatio;
        var fatiguePerTick = fatigueCost / ResidentNeedsRules.StaminaCapacity(person) * ResidentNeedsRules.MaximumPercent;
        var sleepPerTick = ResidentNeedsRules.SleepConsumptionPerTick / ResidentNeedsRules.SleepCapacity(person) * ResidentNeedsRules.MaximumPercent;
        var restTick = SimulationTick + (long)Math.Ceiling(Math.Max(0, Math.Min(
            (DailyRoutineRules.RestFatigueThreshold - person.Agent.Fatigue) / fatiguePerTick,
            (person.Agent.Sleep - ResidentNeedsRules.FullEfficiencyThreshold * ResidentNeedsRules.MaximumPercent) / sleepPerTick)));
        var plan = new ResidentDailyPlan
        {
            PlannedTick = SimulationTick, ReturnHomeTick = returnTick,
            FoodReviewTick = Rules.Hunger ? SupplyReview(person.Inventory.Food, FoodUse(person)) : nextMorning,
            WaterReviewTick = Rules.Thirst ? SupplyReview(person.Inventory.Water, LocalWaterUse(reference)) : nextMorning,
            RestReviewTick = Math.Min(nextMorning, Math.Max(SimulationTick + DailyRoutineRules.MinimumReviewTicks, restTick)),
            WorkGoal = work?.ResetNavigation(),
        };
        var next = NextDailyReview(plan);
        reference.Replace(person.WithAgent(person.Agent with
        {
            DailyPlan = plan,
            NextThinkTick = DailyWorkGoal(person.Agent.Goal.Kind) && person.Hunger < 60 && person.Thirst < 60
                ? Math.Min(person.Agent.NextThinkTick, next) : person.Agent.NextThinkTick,
        }));

        long SupplyReview(double stock, double dailyUse) => Math.Min(nextMorning,
            SimulationTick + Math.Max(DailyRoutineRules.MinimumReviewTicks,
                (long)Math.Floor(Math.Max(0, stock / dailyUse - DailyRoutineRules.SupplyReserveDays) * SimulationTime.TicksPerDay)));
    }

    private long NextDailyReview(ResidentDailyPlan plan)
    {
        var next = RoutineDayStart(plan.PlannedTick) + SimulationTime.TicksPerDay;
        foreach (var scheduled in new[] { plan.FoodReviewTick, plan.WaterReviewTick, plan.RestReviewTick })
            if (scheduled > SimulationTick)
                next = Math.Min(next, scheduled);
        return Math.Max(SimulationTick + 1, next);
    }

    private bool DailyPlanInterrupted(StateReference<Resident> reference, StateReference<Settlement> town)
    {
        var person = reference.Value;
        var goal = person.Agent.Goal;
        if (!DailyWorkGoal(goal.Kind) || !IsWorkDay(reference))
            return false;
        if (person.Health < 40 || person.SicknessTicks > 0 || person.Agent.Fatigue > DailyRoutineRules.RestFatigueThreshold
            || person.Agent.Sleep < ResidentNeedsRules.FullEfficiencyThreshold * ResidentNeedsRules.MaximumPercent)
            return true;
        if (goal.TargetEntityId != 0 && goal.Kind is AgentGoalKind.Work or AgentGoalKind.Study or AgentGoalKind.TrainMagic)
            return Distance(person.X, person.Y, goal.TargetX, goal.TargetY) <= 6
                   && (FindBuilding(goal.TargetEntityId) is not { } building || !BuildingHasWork(building.Value, reference));
        return goal.Kind == AgentGoalKind.Gather ? !FoodSupplyNeeded(reference, town)
            : goal.Kind == AgentGoalKind.Work && !LocalMaterialsNeeded(reference, town);
    }

    private bool RepeatDailyWork(StateReference<Resident> person, StateReference<Settlement> home)
    {
        if (person.Value.Agent.DailyPlan?.WorkGoal is not { } work
            || person.Value.Agent.Goal.Kind is not (AgentGoalKind.Idle or AgentGoalKind.ReturnHome or AgentGoalKind.Rest)
            || person.Value.Hunger >= 20 || person.Value.Thirst >= 60
            || RecoveryGoalContinues(person))
            return false;
        var candidate = new StateReference<Resident>(person.Value.WithAgent(person.Value.Agent.WithGoal(work.ResetNavigation())));
        if (!ProductiveGoalContinues(candidate, home))
            return false;
        ChangeWorkReservation(person.Value.Agent.Goal, candidate.Value.Agent.Goal);
        person.Replace(person.Value.WithAgent(candidate.Value.Agent));
        DeferGoalReview(person);
        return true;
    }
}
