namespace SeWZC.WorldBox.Core.Tests;

/// <summary>依据本地需求、身体条件和真实工作地点调整分工。</summary>
public sealed class WorkforceTests
{
    /// <summary>儿童病患也产生医师需求，能够保留现有医师或从临时劳工中补招。</summary>
    /// <param name="initialProfession">健康候选人的初始职业。</param>
    [Theory]
    [InlineData(Profession.Physician)]
    [InlineData(Profession.Laborer)]
    public void A_sick_child_keeps_or_creates_a_local_physician_job(Profession initialProfession)
    {
        var fixture = Prepare();
        var people = fixture.Engine.Residents.Where(person => person.Value.Id != fixture.ResidentId).ToArray();
        var patient = people[0];
        patient.Replace(patient.Value with
        {
            Age = 10, Profession = Profession.Child, Health = 70, SicknessTicks = 20,
        });
        var physician = people[1];
        physician.Replace(physician.Value with { Profession = initialProfession });
        GrantResearch(fixture, Advancement.Sanitation);
        var ground = fixture.Engine.Tiles[16 * 32 + 17];
        ground.Replace(ground.Value with { ClaimedSettlementId = fixture.Town.Value.Id, NationId = fixture.Town.Value.NationId });
        var hospitalId = fixture.Engine.GrantFacility(fixture.Town.Value.Id, BuildingKind.Hospital, 17, 16);
        fixture.Engine.SimulationTick =
            SimulationTime.TicksPerYear + SimulationTime.TicksPerMonth + SimulationTime.WakeTick;

        fixture.Engine.TickSociety();

        Assert.Equal(Profession.Physician, physician.Value.Profession);
        Assert.Equal(hospitalId, physician.Value.Agent.WorkplaceId);
        Assert.Single(fixture.Engine.State.Residents, person => person.Profession == Profession.Physician);
        Assert.Equal(Profession.Child, patient.Value.Profession);
    }

    /// <summary>人类能够在天然山地邻格开采矿石，石材充足不能抵消矿石缺口。</summary>
    [Fact]
    public void A_mountain_edge_provides_mining_jobs_when_only_ore_is_missing()
    {
        var fixture = Prepare();
        foreach (var tile in fixture.Engine.Tiles)
            tile.Replace(tile.Value with { ResourceAmount = 0, Deposit = null, DepositAmount = 0 });
        fixture.Town.Replace(fixture.Town.Value.WithResources(fixture.Town.Value.Resources with { Stone = 100, Ore = 0 }));
        var mountainIndex = 16 * 32 + 17;
        var mountain = fixture.Engine.Tiles[mountainIndex];
        mountain.Replace(mountain.Value with { Terrain = TerrainType.Mountain, ResourceAmount = 100 });
        fixture.Engine.SimulationTick =
            SimulationTime.TicksPerYear + SimulationTime.TicksPerMonth + SimulationTime.WakeTick;

        fixture.Engine.TickSociety();

        var miners = fixture.Engine.State.Residents.Where(person => person.Profession == Profession.Miner).ToArray();
        Assert.NotEmpty(miners);
        Assert.All(miners, person =>
        {
            Assert.NotEqual(mountainIndex, person.Agent.WorkAreaIndex);
            Assert.Equal(1, Math.Abs(person.Agent.WorkAreaIndex % 32 - 17)
                            + Math.Abs(person.Agent.WorkAreaIndex / 32 - 16));
        });
        Assert.Equal(100, mountain.Value.ResourceAmount);
    }

