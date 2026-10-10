using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private bool CompleteBuildingWorkCycle(StateReference<Building> building, StateReference<Resident> person, bool service = false)
    {
        var progress = building.Value.ProductionProgress + ResidentNeedsRules.WorkEfficiency(person.Value, IsMentalWork(building.Value));
        var complete = progress >= 1;
        building.Replace(building.Value with
        {
            ProductionProgress = complete ? progress - 1 : progress,
            LastServiceTick = service ? SimulationTick : building.Value.LastServiceTick,
        });
        return complete;
    }

    private static int ServiceDurationTicks(Resident person, Building building, int ticks) =>
        (int)(ticks * ResidentNeedsRules.WorkEfficiency(person, IsMentalWork(building)));

    private static double CombatEffort(IEnumerable<StateReference<Resident>> residents, int elapsedTicks)
    {
        var effort = 0d;
        foreach (var person in residents)
        {
            if (person.Value.Health <= 0 || !ResidentNeedsRules.CanWork(person.Value))
                continue;
            effort += ResidentNeedsRules.WorkEfficiency(person.Value);
            person.Replace(person.Value.WithAction(person.Value.Agent with
            {
                Fatigue = ResidentNeedsRules.ExertionFatigue(person.Value, ResidentNeedsRules.PhysicalWorkCostPerTick * elapsedTicks),
            }, ResidentActivity.Working));
        }
        return effort;
    }

    private void UpdateResidentNeeds()
    {
        var homeQuality = new Dictionary<int, double>();
        foreach (var reference in Residents)
        {
            var person = reference.Value;
            if (person.Health <= 0)
                continue;
            var quality = 1d;
            if (person.Activity is ResidentActivity.Resting or ResidentActivity.Sleeping or ResidentActivity.Unconscious
                    or ResidentActivity.Talking or ResidentActivity.Eating
                && _settlements.TryGetValue(person.SettlementId, out var home)
                && Distance(person.X, person.Y, home.Value.X, home.Value.Y) <= 1)
            {
                if (!homeQuality.TryGetValue(home.Value.Id, out quality))
                {
                    quality = HomeRestMultiplier(home.Value.Id, home.Value.X, home.Value.Y);
                    homeQuality.Add(home.Value.Id, quality);
                }
            }
            reference.Replace(person.WithAgent(ResidentNeedsRules.Advance(person, quality)));
        }
    }

    private static bool IsLaborGoal(AgentGoalKind kind) => kind is AgentGoalKind.Gather or AgentGoalKind.Work
        or AgentGoalKind.Study or AgentGoalKind.TrainMagic or AgentGoalKind.ClaimLand or AgentGoalKind.FetchWater
        or AgentGoalKind.Hunt or AgentGoalKind.Fish or AgentGoalKind.ExtinguishFire or AgentGoalKind.DeliverMessage
        or AgentGoalKind.Trade or AgentGoalKind.Petition;

    private static bool IsMentalWork(Building building) => building.IsCompleted && !building.IsUpgrading && building.Health >= 50
        && building.Kind is BuildingKind.Academy or BuildingKind.Waystation or BuildingKind.SignalTower
            or BuildingKind.ArcaneSanctum or BuildingKind.Infirmary or BuildingKind.Crystallizer
            or BuildingKind.Market or BuildingKind.AssemblyHall or BuildingKind.TradeGuild or BuildingKind.SacredGrove
            or BuildingKind.Hospital or BuildingKind.Library or BuildingKind.SurveyOffice or BuildingKind.AlchemyLab
            or BuildingKind.WardTower or BuildingKind.StormSpire or BuildingKind.Waygate;
}
