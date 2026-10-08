using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.Core.Tests;

/// <summary>设施劳动中的工位预约交接与死亡释放。</summary>
public sealed class WorkReservationTests
{
    /// <summary>严重损坏设施的维修也受现场工位上限约束，满员后不扣第二人的石材。</summary>
    [Fact]
    public void Repair_work_respects_the_daily_work_slot_limit()
    {
        var fixture = new WorldFixture();
        fixture.Engine.SpawnResidents(16, 16, RaceKind.Human, 1);
        fixture.Engine.Current.Tick = 1;
        var building = fixture.Engine.Current.Society.Buildings.Single(candidate => candidate.Kind == BuildingKind.TownCenter);
        building.Health = 10;
        building.WorkSlots = 1;
        foreach (var person in fixture.Engine.Current.Residents)
            person.Replace(person.Value with
            {
                Age = 25, Profession = Profession.Builder, X = 16, Y = 16, FromX = 16, FromY = 16,
                Inventory = new ResourceStock { Stone = 1 },
                Agent = person.Agent.Value with
                {
                    Goal = new AgentGoal { Kind = AgentGoalKind.Work, TargetEntityId = building.Id, PlayerDirected = true },
                },
            });
        var other = fixture.Engine.Current.Residents.Single(person => person.Id != fixture.ResidentId);

        Assert.True(fixture.Engine.TryWorkAtBuilding(fixture.Resident.Value));
        var repairedHealth = building.Health;
        Assert.False(fixture.Engine.TryWorkAtBuilding(other.Value));

        Assert.Equal(repairedHealth, building.Health);
        Assert.Equal(1, other.Inventory.Stone);
        Assert.Equal(fixture.ResidentId, Assert.Single(building.Workers));
    }

    /// <summary>现场致死伤害立即释放死者预约，后续居民无需等到日末归档才能接手医疗。</summary>
    [Fact]
    public void Lethal_damage_during_actions_releases_the_work_slot()
    {
        var fixture = new WorldFixture();
        fixture.Engine.SpawnResidents(0, 16, RaceKind.Human, 2);
        fixture.Engine.ConfigureWorld(fixture.Engine.State.Rules with
        {
            Aging = false, Hunger = false, Thirst = false, Disease = false, Births = false,
            Construction = false, Expansion = false, Research = false,
            Migration = false, Secession = false, Wars = false,
        }, false, true);
        var otherTown = fixture.Engine.Current.Settlements.Single(town => town.Id != fixture.Town.Id);
        fixture.Town.MaxClaimRadius = 8;
        for (var x = 8; x <= 16; x++)
        {
            var ground = fixture.Engine.Current.Tiles[16 * 32 + x];
            ground.NationId = fixture.Town.NationId;
            ground.ClaimedSettlementId = fixture.Town.Id;
        }
        for (var x = 0; x <= 5; x++)
        {
            var ground = fixture.Engine.Current.Tiles[16 * 32 + x];
            ground.NationId = otherTown.NationId;
            ground.ClaimedSettlementId = otherTown.Id;
        }
        GrantResearch(fixture, Advancement.BattleMagic);
        var towerId = fixture.Engine.GrantFacility(fixture.Town.Id, BuildingKind.StormSpire, 8, 16);
        var clinicId = fixture.Engine.GrantFacility(otherTown.Id, BuildingKind.Infirmary, 5, 16);
        var clinic = fixture.Engine.Current.Society.Buildings.Single(building => building.Id == clinicId);
        clinic.WorkSlots = 1;
        foreach (var building in fixture.Engine.Current.Society.Buildings)
            building.Enabled = building.Id == towerId || building.Id == clinicId;
        fixture.Town.Resources = otherTown.Resources = new ResourceStock { Food = 100 };
        foreach (var person in fixture.Engine.Current.Residents)
            person.Replace(person.Value with
            {
                Age = 25, Health = 100, SicknessTicks = 0, Profession = Profession.Builder,
                X = 5, Y = 16, FromX = 5, FromY = 16, MoveStartedTick = 0, MoveDurationTicks = 1,
                Inventory = new ResourceStock { Food = 1, Water = 1 },
                Agent = person.Agent.Value with { Initialized = true, NextThinkTick = 100, Goal = new AgentGoal() },
            });
        var caster = fixture.Resident;
        caster.Replace(caster.Value with
        {
            X = 8, FromX = 8, Profession = Profession.Battlemage, MagicTalent = 100, MagicTraining = 8, Mana = 100,
            Inventory = new ResourceStock { Food = 1, Water = 1, Crystals = 2 },
            Agent = caster.Agent.Value with
            {
                Goal = new AgentGoal
                {
                    Kind = AgentGoalKind.Work, TargetEntityId = towerId, TargetX = 8, TargetY = 16,
                    PlayerDirected = true, ReviewTick = 100,
                },
            },
        });
        caster.Agent.Memory.Add(new AgentFact
        {
            Id = 90_001, Kind = AgentFactKind.WarOrder, SubjectId = otherTown.NationId,
            TargetNationId = caster.NationId, OriginResidentId = caster.Id, SourceResidentId = caster.Id,
            Value = 1, Confidence = 1,
        });
        var otherResidents = fixture.Engine.Current.Residents.Where(person => person.SettlementId == otherTown.Id).ToArray();
        var victim = otherResidents[0];
        victim.Replace(victim.Value with
        {
            Health = 1, Armor = 0, PersonalWard = 0,
            Agent = victim.Agent.Value with
            {
                Goal = new AgentGoal
                {
                    Kind = AgentGoalKind.Work, TargetEntityId = clinicId, TargetX = 5, TargetY = 16,
                    PlayerDirected = true, ReviewTick = 100,
                },
            },
        });
        var replacement = otherResidents[1];
        replacement.Health = 50;
        replacement.Agent.NextThinkTick = 0;
        fixture.Engine.Current.Tick = (3 - replacement.Id % 4 + 4) % 4;

        fixture.Engine.Step();

        var deceased = Assert.Single(fixture.Engine.State.ArchivedResidents);
        Assert.Equal(victim.Id, deceased.Id);
        Assert.Equal(DeathCause.Magic, deceased.DeathCause);
        Assert.Equal(0, deceased.Health);
        Assert.Contains(replacement.Id, clinic.Workers);
        Assert.True(replacement.Health > 50.15);
    }

