using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core.Tests;

/// <summary>每日死亡归档与现场医疗劳动的衔接。</summary>
public sealed class MedicalWorkTests
{
    /// <summary>当天病死者退出医疗候选，诊所继续治疗仍存活的患者。</summary>
    [Fact]
    public void Medical_work_treats_the_living_patient_after_daily_death()
    {
        var (fixture, clinic, worker) = PrepareClinic(3);
        var patient = fixture.Engine.Current.Residents.Single(person =>
            person.Id != fixture.ResidentId && person.Id != worker.Id);
        patient.Replace(patient.Value with { Health = 50, SicknessTicks = 1 });

        fixture.Engine.Step();

        var deceased = Assert.Single(fixture.Engine.State.ArchivedResidents);
        Assert.Equal(DeathCause.Disease, deceased.DeathCause);
        Assert.Equal(0, deceased.Health);
        Assert.True(patient.Health > 50 - .2 / SimulationTime.TicksPerDay);
        Assert.Contains(worker.Id, clinic.Workers);
        Assert.Equal(99.95, fixture.Town.Resources.Food, 6);
    }

    /// <summary>附近只剩健康居民时，不为当天病死者消耗医疗物资。</summary>
    [Fact]
    public void Medical_work_does_not_spend_food_on_an_archived_patient()
    {
        var (fixture, clinic, _) = PrepareClinic(2);

        fixture.Engine.Step();

        Assert.Equal(0, Assert.Single(fixture.Engine.State.ArchivedResidents).Health);
        Assert.Empty(clinic.Workers);
        Assert.Equal(100, fixture.Town.Resources.Food);
    }

    /// <summary>病死者预约的唯一工位当天释放，其他居民可自主接手医疗劳动。</summary>
    [Fact]
    public void Daily_death_releases_the_reserved_work_slot()
    {
        var (fixture, clinic, worker) = PrepareClinic(3);
        clinic.WorkSlots = 1;
        fixture.Resident.Agent = fixture.Resident.Agent with { Goal = worker.Agent.Goal };
        var patient = fixture.Engine.Current.Residents.Single(person =>
            person.Id != fixture.ResidentId && person.Id != worker.Id);
        patient.Health = 50;
        foreach (var building in fixture.Engine.Current.Buildings.Where(building => building.Id != clinic.Id))
            building.Enabled = false;
        fixture.Engine.Current.Tick = 8 + (3 - worker.Id % 4 + 4) % 4;
        worker.Replace(worker.Value with
        {
            Inventory = new ResourceStock { Food = 1, Water = 1 },
            Agent = worker.Agent with { NextThinkTick = 0, Goal = new AgentGoal() },
        });

        fixture.Engine.Step();

        Assert.Equal(AgentGoalKind.Work, worker.Agent.Goal.Kind);
        Assert.Equal(clinic.Id, worker.Agent.Goal.TargetEntityId);
        Assert.Contains(worker.Id, clinic.Workers);
        Assert.True(patient.Health > 50 + .15 / SimulationTime.TicksPerDay);
    }

    private static (WorldFixture Fixture, BuildingCursor Clinic, ResidentCursor Worker) PrepareClinic(int residentCount)
    {
        var fixture = new WorldFixture();
        fixture.Engine.SpawnResidents(16, 16, RaceKind.Human, residentCount - 1);
        fixture.Engine.ConfigureWorld(fixture.Engine.State.Rules with
        {
            Aging = false,
            Hunger = false,
            Thirst = false,
            Births = false,
            Construction = false,
            Expansion = false,
            Research = false,
            Migration = false,
            Secession = false,
            Wars = false,
        }, false, false);
        for (var x = 14; x <= 15; x++)
        {
            var ground = fixture.Engine.Current.Tiles[16 * 32 + x];
            ground.NationId = fixture.Town.NationId;
            ground.ClaimedSettlementId = fixture.Town.Id;
        }

        var clinicId = fixture.Engine.GrantFacility(fixture.Town.Id, BuildingKind.Infirmary, 14, 16);
        var clinic = fixture.Engine.Current.Buildings.Single(building => building.Id == clinicId);
        fixture.Town.Resources = new ResourceStock { Food = 100 };
        foreach (var person in fixture.Engine.Current.Residents)
            person.Replace(person.Value with
            {
                Age = 25,
                Health = 100,
                SicknessTicks = 0,
                DiseaseImmuneUntilTick = 180,
                X = 14,
                Y = 16,
                FromX = 14,
                FromY = 16,
                MoveStartedTick = 0,
                MoveDurationTicks = 1,
                Agent = person.Agent with
                {
                    Initialized = true, NextThinkTick = 100, Goal = new AgentGoal { ReviewTick = 100 },
                },
            });
        fixture.Resident.Replace(fixture.Resident.Value with { Health = .001, SicknessTicks = 1 });
        var worker = fixture.Engine.Current.Residents.First(person => person.Id != fixture.ResidentId);
        worker.Replace(worker.Value with
        {
            Profession = Profession.Builder,
            Agent = worker.Agent with
            {
                Goal = new AgentGoal
                {
                    Kind = AgentGoalKind.Work,
                    TargetEntityId = clinicId,
                    TargetX = 14,
                    TargetY = 16,
                    PlayerDirected = true,
                    ReviewTick = 100,
                },
            },
        });
        return (fixture, clinic, worker);
    }
}
