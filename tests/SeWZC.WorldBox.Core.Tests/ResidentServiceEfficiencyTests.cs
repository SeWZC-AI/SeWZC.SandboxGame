using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core.Tests;

/// <summary>需求与年龄降效在现场医疗及离散服务入口产生实际影响。</summary>
public sealed class ResidentServiceEfficiencyTests
{
    /// <summary>医馆、医院及草药园的病程抵扣量按劳动类型与年龄缩减。</summary>
    [Theory]
    [InlineData(BuildingKind.Infirmary, 18, 100, 76)]
    [InlineData(BuildingKind.Infirmary, 18, 25, 88)]
    [InlineData(BuildingKind.Infirmary, 12, 100, 96)]
    [InlineData(BuildingKind.Hospital, 18, 100, 76)]
    [InlineData(BuildingKind.Hospital, 18, 25, 88)]
    [InlineData(BuildingKind.Hospital, 12, 100, 96)]
    [InlineData(BuildingKind.HerbGarden, 18, 100, 76)]
    [InlineData(BuildingKind.HerbGarden, 18, 25, 82)]
    [InlineData(BuildingKind.HerbGarden, 12, 100, 96)]
    public void Medical_work_scales_the_actual_disease_reduction(BuildingKind kind, double age, double sleep, int sickness)
    {
        var (fixture, building) = ServiceWorld(kind, age, sleep);
        var worker = fixture.Resident.Value;
        fixture.Engine.SpawnResidents(16, 16, RaceKind.Human, 1);
        fixture.Resident.Replace(worker);
        var patient = fixture.Engine.Residents.Single(p => p.Value.Id != fixture.ResidentId);
        patient.Replace(patient.Value with { X = 17, Y = 16, Health = 50, SicknessTicks = 100 });

        Assert.True(fixture.Engine.TryWorkAtBuilding(fixture.Resident.Value));

        Assert.Equal(sickness, patient.Value.SicknessTicks);
        Assert.True(patient.Value.Health > 50);
        if (kind == BuildingKind.Hospital)
            Assert.Equal(fixture.Engine.SimulationTick + (int)(SimulationTime.TicksPerYear
                * ResidentNeedsRules.WorkEfficiency(fixture.Resident.Value, true)), patient.Value.DiseaseImmuneUntilTick);
    }

    /// <summary>两成效率的劳动者须累计五轮劳动才完成固定服务，材料在完成时消耗。</summary>
    [Theory]
    [InlineData(BuildingKind.Library)]
    [InlineData(BuildingKind.SurveyOffice)]
    [InlineData(BuildingKind.Armory)]
    [InlineData(BuildingKind.WardTower)]
    [InlineData(BuildingKind.Pasture)]
    public void Discrete_services_wait_for_a_full_work_cycle(BuildingKind kind)
    {
        var (fixture, building) = ServiceWorld(kind, 12, 100);
        var inventory = fixture.Resident.Value.Inventory;
        var goat = fixture.Engine.Tiles[16 * 32 + 17].Value.AnimalPopulation(WildlifeKind.Goat);

        Assert.True(fixture.Engine.TryWorkAtBuilding(fixture.Resident.Value));

        Assert.Equal(.2, building.Value.ProductionProgress, 10);
        Assert.Equal(0, building.Value.ServiceActions);
        Assert.Equal(inventory, fixture.Resident.Value.Inventory);
        Assert.Equal(0, fixture.Resident.Value.Armor);
        Assert.Equal(0, fixture.Resident.Value.PersonalWard);
        Assert.Equal(0, building.Value.LivestockPopulation);
        Assert.Equal(goat, fixture.Engine.Tiles[16 * 32 + 17].Value.AnimalPopulation(WildlifeKind.Goat));
        for (var cycle = 1; cycle < 5; cycle++)
        {
            fixture.Engine.SimulationTick += 12;
            Assert.True(fixture.Engine.TryWorkAtBuilding(fixture.Resident.Value));
        }

        Assert.Equal(0, building.Value.ProductionProgress, 10);
        Assert.Equal(1, building.Value.ServiceActions);
        switch (kind)
        {
            case BuildingKind.Library:
                Assert.Contains(fixture.Resident.Value.Agent.Memory, f => f.Kind == AgentFactKind.Research);
                break;
            case BuildingKind.SurveyOffice:
                Assert.Contains(fixture.Resident.Value.Agent.Memory, f => f.Kind == AgentFactKind.SettlementLocation);
                break;
            case BuildingKind.Armory:
                Assert.Equal(30, fixture.Resident.Value.Armor);
                Assert.Equal(inventory.Alloy - 2, fixture.Resident.Value.Inventory.Alloy);
                break;
            case BuildingKind.WardTower:
                Assert.Equal(24, fixture.Resident.Value.PersonalWard);
                Assert.Equal(inventory.Crystals - .25, fixture.Resident.Value.Inventory.Crystals);
                break;
            case BuildingKind.Pasture:
                Assert.Equal(1, building.Value.LivestockPopulation);
                Assert.Equal(goat - 1, fixture.Engine.Tiles[16 * 32 + 17].Value.AnimalPopulation(WildlifeKind.Goat));
                break;
        }
    }

