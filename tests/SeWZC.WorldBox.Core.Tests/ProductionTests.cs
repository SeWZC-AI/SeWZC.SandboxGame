using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core.Tests;

/// <summary>到场加工的材料消耗、实物产出和拒绝边界。</summary>
public sealed class ProductionTests
{
    private static (WorldFixture Fixture, StateReference<Building> Foundry) FoundryWorld()
    {
        var fixture = new WorldFixture();
        foreach (var prerequisite in Advancement.Industry.Prerequisites)
            fixture.Engine.GrantReceivedResearch(fixture.Town.Value.Id, prerequisite);
        fixture.Engine.GrantReceivedResearch(fixture.Town.Value.Id, Advancement.Industry);
        var foundry = new StateReference<Building>(new Building
        {
            Id = fixture.Engine.NextId++,
            SettlementId = fixture.Town.Value.Id,
            Kind = BuildingKind.Foundry,
            X = 17,
            Y = 16,
            ConstructionProgress = 30,
        });
        fixture.Engine.Buildings.Add(foundry);
        var ground = fixture.Engine.Tiles[16 * 32 + 17];
        ground.Replace(ground.Value.WithNationId(fixture.Town.Value.NationId));
        ground.Replace(ground.Value.WithClaimedSettlementId(fixture.Town.Value.Id));
        var worker = fixture.Resident;
        worker.Replace(worker.Value with { Age = 25 });
        worker.Replace(worker.Value with { Profession = Profession.Builder });
        worker.Replace(worker.Value with { FromX = 17, X = 17 });
        worker.Replace(worker.Value with { FromY = 16, Y = 16 });
        worker.Replace(worker.Value.WithInventory(new ResourceStock { Coal = 1, Ore = 2 }));
        worker.Replace(worker.Value.WithAgent(worker.Value.Agent with
        {
            Goal = new AgentGoal
            {
                Kind = AgentGoalKind.Work,
                TargetEntityId = foundry.Value.Id,
                TargetX = 17,
                TargetY = 16,
            },
        }));
        return (fixture, foundry);
    }

    /// <summary>一次加工从随身原料生成随身产物，尚未递送至仓库。</summary>
    [Fact]
    public void Work_consumes_carried_inputs_and_keeps_output_with_the_worker()
    {
        var (fixture, foundry) = FoundryWorld();
        var warehouseAlloy = fixture.Town.Value.Resources.Alloy;

        Assert.True(fixture.Engine.TryWorkAtBuilding(fixture.Resident.Value));

        Assert.Equal(0, fixture.Resident.Value.Inventory.Coal);
        Assert.Equal(0, fixture.Resident.Value.Inventory.Ore);
        Assert.Equal(1, fixture.Resident.Value.Inventory.Alloy);
        Assert.Equal(warehouseAlloy, fixture.Town.Value.Resources.Alloy);
        Assert.Equal(1, foundry.Value.ProductionBatches);
    }

    /// <summary>加工缺料不会消耗已有原料。</summary>
    [Fact]
    public void Missing_input_does_not_partially_consume_materials()
    {
        var (fixture, foundry) = FoundryWorld();
        fixture.Resident.Replace(fixture.Resident.Value.WithInventory(fixture.Resident.Value.Inventory with { Ore = 1 }));

        Assert.False(fixture.Engine.TryWorkAtBuilding(fixture.Resident.Value));

        Assert.Equal(1, fixture.Resident.Value.Inventory.Coal);
        Assert.Equal(1, fixture.Resident.Value.Inventory.Ore);
        Assert.Equal(0, fixture.Resident.Value.Inventory.Alloy);
        Assert.Equal(0, foundry.Value.ProductionBatches);
    }

    /// <summary>停用设施不消耗居民随身原料。</summary>
    [Fact]
    public void Disabled_facility_does_not_produce()
    {
        var (fixture, foundry) = FoundryWorld();
        foundry.Replace(foundry.Value with { Enabled = false });

        Assert.False(fixture.Engine.TryWorkAtBuilding(fixture.Resident.Value));

        Assert.Equal(1, fixture.Resident.Value.Inventory.Coal);
        Assert.Equal(2, fixture.Resident.Value.Inventory.Ore);
        Assert.Equal(0, foundry.Value.ProductionBatches);
    }