    /// <summary>矮人已有的天然山地工作点可继续使用，不因普通种族的通行限制被撤销。</summary>
    [Fact]
    public void A_dwarf_keeps_a_work_area_on_a_natural_mountain()
    {
        var fixture = Prepare();
        foreach (var tile in fixture.Engine.Tiles)
            tile.Replace(tile.Value with { ResourceAmount = 0, Deposit = null, DepositAmount = 0 });
        fixture.Town.Replace(fixture.Town.Value.WithResources(fixture.Town.Value.Resources with { Stone = 0, Ore = 0 }));
        var mountainIndex = 16 * 32 + 17;
        var mountain = fixture.Engine.Tiles[mountainIndex];
        mountain.Replace(mountain.Value with { Terrain = TerrainType.Mountain, ResourceAmount = 100 });
        var miner = fixture.Engine.Residents.First(person => person.Value.Id != fixture.ResidentId);
        miner.Replace(miner.Value with { Race = RaceKind.Dwarf, Profession = Profession.Miner });
        miner.Replace(miner.Value.WithAgent(miner.Value.Agent with { WorkAreaIndex = mountainIndex }));
        fixture.Engine.SimulationTick =
            SimulationTime.TicksPerYear + SimulationTime.TicksPerMonth + SimulationTime.WakeTick;

        fixture.Engine.TickSociety();

        Assert.Equal(Profession.Miner, miner.Value.Profession);
        Assert.Equal(mountainIndex, miner.Value.Agent.WorkAreaIndex);
    }

    /// <summary>已解锁且已发现的阶段矿藏短缺仍需要矿工，安排岗位本身不能发现未知矿藏。</summary>
    /// <param name="kind">待补充的阶段矿藏资源。</param>
    /// <param name="discovered">本地是否已发现该矿藏。</param>
    [Theory]
    [InlineData(ResourceKind.Coal, true)]
    [InlineData(ResourceKind.Oil, true)]
    [InlineData(ResourceKind.RareEarth, true)]
    [InlineData(ResourceKind.Coal, false)]
    public void Known_deposit_demand_is_not_hidden_by_full_stone_and_ore(ResourceKind kind, bool discovered)
    {
        var fixture = Prepare();
        foreach (var tile in fixture.Engine.Tiles)
            tile.Replace(tile.Value with { ResourceAmount = 0, Deposit = null, DepositAmount = 0 });
        var deposit = fixture.Engine.Tiles[16 * 32 + 17];
        deposit.Replace(deposit.Value with { Deposit = kind, DepositAmount = 100, DepositDiscovered = discovered });
        GrantResearch(fixture, WorldEngine.DepositResearch(kind)!);
        fixture.Town.Replace(fixture.Town.Value.WithResources(fixture.Town.Value.Resources with
        {
            Stone = 10_000,
            Ore = 10_000,
            Coal = 16,
            Oil = 16,
            RareEarth = 16,
        }));
        fixture.Town.Replace(fixture.Town.Value.WithResources(fixture.Town.Value.Resources.WithAmount(kind, 0)));
        var miner = fixture.Engine.Residents.First(person => person.Value.Id != fixture.ResidentId);
        miner.Replace(miner.Value with { Profession = Profession.Miner });
        miner.Replace(miner.Value.WithAgent(miner.Value.Agent with { WorkAreaIndex = 16 * 32 + 17 }));
        fixture.Engine.SimulationTick =
            SimulationTime.TicksPerYear + SimulationTime.TicksPerMonth + SimulationTime.WakeTick;

        fixture.Engine.TickSociety();

        Assert.Equal(discovered ? Profession.Miner : Profession.Laborer, miner.Value.Profession);
        Assert.Equal(discovered ? 16 * 32 + 17 : -1, miner.Value.Agent.WorkAreaIndex);
        Assert.Equal(discovered, deposit.Value.DepositDiscovered);
        Assert.Equal(100, deposit.Value.DepositAmount);
        Assert.Equal(0, miner.Value.Inventory.Get(kind));
    }

