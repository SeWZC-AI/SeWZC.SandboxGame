namespace SeWZC.WorldBox.Core.Tests;

/// <summary>移动速度、住宅分配、日程与背负救助的行为边界。</summary>
public sealed class ResidentRoutineTests
{
    /// <summary>心智重新初始化保留实际移动起点与整段路线。</summary>
    [Fact]
    public void Mind_reinitialization_keeps_the_completed_movement_route_valid()
    {
        var fixture = Prepare();
        fixture.Resident.Replace(fixture.Resident.Value.WithPosition(10, 10) with
        {
            Agent = fixture.Resident.Value.Agent with
            {
                Goal = new AgentGoal { Kind = AgentGoalKind.Explore, TargetX = 24, TargetY = 10, PlayerDirected = true, ReviewTick = 100 },
            },
        });
        fixture.Engine.Step();
        Assert.NotEmpty(fixture.Resident.Value.MovementRoute);
        var route = fixture.Resident.Value.MovementRoute;
        fixture.Resident.Replace(fixture.Resident.Value with { FrozenUntilTick = fixture.Engine.SimulationTick + 2 });
        fixture.Engine.EditResident(fixture.ResidentId, new ResidentEdit
        {
            Agent = fixture.Resident.Value.Agent with { Initialized = false },
        });

        fixture.Engine.Step();

        Assert.Equal(fixture.Engine.ExportJson(), WorldEngine.ImportJson(fixture.Engine.ExportJson()).ExportJson());
        Assert.Equal(route, fixture.Engine.GetResident(fixture.ResidentId)!.MovementRoute);
    }

    /// <summary>眼前被山墙隔开的住房不能分配或继续占用，有床位时改住可达住宅。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Housing_assignment_uses_an_accessible_house(bool alreadyAssigned)
    {
        var fixture = Prepare();
        fixture.Engine.Buildings.RemoveAll(building => building.Value.Kind != BuildingKind.TownCenter);
        for (var x = 13; x <= 18; x++)
        {
            var tile = fixture.Engine.Tiles[16 * fixture.Engine.Width + x];
            tile.Replace(tile.Value with { ClaimedSettlementId = fixture.Town.Value.Id, NationId = fixture.Town.Value.NationId });
        }
        var blocked = fixture.Engine.GrantFacility(fixture.Town.Value.Id, BuildingKind.Housing, 18, 16);
        var reachable = fixture.Engine.GrantFacility(fixture.Town.Value.Id, BuildingKind.Housing, 13, 16);
        fixture.Resident.Replace(fixture.Resident.Value.WithPosition(16, 16) with { HomeBuildingId = alreadyAssigned ? blocked : 0 });
        for (var y = 0; y < fixture.Engine.Height; y++)
        {
            var tile = fixture.Engine.Tiles[y * fixture.Engine.Width + 17];
            tile.Replace(tile.Value.WithTerrain(TerrainType.Mountain));
        }

        fixture.Engine.SetBuildingEnabled(blocked, true);

        Assert.Equal(reachable, fixture.Resident.Value.HomeBuildingId);
        Assert.Equal(fixture.Engine.ExportJson(), WorldEngine.ImportJson(fixture.Engine.ExportJson()).ExportJson());
    }

    /// <summary>住宅引用校验前仍先校验建筑集合，非法空元素返回参数错误。</summary>
    [Fact]
    public void Null_building_in_a_save_is_rejected_as_invalid_input()
    {
        var fixture = Prepare();
        var state = System.Text.Json.Nodes.JsonNode.Parse(fixture.Engine.ExportJson())!;
        state["Society"]!["Buildings"]![0] = null;

        Assert.Throws<ArgumentException>(() => WorldEngine.ImportJson(state.ToJsonString()));
    }

    /// <summary>聚落无法迁移而居民转入另一同国聚落时，清理原有移动与日程。</summary>
    [Fact]
    public void Settlement_removal_clears_a_relocated_residents_route()
    {
        var fixture = Prepare();
        fixture.Engine.SpawnResidents(0, 0, RaceKind.Human, 1);
        var destination = fixture.Engine.State.Settlements.Single(town => town.Id != fixture.Town.Value.Id);
        fixture.Engine.TransferTerritory(0, 0, fixture.Town.Value.NationId, 0);
        fixture.Resident.Replace(fixture.Resident.Value.WithPosition(10, 10) with
        {
            Agent = fixture.Resident.Value.Agent with
            {
                Goal = new AgentGoal { Kind = AgentGoalKind.Explore, TargetX = 24, TargetY = 10, PlayerDirected = true, ReviewTick = 100 },
            },
        });
        fixture.Engine.Step();
        Assert.NotEmpty(fixture.Resident.Value.MovementRoute);

        fixture.Engine.PaintTerrain(16, 16, TerrainType.Mountain, 13);

        var relocated = fixture.Engine.GetResident(fixture.ResidentId)!;
        Assert.Equal(destination.Id, relocated.SettlementId);
        Assert.Equal(fixture.Engine.ExportJson(), WorldEngine.ImportJson(fixture.Engine.ExportJson()).ExportJson());
        Assert.Empty(relocated.MovementRoute);
        Assert.Null(relocated.Agent.DailyPlan);
    }

