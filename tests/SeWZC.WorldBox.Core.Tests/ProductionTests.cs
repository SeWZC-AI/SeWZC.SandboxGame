using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core.Tests;

/// <summary>到场加工的材料消耗、实物产出和拒绝边界。</summary>
public sealed class ProductionTests
{
    private static (WorldFixture Fixture, BuildingCursor Foundry) FoundryWorld()
    {
        var fixture = new WorldFixture();
        foreach (var prerequisite in Advancement.Industry.Prerequisites)
            fixture.Engine.GrantReceivedResearch(fixture.Town.Id, prerequisite);
        fixture.Engine.GrantReceivedResearch(fixture.Town.Id, Advancement.Industry);
        var foundry = new BuildingCursor
        {
            Id = fixture.Engine.Current.NextId++,
            SettlementId = fixture.Town.Id,
            Kind = BuildingKind.Foundry,
            X = 17,
            Y = 16,
            ConstructionProgress = 30,
        };
        fixture.Engine.Current.Society.Buildings.Add(foundry);
        var ground = fixture.Engine.Current.Tiles[16 * 32 + 17];
        ground.NationId = fixture.Town.NationId;
        ground.ClaimedSettlementId = fixture.Town.Id;
        var worker = fixture.Resident;
        worker.Age = 25;
        worker.Profession = Profession.Builder;
        worker.X = worker.FromX = 17;
        worker.Y = worker.FromY = 16;
        worker.Inventory = new ResourceStock { Coal = 1, Ore = 2 };
        worker.Agent.Goal = new AgentGoal
        {
            Kind = AgentGoalKind.Work,
            TargetEntityId = foundry.Id,
            TargetX = 17,
            TargetY = 16,
        };
        return (fixture, foundry);
    }

    /// <summary>一次加工从随身原料生成随身产物，尚未递送至仓库。</summary>
    [Fact]
    public void Work_consumes_carried_inputs_and_keeps_output_with_the_worker()
    {
        var (fixture, foundry) = FoundryWorld();
        var warehouseAlloy = fixture.Town.Resources.Alloy;

        Assert.True(fixture.Engine.TryWorkAtBuilding(fixture.Resident));

        Assert.Equal(0, fixture.Resident.Inventory.Coal);
        Assert.Equal(0, fixture.Resident.Inventory.Ore);
        Assert.Equal(1, fixture.Resident.Inventory.Alloy);
        Assert.Equal(warehouseAlloy, fixture.Town.Resources.Alloy);
        Assert.Equal(1, foundry.ProductionBatches);
    }

    /// <summary>加工缺料不会消耗已有原料。</summary>
    [Fact]
    public void Missing_input_does_not_partially_consume_materials()
    {
        var (fixture, foundry) = FoundryWorld();
        fixture.Resident.Inventory = fixture.Resident.Inventory with { Ore = 1 };

        Assert.False(fixture.Engine.TryWorkAtBuilding(fixture.Resident));

        Assert.Equal(1, fixture.Resident.Inventory.Coal);
        Assert.Equal(1, fixture.Resident.Inventory.Ore);
        Assert.Equal(0, fixture.Resident.Inventory.Alloy);
        Assert.Equal(0, foundry.ProductionBatches);
    }

    /// <summary>停用设施不消耗居民随身原料。</summary>
    [Fact]
    public void Disabled_facility_does_not_produce()
    {
        var (fixture, foundry) = FoundryWorld();
        foundry.Enabled = false;

        Assert.False(fixture.Engine.TryWorkAtBuilding(fixture.Resident));

        Assert.Equal(1, fixture.Resident.Inventory.Coal);
        Assert.Equal(2, fixture.Resident.Inventory.Ore);
        Assert.Equal(0, foundry.ProductionBatches);
    }

    /// <summary>知道项目却缺少运行前置知识时不能加工。</summary>
    [Fact]
    public void Missing_operating_prerequisite_prevents_production()
    {
        var (fixture, foundry) = FoundryWorld();
        fixture.Engine.Current.Society.Research.Single().Completed.Remove(Advancement.Industry.Prerequisites[0]);

        Assert.False(fixture.Engine.TryWorkAtBuilding(fixture.Resident));

        Assert.Equal(1, fixture.Resident.Inventory.Coal);
        Assert.Equal(2, fixture.Resident.Inventory.Ore);
        Assert.Equal(0, foundry.ProductionBatches);
    }

    /// <summary>居民必须实际到场才能加工。</summary>
    [Fact]
    public void Distant_worker_cannot_produce()
    {
        var (fixture, foundry) = FoundryWorld();
        fixture.Resident.X = fixture.Resident.FromX = 10;
        fixture.Resident.Y = fixture.Resident.FromY = 10;

        Assert.False(fixture.Engine.TryWorkAtBuilding(fixture.Resident));

        Assert.Equal(0, foundry.ProductionBatches);
        Assert.Equal(2, fixture.Resident.Inventory.Ore);
    }

    /// <summary>同一天重复工作不能重复结算加工。</summary>
    [Fact]
    public void Worker_can_produce_only_once_per_tick()
    {
        var (fixture, foundry) = FoundryWorld();
        fixture.Resident.Inventory = fixture.Resident.Inventory with
        {
            Coal = 2,
            Ore = 4,
        };
        Assert.True(fixture.Engine.TryWorkAtBuilding(fixture.Resident));

        Assert.False(fixture.Engine.TryWorkAtBuilding(fixture.Resident));

        Assert.Equal(1, fixture.Resident.Inventory.Alloy);
        Assert.Equal(1, fixture.Resident.Inventory.Coal);
        Assert.Equal(2, fixture.Resident.Inventory.Ore);
        Assert.Equal(1, foundry.ProductionBatches);
    }
}