    /// <summary>容量减少后，多余的本地专业工人可以等待换岗，但不能共同保留同一个容量不足的固定岗位。</summary>
    [Fact]
    public void Reduced_workplace_capacity_releases_excess_assignments_even_during_a_career_cooldown()
    {
        var fixture = Prepare();
        fixture.Town.Replace(fixture.Town.Value.WithResources(fixture.Town.Value.Resources with { Wood = 48 }));
        var plot = fixture.Engine.Tiles[16 * 32 + 17];
        plot.Replace(plot.Value with
        {
            Terrain = TerrainType.Forest,
            ResourceAmount = 100,
            Plants = new PlantCoverage { Trees = 1 },
            ClaimedSettlementId = fixture.Town.Value.Id,
            NationId = fixture.Town.Value.NationId,
        });
        fixture.Engine.Tiles[16 * 32 + 18].Replace(plot.Value);
        var id = fixture.Engine.GrantFacility(fixture.Town.Value.Id, BuildingKind.LumberCamp, 17, 16);
        var building = fixture.Engine.Buildings.Single(b => b.Value.Id == id);
        building.Replace(building.Value with { WorkSlots = 1 });
        var people = fixture.Engine.Residents.Where(p => p.Value.Id != fixture.ResidentId).Take(2).ToArray();
        foreach (var person in people)
        {
            person.Replace(person.Value with { Profession = Profession.Lumberjack });
            person.Replace(person.Value.WithAgent(person.Value.Agent with { WorkplaceId = id }));
            person.Replace(person.Value.WithAgent(person.Value.Agent with { JobChangedTick = 20 }));
        }

        fixture.Engine.SimulationTick = SimulationTime.TicksPerMonth + SimulationTime.WakeTick;

        fixture.Engine.TickSociety();

        Assert.All(people, p => Assert.Equal(Profession.Lumberjack, p.Value.Profession));
        Assert.Single(people, p => p.Value.Agent.WorkplaceId == id);
    }

    /// <summary>临时劳工可接受新出现的工位，已有专业分工仍受换岗间隔约束。</summary>
    [Fact]
    public void An_unassigned_laborer_can_take_a_new_job_without_waiting_a_year()
    {
        var fixture = Prepare();
        fixture.Town.Replace(fixture.Town.Value.WithResources(fixture.Town.Value.Resources with { Wood = 48 }));
        foreach (var tile in fixture.Engine.Tiles)
            tile.Replace(tile.Value with { ResourceAmount = 0, Plants = new PlantCoverage() });
        var plot = fixture.Engine.Tiles[16 * 32 + 17];
        plot.Replace(plot.Value with
        {
            Terrain = TerrainType.Forest, ResourceAmount = 100, Plants = new PlantCoverage { Trees = 1 },
        });
        var available = fixture.Engine.Residents.First(p => p.Value.Id != fixture.ResidentId);
        available.Replace(available.Value with { Profession = Profession.Laborer });
        available.Replace(available.Value.WithAgent(available.Value.Agent with { JobChangedTick = 20 }));
        fixture.Engine.SimulationTick = SimulationTime.TicksPerMonth + SimulationTime.WakeTick;

        fixture.Engine.TickSociety();

        Assert.Equal(Profession.Lumberjack, available.Value.Profession);
        Assert.Equal(16 * 32 + 17, available.Value.Agent.WorkAreaIndex);
    }

    /// <summary>没有可耕地的沿水聚落仍能根据粮食缺口和鱼群产量安排渔民。</summary>
    [Fact]
    public void A_fishing_job_does_not_require_a_farming_plot()
    {
        var fixture = Prepare();
        fixture.Town.Replace(fixture.Town.Value.WithResources(fixture.Town.Value.Resources with { Food = 0 }));
        foreach (var tile in fixture.Engine.Tiles)
            tile.Replace(tile.Value with
            {
                ResourceAmount = 0,
                Plants = new PlantCoverage(),
                Wildlife = WildlifeKind.None,
                WildlifePopulation = 0,
                OtherWildlife = new WildlifePopulations(),
            });
        var water = fixture.Engine.Tiles[16 * 32 + 17];
        water.Replace(water.Value with
        {
            Terrain = TerrainType.River, Wildlife = WildlifeKind.Fish, WildlifePopulation = 20,
        });
        fixture.Engine.SimulationTick = SimulationTime.TicksPerMonth + SimulationTime.WakeTick;

        fixture.Engine.TickSociety();

        Assert.Single(fixture.Engine.State.Residents, p => p.Profession == Profession.Fisher);
        Assert.DoesNotContain(fixture.Engine.State.Residents, p => p.Profession == Profession.Farmer);
        Assert.All(fixture.Engine.State.Residents, p => Assert.Equal(0, p.Inventory.Food));
    }