    /// <summary>运回产物时释放设施预约，后续居民可在同日接手仍空闲的工位。</summary>
    [Theory]
    [InlineData(BuildingKind.MiningHall, RaceKind.Dwarf)]
    [InlineData(BuildingKind.HuntingCamp, RaceKind.Orc)]
    [InlineData(BuildingKind.Reservoir, RaceKind.Human)]
    public void Returning_with_output_releases_the_work_slot(BuildingKind kind, RaceKind race)
    {
        var fixture = new WorldFixture();
        fixture.Engine.SpawnResidents(16, 16, RaceKind.Human, 1);
        fixture.Engine.ConfigureWorld(fixture.Engine.State.Rules with
        {
            Aging = false, Hunger = false, Thirst = false, Disease = false, Births = false,
            Construction = false, Expansion = false, Research = false,
            Migration = false, Secession = false, Wars = false,
        }, false, false);
        foreach (var person in fixture.Engine.Current.Residents)
            person.Replace(person.Value with
            {
                Race = race, Age = 25, Profession = Profession.Builder,
                X = 14, Y = 16, FromX = 14, FromY = 16, MoveStartedTick = 0, MoveDurationTicks = 1,
                Inventory = new ResourceStock { Food = 1, Water = 1 },
                Agent = person.Agent.Value with { Initialized = true, NextThinkTick = 100, Goal = new AgentGoal() },
            });
        for (var x = 14; x <= 15; x++)
        {
            var ground = fixture.Engine.Current.Tiles[16 * 32 + x];
            ground.NationId = fixture.Town.NationId;
            ground.ClaimedSettlementId = fixture.Town.Id;
        }
        var site = fixture.Engine.Current.Tiles[16 * 32 + 14];
        site.Terrain = kind switch
        {
            BuildingKind.MiningHall => TerrainType.Hills,
            BuildingKind.HuntingCamp => TerrainType.Forest,
            _ => TerrainType.Grass,
        };
        site.ResourceAmount = 100;
        site.NaturalWaterYield = 20;
        site.SetAnimalPopulation(WildlifeKind.Deer, 10);
        if (kind == BuildingKind.Reservoir)
            GrantResearch(fixture, Advancement.CivilEngineering);
        var buildingId = fixture.Engine.GrantFacility(fixture.Town.Id, kind, 14, 16);
        var building = fixture.Engine.Current.Society.Buildings.Single(candidate => candidate.Id == buildingId);
        building.WorkSlots = 1;
        foreach (var other in fixture.Engine.Current.Society.Buildings.Where(candidate => candidate.Id != buildingId))
            other.Enabled = false;
        fixture.Resident.Inventory = kind == BuildingKind.Reservoir
            ? new ResourceStock { Food = 1, Water = 3 }
            : new ResourceStock { Food = 3, Water = 1 };
        fixture.Resident.Agent.Goal = new AgentGoal
        {
            Kind = AgentGoalKind.Work, TargetEntityId = buildingId, TargetX = 14, TargetY = 16, ReviewTick = 100,
        };
        var replacement = fixture.Engine.Current.Residents.Single(person => person.Id != fixture.ResidentId);
        replacement.Agent.NextThinkTick = 0;
        fixture.Engine.Current.Tick = (3 - replacement.Id % 4 + 4) % 4;

        fixture.Engine.Step();

        Assert.Equal(AgentGoalKind.ReturnHome, fixture.Resident.Agent.Goal.Kind);
        Assert.Contains(replacement.Id, building.Workers);
        Assert.DoesNotContain(fixture.ResidentId, building.Workers);
        Assert.Equal(AgentGoalKind.Work, Assert.Single(replacement.Agent.Decisions).Goal);

    }

    private static void GrantResearch(WorldFixture fixture, Advancement research)
    {
        foreach (var prerequisite in research.Prerequisites)
            GrantResearch(fixture, prerequisite);
        fixture.Engine.GrantReceivedResearch(fixture.Town.Id, research);
    }
}