    /// <summary>背负者的实际运输方式也用于患者普通编辑的位置校验。</summary>
    [Fact]
    public void A_carried_passenger_can_be_renamed_on_water()
    {
        var fixture = Prepare();
        fixture.Engine.SpawnResidents(16, 16, RaceKind.Human, 1);
        var patient = fixture.Engine.Residents.Single(person => person.Value.Id != fixture.ResidentId);
        fixture.Engine.Tiles[10 * 32 + 10].Replace(fixture.Engine.Tiles[10 * 32 + 10].Value.WithTerrain(TerrainType.Water));
        fixture.Resident.Replace(fixture.Resident.Value.WithPosition(10, 10) with
        {
            TravelMode = TravelMode.Boat, Inventory = new ResourceStock { Boats = 1 },
            Agent = fixture.Resident.Value.Agent with { Goal = new AgentGoal { Kind = AgentGoalKind.Rescue, TargetEntityId = patient.Value.Id } },
        });
        patient.Replace(patient.Value.WithPosition(10, 10) with
        {
            CarriedByResidentId = fixture.ResidentId, Age = 25, Activity = ResidentActivity.Unconscious,
            Agent = patient.Value.Agent with { Sleep = 0 },
        });

        fixture.Engine.EditResident(patient.Value.Id, new ResidentEdit { Name = "获救居民" });

        Assert.Equal("获救居民", fixture.Engine.GetResident(patient.Value.Id)!.Name);
        Assert.Equal(fixture.Engine.ExportJson(), WorldEngine.ImportJson(fixture.Engine.ExportJson()).ExportJson());
    }

    /// <summary>暂存日程也不能引用尚不存在的目标证据或事件。</summary>
    [Theory]
    [InlineData("EvidenceFactId")]
    [InlineData("CauseEventId")]
    public void Daily_work_rejects_invalid_story_references(string field)
    {
        var fixture = Prepare();
        fixture.Resident.Replace(fixture.Resident.Value with
        {
            Agent = fixture.Resident.Value.Agent with { DailyPlan = new ResidentDailyPlan { WorkGoal = new AgentGoal { Kind = AgentGoalKind.Gather } } },
        });
        var state = System.Text.Json.Nodes.JsonNode.Parse(fixture.Engine.ExportJson())!;
        state["Residents"]![0]!["Agent"]!["DailyPlan"]!["WorkGoal"]![field] = fixture.Engine.State.NextId;

        Assert.Throws<ArgumentException>(() => WorldEngine.ImportJson(state.ToJsonString()));
    }

    /// <summary>昨天无法在清醒时段往返的远工作点不能把晨起后的整天锁在住宅。</summary>
    [Fact]
    public void Morning_reconsiders_a_previous_job_that_is_too_far_from_home()
    {
        var fixture = Prepare();
        var home = fixture.Engine.GetResidentHome(fixture.ResidentId)!;
        var ground = fixture.Engine.Tiles[home.Y * fixture.Engine.State.Width + home.X];
        ground.Replace(ground.Value with { Terrain = TerrainType.Sand, RoadLevel = 0, Improvement = LandImprovement.None });
        fixture.Resident.Replace(fixture.Resident.Value.WithPosition(home.X, home.Y) with
        {
            IsInsideHome = true, Activity = ResidentActivity.Sleeping,
            Agent = fixture.Resident.Value.Agent with
            {
                Goal = new AgentGoal { Kind = AgentGoalKind.Sleep, TargetX = home.X, TargetY = home.Y, TargetEntityId = home.Id },
                DaytimeGoal = new AgentGoal
                {
                    Kind = AgentGoalKind.Gather,
                    TargetX = home.X >= fixture.Engine.State.Width / 2 ? 0 : fixture.Engine.State.Width - 1,
                    TargetY = home.Y >= fixture.Engine.State.Height / 2 ? 0 : fixture.Engine.State.Height - 1,
                },
            },
        });
        fixture.Engine.SimulationTick = SimulationTime.WakeTick - 1;

        fixture.Engine.Step();

        Assert.True(fixture.Resident.Value.Agent.DailyPlan!.ReturnHomeTick > SimulationTime.WakeTick);
        Assert.NotEqual(AgentGoalKind.Sleep, fixture.Resident.Value.Agent.Goal.Kind);
    }

    /// <summary>折跃后只在患者可通行的位置放下，否则保留实际背负关系。</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void A_waygate_trip_keeps_the_rescued_patient_at_a_safe_destination(bool mountain, bool surrounded)
    {
        var fixture = Prepare();
        fixture.Engine.ConfigureWorld(fixture.Engine.State.Rules, false, true);
        fixture.Engine.SpawnResidents(16, 16, RaceKind.Human, 1);
        var patient = fixture.Engine.Residents.Single(person => person.Value.Id != fixture.ResidentId);
        Grant(Advancement.SpatialMagic);
        for (var x = 14; x <= 18; x++)
        {
            var ground = fixture.Engine.Tiles[16 * 32 + x];
            ground.Replace(ground.Value with { ClaimedSettlementId = fixture.Town.Value.Id, NationId = fixture.Town.Value.NationId });
        }
        if (mountain)
        {
            var radius = surrounded ? 1 : 0;
            fixture.Engine.Buildings.RemoveAll(building => Math.Abs(building.Value.X - 18) <= radius
                                                          && Math.Abs(building.Value.Y - 16) <= radius);
            for (var y = 16 - radius; y <= 16 + radius; y++)
            for (var x = 18 - radius; x <= 18 + radius; x++)
            {
                var ground = fixture.Engine.Tiles[y * 32 + x];
                ground.Replace(ground.Value.WithTerrain(TerrainType.Mountain) with { RoadLevel = 0, Improvement = LandImprovement.None });
            }
            fixture.Resident.Replace(fixture.Resident.Value with { Race = RaceKind.Dwarf });
        }
        fixture.Engine.GrantFacility(fixture.Town.Value.Id, BuildingKind.Waygate, 14, 16);
        var destination = fixture.Engine.GrantFacility(fixture.Town.Value.Id, BuildingKind.Waygate, 18, 16);
        fixture.Engine.SimulationTick = 1;
        fixture.Resident.Replace(fixture.Resident.Value.WithPosition(14, 16) with
        {
            MagicTalent = 50, MagicTraining = 10, Mana = 100, Inventory = new ResourceStock { Crystals = 2 },
            Agent = fixture.Resident.Value.Agent with { Goal = new AgentGoal { Kind = AgentGoalKind.Rescue, TargetEntityId = patient.Value.Id } },
        });
        patient.Replace(patient.Value.WithPosition(14, 16) with
        {
            CarriedByResidentId = fixture.ResidentId, Age = 25, Activity = ResidentActivity.Unconscious,
            Agent = patient.Value.Agent with { Sleep = 0 },
        });

        fixture.Engine.TravelByWaygate(fixture.ResidentId, destination);

        Assert.Equal(fixture.Engine.ExportJson(), WorldEngine.ImportJson(fixture.Engine.ExportJson()).ExportJson());
        var rescued = fixture.Engine.GetResident(patient.Value.Id)!;
        if (surrounded)
        {
            Assert.Equal((18, 16), (rescued.X, rescued.Y));
            Assert.Equal(fixture.ResidentId, rescued.CarriedByResidentId);
        }
        else
        {
            Assert.True(Math.Abs(rescued.X - 18) + Math.Abs(rescued.Y - 16) <= 1);
            Assert.True(WorldEngine.CanTraverse(fixture.Engine.State.Tiles[rescued.Y * 32 + rescued.X], rescued.TravelMode, rescued.Race));
            Assert.Equal(0, rescued.CarriedByResidentId);
        }

        void Grant(Advancement research)
        {
            foreach (var prerequisite in research.Prerequisites)
                Grant(prerequisite);
            fixture.Engine.GrantReceivedResearch(fixture.Town.Value.Id, research);
        }
    }

