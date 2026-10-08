using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.Core.Tests;

/// <summary>居民自主选路、供水、避险与休养的最小现场场景。</summary>
public sealed class ResidentBehaviorTests
{
    /// <summary>可见道路总耗时更少时，允许先偏离目标方向再沿路抵达。</summary>
    [Fact]
    public void Navigation_prefers_a_faster_road_detour()
    {
        var fixture = Prepare();
        SetJourney(fixture, 10, 10, 14, 10);
        for (var x = 10; x <= 14; x++) fixture.Engine.Current.Tiles[11 * 32 + x].RoadLevel = 1;

        fixture.Engine.Step();

        Assert.Equal(10, fixture.Resident.X);
        Assert.Equal(11, fixture.Resident.Y);
        Assert.Equal(1, fixture.Resident.MoveDurationTicks);
    }

    /// <summary>耗时相近时更愿意走本人记得的路线。</summary>
    [Fact]
    public void Navigation_prefers_familiar_places_when_costs_are_similar()
    {
        var fixture = Prepare();
        SetJourney(fixture, 10, 10, 12, 12);
        fixture.Resident.Agent.Replace(fixture.Resident.Agent.Value with { FamiliarTiles = [11 * 32 + 10] });

        fixture.Engine.Step();

        Assert.Equal(10, fixture.Resident.X);
        Assert.Equal(11, fixture.Resident.Y);
    }

    /// <summary>熟路被地形编辑切断后使用当前可行路径，不能盲从记忆。</summary>
    [Fact]
    public void Navigation_does_not_follow_a_blocked_familiar_place()
    {
        var fixture = Prepare();
        SetJourney(fixture, 10, 10, 12, 12);
        fixture.Resident.Agent.Replace(fixture.Resident.Agent.Value with { FamiliarTiles = [11 * 32 + 10] });
        fixture.Engine.Current.Tiles[11 * 32 + 10].Terrain = TerrainType.Mountain;

        fixture.Engine.Step();

        Assert.Equal(11, fixture.Resident.X);
        Assert.Equal(10, fixture.Resident.Y);
    }

    /// <summary>实际抵达前只记住出发地，不把逻辑目的地提前当作熟路。</summary>
    [Fact]
    public void Navigation_records_places_after_actual_arrival()
    {
        var fixture = Prepare();
        SetJourney(fixture, 10, 10, 12, 10);

        fixture.Engine.Step();

        Assert.Contains(10 * 32 + 10, fixture.Resident.Agent.Value.FamiliarTiles);
        Assert.DoesNotContain(10 * 32 + 11, fixture.Resident.Agent.Value.FamiliarTiles);
        fixture.Engine.Step(fixture.Resident.MoveDurationTicks);
        Assert.Contains(10 * 32 + 11, fixture.Resident.Agent.Value.FamiliarTiles);
    }

    /// <summary>日常补水优先选择可达河岸，不依赖最近的零散陆地供水。</summary>
    [Fact]
    public void Ordinary_water_collection_prefers_a_reliable_river()
    {
        var fixture = Prepare(thirst: true);
        fixture.Resident.Inventory = new ResourceStock { Food = 10 };
        fixture.Resident.Thirst = 20;
        fixture.Resident.FrozenUntilTick = fixture.Engine.Current.Tick + 2;
        fixture.Engine.Current.Tiles[16 * 32 + 17].NaturalWaterYield = .08;
        fixture.Engine.Current.Tiles[16 * 32 + 19].Terrain = TerrainType.River;

        fixture.Engine.Step();

        Assert.Equal(AgentGoalKind.FetchWater, fixture.Resident.Agent.Goal.Kind);
        Assert.Equal(16 * 32 + 19 + 1, fixture.Resident.Agent.Goal.TargetEntityId);
        Assert.Equal(18, fixture.Resident.Agent.Goal.TargetX);
        Assert.Equal(16, fixture.Resident.Agent.Goal.TargetY);
    }