    /// <summary>缺粮也不能凭空增加农田岗位，多余人力可以临时劳动而不是全部变成农民。</summary>
    [Fact]
    public void Farming_jobs_are_bounded_by_known_usable_plots()
    {
        var fixture = Prepare();
        fixture.Town.Replace(fixture.Town.Value.WithResources(fixture.Town.Value.Resources with { Food = 0 }));
        foreach (var tile in fixture.Engine.Tiles)
            tile.Replace(tile.Value with { ResourceAmount = 0, Plants = new PlantCoverage() });
        var index = 16 * 32 + 17;
        var plot = fixture.Engine.Tiles[index];
        plot.Replace(plot.Value with
        {
            ResourceAmount = 100,
            Fertility = 100,
            Plants = new PlantCoverage { Grass = 1 },
            Improvement = LandImprovement.None,
        });
        fixture.Engine.SimulationTick = SimulationTime.TicksPerMonth + SimulationTime.WakeTick;

        fixture.Engine.TickSociety();

        var farmer = Assert.Single(fixture.Engine.State.Residents, p => p.Profession == Profession.Farmer);
        Assert.Equal(index, farmer.Agent.WorkAreaIndex);
        Assert.Contains(fixture.Engine.State.Residents, p => p.Profession == Profession.Laborer);
        Assert.All(fixture.Engine.State.Residents, p => Assert.Equal(0, p.Inventory.Food));
        var restored = WorldEngine.ImportJson(fixture.Engine.ExportJson());
        Assert.Equal(index, restored.GetResident(farmer.Id)!.Agent.WorkAreaIndex);
    }

    /// <summary>没有研究工位时减少学者，但不能改派远方、在途、受伤或受玩家指挥的人。</summary>
    [Fact]
    public void Surplus_professions_are_adjusted_only_when_people_can_accept_local_work()
    {
        var fixture = Prepare();
        var people = fixture.Engine.Residents.Where(p => p.Value.Id != fixture.ResidentId).ToArray();
        people[0].Replace(people[0].Value with { X = 30, FromX = 30 });
        people[1].Replace(people[1].Value with
        {
            MoveStartedTick = SimulationTime.TicksPerMonth + SimulationTime.WakeTick - 1, MoveDurationTicks = 4,
        });
        people[2].Replace(people[2].Value.WithHealth(40));
        people[3].Replace(people[3].Value.WithAgent(people[3].Value.Agent with
        {
            Goal = new AgentGoal { PlayerDirected = true, ReviewTick = 2 * SimulationTime.TicksPerMonth },
        }));
        people[4].Replace(people[4].Value.WithAgent(people[4].Value.Agent with { JobChangedTick = 20 }));
        fixture.Engine.SimulationTick = SimulationTime.TicksPerMonth + SimulationTime.WakeTick;

        fixture.Engine.TickSociety();

        Assert.All(people.Take(5), p => Assert.Equal(Profession.Scholar, p.Value.Profession));
        Assert.DoesNotContain(people.Skip(5), p => p.Value.Profession == Profession.Scholar);
        Assert.Contains(people.Skip(5), p => p.Value.Profession == Profession.Laborer);
    }