    /// <summary>被背负者的住房通路使用救助者的种族通行能力。</summary>
    [Fact]
    public void A_dwarf_can_carry_a_human_across_mountains_to_the_assigned_home()
    {
        var fixture = Prepare();
        fixture.Engine.SpawnResidents(16, 16, RaceKind.Human, 1);
        var patient = fixture.Engine.Residents.Single(person => person.Value.Id != fixture.ResidentId);
        fixture.Engine.Buildings.RemoveAll(building => building.Value.Kind != BuildingKind.TownCenter);
        for (var x = 13; x <= 16; x++)
        {
            var tile = fixture.Engine.Tiles[16 * 32 + x];
            tile.Replace(tile.Value with { ClaimedSettlementId = fixture.Town.Value.Id, NationId = fixture.Town.Value.NationId });
        }
        var home = fixture.Engine.GrantFacility(fixture.Town.Value.Id, BuildingKind.Housing, 13, 16);
        for (var y = 0; y < 32; y++)
        {
            var tile = fixture.Engine.Tiles[y * 32 + 14];
            tile.Replace(tile.Value.WithTerrain(TerrainType.Mountain));
        }
        fixture.Resident.Replace(fixture.Resident.Value.WithPosition(15, 16) with
        {
            Race = RaceKind.Dwarf,
            Agent = fixture.Resident.Value.Agent with { Goal = new AgentGoal { Kind = AgentGoalKind.Rescue, TargetEntityId = patient.Value.Id } },
        });
        patient.Replace(patient.Value.WithPosition(15, 16) with
        {
            CarriedByResidentId = fixture.ResidentId, Age = 25, Activity = ResidentActivity.Unconscious,
            Agent = patient.Value.Agent with { Sleep = 0 },
        });

        fixture.Engine.SetBuildingEnabled(home, true);

        Assert.Equal(home, patient.Value.HomeBuildingId);
        Assert.Equal(fixture.ResidentId, patient.Value.CarriedByResidentId);
        Assert.Equal(fixture.Engine.ExportJson(), WorldEngine.ImportJson(fixture.Engine.ExportJson()).ExportJson());
    }

    /// <summary>体力降速在半体力以下连续变化，耗尽时最多减少一半。</summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(50, 1)]
    [InlineData(75, .75)]
    [InlineData(100, .5)]
    public void Stamina_speed_has_a_half_speed_floor(double fatigue, double expected)
    {
        var person = new Resident { Agent = new AgentState { Fatigue = fatigue } };
        Assert.Equal(expected, ResidentMovementRules.StaminaMultiplier(person));
    }

    /// <summary>平地一刻提交两格相邻路径，负重减少实际行走距离。</summary>
    [Theory]
    [InlineData(0, 2)]
    [InlineData(65, 1)]
    public void Walking_speed_accounts_for_actual_load(double wood, int steps)
    {
        var fixture = Prepare();
        fixture.Resident.Replace(fixture.Resident.Value.WithPosition(10, 10) with
        {
            Inventory = new ResourceStock { Wood = wood },
            Agent = fixture.Resident.Value.Agent with
            {
                Goal = new AgentGoal { Kind = AgentGoalKind.Explore, TargetX = 22, TargetY = 10, PlayerDirected = true, ReviewTick = 100 },
            },
        });
        fixture.Engine.Step();
        var person = fixture.Resident.Value;
        Assert.Equal(10 + steps, person.X);
        Assert.Equal(steps + 1, person.MovementRoute.Length);
        Assert.Equal(1, person.MoveDurationTicks);
        Assert.Equal(steps * ResidentNeedsRules.WalkingCost,
            ResidentNeedsRules.StaminaCapacity(person) * person.Agent.Fatigue / 100, 6);
        var restored = WorldEngine.ImportJson(fixture.Engine.ExportJson());
        fixture.Engine.Step(2);
        restored.Step(2);
        Assert.Equal(fixture.Engine.ExportJson(), restored.ExportJson());
    }

