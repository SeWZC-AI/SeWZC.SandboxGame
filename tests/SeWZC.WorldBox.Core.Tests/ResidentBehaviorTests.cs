namespace SeWZC.WorldBox.Core.Tests;

/// <summary>居民自主选路、供水、避险与休养的最小现场场景。</summary>
public sealed class ResidentBehaviorTests
{
    /// <summary>初始日可直接更新社会状态，尚无经过的日数时不提前累计自然资源恢复。</summary>
    [Fact]
    public void Initial_society_update_does_not_index_a_negative_recovery_band()
    {
        var engine = WorldEngine.Create(42, 32, 32, false);
        var source = engine.Current.Tiles[5];
        source.Replace(source.Value with { Terrain = TerrainType.Grass, ResourceAmount = 0, Fertility = 85 });

        engine.TickSociety();

        Assert.Equal(0, engine.State.Tick);
        Assert.Equal(0, source.ResourceAmount);
    }

    /// <summary>实地猎物出现或被采空后，觅食安排反映最新种群，不沿用旧的空猎场或旧猎物地址。</summary>
    [Fact]
    public void Wildlife_choices_follow_the_actual_population_after_an_empty_search()
    {
        var fixture = Prepare();
        fixture.Engine.ConfigureWorld(fixture.Engine.State.Rules with { Hunger = true }, false, false);
        fixture.Town.Resources = new ResourceStock();
        foreach (var tile in fixture.Engine.Current.Tiles)
            tile.Replace(tile.Value with
            {
                ResourceAmount = 0,
                Wildlife = WildlifeKind.None,
                WildlifePopulation = 0,
                OtherWildlife = new WildlifePopulations(),
            });
        fixture.Resident.Inventory = new ResourceStock { Water = 10 };
        fixture.Resident.Hunger = 90;
        fixture.Resident.FrozenUntilTick = 100;
        fixture.Engine.Step();
        Assert.Equal(AgentGoalKind.Explore, fixture.Resident.Agent.Goal.Kind);
        var source = fixture.Engine.Current.Tiles[16 * 32 + 19];
        source.Replace(source.Value with { Wildlife = WildlifeKind.Rabbit, WildlifePopulation = 20 });
        fixture.Resident.Agent = fixture.Resident.Agent with { Goal = new AgentGoal() };
        fixture.Resident.Agent = fixture.Resident.Agent with { NextThinkTick = 0 };
        fixture.Engine.Step(4);
        Assert.Equal(AgentGoalKind.Hunt, fixture.Resident.Agent.Goal.Kind);
        Assert.Equal(19, fixture.Resident.Agent.Goal.TargetX);
        source.Replace(source.Value with { WildlifePopulation = 0 });
        fixture.Resident.Agent = fixture.Resident.Agent with { Goal = new AgentGoal() };
        fixture.Resident.Agent = fixture.Resident.Agent with { NextThinkTick = 0 };

        fixture.Engine.Step(4);

        Assert.NotEqual(AgentGoalKind.Hunt, fixture.Resident.Agent.Goal.Kind);
    }

    /// <summary>已找到的采集点被新山障或火场隔断后，下次安排不能沿旧可达性继续派工。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Resource_work_is_reconsidered_when_its_access_is_cut_off(bool fire)
    {
        var fixture = Prepare();
        fixture.Engine.ConfigureWorld(fixture.Engine.State.Rules with { Hunger = true }, false, false);
        fixture.Town.Resources = new ResourceStock();
        foreach (var tile in fixture.Engine.Current.Tiles)
            tile.Replace(tile.Value with
            {
                Terrain = TerrainType.Mountain,
                ResourceAmount = 0,
                Wildlife = WildlifeKind.None,
                WildlifePopulation = 0,
                OtherWildlife = new WildlifePopulations(),
            });
        for (var x = 16; x <= 20; x++)
            fixture.Engine.Current.Tiles[16 * 32 + x].Terrain = TerrainType.Grass;
        var source = fixture.Engine.Current.Tiles[16 * 32 + 20];
        source.Replace(source.Value with
        {
            ResourceAmount = 100, Fertility = 100, Plants = new PlantCoverage { Grass = 1 },
        });
        fixture.Resident.Inventory = new ResourceStock { Water = 10 };
        fixture.Resident.Hunger = 90;
        fixture.Resident.FrozenUntilTick = 100;
        fixture.Engine.Step();
        Assert.Equal(AgentGoalKind.Gather, fixture.Resident.Agent.Goal.Kind);
        Assert.Equal(20, fixture.Resident.Agent.Goal.TargetX);
        var blocked = fixture.Engine.Current.Tiles[16 * 32 + 18];
        if (fire)
            blocked.FireTicks = 12;
        else
            blocked.Terrain = TerrainType.Mountain;
        fixture.Resident.Agent = fixture.Resident.Agent with { Goal = new AgentGoal() };
        fixture.Resident.Agent = fixture.Resident.Agent with { NextThinkTick = 0 };

        fixture.Engine.Step(4);

        Assert.NotEqual(AgentGoalKind.Gather, fixture.Resident.Agent.Goal.Kind);
    }