    /// <summary>严重脱水时先去最近的足量水源，不能为惯常河岸延误补水。</summary>
    [Fact]
    public void Critical_thirst_prefers_nearby_sufficient_water()
    {
        var fixture = Prepare(thirst: true);
        fixture.Resident.Inventory = new ResourceStock { Food = 10 };
        fixture.Resident.Thirst = 90;
        fixture.Resident.FrozenUntilTick = fixture.Engine.Current.Tick + 2;
        fixture.Engine.Current.Tiles[16 * 32 + 17].NaturalWaterYield = .1;
        fixture.Engine.Current.Tiles[16 * 32 + 21].Terrain = TerrainType.River;

        fixture.Engine.Step();

        Assert.Equal(16 * 32 + 17 + 1, fixture.Resident.Agent.Goal.TargetEntityId);
    }

    /// <summary>日常取水选择实际运营水井，并在停用后按真实自然供水重选。</summary>
    [Theory]
    [InlineData(true, 18)]
    [InlineData(false, 17)]
    public void Water_collection_accounts_for_well_operation(bool enabled, int expectedX)
    {
        var fixture = Prepare(thirst: true);
        fixture.Resident.Inventory = new ResourceStock { Food = 10 };
        fixture.Resident.Thirst = 20;
        fixture.Resident.FrozenUntilTick = fixture.Engine.Current.Tick + 2;
        for (var x = 17; x <= 18; x++)
        {
            var ground = fixture.Engine.Current.Tiles[16 * 32 + x];
            ground.NationId = fixture.Town.NationId;
            ground.ClaimedSettlementId = fixture.Town.Id;
        }
        fixture.Engine.Current.Tiles[16 * 32 + 17].NaturalWaterYield = .08;
        fixture.Engine.Current.Tiles[16 * 32 + 18].NaturalWaterYield = .06;
        var wellId = fixture.Engine.GrantFacility(fixture.Town.Id, BuildingKind.Well, 18, 16);
        fixture.Engine.Current.Society.Buildings.Single(building => building.Id == wellId).Enabled = enabled;

        fixture.Engine.Step();

        Assert.Equal(16 * 32 + expectedX + 1, fixture.Resident.Agent.Goal.TargetEntityId);
    }

    /// <summary>视野内的淡水也必须有可达取水位置，隔山的河流不能成为目标。</summary>
    [Fact]
    public void Water_collection_rejects_an_inaccessible_river()
    {
        var fixture = Prepare(thirst: true);
        fixture.Resident.Inventory = new ResourceStock { Food = 10 };
        fixture.Resident.Thirst = 20;
        fixture.Resident.FrozenUntilTick = fixture.Engine.Current.Tick + 2;
        for (var y = 0; y < 32; y++) fixture.Engine.Current.Tiles[y * 32 + 18].Terrain = TerrainType.Mountain;
        fixture.Engine.Current.Tiles[16 * 32 + 17].NaturalWaterYield = .08;
        fixture.Engine.Current.Tiles[16 * 32 + 20].Terrain = TerrainType.River;

        fixture.Engine.Step();

        Assert.Equal(16 * 32 + 17 + 1, fixture.Resident.Agent.Goal.TargetEntityId);
    }

    /// <summary>剩余几天饮水时主动补水，不等到口渴或脱水才安排。</summary>
    [Fact]
    public void Water_collection_replenishes_a_low_reserve_before_thirst()
    {
        var fixture = Prepare(thirst: true);
        fixture.Resident.Inventory = new ResourceStock { Food = 10, Water = .08 };
        fixture.Resident.FrozenUntilTick = fixture.Engine.Current.Tick + 2;
        fixture.Engine.Current.Tiles[16 * 32 + 16].NaturalWaterYield = .03;
        fixture.Engine.Current.Tiles[16 * 32 + 19].Terrain = TerrainType.River;

        fixture.Engine.Step();

        Assert.Equal(0, fixture.Resident.Thirst);
        Assert.Equal(AgentGoalKind.FetchWater, fixture.Resident.Agent.Goal.Kind);
        Assert.Equal(16 * 32 + 19 + 1, fixture.Resident.Agent.Goal.TargetEntityId);
    }