    /// <summary>住房只有实际容量，分配稳定且零刻存档可恢复。</summary>
    [Fact]
    public void Housing_assignment_never_exceeds_real_capacity()
    {
        var fixture = Prepare();
        fixture.Engine.SpawnResidents(16, 16, RaceKind.Human, WorldEngine.HousingCapacityPerLevel);
        var house = fixture.Engine.State.Society.Buildings.Single(b => b.Kind == BuildingKind.Housing);
        Assert.Equal(WorldEngine.HousingCapacityPerLevel, fixture.Engine.GetHousingOccupancy(house.Id));
        Assert.Single(fixture.Engine.State.Residents, person => person.HomeBuildingId == 0);
        Assert.Equal(fixture.Engine.ExportJson(), WorldEngine.ImportJson(fixture.Engine.ExportJson()).ExportJson());
    }

    /// <summary>在住宅内等待入睡，不把城镇中心当作睡眠地点。</summary>
    [Fact]
    public void Night_sleep_uses_the_assigned_house()
    {
        var fixture = Prepare();
        var house = fixture.Engine.GetResidentHome(fixture.ResidentId)!;
        fixture.Resident.Replace(fixture.Resident.Value.WithPosition(house.X, house.Y));
        fixture.Engine.SimulationTick = SimulationTime.SleepTick - 1;
        fixture.Engine.Step();
        Assert.True(fixture.Resident.Value.IsInsideHome);
        Assert.Equal(house.Id, fixture.Resident.Value.Agent.Goal.TargetEntityId);
        Assert.Equal(ResidentActivity.Sleeping, fixture.Resident.Value.Activity);
        Assert.NotEqual((fixture.Town.Value.X, fixture.Town.Value.Y), (house.X, house.Y));
    }

    /// <summary>远处工作提前结束，普通刻复用当天计划而不重新作出完整决策。</summary>
    [Fact]
    public void Daily_plan_is_reused_and_distant_work_returns_earlier()
    {
        var fixture = Prepare();
        fixture.Engine.SimulationTick = SimulationTime.WakeTick;
        fixture.Resident.Replace(fixture.Resident.Value with
        {
            Agent = fixture.Resident.Value.Agent with
            {
                Goal = new AgentGoal { Kind = AgentGoalKind.Gather, TargetX = 31, TargetY = 16, ReviewTick = 100 },
            },
        });
        fixture.Town.Replace(fixture.Town.Value.WithResources(new ResourceStock()));
        fixture.Engine.Step();
        var plan = fixture.Resident.Value.Agent.DailyPlan!;
        Assert.True(plan.ReturnHomeTick < SimulationTime.ReturnHomeTick);
        Assert.NotNull(plan.WorkGoal);
        fixture.Engine.Step();
        Assert.Same(plan, fixture.Resident.Value.Agent.DailyPlan);
        Assert.Empty(fixture.Resident.Value.Agent.Decisions);
    }

    /// <summary>昏迷在露天缓慢恢复，获救卧床后加快，但仍须达到苏醒门槛。</summary>
    [Fact]
    public void Unconscious_recovery_is_slower_until_rescued_into_a_bed()
    {
        var person = new Resident { Age = 25, Activity = ResidentActivity.Unconscious, Agent = new AgentState { Sleep = 0, Fatigue = 100 } };
        var outdoor = ResidentNeedsRules.Advance(person, ResidentNeedsRules.OutdoorRestQuality);
        var bed = ResidentNeedsRules.Advance(person with { IsInsideHome = true, BedRestAfterRescue = true }, 1);
        Assert.True(outdoor.Sleep > 0);
        Assert.True(outdoor.Sleep < bed.Sleep);
        Assert.True(outdoor.Fatigue > bed.Fatigue);
        Assert.True(ResidentNeedsRules.IsUnconscious(person.WithAgent(bed)));
    }

    /// <summary>救助者须到场背负，途中存档保留关系，实际入住房后才获得床位恢复。</summary>
    [Fact]
    public void Rescue_carries_a_person_home_and_resumes_from_a_mid_route_save()
    {
        var fixture = Prepare();
        fixture.Engine.SpawnResidents(16, 16, RaceKind.Human, 1);
        var patient = fixture.Engine.Residents.Single(r => r.Value.Id != fixture.ResidentId);
        var house = fixture.Engine.GetResidentHome(patient.Value.Id)!;
        patient.Replace(patient.Value.WithPosition(22, 16) with
        {
            Age = 25, Activity = ResidentActivity.Unconscious, Inventory = new ResourceStock { Food = 10, Water = 10 },
            Agent = patient.Value.Agent with { Sleep = 0, Fatigue = 90 },
        });
        fixture.Resident.Replace(fixture.Resident.Value.WithPosition(22, 16) with
        {
            Agent = fixture.Resident.Value.Agent with
            {
                Goal = new AgentGoal { Kind = AgentGoalKind.Rescue, TargetEntityId = patient.Value.Id, TargetX = 22, TargetY = 16, PlayerDirected = true, ReviewTick = 100 },
            },
        });
        fixture.Engine.Step();
        Assert.Equal(fixture.ResidentId, patient.Value.CarriedByResidentId);
        Assert.Equal(fixture.Resident.Value.MovementRoute, patient.Value.MovementRoute);
        Assert.False(patient.Value.IsInsideHome);
        var restored = WorldEngine.ImportJson(fixture.Engine.ExportJson());
        fixture.Engine.Step(8);
        restored.Step(8);
        Assert.Equal(fixture.Engine.ExportJson(), restored.ExportJson());
        Assert.Equal(0, patient.Value.CarriedByResidentId);
        Assert.True(patient.Value.IsInsideHome);
        Assert.True(patient.Value.BedRestAfterRescue);
        Assert.Equal((house.X, house.Y), (patient.Value.X, patient.Value.Y));
    }