    /// <summary>按木材缺口挑选适合的工人，固定工位不超过容量，也不会被更近的新设施反复吸走。</summary>
    [Fact]
    public void Local_work_uses_suitable_people_and_keeps_a_bounded_workplace()
    {
        var fixture = Prepare();
        fixture.Town.Replace(fixture.Town.Value.WithResources(fixture.Town.Value.Resources with { Wood = 48 }));
        var people = fixture.Engine.Residents.Where(p => p.Value.Id != fixture.ResidentId).ToArray();
        foreach (var person in people)
            person.Replace(person.Value.WithAgent(person.Value.Agent with { Personality = person.Value.Agent.Personality with { Diligence = .2 } }));
        var suitable = people[^1];
        suitable.Replace(suitable.Value with { Race = RaceKind.Elf });
        suitable.Replace(suitable.Value.WithAgent(suitable.Value.Agent with { Personality = suitable.Value.Agent.Personality with { Diligence = .9 } }));
        var source = fixture.Engine.Tiles[16 * 32 + 18];
        source.Replace(source.Value with
        {
            Terrain = TerrainType.Forest,
            ResourceAmount = 100,
            Plants = new PlantCoverage { Trees = 1 },
            ClaimedSettlementId = fixture.Town.Value.Id,
            NationId = fixture.Town.Value.NationId,
        });
        fixture.Engine.Tiles[16 * 32 + 19].Replace(source.Value);
        var workplace = fixture.Engine.GrantFacility(fixture.Town.Value.Id, BuildingKind.LumberCamp, 18, 16);
        fixture.Engine.Buildings.Single(b => b.Value.Id == workplace).Replace(fixture.Engine.Buildings.Single(b => b.Value.Id == workplace).Value with { WorkSlots = 1 });
        var before = fixture.Engine.State;
        fixture.Engine.SimulationTick = SimulationTime.TicksPerMonth + SimulationTime.WakeTick;

        fixture.Engine.TickSociety();

        Assert.Equal(Profession.Lumberjack, suitable.Value.Profession);
        Assert.Equal(workplace, suitable.Value.Agent.WorkplaceId);
        Assert.Single(fixture.Engine.State.Residents, p => p.Profession == Profession.Lumberjack);
        Assert.Single(fixture.Engine.State.Residents, p => p.Agent.WorkplaceId == workplace);
        Assert.Equal(0, before.Residents.Single(p => p.Id == suitable.Value.Id).Agent.WorkplaceId);
        var restored = WorldEngine.ImportJson(fixture.Engine.ExportJson());
        Assert.Equal(workplace, restored.State.Residents.Single(p => p.Id == suitable.Value.Id).Agent.WorkplaceId);
        var nearer = fixture.Engine.Tiles[16 * 32 + 17];
        nearer.Replace(nearer.Value with
        {
            Terrain = TerrainType.Forest,
            ResourceAmount = 100,
            Plants = new PlantCoverage { Trees = 1 },
            ClaimedSettlementId = fixture.Town.Value.Id,
            NationId = fixture.Town.Value.NationId,
        });
        fixture.Engine.GrantFacility(fixture.Town.Value.Id, BuildingKind.LumberCamp, 17, 16);
        fixture.Engine.SimulationTick =
            SimulationTime.TicksPerYear + SimulationTime.TicksPerMonth + SimulationTime.WakeTick;

        fixture.Engine.TickSociety();

        Assert.Equal(workplace, suitable.Value.Agent.WorkplaceId);
    }

    /// <summary>缺石材且只有低产露头时仍安排真实开采岗位，不能把少量可用石材视为不存在。</summary>
    [Fact]
    public void A_low_yield_stone_plot_can_supply_a_mining_job()
    {
        var fixture = Prepare();
        foreach (var tile in fixture.Engine.Tiles)
            tile.Replace(tile.Value with { ResourceAmount = 0, Deposit = null, DepositAmount = 0 });
        fixture.Town.Replace(fixture.Town.Value.WithResources(fixture.Town.Value.Resources with { Stone = 0 }));
        var index = 16 * 32 + 17;
        var source = fixture.Engine.Tiles[index];
        source.Replace(source.Value with { Terrain = TerrainType.Grass, ResourceAmount = 100 });
        fixture.Engine.SimulationTick =
            SimulationTime.TicksPerYear + SimulationTime.TicksPerMonth + SimulationTime.WakeTick;

        fixture.Engine.TickSociety();

        Assert.Contains(fixture.Engine.State.Residents,
            person => person.Profession == Profession.Miner && person.Agent.WorkAreaIndex == index);
        Assert.Equal(100, source.Value.ResourceAmount);
    }