    /// <summary>同样的土地按同样速率恢复，地图变大不能令该地格的恢复量降低。</summary>
    [Fact]
    public void Natural_resource_recovery_does_not_slow_down_on_larger_maps()
    {
        double Recover(int size)
        {
            var engine = WorldEngine.Create(42, size, size, false);
            var source = engine.Current.Tiles[5];
            source.Replace(source.Value with { Terrain = TerrainType.Grass, ResourceAmount = 0, Fertility = 85 });
            engine.Current.Tick = 1;
            engine.TickSociety();
            var recovered = source.ResourceAmount;
            engine.Current.Tick = 2;
            engine.TickSociety();
            Assert.Equal(recovered, source.ResourceAmount);
            return recovered;
        }

        var small = Recover(32);
        Assert.True(small > 0);
        Assert.Equal(small, Recover(64));
    }

    /// <summary>已亲眼看到空仓且附近无粮食时沿实际可通行土地继续觅食，不反复回空仓。</summary>
    [Fact]
    public void Hungry_resident_explores_when_home_and_visible_sources_are_empty()
    {
        var fixture = Prepare();
        fixture.Engine.ConfigureWorld(fixture.Engine.State.Rules with { Hunger = true }, false, false);
        fixture.Town.Resources = new ResourceStock();
        foreach (var tile in fixture.Engine.Current.Tiles)
            tile.Replace(tile.Value with
            {
                ResourceAmount = 0,
                Wildlife = WildlifeKind.None,
                WildlifePopulation = 0,
                OtherWildlife = new WildlifePopulations(),
            });
        fixture.Resident.Inventory = new ResourceStock { Water = 10 };
        fixture.Resident.Hunger = 90;
        fixture.Resident.FrozenUntilTick = 100;

        fixture.Engine.Step();

        Assert.Equal(AgentGoalKind.Explore, fixture.Resident.Agent.Goal.Kind);
        var target = fixture.Resident.Agent.Goal;
        Assert.InRange(Math.Abs(target.TargetX - 16) + Math.Abs(target.TargetY - 16), 1, 6);
        Assert.True(RaceTerrainRules.CanWalk(fixture.Engine.State.Tiles[target.TargetY * 32 + target.TargetX],
            RaceKind.Human));
        Assert.Equal(0, fixture.Resident.Inventory.Food);
        Assert.True(fixture.Resident.Hunger > 90);
    }

    /// <summary>已查过的可采食地点变成贫瘠地、裸地或旱地后，下一次决策读取实际变化。</summary>
    [Theory]
    [InlineData("stock")]
    [InlineData("plants")]
    [InlineData("fertility")]
    [InlineData("drought")]
    [InlineData("terrain")]
    public void Food_site_queries_refresh_after_the_source_changes(string change)
    {
        var fixture = Prepare();
        fixture.Town.Resources = new ResourceStock();
        fixture.Engine.ConfigureWorld(fixture.Engine.State.Rules with { Hunger = true }, false, false);
        foreach (var tile in fixture.Engine.Current.Tiles)
            tile.ResourceAmount = 0;
        var source = fixture.Engine.Current.Tiles[16 * 32 + 17];
        source.Replace(source.Value with
        {
            ResourceAmount = 100,
            Plants = new PlantCoverage { Grass = 1 },
            Fertility = 50,
            Improvement = LandImprovement.None,
        });
        fixture.Resident.Inventory = new ResourceStock { Water = 10 };
        fixture.Resident.Hunger = 90;
        fixture.Resident.FrozenUntilTick = 100;
        fixture.Resident.Agent = fixture.Resident.Agent with { Goal = new AgentGoal() };
        fixture.Resident.Agent = fixture.Resident.Agent with { NextThinkTick = 0 };
        fixture.Engine.Step();
        Assert.Equal(AgentGoalKind.Gather, fixture.Resident.Agent.Goal.Kind);
        Assert.Equal(17, fixture.Resident.Agent.Goal.TargetX);

        source.Replace(change switch
        {
            "stock" => source.Value with { ResourceAmount = 0 },
            "plants" => source.Value with { Plants = new PlantCoverage { Trees = 1 } },
            "fertility" => source.Value with { Fertility = 1 },
            "drought" => source.Value with { DroughtTicks = 12 },
            _ => source.Value with { Terrain = TerrainType.Mountain },
        });
        fixture.Resident.Agent = fixture.Resident.Agent with { Goal = new AgentGoal() };
        fixture.Resident.Agent = fixture.Resident.Agent with { NextThinkTick = 0 };
        fixture.Engine.Step(4);

        Assert.NotEqual(AgentGoalKind.Gather, fixture.Resident.Agent.Goal.Kind);
    }