    /// <summary>复评时继续有效的救助，途中补觉不会把昏迷者放下。</summary>
    [Fact]
    public void Rescue_review_keeps_the_passenger_while_the_carrier_sleeps()
    {
        var fixture = Prepare();
        fixture.Engine.SpawnResidents(16, 16, RaceKind.Human, 1);
        var patient = fixture.Engine.Residents.Single(r => r.Value.Id != fixture.ResidentId);
        fixture.Engine.SimulationTick = SimulationTime.TicksPerDay + SimulationTime.WakeTick;
        while ((fixture.Engine.SimulationTick + 1 + fixture.ResidentId) % 4 != 0)
            fixture.Engine.SimulationTick++;
        fixture.Resident.Replace(fixture.Resident.Value.WithPosition(10, 10) with
        {
            Activity = ResidentActivity.Sleeping,
            Agent = fixture.Resident.Value.Agent with
            {
                Fatigue = 70, Sleep = 50, NextThinkTick = SimulationTime.TicksPerDay,
                Goal = new AgentGoal { Kind = AgentGoalKind.Rescue, TargetEntityId = patient.Value.Id,
                    TargetX = 10, TargetY = 10, ReviewTick = SimulationTime.TicksPerDay },
            },
        });
        patient.Replace(patient.Value.WithPosition(10, 10) with
        {
            Age = 25, CarriedByResidentId = fixture.ResidentId, Activity = ResidentActivity.Unconscious,
            Agent = patient.Value.Agent with { Sleep = 0, Fatigue = 90 },
        });

        fixture.Engine.Step();

        Assert.Equal(AgentGoalKind.Rescue, fixture.Resident.Value.Agent.Goal.Kind);
        Assert.Equal(fixture.ResidentId, patient.Value.CarriedByResidentId);
        Assert.Equal(ResidentActivity.Sleeping, fixture.Resident.Value.Activity);
    }

    private static WorldFixture Prepare()
    {
        var fixture = new WorldFixture();
        fixture.Engine.ConfigureWorld(fixture.Engine.State.Rules with
        {
            Births = false, Construction = false, Expansion = false, Wars = false, Secession = false,
            Migration = false, Research = false, Disease = false,
        }, false, false);
        fixture.Resident.Replace(fixture.Resident.Value with
        {
            Age = 25, Inventory = new ResourceStock { Food = 1, Water = 1 },
            Agent = fixture.Resident.Value.Agent with { Initialized = true, NextThinkTick = 100 },
        });
        return fixture;
    }

    /// <summary>返家睡眠必须真正进入住宅，不能在邻格提前开始补觉。</summary>
    [Fact]
    public void Recovery_walks_into_the_house_before_sleeping()
    {
        var fixture = Prepare();
        var house = fixture.Engine.GetResidentHome(fixture.ResidentId)!;
        fixture.Engine.SimulationTick = SimulationTime.WakeTick;
        fixture.Resident.Replace(fixture.Resident.Value.WithPosition(house.X + 1, house.Y) with
        {
            Agent = fixture.Resident.Value.Agent with
            {
                Sleep = 30, Goal = new AgentGoal { Kind = AgentGoalKind.Rest, TargetEntityId = house.Id,
                    TargetX = house.X, TargetY = house.Y, PlayerDirected = true, ReviewTick = 100 },
            },
        });
        fixture.Engine.Step();
        Assert.Equal(ResidentActivity.Wandering, fixture.Resident.Value.Activity);
        Assert.False(fixture.Resident.Value.IsInsideHome);
        fixture.Engine.Step();
        Assert.True(fixture.Resident.Value.IsInsideHome);
        Assert.Equal(ResidentActivity.Sleeping, fixture.Resident.Value.Activity);
    }

    /// <summary>暂停时停用住宅立即解除入住，在地图重新出现且可保存。</summary>
    [Fact]
    public void Disabling_a_home_releases_its_residents_immediately()
    {
        var fixture = Prepare();
        var house = fixture.Engine.GetResidentHome(fixture.ResidentId)!;
        fixture.Resident.Replace(fixture.Resident.Value.WithPosition(house.X, house.Y) with { IsInsideHome = true });
        fixture.Engine.SetBuildingEnabled(house.Id, false);
        Assert.Equal(0, fixture.Resident.Value.HomeBuildingId);
        Assert.False(fixture.Resident.Value.IsInsideHome);
        Assert.Equal(0, fixture.Engine.GetHousingCapacity(fixture.Town.Value.Id));
        Assert.Equal(fixture.Engine.ExportJson(), WorldEngine.ImportJson(fixture.Engine.ExportJson()).ExportJson());
    }

    /// <summary>在途居民的位置编辑清除原来的移动路径，编辑后存档能够恢复。</summary>
    [Fact]
    public void Position_edit_replaces_an_in_progress_multi_tile_route()
    {
        var fixture = Prepare();
        fixture.Resident.Replace(fixture.Resident.Value.WithPosition(10, 10) with
        {
            Agent = fixture.Resident.Value.Agent with
            {
                Goal = new AgentGoal { Kind = AgentGoalKind.Explore, TargetX = 24, TargetY = 10, PlayerDirected = true, ReviewTick = 100 },
            },
        });
        fixture.Engine.Step();
        Assert.NotEmpty(fixture.Resident.Value.MovementRoute);
        fixture.Engine.EditResident(fixture.ResidentId, new ResidentEdit { X = 20, Y = 20 });
        var person = fixture.Engine.GetResident(fixture.ResidentId)!;
        Assert.Empty(person.MovementRoute);
        Assert.Equal((20, 20), (person.FromX, person.FromY));
        Assert.False(person.IsInsideHome);
        Assert.Equal(fixture.Engine.ExportJson(), WorldEngine.ImportJson(fixture.Engine.ExportJson()).ExportJson());
    }

    /// <summary>非法日程不能通过心智编辑改变世界或随机序列。</summary>
    [Fact]
    public void Invalid_daily_plan_edit_is_atomic()
    {
        var fixture = Prepare();
        var before = fixture.Engine.ExportJson();
        Assert.Throws<ArgumentException>(() => fixture.Engine.EditResident(fixture.ResidentId, new ResidentEdit
        {
            Agent = fixture.Resident.Value.Agent with { DailyPlan = new ResidentDailyPlan { PlannedTick = -1 } },
        }));
        Assert.Equal(before, fixture.Engine.ExportJson());
    }