    /// <summary>低睡眠的结界劳动需要两轮，每轮之间仍遵守原有服务间隔，进度可保存恢复。</summary>
    [Fact]
    public void Ward_progress_and_service_cadence_survive_save_and_resume()
    {
        var (fixture, building) = ServiceWorld(BuildingKind.WardTower, 18, 25);
        Assert.True(fixture.Engine.TryWorkAtBuilding(fixture.Resident.Value));
        Assert.Equal(.5, building.Value.ProductionProgress);
        fixture.Engine.SimulationTick++;
        Assert.False(fixture.Engine.TryWorkAtBuilding(fixture.Resident.Value));
        var loaded = WorldEngine.ImportJson(fixture.Engine.ExportJson());
        loaded.SimulationTick += 11;

        Assert.True(loaded.TryWorkAtBuilding(loaded.GetResident(fixture.ResidentId)!));

        Assert.Equal(24, loaded.GetResident(fixture.ResidentId)!.PersonalWard);
        Assert.Equal(0, loaded.State.Society.Buildings.Single(b => b.Id == building.Value.Id).ProductionProgress);
        Assert.Equal(1, loaded.State.Society.Buildings.Single(b => b.Id == building.Value.Id).ServiceActions);
    }

    private static (WorldFixture Fixture, StateReference<Building> Building) ServiceWorld(BuildingKind kind, double age, double sleep)
    {
        var fixture = new WorldFixture();
        fixture.Engine.Buildings.RemoveAll(b => b.Value.Kind != BuildingKind.TownCenter);
        fixture.Town.Replace(fixture.Town.Value.WithResources(fixture.Town.Value.Resources with { Alloy = 4 }));
        if (ResearchRules.Unlocking(kind) is { } research)
            GrantResearch(fixture, research);
        if (kind == BuildingKind.Pasture)
            GrantResearch(fixture, Advancement.Agriculture);
        var ground = fixture.Engine.Tiles[16 * 32 + 17];
        ground.Replace(ground.Value with
        {
            Terrain = TerrainType.Grass, NationId = fixture.Town.Value.NationId,
            ClaimedSettlementId = fixture.Town.Value.Id,
        });
        ground.Replace(ground.Value.WithAnimalPopulation(WildlifeKind.Goat, 4));
        var building = new StateReference<Building>(new Building
        {
            Id = fixture.Engine.NextId++, SettlementId = fixture.Town.Value.Id, Kind = kind,
            X = 17, Y = 16, ConstructionProgress = 30,
        });
        fixture.Engine.Buildings.Add(building);
        fixture.Engine.SimulationTick = 12;
        fixture.Resident.Replace(fixture.Resident.Value with
        {
            Age = age, Race = kind == BuildingKind.HerbGarden ? RaceKind.Elf : RaceKind.Human,
            Profession = kind switch
            {
                BuildingKind.Library => Profession.Archivist,
                BuildingKind.SurveyOffice => Profession.Surveyor,
                BuildingKind.Pasture => Profession.Farmer,
                _ => Profession.Physician,
            },
            X = 17, Y = 16, FromX = 17, FromY = 16, Armor = 0, PersonalWard = 0,
            MagicTalent = 60, MagicTraining = 20, Mana = 100,
            Inventory = new ResourceStock { Food = 10, Water = 10, Medicine = 1, Alloy = 4, Crystals = 1 },
            Agent = new AgentState
            {
                Initialized = true, Sleep = sleep,
                Goal = new AgentGoal
                {
                    Kind = AgentGoalKind.Work, TargetEntityId = building.Value.Id, TargetX = 17, TargetY = 16,
                    PlayerDirected = true, ReviewTick = 100,
                },
            },
        });
        return (fixture, building);
    }

    private static void GrantResearch(WorldFixture fixture, Advancement research)
    {
        foreach (var prerequisite in research.Prerequisites)
            GrantResearch(fixture, prerequisite);
        fixture.Engine.GrantReceivedResearch(fixture.Town.Value.Id, research);
    }
}