    /// <summary>火场避险只使用眼前能实际走到的安全地格，不选择山墙外的远处。</summary>
    [Fact]
    public void Fleeing_uses_a_reachable_safe_place()
    {
        var fixture = Prepare();
        foreach (var tile in fixture.Engine.Current.Tiles) tile.Terrain = TerrainType.Mountain;
        fixture.Engine.Current.Tiles[16 * 32 + 16].Terrain = TerrainType.Grass;
        fixture.Engine.Current.Tiles[16 * 32 + 17].Terrain = TerrainType.Grass;
        fixture.Engine.Current.Tiles[16 * 32 + 21].Terrain = TerrainType.Grass;
        fixture.Engine.TriggerDisaster(16, 16, DisasterKind.Fire, 1);

        fixture.Engine.Step();

        Assert.Equal(AgentGoalKind.Flee, fixture.Resident.Agent.Goal.Kind);
        Assert.Equal(17, fixture.Resident.Agent.Goal.TargetX);
        Assert.Equal(16, fixture.Resident.Agent.Goal.TargetY);
    }

    /// <summary>病伤者不因扑火优先级高于普通工作而再次参与危险劳动。</summary>
    [Theory]
    [InlineData(100, 20)]
    [InlineData(30, 0)]
    public void Poor_health_does_not_volunteer_for_firefighting(double health, int sickness)
    {
        var fixture = Prepare();
        fixture.Resident.Health = health;
        fixture.Resident.SicknessTicks = sickness;
        fixture.Engine.TriggerDisaster(18, 16, DisasterKind.Fire, 1);

        fixture.Engine.Step();

        Assert.Equal(AgentGoalKind.Rest, fixture.Resident.Agent.Goal.Kind);
    }

    /// <summary>火场虽在视野内，隔山无法到达安全边缘时不能接下扑火任务。</summary>
    [Fact]
    public void Firefighting_requires_a_reachable_safe_edge()
    {
        var fixture = Prepare();
        for (var y = 0; y < 32; y++) fixture.Engine.Current.Tiles[y * 32 + 17].Terrain = TerrainType.Mountain;
        fixture.Engine.TriggerDisaster(19, 16, DisasterKind.Fire, 1);

        fixture.Engine.Step();

        Assert.NotEqual(AgentGoalKind.ExtinguishFire, fixture.Resident.Agent.Goal.Kind);
    }

    /// <summary>患病或重伤打断普通工作，自主选择休养。</summary>
    [Theory]
    [InlineData(100, 20)]
    [InlineData(30, 0)]
    public void Poor_health_interrupts_ordinary_work(double health, int sickness)
    {
        var fixture = Prepare();
        fixture.Resident.Health = health;
        fixture.Resident.SicknessTicks = sickness;
        fixture.Resident.Agent.Goal = new AgentGoal
        {
            Kind = AgentGoalKind.Gather, TargetX = 16, TargetY = 16, ReviewTick = 0,
        };

        fixture.Engine.Step();

        Assert.Equal(AgentGoalKind.Rest, fixture.Resident.Agent.Goal.Kind);
        Assert.Equal(ResidentActivity.Resting, fixture.Resident.Activity);
    }