    /// <summary>地形编辑挪走在途居民时，旧路线不能残留在新位置的存档中。</summary>
    [Fact]
    public void Terrain_relocation_clears_the_old_movement_route()
    {
        var fixture = Prepare();
        fixture.Resident.Replace(fixture.Resident.Value.WithPosition(10, 10) with
        {
            Agent = fixture.Resident.Value.Agent with
            {
                Goal = new AgentGoal { Kind = AgentGoalKind.Explore, TargetX = 24, TargetY = 10, PlayerDirected = true, ReviewTick = 100 },
            },
        });
        fixture.Engine.Step();
        var travelling = fixture.Resident.Value;
        fixture.Engine.PaintTerrain(travelling.X, travelling.Y, TerrainType.Mountain, 0);
        Assert.Empty(fixture.Resident.Value.MovementRoute);
        Assert.Equal(fixture.Engine.ExportJson(), WorldEngine.ImportJson(fixture.Engine.ExportJson()).ExportJson());
    }

    /// <summary>被背负的人使用救助者的通行条件，不会因自己的步行状态被挪走。</summary>
    [Fact]
    public void A_carried_person_stays_with_a_boat_carrier_during_terrain_edits()
    {
        var fixture = Prepare();
        fixture.Engine.SpawnResidents(16, 16, RaceKind.Human, 1);
        var patient = fixture.Engine.Residents.Single(r => r.Value.Id != fixture.ResidentId);
        fixture.Engine.Tiles[10 * 32 + 10].Replace(fixture.Engine.Tiles[10 * 32 + 10].Value.WithTerrain(TerrainType.Water));
        fixture.Resident.Replace(fixture.Resident.Value.WithPosition(10, 10) with
        {
            TravelMode = TravelMode.Boat, Inventory = new ResourceStock { Boats = 1 },
            Agent = fixture.Resident.Value.Agent with
            {
                Goal = new AgentGoal { Kind = AgentGoalKind.Rescue, TargetEntityId = patient.Value.Id, TargetX = 10, TargetY = 10 },
            },
        });
        patient.Replace(patient.Value.WithPosition(10, 10) with
        {
            CarriedByResidentId = fixture.ResidentId, Activity = ResidentActivity.Unconscious,
            Agent = patient.Value.Agent with { Sleep = 0 },
        });
        fixture.Engine.PaintTerrain(2, 2, TerrainType.Grass, 0);
        Assert.Equal(fixture.ResidentId, patient.Value.CarriedByResidentId);
        Assert.Equal((10, 10), (patient.Value.X, patient.Value.Y));
        Assert.Equal(fixture.Engine.ExportJson(), WorldEngine.ImportJson(fixture.Engine.ExportJson()).ExportJson());
        fixture.Engine.SetBuildingEnabled(patient.Value.HomeBuildingId, false);
        Assert.Equal(fixture.Engine.ExportJson(), WorldEngine.ImportJson(fixture.Engine.ExportJson()).ExportJson());
    }

    /// <summary>地形改变导致承载者归档时，乘客落地后仍可保存恢复。</summary>
    [Fact]
    public void Terrain_changes_can_release_a_passenger_after_the_carrier_dies()
    {
        var fixture = Prepare();
        fixture.Engine.SpawnResidents(16, 16, RaceKind.Human, 1);
        var patient = fixture.Engine.Residents.Single(person => person.Value.Id != fixture.ResidentId);
        fixture.Engine.Tiles[10 * 32 + 10].Replace(fixture.Engine.Tiles[10 * 32 + 10].Value.WithTerrain(TerrainType.Water));
        fixture.Resident.Replace(fixture.Resident.Value.WithPosition(10, 10) with
        {
            Health = 10, TravelMode = TravelMode.Boat, Inventory = new ResourceStock { Boats = 1 },
            Agent = fixture.Resident.Value.Agent with { Goal = new AgentGoal { Kind = AgentGoalKind.Rescue, TargetEntityId = patient.Value.Id } },
        });
        patient.Replace(patient.Value.WithPosition(10, 10) with
        {
            CarriedByResidentId = fixture.ResidentId, Age = 25, Activity = ResidentActivity.Unconscious,
            Agent = patient.Value.Agent with { Sleep = 0 },
        });

        fixture.Engine.PaintTerrain(10, 10, TerrainType.Mountain, 0);

        Assert.Equal(0, fixture.Engine.GetResident(fixture.ResidentId)!.Health);
        Assert.Equal(0, patient.Value.CarriedByResidentId);
        Assert.Equal(fixture.Engine.ExportJson(), WorldEngine.ImportJson(fixture.Engine.ExportJson()).ExportJson());
    }

    /// <summary>不能出门的孩子从实际回家的成年人处取得粮水，粮仓不会隔空补给。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Children_share_only_supplies_that_have_reached_their_house(bool adultAtHome)
    {
        var fixture = Prepare();
        fixture.Engine.SpawnResidents(16, 16, RaceKind.Human, 1);
        var child = fixture.Engine.Residents.Single(r => r.Value.Id != fixture.ResidentId);
        var house = fixture.Engine.GetResidentHome(child.Value.Id)!;
        child.Replace(child.Value.WithPosition(house.X, house.Y) with
        {
            Age = 4, IsInsideHome = true, Hunger = 40, Thirst = 40, Inventory = new ResourceStock(),
        });
        fixture.Resident.Replace(fixture.Resident.Value.WithPosition(adultAtHome ? house.X : 16, adultAtHome ? house.Y : 16) with
        {
            IsInsideHome = adultAtHome,
            Agent = fixture.Resident.Value.Agent with { Goal = new AgentGoal { PlayerDirected = true, ReviewTick = 100 } },
        });
        fixture.Engine.SimulationTick = SimulationTime.WakeTick;
        fixture.Engine.Step();
        var current = fixture.Engine.GetResident(child.Value.Id)!;
        Assert.Equal(adultAtHome, current.Hunger < 40);
        Assert.Equal(adultAtHome, current.Thirst < 40);
    }