    /// <summary>知道项目却缺少运行前置知识时不能加工。</summary>
    [Fact]
    public void Missing_operating_prerequisite_prevents_production()
    {
        var (fixture, foundry) = FoundryWorld();
        var society = fixture.Engine.Society;
        var research = society.Research.Single();
        fixture.Engine.Society = society with
        {
            Research = society.Research.SetItem(0,
                research with { Completed = research.Completed.Remove(Advancement.Industry.Prerequisites[0]) }),
        };

        Assert.False(fixture.Engine.TryWorkAtBuilding(fixture.Resident.Value));

        Assert.Equal(1, fixture.Resident.Value.Inventory.Coal);
        Assert.Equal(2, fixture.Resident.Value.Inventory.Ore);
        Assert.Equal(0, foundry.Value.ProductionBatches);
    }

    /// <summary>居民必须实际到场才能加工。</summary>
    [Fact]
    public void Distant_worker_cannot_produce()
    {
        var (fixture, foundry) = FoundryWorld();
        fixture.Resident.Replace(fixture.Resident.Value with { FromX = 10, X = 10 });
        fixture.Resident.Replace(fixture.Resident.Value with { FromY = 10, Y = 10 });

        Assert.False(fixture.Engine.TryWorkAtBuilding(fixture.Resident.Value));

        Assert.Equal(0, foundry.Value.ProductionBatches);
        Assert.Equal(2, fixture.Resident.Value.Inventory.Ore);
    }

    /// <summary>同一天重复工作不能重复结算加工。</summary>
    [Fact]
    public void Worker_can_produce_only_once_per_day()
    {
        var (fixture, foundry) = FoundryWorld();
        fixture.Resident.Replace(fixture.Resident.Value.WithInventory(fixture.Resident.Value.Inventory with { Coal = 2, Ore = 4 }));
        Assert.True(fixture.Engine.TryWorkAtBuilding(fixture.Resident.Value));

        Assert.False(fixture.Engine.TryWorkAtBuilding(fixture.Resident.Value));
        fixture.Engine.SimulationTick += 4;
        Assert.False(fixture.Engine.TryWorkAtBuilding(fixture.Resident.Value));

        Assert.Equal(1, fixture.Resident.Value.Inventory.Alloy);
        Assert.Equal(1, fixture.Resident.Value.Inventory.Coal);
        Assert.Equal(2, fixture.Resident.Value.Inventory.Ore);
        Assert.Equal(1, foundry.Value.ProductionBatches);
        fixture.Engine.SimulationTick = SimulationTime.TicksPerDay;
        Assert.True(fixture.Engine.TryWorkAtBuilding(fixture.Resident.Value));
        Assert.Equal(2, foundry.Value.ProductionBatches);
    }

    /// <summary>日内换班不会突破加工设施的当日批次工位上限。</summary>
    [Fact]
    public void Production_capacity_is_shared_across_ticks_of_the_same_day()
    {
        var (fixture, foundry) = FoundryWorld();
        foundry.Replace(foundry.Value with { WorkSlots = 1 });
        Assert.True(fixture.Engine.TryWorkAtBuilding(fixture.Resident.Value));
        fixture.Engine.SpawnResidents(16, 16, RaceKind.Human, 1);
        var other = fixture.Engine.Residents.Single(p => p.Value.Id != fixture.ResidentId);
        other.Replace(fixture.Resident.Value with
        {
            Id = other.Value.Id, Inventory = new ResourceStock { Coal = 1, Ore = 2 },
        });
        fixture.Engine.SimulationTick += 4;

        Assert.False(fixture.Engine.TryWorkAtBuilding(other.Value));

        Assert.Single(foundry.Value.Workers);
        Assert.Equal(1, foundry.Value.ProductionBatches);
    }
}