    /// <summary>患者前往眼前运营的医疗点，医疗点停用时改回家休养。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Recovery_uses_an_operating_clinic_or_home(bool enabled)
    {
        var fixture = Prepare();
        fixture.Engine.SpawnResidents(16, 16, RaceKind.Human, 1);
        for (var x = 14; x <= 15; x++)
        {
            var ground = fixture.Engine.Current.Tiles[16 * 32 + x];
            ground.NationId = fixture.Town.NationId;
            ground.ClaimedSettlementId = fixture.Town.Id;
        }
        var clinicId = fixture.Engine.GrantFacility(fixture.Town.Id, BuildingKind.Infirmary, 14, 16);
        var clinic = fixture.Engine.Current.Society.Buildings.Single(building => building.Id == clinicId);
        var worker = fixture.Engine.Current.Residents.Single(person => person.Id != fixture.ResidentId);
        worker.Replace(worker.Value with
        {
            Age = 25, Profession = Profession.Builder, X = 14, Y = 16, FromX = 14, FromY = 16,
            Agent = worker.Agent.Value with { Initialized = true, NextThinkTick = 100,
                Goal = new AgentGoal { PlayerDirected = true, ReviewTick = 100 } },
        });
        clinic.Enabled = enabled;
        clinic.LastWorkedTick = fixture.Engine.Current.Tick;
        clinic.Workers.Add(worker.Id);
        fixture.Resident.Health = 30;

        fixture.Engine.Step();

        Assert.Equal(AgentGoalKind.Rest, fixture.Resident.Agent.Goal.Kind);
        Assert.Equal(enabled ? clinicId : 0, fixture.Resident.Agent.Goal.TargetEntityId);
        Assert.Equal(enabled ? 14 : 16, fixture.Resident.Agent.Goal.TargetX);
    }

    /// <summary>养伤期间保持休养，身体恢复到安全水平后才重新安排工作。</summary>
    [Theory]
    [InlineData(50, true)]
    [InlineData(80, false)]
    public void Recovery_has_a_higher_release_threshold(double health, bool shouldRest)
    {
        var fixture = Prepare();
        fixture.Resident.Health = health;
        fixture.Resident.Agent.Goal = new AgentGoal { Kind = AgentGoalKind.Rest, TargetX = 16, TargetY = 16 };

        fixture.Engine.Step();

        Assert.Equal(shouldRest, fixture.Resident.Agent.Goal.Kind == AgentGoalKind.Rest);
    }

    private static WorldFixture Prepare(bool thirst = false)
    {
        var fixture = new WorldFixture();
        fixture.Engine.ConfigureWorld(fixture.Engine.State.Rules with
        {
            Aging = false, Hunger = false, Thirst = thirst, Disease = false, Births = false,
            Construction = false, Expansion = false, Research = false, Trade = false,
            Migration = false, Secession = false, Wars = false, Conflict = 0, FireSpread = false,
            ResourceRegeneration = false,
        }, false, false);
        foreach (var tile in fixture.Engine.Current.Tiles) tile.NaturalWaterYield = 0;
        fixture.Engine.Current.Tick = 8 + (3 - fixture.ResidentId % 4 + 4) % 4;
        fixture.Resident.Replace(fixture.Resident.Value with
        {
            Age = 25, Profession = Profession.Farmer, X = 16, Y = 16, FromX = 16, FromY = 16,
            Inventory = new ResourceStock { Food = 10, Water = 10 },
            Agent = fixture.Resident.Agent.Value with { Initialized = true, NextThinkTick = 0, Goal = new AgentGoal() },
        });
        fixture.Town.Resources = new ResourceStock { Food = 40 };
        return fixture;
    }

    private static void SetJourney(WorldFixture fixture, int x, int y, int targetX, int targetY)
    {
        fixture.Resident.Replace(fixture.Resident.Value with
        {
            X = x, Y = y, FromX = x, FromY = y,
            Agent = fixture.Resident.Agent.Value with
            {
                NextThinkTick = 100,
                Goal = new AgentGoal
                {
                    Kind = AgentGoalKind.Explore, TargetX = targetX, TargetY = targetY,
                    PlayerDirected = true, ReviewTick = 100,
                },
            },
        });
    }
}