    /// <summary>指定救助也遵守十二岁以下不劳动的限制。</summary>
    [Fact]
    public void A_child_cannot_carry_an_unconscious_adult()
    {
        var fixture = Prepare();
        fixture.Engine.SpawnResidents(16, 16, RaceKind.Human, 1);
        var patient = fixture.Engine.Residents.Single(r => r.Value.Id != fixture.ResidentId);
        patient.Replace(patient.Value.WithPosition(10, 10) with { Age = 25, Activity = ResidentActivity.Unconscious,
            Agent = patient.Value.Agent with { Sleep = 0 } });
        fixture.Resident.Replace(fixture.Resident.Value.WithPosition(10, 10) with
        {
            Age = 11, Agent = fixture.Resident.Value.Agent with
            {
                Goal = new AgentGoal { Kind = AgentGoalKind.Rescue, TargetEntityId = patient.Value.Id, TargetX = 10,
                    TargetY = 10, PlayerDirected = true, ReviewTick = 100 },
            },
        });
        fixture.Engine.Step();
        Assert.Equal(0, fixture.Engine.GetResident(patient.Value.Id)!.CarriedByResidentId);
        Assert.Equal(ResidentActivity.Resting, fixture.Resident.Value.Activity);
    }

    /// <summary>低睡眠触发的重新安排真正停止原有采集，不被任务持续分支吞掉。</summary>
    [Fact]
    public void Low_sleep_interrupts_an_otherwise_productive_daily_job()
    {
        var fixture = Prepare();
        fixture.Engine.SimulationTick = SimulationTime.WakeTick;
        while ((fixture.Engine.SimulationTick + 1 + fixture.ResidentId) % 4 != 0)
            fixture.Engine.SimulationTick++;
        fixture.Town.Replace(fixture.Town.Value.WithResources(new ResourceStock()));
        fixture.Resident.Replace(fixture.Resident.Value.WithPosition(10, 10) with
        {
            Agent = fixture.Resident.Value.Agent with { Sleep = 40, Goal = new AgentGoal
            {
                Kind = AgentGoalKind.Gather, TargetX = 10, TargetY = 10, ReviewTick = 100,
            } },
        });
        fixture.Engine.Tiles[10 * 32 + 10].Replace(fixture.Engine.Tiles[10 * 32 + 10].Value with
            { ResourceAmount = 100, Fertility = 100, Plants = new PlantCoverage { Grass = 1 } });
        fixture.Engine.Step();
        Assert.Equal(AgentGoalKind.Rest, fixture.Resident.Value.Agent.Goal.Kind);
    }

    /// <summary>每日安排只会因眼前已发现的设施失效而提前中断。</summary>
    [Theory]
    [InlineData(4, true)]
    [InlineData(8, false)]
    public void Daily_work_does_not_remotely_detect_a_facility_fire(int distance, bool interrupted)
    {
        var fixture = Prepare();
        for (var x = 16; x <= 24; x++)
            Claim(x, 16);
        var farm = fixture.Engine.GrantFacility(fixture.Town.Value.Id, BuildingKind.Farm, 24, 16);
        fixture.Town.Replace(fixture.Town.Value.WithResources(new ResourceStock()));
        fixture.Engine.SimulationTick = SimulationTime.WakeTick;
        while ((fixture.Engine.SimulationTick + 1 + fixture.ResidentId) % 4 != 0)
            fixture.Engine.SimulationTick++;
        fixture.Resident.Replace(fixture.Resident.Value.WithPosition(24 - distance, 16) with
        {
            Agent = fixture.Resident.Value.Agent with { NextThinkTick = 100, Goal = new AgentGoal
            {
                Kind = AgentGoalKind.Work, TargetEntityId = farm, TargetX = 24, TargetY = 16, ReviewTick = 100,
            } },
        });
        var target = fixture.Engine.Tiles[16 * 32 + 24];
        target.Replace(target.Value with { FireTicks = 20 });

        fixture.Engine.Step();

        Assert.Equal(interrupted, fixture.Resident.Value.Agent.Goal.TargetEntityId != farm);

        void Claim(int x, int y)
        {
            var tile = fixture.Engine.Tiles[y * 32 + x];
            tile.Replace(tile.Value with { ClaimedSettlementId = fixture.Town.Value.Id, NationId = fixture.Town.Value.NationId });
        }
    }

    /// <summary>多格移动在取水岸格停止，不能继续走回原地。</summary>
    [Fact]
    public void Multi_tile_movement_stops_at_the_usable_shore()
    {
        var fixture = Prepare();
        for (var y = 8; y <= 12; y++)
        for (var x = 8; x <= 15; x++)
            fixture.Engine.Tiles[y * 32 + x].Replace(fixture.Engine.Tiles[y * 32 + x].Value.WithTerrain(TerrainType.Mountain));
        for (var x = 10; x <= 11; x++)
            fixture.Engine.Tiles[10 * 32 + x].Replace(fixture.Engine.Tiles[10 * 32 + x].Value.WithTerrain(TerrainType.Grass));
        fixture.Engine.Tiles[10 * 32 + 12].Replace(fixture.Engine.Tiles[10 * 32 + 12].Value.WithTerrain(TerrainType.River));
        fixture.Engine.SimulationTick = SimulationTime.WakeTick;
        fixture.Resident.Replace(fixture.Resident.Value.WithPosition(10, 10) with
        {
            Inventory = new ResourceStock { Food = 1 },
            Agent = fixture.Resident.Value.Agent with { Goal = new AgentGoal
            {
                Kind = AgentGoalKind.FetchWater, TargetX = 12, TargetY = 10, TargetEntityId = 10 * 32 + 12 + 1,
                PlayerDirected = true, ReviewTick = 100,
            } },
        });
        fixture.Engine.Step();
        Assert.Equal((11, 10), (fixture.Resident.Value.X, fixture.Resident.Value.Y));
        fixture.Engine.Step();
        Assert.True(fixture.Resident.Value.Inventory.Water > 0);
    }