    /// <summary>短路线来自原有视野，抵达后继续使用；存档恢复保持路线与下一步一致。</summary>
    [Fact]
    public void Navigation_keeps_a_visible_route_across_save_and_arrival()
    {
        var fixture = Prepare();
        SetJourney(fixture, 10, 10, 15, 10);
        fixture.Engine.Step();
        var goal = fixture.Resident.Agent.Goal;
        Assert.InRange(goal.NavigationRoute.Length, 2, 7);
        Assert.All(goal.NavigationRoute, index =>
            Assert.InRange(Math.Abs(index % 32 - 10) + Math.Abs(index / 32 - 10), 0, 6));
        var restored = WorldEngine.ImportJson(fixture.Engine.ExportJson());
        var next = goal.NavigationRoute[goal.NavigationRouteOffset];

        fixture.Engine.Step(fixture.Resident.MoveDurationTicks);
        restored.Step(restored.State.Residents[0].MoveDurationTicks);

        Assert.Equal(next, fixture.Resident.Y * 32 + fixture.Resident.X);
        Assert.Equal(fixture.Engine.ExportJson(), restored.ExportJson());
        Assert.Equal(2, goal.NavigationRouteOffset);
    }

    /// <summary>已规划路线的下一格被切断后重新观察，不能穿过新障碍。</summary>
    [Fact]
    public void Navigation_replans_when_the_cached_next_step_is_blocked()
    {
        var fixture = Prepare();
        SetJourney(fixture, 10, 10, 15, 10);
        fixture.Engine.Step();
        var goal = fixture.Resident.Agent.Goal;
        var blocked = goal.NavigationRoute[goal.NavigationRouteOffset];
        fixture.Engine.Current.Tiles[blocked].Terrain = TerrainType.Mountain;

        fixture.Engine.Step(fixture.Resident.MoveDurationTicks);

        Assert.NotEqual(blocked, fixture.Resident.Y * 32 + fixture.Resident.X);
        Assert.True(RaceTerrainRules.CanWalk(fixture.Engine.State.Tiles[fixture.Resident.Y * 32 + fixture.Resident.X],
            fixture.Resident.Race));
    }

    /// <summary>目标在视野内外时均比较可见道路，允许先偏离目标方向再沿更快的路前进。</summary>
    [Theory]
    [InlineData(14)]
    [InlineData(22)]
    public void Navigation_prefers_a_faster_road_detour(int targetX)
    {
        var fixture = Prepare();
        SetJourney(fixture, 10, 10, targetX, 10);
        for (var x = 10; x <= 15; x++)
            fixture.Engine.Current.Tiles[11 * 32 + x].RoadLevel = 1;

        fixture.Engine.Step();

        Assert.Equal(10, fixture.Resident.X);
        Assert.Equal(11, fixture.Resident.Y);
        Assert.Equal(1, fixture.Resident.MoveDurationTicks);
    }