    /// <summary>人口超过住房时仍先建立唯一研究岗位，避免连续补住宅耗尽学舍材料。</summary>
    [Fact]
    public void An_overcrowded_town_builds_its_first_academy_before_more_housing()
    {
        var fixture = Prepare();
        fixture.Engine.ConfigureWorld(fixture.Engine.State.Rules with { Research = true, Construction = true }, false,
            false);
        fixture.Town.Replace(fixture.Town.Value with { Population = 100 });
        fixture.Town.Replace(fixture.Town.Value.WithResources(new ResourceStock { Food = 400, Water = 10_000, Wood = 50, Stone = 20 }));
        fixture.Engine.SimulationTick =
            2 * SimulationTime.TicksPerMonth - fixture.Town.Value.Id % (2 * SimulationTime.TicksPerMonth);

        fixture.Engine.TickSociety();

        Assert.Contains(fixture.Engine.State.Society.Buildings,
            building => building.Kind == BuildingKind.Academy && !building.IsCompleted);
        Assert.DoesNotContain(fixture.Engine.State.Society.Buildings,
            building => building.Kind == BuildingKind.Housing);
    }

    /// <summary>住宅不消耗矿石，后续奥术研究缺矿不能阻止已可支付的住宅建设。</summary>
    [Fact]
    public void Housing_is_not_blocked_by_unshared_research_materials()
    {
        var fixture = Prepare();
        fixture.Engine.SetDevelopmentFocus(fixture.Town.Value.NationId, DevelopmentFocus.MagicPractice);
        GrantResearch(fixture, Advancement.Agriculture);
        GrantResearch(fixture, Advancement.Forestry);
        GrantResearch(fixture, Advancement.Logistics);
        GrantResearch(fixture, Advancement.Medicine);
        fixture.Engine.GrantFacility(fixture.Town.Value.Id, BuildingKind.Academy, 17, 16);
        fixture.Engine.ConfigureWorld(fixture.Engine.State.Rules with { Research = true, Construction = true }, false,
            true);
        fixture.Town.Replace(fixture.Town.Value with { Population = 100 });
        fixture.Town.Replace(fixture.Town.Value.WithResources(new ResourceStock
        {
            Food = 400,
            Water = 10_000,
            Wood = 100,
            Stone = 100,
            Ore = 0,
        }));
        fixture.Engine.SimulationTick =
            2 * SimulationTime.TicksPerMonth - fixture.Town.Value.Id % (2 * SimulationTime.TicksPerMonth);

        fixture.Engine.TickSociety();

        Assert.Contains(fixture.Engine.State.Society.Buildings, building => building.Kind == BuildingKind.Housing);
        Assert.Equal(0, fixture.Town.Value.Resources.Ore);
    }