    /// <summary>自主救助沿用劳动年龄门槛，允许十二岁以上的居民参与。</summary>
    [Theory]
    [InlineData(11, false)]
    [InlineData(12, true)]
    [InlineData(17, true)]
    [InlineData(18, true)]
    public void Automatic_rescue_uses_the_normal_working_age(double age, bool canRescue)
    {
        var fixture = Prepare();
        fixture.Engine.SpawnResidents(16, 16, RaceKind.Human, 1);
        var patient = fixture.Engine.Residents.Single(person => person.Value.Id != fixture.ResidentId);
        fixture.Engine.SimulationTick = SimulationTime.WakeTick;
        while ((fixture.Engine.SimulationTick + 1 + fixture.ResidentId) % 4 != 0)
            fixture.Engine.SimulationTick++;
        fixture.Resident.Replace(fixture.Resident.Value.WithPosition(10, 10) with { Age = age });
        patient.Replace(patient.Value.WithPosition(10, 10) with
        {
            Age = 25, Activity = ResidentActivity.Unconscious,
            Agent = patient.Value.Agent with { Sleep = 0 },
        });

        fixture.Engine.Step();

        Assert.Equal(canRescue, patient.Value.CarriedByResidentId == fixture.ResidentId);
    }

    /// <summary>附近有人需要帮助时，不覆盖已经出发的异地递送使命。</summary>
    [Fact]
    public void Automatic_rescue_preserves_an_existing_delivery_mission()
    {
        var fixture = Prepare();
        fixture.Engine.SpawnResidents(16, 16, RaceKind.Human, 1);
        var patient = fixture.Engine.Residents.Single(r => r.Value.Id != fixture.ResidentId);
        patient.Replace(patient.Value.WithPosition(10, 10) with { Age = 25, Activity = ResidentActivity.Unconscious,
            Agent = patient.Value.Agent with { Sleep = 0 } });
        fixture.Engine.SimulationTick = SimulationTime.WakeTick;
        while ((fixture.Engine.SimulationTick + 1 + fixture.ResidentId) % 4 != 0)
            fixture.Engine.SimulationTick++;
        fixture.Resident.Replace(fixture.Resident.Value.WithPosition(10, 10) with
        {
            Agent = fixture.Resident.Value.Agent with { DestinationSettlementId = fixture.Town.Value.Id, Goal = new AgentGoal
            {
                Kind = AgentGoalKind.DeliverMessage, TargetSettlementId = fixture.Town.Value.Id,
                TargetX = 16, TargetY = 16, ReviewTick = 100,
            } },
        });
        fixture.Engine.Step();
        Assert.Equal(AgentGoalKind.DeliverMessage, fixture.Resident.Value.Agent.Goal.Kind);
        Assert.Equal(0, fixture.Engine.GetResident(patient.Value.Id)!.CarriedByResidentId);
    }

    /// <summary>没有邻岸时保留实际同舟关系；承载者死亡后将真实舟船转交给乘客。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_passenger_remains_safe_when_rescue_ends_away_from_shore(bool carrierDies)
    {
        var fixture = Prepare();
        fixture.Engine.SpawnResidents(16, 16, RaceKind.Human, 1);
        var patient = fixture.Engine.Residents.Single(r => r.Value.Id != fixture.ResidentId);
        for (var y = 9; y <= 11; y++)
        for (var x = 9; x <= 11; x++)
            fixture.Engine.Tiles[y * 32 + x].Replace(fixture.Engine.Tiles[y * 32 + x].Value.WithTerrain(TerrainType.Water));
        fixture.Resident.Replace(fixture.Resident.Value.WithPosition(10, 10) with
        {
            TravelMode = TravelMode.Boat, Inventory = new ResourceStock { Boats = 1 },
            Agent = fixture.Resident.Value.Agent with { Goal = new AgentGoal { Kind = AgentGoalKind.Rescue,
                TargetEntityId = patient.Value.Id, TargetX = 10, TargetY = 10 } },
        });
        patient.Replace(patient.Value.WithPosition(10, 10) with
        {
            CarriedByResidentId = fixture.ResidentId, Age = 25, Activity = ResidentActivity.Unconscious,
            Agent = patient.Value.Agent with { Sleep = 0 },
        });
        if (carrierDies)
            fixture.Engine.EditResident(fixture.ResidentId, new ResidentEdit { Health = 0 });
        else
            fixture.Engine.SetBuildingEnabled(patient.Value.HomeBuildingId, false);
        var passenger = fixture.Engine.GetResident(patient.Value.Id)!;
        Assert.Equal((10, 10), (passenger.X, passenger.Y));
        Assert.Equal(carrierDies ? 0 : fixture.ResidentId, passenger.CarriedByResidentId);
        if (carrierDies)
        {
            Assert.Equal(TravelMode.Boat, passenger.TravelMode);
            Assert.Equal(1, passenger.Inventory.Boats);
            Assert.Equal(0, fixture.Engine.GetResident(fixture.ResidentId)!.Inventory.Boats);
        }
        Assert.Equal(fixture.Engine.ExportJson(), WorldEngine.ImportJson(fixture.Engine.ExportJson()).ExportJson());
    }
}