    /// <summary>目标在视野内外时，耗时相近均更愿意走本人记得的路线。</summary>
    [Theory]
    [InlineData(12)]
    [InlineData(22)]
    public void Navigation_prefers_familiar_places_when_costs_are_similar(int target)
    {
        var fixture = Prepare();
        SetJourney(fixture, 10, 10, target, target);
        fixture.Resident.Agent = fixture.Resident.Agent with { FamiliarTiles = [11 * 32 + 10] };

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
        fixture.Resident.Agent = fixture.Resident.Agent with { FamiliarTiles = [11 * 32 + 10] };
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

        Assert.Contains(10 * 32 + 10, fixture.Resident.Agent.FamiliarTiles);
        Assert.DoesNotContain(10 * 32 + 11, fixture.Resident.Agent.FamiliarTiles);
        fixture.Engine.Step(fixture.Resident.MoveDurationTicks);
        Assert.Contains(10 * 32 + 11, fixture.Resident.Agent.FamiliarTiles);
    }

    /// <summary>日常补水优先选择可达河岸，不依赖最近的零散陆地供水。</summary>
    [Fact]
    public void Ordinary_water_collection_prefers_a_reliable_river()
    {
        var fixture = Prepare(true);
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

    /// <summary>近处自然供水充足时，仍比较评分更高的远处河岸。</summary>
    [Fact]
    public void Water_collection_compares_a_farther_river_with_abundant_groundwater()
    {
        var fixture = Prepare(true);
        fixture.Resident.Inventory = new ResourceStock { Food = 10 };
        fixture.Resident.Thirst = 20;
        fixture.Resident.FrozenUntilTick = fixture.Engine.Current.Tick + 2;
        fixture.Engine.Current.Tiles[16 * 32 + 17].NaturalWaterYield = 1;
        fixture.Engine.Current.Tiles[16 * 32 + 20].Terrain = TerrainType.River;

        fixture.Engine.Step();

        Assert.Equal(16 * 32 + 20 + 1, fixture.Resident.Agent.Goal.TargetEntityId);
        Assert.Equal(19, fixture.Resident.Agent.Goal.TargetX);
    }

    /// <summary>严重缺水时近处不足一天的水不能排除远处足量水源。</summary>
    [Fact]
    public void Critical_water_collection_does_not_stop_at_an_insufficient_nearby_source()
    {
        var fixture = Prepare(true);
        fixture.Resident.Inventory = new ResourceStock { Food = 10 };
        fixture.Resident.Thirst = 90;
        fixture.Resident.FrozenUntilTick = fixture.Engine.Current.Tick + 2;
        fixture.Engine.Current.Tiles[16 * 32 + 17].NaturalWaterYield = .01;
        fixture.Engine.Current.Tiles[16 * 32 + 21].Terrain = TerrainType.River;

        fixture.Engine.Step();

        Assert.Equal(16 * 32 + 21 + 1, fixture.Resident.Agent.Goal.TargetEntityId);
    }

    /// <summary>严重脱水时先去最近的运营水井，不能为惯常河岸延误补水。</summary>
    [Fact]
    public void Critical_thirst_prefers_nearby_sufficient_water()
    {
        var fixture = Prepare(true);
        fixture.Resident.Inventory = new ResourceStock { Food = 10 };
        fixture.Resident.Thirst = 90;
        fixture.Resident.FrozenUntilTick = fixture.Engine.Current.Tick + 2;
        fixture.AddWell(17, 16, .1);
        fixture.Engine.Current.Tiles[16 * 32 + 21].Terrain = TerrainType.River;

        fixture.Engine.Step();

        Assert.Equal(16 * 32 + 17 + 1, fixture.Resident.Agent.Goal.TargetEntityId);
    }

    /// <summary>日常取水选择实际运营水井，停用后只能转向河湖，不能打取普通地块供水。</summary>
    [Theory]
    [InlineData(true, 18)]
    [InlineData(false, 20)]
    public void Water_collection_accounts_for_well_operation(bool enabled, int expectedX)
    {
        var fixture = Prepare(true);
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
        fixture.Engine.Current.Tiles[16 * 32 + 20].Terrain = TerrainType.River;
        var wellId = fixture.Engine.GrantFacility(fixture.Town.Id, BuildingKind.Well, 18, 16);
        fixture.Engine.Current.Buildings.Single(building => building.Value.Id == wellId).Replace(fixture.Engine.Current.Buildings.Single(building => building.Value.Id == wellId).Value with { Enabled = enabled });

        fixture.Engine.Step();

        Assert.Equal(16 * 32 + expectedX + 1, fixture.Resident.Agent.Goal.TargetEntityId);
    }

    /// <summary>视野内的淡水也必须有可达取水位置，隔山的河流不能成为目标。</summary>
    [Fact]
    public void Water_collection_rejects_an_inaccessible_river()
    {
        var fixture = Prepare(true);
        fixture.Resident.Inventory = new ResourceStock { Food = 10 };
        fixture.Resident.Thirst = 20;
        fixture.Resident.FrozenUntilTick = fixture.Engine.Current.Tick + 2;
        for (var y = 0; y < 32; y++)
            fixture.Engine.Current.Tiles[y * 32 + 18].Terrain = TerrainType.Mountain;
        fixture.AddWell(17, 16, .08);
        fixture.Engine.Current.Tiles[16 * 32 + 20].Terrain = TerrainType.River;

        fixture.Engine.Step();

        Assert.Equal(16 * 32 + 17 + 1, fixture.Resident.Agent.Goal.TargetEntityId);
    }

    /// <summary>剩余几天饮水时主动补水，不等到口渴或脱水才安排。</summary>
    [Fact]
    public void Water_collection_replenishes_a_low_reserve_before_thirst()
    {
        var fixture = Prepare(true);
        fixture.Resident.Inventory = new ResourceStock { Food = 10, Water = .08 };
        fixture.Resident.FrozenUntilTick = fixture.Engine.Current.Tick + 2;
        fixture.Engine.Current.Tiles[16 * 32 + 16].NaturalWaterYield = .03;
        fixture.Engine.Current.Tiles[16 * 32 + 19].Terrain = TerrainType.River;

        fixture.Engine.Step();

        Assert.Equal(0, fixture.Resident.Thirst);
        Assert.Equal(AgentGoalKind.FetchWater, fixture.Resident.Agent.Goal.Kind);
        Assert.Equal(16 * 32 + 19 + 1, fixture.Resident.Agent.Goal.TargetEntityId);
    }

    /// <summary>普通陆地的环境供水不能装进背包，失效的取水任务立即请求重选。</summary>
    [Fact]
    public void Ordinary_ground_does_not_supply_collectable_water()
    {
        var fixture = Prepare(true);
        fixture.Engine.Current.Tiles[16 * 32 + 16].NaturalWaterYield = .08;
        fixture.Resident.Inventory = new ResourceStock { Food = 10, Water = .03 };
        fixture.Resident.Agent = fixture.Resident.Agent with
        {
            Goal = new AgentGoal
            {
                Kind = AgentGoalKind.FetchWater, TargetX = 16, TargetY = 16, TargetEntityId = 16 * 32 + 16 + 1,
            },
        };

        Assert.False(fixture.Engine.TryFetchWater(fixture.Resident.Value));

        Assert.Equal(.03, fixture.Resident.Inventory.Water, 6);
        Assert.Equal(AgentGoalKind.Idle, fixture.Resident.Agent.Goal.Kind);
        Assert.Equal(fixture.Engine.Current.Tick, fixture.Resident.Agent.NextThinkTick);
        Assert.Equal(0, fixture.Engine.AvailableWater(16, 16));
        Assert.Equal(0, fixture.Engine.GetDailyWaterCapacity(16, 16));
        Assert.Equal(0, fixture.Town.Resources.Water);
    }

    /// <summary>水井停用或干旱导致产量过低时，取水任务立即结束，环境供水仍保留。</summary>
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 10)]
    public void Water_collection_abandons_a_well_that_cannot_build_reserves(bool enabled, int drought)
    {
        var fixture = Prepare(true);
        var wellId = fixture.AddWell(16, 17, .101);
        fixture.Engine.Current.Buildings.Single(building => building.Value.Id == wellId).Replace(fixture.Engine.Current.Buildings.Single(building => building.Value.Id == wellId).Value with { Enabled = enabled });
        fixture.Engine.Current.Tiles[17 * 32 + 16].DroughtTicks = drought;
        fixture.Resident.X = fixture.Resident.FromX = 16;
        fixture.Resident.Y = fixture.Resident.FromY = 17;
        fixture.Resident.Inventory = new ResourceStock();
        fixture.Resident.Agent = fixture.Resident.Agent with { NextThinkTick = fixture.Engine.Current.Tick + 100 };
        fixture.Resident.Agent = fixture.Resident.Agent with
        {
            Goal = new AgentGoal
            {
                Kind = AgentGoalKind.FetchWater, TargetX = 16, TargetY = 17, TargetEntityId = 17 * 32 + 16 + 1,
            },
        };

        Assert.False(fixture.Engine.TryFetchWater(fixture.Resident.Value));

        Assert.Equal(AgentGoalKind.Idle, fixture.Resident.Agent.Goal.Kind);
        Assert.Equal(fixture.Engine.Current.Tick, fixture.Resident.Agent.NextThinkTick);
        Assert.Equal(0, fixture.Resident.Inventory.Water);
        Assert.True(WorldEngine.DailyWaterYield(fixture.Engine.State.Tiles[17 * 32 + 16]) > 0);
    }

    /// <summary>地块环境供水独立展示，只有实际水井才显示每日可打水量，且不叠加环境值。</summary>
    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(true, false, 2.4)]
    [InlineData(true, true, .6)]
    public void Tile_water_summary_separates_environment_from_collectable_water(bool well, bool drought,
        double capacity)
    {
        var fixture = new WorldFixture();
        if (well)
            fixture.AddWell(16, 17, .1);
        var ground = fixture.Engine.Current.Tiles[17 * 32 + 16];
        ground.NaturalWaterYield = .1;
        ground.DroughtTicks = drought ? 10 : 0;
        if (drought)
            ground.NaturalWaterYield = .2;

        var summary = fixture.Engine.GetTileProductionSummary(16, 17);

        Assert.Contains("地块供水量", summary);
        Assert.Equal(well, summary.Contains("每日可打水量"));
        Assert.Equal(capacity, fixture.Engine.GetDailyWaterCapacity(16, 17), 8);
        Assert.DoesNotContain("/ 日", summary);
    }

    /// <summary>淡水河湖保持无限可打水量，海水即使有环境供水值也不能打取饮水。</summary>
    [Theory]
    [InlineData(TerrainType.River, true)]
    [InlineData(TerrainType.Lake, true)]
    [InlineData(TerrainType.Stream, true)]
    [InlineData(TerrainType.LargeRiver, true)]
    [InlineData(TerrainType.Water, false)]
    [InlineData(TerrainType.DeepWater, false)]
    public void Only_freshwater_terrain_provides_unlimited_collectable_water(TerrainType terrain, bool fresh)
    {
        var fixture = new WorldFixture();
        var tile = fixture.Engine.Current.Tiles[17 * 32 + 16];
        tile.Terrain = terrain;
        tile.NaturalWaterYield = .1;

        var available = fixture.Engine.AvailableWater(16, 17);

        Assert.Equal(fresh ? double.PositiveInfinity : 0, available);
        Assert.Equal(available, fixture.Engine.GetDailyWaterCapacity(16, 17));
    }

    /// <summary>火场避险只使用眼前能实际走到的安全地格，不选择山墙外的远处。</summary>
    [Fact]
    public void Fleeing_uses_a_reachable_safe_place()
    {
        var fixture = Prepare();
        foreach (var tile in fixture.Engine.Current.Tiles)
            tile.Terrain = TerrainType.Mountain;
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
        for (var y = 0; y < 32; y++)
            fixture.Engine.Current.Tiles[y * 32 + 17].Terrain = TerrainType.Mountain;
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
        fixture.Resident.Agent = fixture.Resident.Agent with
        {
            Goal = new AgentGoal
            {
                Kind = AgentGoalKind.Gather, TargetX = 16, TargetY = 16, ReviewTick = 0,
            },
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
        var clinic = fixture.Engine.Current.Buildings.Single(building => building.Value.Id == clinicId);
        var worker = fixture.Engine.Current.Residents.Single(person => person.Id != fixture.ResidentId);
        worker.Replace(worker.Value with
        {
            Age = 25,
            Profession = Profession.Builder,
            X = 14,
            Y = 16,
            FromX = 14,
            FromY = 16,
            Agent = worker.Agent with
            {
                Initialized = true,
                NextThinkTick = 100,
                Goal = new AgentGoal { PlayerDirected = true, ReviewTick = 100 },
            },
        });
        clinic.Replace(clinic.Value with { Enabled = enabled });
        clinic.Replace(clinic.Value with { LastWorkedTick = fixture.Engine.Current.Tick });
        clinic.Replace(clinic.Value with { Workers = clinic.Value.Workers.Add(worker.Id) });
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
        fixture.Resident.Agent = fixture.Resident.Agent with
        {
            Goal = new AgentGoal { Kind = AgentGoalKind.Rest, TargetX = 16, TargetY = 16 },
        };

        fixture.Engine.Step();

        Assert.Equal(shouldRest, fixture.Resident.Agent.Goal.Kind == AgentGoalKind.Rest);
    }

    /// <summary>邻山采矿须开采已选中的山体，不能因脚下草地也有少量石头而长期采不到矿石。</summary>
    [Fact]
    public void Mining_at_a_mountain_edge_extracts_ore_from_the_mountain()
    {
        var fixture = Prepare();
        var ground = fixture.Engine.Current.Tiles[16 * 32 + 16];
        ground.Replace(ground.Value with { Terrain = TerrainType.Grass, ResourceAmount = 100 });
        var mountain = fixture.Engine.Current.Tiles[16 * 32 + 17];
        mountain.Replace(mountain.Value with { Terrain = TerrainType.Mountain, ResourceAmount = 100 });
        fixture.Resident.Profession = Profession.Miner;
        fixture.Resident.Agent = fixture.Resident.Agent with
        {
            Goal = new AgentGoal
            {
                Kind = AgentGoalKind.Work,
                TargetX = 16,
                TargetY = 16,
                PlayerDirected = true,
                ReviewTick = fixture.Engine.Current.Tick + 100,
            },
        };

        fixture.Engine.Step();

        Assert.True(fixture.Resident.Inventory.Ore > 0);
        Assert.True(mountain.ResourceAmount < 100);
        Assert.Equal(100, ground.ResourceAmount);
    }

    /// <summary>基础建设缺石材时，尚有矿石的居民先补石材，不因矿石未达到常备目标而拒绝纯石材地块。</summary>
    [Fact]
    public void A_miner_prioritizes_the_larger_stone_shortage()
    {
        var fixture = Prepare();
        fixture.Engine.Current.Buildings.RemoveAll(building => building.Value.Kind != BuildingKind.TownCenter);
        fixture.Town.Resources = new ResourceStock { Food = 40, Stone = 0, Ore = 12 };
        fixture.Resident.Profession = Profession.Miner;

        fixture.Engine.Step();

        Assert.Equal(ResourceKind.Stone, fixture.Resident.Agent.MaterialPriority);
        Assert.Equal(AgentGoalKind.Work, fixture.Resident.Agent.Goal.Kind);
    }

    private static WorldFixture Prepare(bool thirst = false)
    {
        var fixture = new WorldFixture();
        fixture.Engine.ConfigureWorld(fixture.Engine.State.Rules with
        {
            Aging = false,
            Hunger = false,
            Thirst = thirst,
            Disease = false,
            Births = false,
            Construction = false,
            Expansion = false,
            Research = false,
            Trade = false,
            Migration = false,
            Secession = false,
            Wars = false,
            Conflict = 0,
            FireSpread = false,
            ResourceRegeneration = false,
        }, false, false);
        foreach (var tile in fixture.Engine.Current.Tiles)
            tile.NaturalWaterYield = 0;
        fixture.Engine.Current.Tick = 8 + (3 - fixture.ResidentId % 4 + 4) % 4;
        fixture.Resident.Replace(fixture.Resident.Value with
        {
            Age = 25,
            Profession = Profession.Farmer,
            X = 16,
            Y = 16,
            FromX = 16,
            FromY = 16,
            Inventory = new ResourceStock { Food = 10, Water = 10 },
            Agent = fixture.Resident.Agent with { Initialized = true, NextThinkTick = 0, Goal = new AgentGoal() },
        });
        fixture.Town.Resources = new ResourceStock { Food = 40 };
        return fixture;
    }

    private static void SetJourney(WorldFixture fixture, int x, int y, int targetX, int targetY)
    {
        fixture.Resident.Replace(fixture.Resident.Value with
        {
            X = x,
            Y = y,
            FromX = x,
            FromY = y,
            Agent = fixture.Resident.Agent with
            {
                NextThinkTick = 100,
                Goal = new AgentGoal
                {
                    Kind = AgentGoalKind.Explore,
                    TargetX = targetX,
                    TargetY = targetY,
                    PlayerDirected = true,
                    ReviewTick = 100,
                },
            },
        });
    }
}