    /// <summary>已有住宅满员后继续扩建，计入在建床位以免重复立项。</summary>
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(1, true)]
    public void Housing_expands_after_existing_beds_fill(int level, bool pending)
    {
        var fixture = Prepare();
        var id = fixture.Engine.GrantFacility(fixture.Town.Value.Id, BuildingKind.Housing, 17, 16);
        var house = fixture.Engine.Buildings.Single(building => building.Value.Id == id);
        house.Replace(house.Value with { Level = level });
        if (pending)
        {
            var ground = fixture.Engine.Tiles[17 * 32 + 16];
            ground.Replace(ground.Value with { ClaimedSettlementId = fixture.Town.Value.Id, NationId = fixture.Town.Value.NationId });
            var plannedId = fixture.Engine.GrantFacility(fixture.Town.Value.Id, BuildingKind.Housing, 16, 17);
            var planned = fixture.Engine.Buildings.Single(building => building.Value.Id == plannedId);
            planned.Replace(planned.Value with { ConstructionProgress = 0 });
        }
        fixture.Engine.ConfigureWorld(fixture.Engine.State.Rules with { Construction = true }, false, false);
        fixture.Town.Replace(fixture.Town.Value with { Population = level * WorldEngine.HousingCapacityPerLevel + 1 });
        fixture.Engine.SimulationTick =
            2 * SimulationTime.TicksPerMonth - fixture.Town.Value.Id % (2 * SimulationTime.TicksPerMonth);

        fixture.Engine.TickSociety();

        Assert.Equal(2, fixture.Engine.State.Society.Buildings.Count(building => building.Kind == BuildingKind.Housing));
    }

    /// <summary>公开职业分配与自动岗位调整均使旧日工作计划失效。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Profession_assignment_invalidates_previous_daily_work(bool automatic)
    {
        var fixture = Prepare();
        var worker = fixture.Engine.Residents.First(person => person.Value.Id != fixture.ResidentId);
        worker.Replace(worker.Value with
        {
            Profession = Profession.Laborer,
            Agent = worker.Value.Agent with
            {
                DailyPlan = new ResidentDailyPlan { WorkGoal = new AgentGoal { Kind = AgentGoalKind.Gather } },
                Personality = worker.Value.Agent.Personality with { Diligence = 1 },
            },
        });
        if (automatic)
        {
            foreach (var patient in fixture.Engine.Residents.Where(person => person.Value.Id != fixture.ResidentId && person.Value.Id != worker.Value.Id))
                patient.Replace(patient.Value with { Age = 10, Profession = Profession.Child, SicknessTicks = 20 });
            GrantResearch(fixture, Advancement.Sanitation);
            fixture.Engine.GrantFacility(fixture.Town.Value.Id, BuildingKind.Hospital, 17, 16);
            fixture.Engine.SimulationTick = SimulationTime.TicksPerYear + SimulationTime.TicksPerMonth + SimulationTime.WakeTick;
            fixture.Engine.TickSociety();
        }
        else
        {
            GrantResearch(fixture, ResearchRules.Unlocking(Profession.Physician)!);
            fixture.Engine.AssignResearchProfession(worker.Value.Id, Profession.Physician);
        }

        Assert.NotEqual(Profession.Laborer, worker.Value.Profession);
        Assert.Null(worker.Value.Agent.DailyPlan);
    }

    /// <summary>住房不足压低出生率，粮食充足且有健康成年人时仍能延续下一代。</summary>
    [Fact]
    public void Overcrowding_slows_births_without_stopping_generation_replacement()
    {
        var fixture = Prepare();
        fixture.Engine.ConfigureWorld(fixture.Engine.State.Rules with { Births = true }, false, false);
        fixture.Engine.Buildings.RemoveAll(building => building.Value.Kind == BuildingKind.Housing);
        fixture.Engine.SimulationTick = SimulationTime.TicksPerYear / 10 - 1;
        var before = fixture.Engine.State.Population;

        fixture.Engine.Step();

        Assert.Equal(before + 1, fixture.Engine.State.Population);
        Assert.Single(fixture.Engine.State.Residents,
            person => person.Age == 0 && person.Profession == Profession.Child);
    }

    /// <summary>已返家的家庭有实物口粮时，空公共仓库不能阻止下一代；新生儿口粮由家庭支付。</summary>
    [Fact]
    public void Fed_households_can_support_a_child_without_a_full_public_warehouse()
    {
        var fixture = Prepare();
        fixture.Engine.ConfigureWorld(fixture.Engine.State.Rules with { Births = true, Hunger = false, Aging = false },
            false, false);
        fixture.Engine.SimulationTick = SimulationTime.TicksPerYear / 10 - 1;
        fixture.Town.Replace(fixture.Town.Value.WithResources(new ResourceStock()));
        foreach (var person in fixture.Engine.Residents)
        {
            person.Replace(person.Value.WithInventory(new ResourceStock { Food = .8, Water = 1 }));
            person.Replace(person.Value with { FrozenUntilTick = fixture.Engine.SimulationTick + 2 });
        }

        var before = fixture.Engine.State;

        fixture.Engine.Step();

        var after = fixture.Engine.State;
        Assert.Equal(before.Population + 1, after.Population);
        Assert.Equal(.6, Assert.Single(after.Residents, person => person.Age == 0).Inventory.Food, 8);
        Assert.Equal(before.Residents.Sum(person => person.Inventory.Food) + before.Settlements[0].Resources.Food,
            after.Residents.Sum(person => person.Inventory.Food) + after.Settlements[0].Resources.Food, 8);
    }

    /// <summary>已住进远离中心的实际住宅的家庭仍能抚育新生儿。</summary>
    [Fact]
    public void A_family_inside_real_housing_can_support_a_birth()
    {
        var fixture = Prepare();
        fixture.Engine.ConfigureWorld(fixture.Engine.State.Rules with { Births = true, Hunger = false, Thirst = false, Aging = false }, false, false);
        for (var x = 17; x <= 18; x++)
        {
            var ground = fixture.Engine.Tiles[16 * 32 + x];
            ground.Replace(ground.Value with { ClaimedSettlementId = fixture.Town.Value.Id, NationId = fixture.Town.Value.NationId });
        }
        var homeId = fixture.Engine.GrantFacility(fixture.Town.Value.Id, BuildingKind.Housing, 18, 16);
        fixture.Engine.SimulationTick = SimulationTime.TicksPerYear / 10 - 1;
        fixture.Town.Replace(fixture.Town.Value.WithResources(new ResourceStock()));
        foreach (var person in fixture.Engine.Residents)
            person.Replace(person.Value.WithPosition(18, 16) with
            {
                HomeBuildingId = homeId, IsInsideHome = true,
                Inventory = new ResourceStock { Food = .8, Water = 1 },
                FrozenUntilTick = fixture.Engine.SimulationTick + 2,
            });
        var before = fixture.Engine.State;

        fixture.Engine.Step();

        var after = fixture.Engine.State;
        Assert.Equal(before.Population + 1, after.Population);
        var newborn = Assert.Single(after.Residents, person => person.Age == 0);
        Assert.True(newborn.IsInsideHome);
        Assert.Equal(homeId, newborn.HomeBuildingId);
        Assert.Equal(before.Residents.Sum(person => person.Inventory.Food), after.Residents.Sum(person => person.Inventory.Food), 8);
    }

    /// <summary>尚未返家的家庭携带粮食不能隔空支付家园新生儿的补给。</summary>
    [Fact]
    public void Food_away_from_home_cannot_support_a_local_birth()
    {
        var fixture = Prepare();
        fixture.Engine.ConfigureWorld(fixture.Engine.State.Rules with { Births = true }, false, false);
        fixture.Engine.SimulationTick = SimulationTime.TicksPerYear / 10 - 1;
        fixture.Town.Replace(fixture.Town.Value.WithResources(new ResourceStock()));
        foreach (var person in fixture.Engine.Residents)
        {
            person.Replace(person.Value with { FromX = 24, X = 24 });
            person.Replace(person.Value.WithInventory(new ResourceStock { Food = .8, Water = 1 }));
            person.Replace(person.Value with { FrozenUntilTick = fixture.Engine.SimulationTick + 2 });
        }

        var before = fixture.Engine.State.Population;

        fixture.Engine.Step();

        Assert.Equal(before, fixture.Engine.State.Population);
    }

    private static void GrantResearch(WorldFixture fixture, Advancement research)
    {
        foreach (var prerequisite in research.Prerequisites)
            GrantResearch(fixture, prerequisite);
        fixture.Engine.GrantReceivedResearch(fixture.Town.Value.Id, research);
    }

    private static WorldFixture Prepare()
    {
        var fixture = new WorldFixture();
        fixture.Engine.SpawnResidents(16, 16, RaceKind.Human);
        fixture.Engine.Buildings.RemoveAll(b => b.Value.Kind != BuildingKind.TownCenter);
        fixture.Town.Replace(fixture.Town.Value.WithResources(new ResourceStock
        {
            Food = 10_000,
            Water = 10_000,
            Wood = 60,
            Stone = 80,
            Ore = 80,
        }));
        foreach (var person in fixture.Engine.Residents)
            person.Replace(person.Value with
            {
                Age = 25,
                Health = 100,
                SicknessTicks = 0,
                Profession = Profession.Scholar,
                X = 16,
                Y = 16,
                FromX = 16,
                FromY = 16,
                Inventory = new ResourceStock(),
                Agent = new AgentState { Initialized = true },
                MoveStartedTick = 0,
                MoveDurationTicks = 1,
            });
        fixture.Resident.Replace(fixture.Resident.Value with { Profession = Profession.Representative });
        fixture.Town.Replace(fixture.Town.Value with { RepresentativeId = fixture.ResidentId });
        fixture.Engine.ConfigureWorld(
            fixture.Engine.State.Rules with { Research = false, Construction = false, Expansion = false }, false,
            false);
        return fixture;
    }
}
