using SeWZC.WorldBox.Core;

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
        var people = fixture.Engine.Current.Residents.Where(person => person.Id != fixture.ResidentId).ToArray();
        var patient = people[0];
        patient.Replace(patient.Value with { Age = 10, Profession = Profession.Child, Health = 70, SicknessTicks = 20 });
        var physician = people[1];
        physician.Profession = initialProfession;
        GrantResearch(fixture, Advancement.Sanitation);
        var ground = fixture.Engine.Current.Tiles[16 * 32 + 17];
        ground.Replace(ground.Value with { ClaimedSettlementId = fixture.Town.Id, NationId = fixture.Town.NationId });
        var hospitalId = fixture.Engine.GrantFacility(fixture.Town.Id, BuildingKind.Hospital, 17, 16);
        fixture.Engine.Current.Tick = 150;

        fixture.Engine.TickSociety();

        Assert.Equal(Profession.Physician, physician.Profession);
        Assert.Equal(hospitalId, physician.Agent.WorkplaceId);
        Assert.Single(fixture.Engine.State.Residents, person => person.Profession == Profession.Physician);
        Assert.Equal(Profession.Child, patient.Profession);
    }

    /// <summary>人类能够在天然山地邻格开采矿石，石材充足不能抵消矿石缺口。</summary>
    [Fact]
    public void A_mountain_edge_provides_mining_jobs_when_only_ore_is_missing()
    {
        var fixture = Prepare();
        foreach (var tile in fixture.Engine.Current.Tiles)
            tile.Replace(tile.Value with { ResourceAmount = 0, Deposit = null, DepositAmount = 0 });
        fixture.Town.Resources = fixture.Town.Resources with { Stone = 100, Ore = 0 };
        var mountainIndex = 16 * 32 + 17;
        var mountain = fixture.Engine.Current.Tiles[mountainIndex];
        mountain.Replace(mountain.Value with { Terrain = TerrainType.Mountain, ResourceAmount = 100 });
        fixture.Engine.Current.Tick = 150;

        fixture.Engine.TickSociety();

        var miners = fixture.Engine.State.Residents.Where(person => person.Profession == Profession.Miner).ToArray();
        Assert.NotEmpty(miners);
        Assert.All(miners, person =>
        {
            Assert.NotEqual(mountainIndex, person.Agent.WorkAreaIndex);
            Assert.Equal(1, Math.Abs(person.Agent.WorkAreaIndex % 32 - 17)
                + Math.Abs(person.Agent.WorkAreaIndex / 32 - 16));
        });
        Assert.Equal(100, mountain.ResourceAmount);
    }

    /// <summary>矮人已有的天然山地工作点可继续使用，不因普通种族的通行限制被撤销。</summary>
    [Fact]
    public void A_dwarf_keeps_a_work_area_on_a_natural_mountain()
    {
        var fixture = Prepare();
        foreach (var tile in fixture.Engine.Current.Tiles)
            tile.Replace(tile.Value with { ResourceAmount = 0, Deposit = null, DepositAmount = 0 });
        fixture.Town.Resources = fixture.Town.Resources with { Stone = 0, Ore = 0 };
        var mountainIndex = 16 * 32 + 17;
        var mountain = fixture.Engine.Current.Tiles[mountainIndex];
        mountain.Replace(mountain.Value with { Terrain = TerrainType.Mountain, ResourceAmount = 100 });
        var miner = fixture.Engine.Current.Residents.First(person => person.Id != fixture.ResidentId);
        miner.Replace(miner.Value with { Race = RaceKind.Dwarf, Profession = Profession.Miner });
        miner.Agent.WorkAreaIndex = mountainIndex;
        fixture.Engine.Current.Tick = 150;

        fixture.Engine.TickSociety();

        Assert.Equal(Profession.Miner, miner.Profession);
        Assert.Equal(mountainIndex, miner.Agent.WorkAreaIndex);
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
        foreach (var tile in fixture.Engine.Current.Tiles)
            tile.Replace(tile.Value with { ResourceAmount = 0, Deposit = null, DepositAmount = 0 });
        var deposit = fixture.Engine.Current.Tiles[16 * 32 + 17];
        deposit.Replace(deposit.Value with { Deposit = kind, DepositAmount = 100, DepositDiscovered = discovered });
        GrantResearch(fixture, WorldEngine.DepositResearch(kind)!);
        fixture.Town.Resources = fixture.Town.Resources with { Stone = 10_000, Ore = 10_000, Coal = 16, Oil = 16, RareEarth = 16 };
        fixture.Town.Resources = fixture.Town.Resources.WithAmount(kind, 0);
        var miner = fixture.Engine.Current.Residents.First(person => person.Id != fixture.ResidentId);
        miner.Profession = Profession.Miner;
        miner.Agent.WorkAreaIndex = 16 * 32 + 17;
        fixture.Engine.Current.Tick = 150;

        fixture.Engine.TickSociety();

        Assert.Equal(discovered ? Profession.Miner : Profession.Laborer, miner.Profession);
        Assert.Equal(discovered ? 16 * 32 + 17 : -1, miner.Agent.WorkAreaIndex);
        Assert.Equal(discovered, deposit.DepositDiscovered);
        Assert.Equal(100, deposit.DepositAmount);
        Assert.Equal(0, miner.Inventory.Get(kind));
    }

    /// <summary>容量减少后，多余的本地专业工人可以等待换岗，但不能共同保留同一个容量不足的固定岗位。</summary>
    [Fact]
    public void Reduced_workplace_capacity_releases_excess_assignments_even_during_a_career_cooldown()
    {
        var fixture = Prepare();
        fixture.Town.Resources = fixture.Town.Resources with { Wood = 48 };
        var plot = fixture.Engine.Current.Tiles[16 * 32 + 17];
        plot.Replace(plot.Value with { Terrain = TerrainType.Forest, ResourceAmount = 100,
            Plants = new PlantCoverage { Trees = 1 },
            ClaimedSettlementId = fixture.Town.Id, NationId = fixture.Town.NationId });
        fixture.Engine.Current.Tiles[16 * 32 + 18].Replace(plot.Value);
        var id = fixture.Engine.GrantFacility(fixture.Town.Id, BuildingKind.LumberCamp, 17, 16);
        var building = fixture.Engine.Current.Society.Buildings.Single(b => b.Id == id);
        building.WorkSlots = 1;
        var people = fixture.Engine.Current.Residents.Where(p => p.Id != fixture.ResidentId).Take(2).ToArray();
        foreach (var person in people)
        {
            person.Profession = Profession.Lumberjack;
            person.Agent.WorkplaceId = id;
            person.Agent.JobChangedTick = 20;
        }
        fixture.Engine.Current.Tick = 30;

        fixture.Engine.TickSociety();

        Assert.All(people, p => Assert.Equal(Profession.Lumberjack, p.Profession));
        Assert.Single(people, p => p.Agent.WorkplaceId == id);
    }

    /// <summary>临时劳工可接受新出现的工位，已有专业分工仍受换岗间隔约束。</summary>
    [Fact]
    public void An_unassigned_laborer_can_take_a_new_job_without_waiting_a_year()
    {
        var fixture = Prepare();
        fixture.Town.Resources = fixture.Town.Resources with { Wood = 48 };
        foreach (var tile in fixture.Engine.Current.Tiles)
            tile.Replace(tile.Value with { ResourceAmount = 0, Plants = new PlantCoverage() });
        var plot = fixture.Engine.Current.Tiles[16 * 32 + 17];
        plot.Replace(plot.Value with { Terrain = TerrainType.Forest, ResourceAmount = 100,
            Plants = new PlantCoverage { Trees = 1 } });
        var available = fixture.Engine.Current.Residents.First(p => p.Id != fixture.ResidentId);
        available.Profession = Profession.Laborer;
        available.Agent.JobChangedTick = 20;
        fixture.Engine.Current.Tick = 30;

        fixture.Engine.TickSociety();

        Assert.Equal(Profession.Lumberjack, available.Profession);
        Assert.Equal(16 * 32 + 17, available.Agent.WorkAreaIndex);
    }

    /// <summary>没有可耕地的沿水聚落仍能根据粮食缺口和鱼群产量安排渔民。</summary>
    [Fact]
    public void A_fishing_job_does_not_require_a_farming_plot()
    {
        var fixture = Prepare();
        fixture.Town.Resources = fixture.Town.Resources with { Food = 0 };
        foreach (var tile in fixture.Engine.Current.Tiles)
            tile.Replace(tile.Value with { ResourceAmount = 0, Plants = new PlantCoverage(),
                Wildlife = WildlifeKind.None, WildlifePopulation = 0, OtherWildlife = new WildlifePopulations() });
        var water = fixture.Engine.Current.Tiles[16 * 32 + 17];
        water.Replace(water.Value with { Terrain = TerrainType.River, Wildlife = WildlifeKind.Fish,
            WildlifePopulation = 20 });
        fixture.Engine.Current.Tick = 30;

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
        fixture.Town.Resources = fixture.Town.Resources with { Food = 0 };
        foreach (var tile in fixture.Engine.Current.Tiles)
            tile.Replace(tile.Value with { ResourceAmount = 0, Plants = new PlantCoverage() });
        var index = 16 * 32 + 17;
        var plot = fixture.Engine.Current.Tiles[index];
        plot.Replace(plot.Value with { ResourceAmount = 100, Fertility = 100,
            Plants = new PlantCoverage { Grass = 1 }, Improvement = LandImprovement.None });
        fixture.Engine.Current.Tick = 30;

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
        var people = fixture.Engine.Current.Residents.Where(p => p.Id != fixture.ResidentId).ToArray();
        people[0].Replace(people[0].Value with { X = 30, FromX = 30 });
        people[1].Replace(people[1].Value with { MoveStartedTick = 29, MoveDurationTicks = 4 });
        people[2].Health = 40;
        people[3].Agent.Goal = new AgentGoal { PlayerDirected = true, ReviewTick = 100 };
        people[4].Agent.JobChangedTick = 20;
        fixture.Engine.Current.Tick = 30;

        fixture.Engine.TickSociety();

        Assert.All(people.Take(5), p => Assert.Equal(Profession.Scholar, p.Profession));
        Assert.DoesNotContain(people.Skip(5), p => p.Profession == Profession.Scholar);
        Assert.Contains(people.Skip(5), p => p.Profession == Profession.Laborer);
    }

    /// <summary>按木材缺口挑选适合的工人，固定工位不超过容量，也不会被更近的新设施反复吸走。</summary>
    [Fact]
    public void Local_work_uses_suitable_people_and_keeps_a_bounded_workplace()
    {
        var fixture = Prepare();
        fixture.Town.Resources = fixture.Town.Resources with { Wood = 48 };
        var people = fixture.Engine.Current.Residents.Where(p => p.Id != fixture.ResidentId).ToArray();
        foreach (var person in people)
            person.Agent.Personality = person.Agent.Personality with { Diligence = .2 };
        var suitable = people[^1];
        suitable.Race = RaceKind.Elf;
        suitable.Agent.Personality = suitable.Agent.Personality with { Diligence = .9 };
        var source = fixture.Engine.Current.Tiles[16 * 32 + 18];
        source.Replace(source.Value with { Terrain = TerrainType.Forest, ResourceAmount = 100,
            Plants = new PlantCoverage { Trees = 1 },
            ClaimedSettlementId = fixture.Town.Id, NationId = fixture.Town.NationId });
        fixture.Engine.Current.Tiles[16 * 32 + 19].Replace(source.Value);
        var workplace = fixture.Engine.GrantFacility(fixture.Town.Id, BuildingKind.LumberCamp, 18, 16);
        fixture.Engine.Current.Society.Buildings.Single(b => b.Id == workplace).WorkSlots = 1;
        var before = fixture.Engine.State;
        fixture.Engine.Current.Tick = 30;

        fixture.Engine.TickSociety();

        Assert.Equal(Profession.Lumberjack, suitable.Profession);
        Assert.Equal(workplace, suitable.Agent.WorkplaceId);
        Assert.Single(fixture.Engine.State.Residents, p => p.Profession == Profession.Lumberjack);
        Assert.Single(fixture.Engine.State.Residents, p => p.Agent.WorkplaceId == workplace);
        Assert.Equal(0, before.Residents.Single(p => p.Id == suitable.Id).Agent.WorkplaceId);
        var restored = WorldEngine.ImportJson(fixture.Engine.ExportJson());
        Assert.Equal(workplace, restored.State.Residents.Single(p => p.Id == suitable.Id).Agent.WorkplaceId);
        var nearer = fixture.Engine.Current.Tiles[16 * 32 + 17];
        nearer.Replace(nearer.Value with { Terrain = TerrainType.Forest, ResourceAmount = 100,
            Plants = new PlantCoverage { Trees = 1 },
            ClaimedSettlementId = fixture.Town.Id, NationId = fixture.Town.NationId });
        fixture.Engine.GrantFacility(fixture.Town.Id, BuildingKind.LumberCamp, 17, 16);
        fixture.Engine.Current.Tick = 150;

        fixture.Engine.TickSociety();

        Assert.Equal(workplace, suitable.Agent.WorkplaceId);
    }

    private static void GrantResearch(WorldFixture fixture, Advancement research)
    {
        foreach (var prerequisite in research.Prerequisites)
            GrantResearch(fixture, prerequisite);
        fixture.Engine.GrantReceivedResearch(fixture.Town.Id, research);
    }

    private static WorldFixture Prepare()
    {
        var fixture = new WorldFixture();
        fixture.Engine.SpawnResidents(16, 16, RaceKind.Human, 12);
        fixture.Engine.Current.Society.Buildings.RemoveAll(b => b.Kind != BuildingKind.TownCenter);
        fixture.Town.Resources = new ResourceStock { Food = 10_000, Water = 10_000, Wood = 60, Stone = 80, Ore = 80 };
        foreach (var person in fixture.Engine.Current.Residents)
            person.Replace(person.Value with { Age = 25, Health = 100, SicknessTicks = 0,
                Profession = Profession.Scholar, X = 16, Y = 16, FromX = 16, FromY = 16,
                Inventory = new ResourceStock(),
                Agent = new AgentState { Initialized = true }, MoveStartedTick = 0, MoveDurationTicks = 1 });
        fixture.Resident.Profession = Profession.Representative;
        fixture.Town.RepresentativeId = fixture.ResidentId;
        fixture.Engine.ConfigureWorld(fixture.Engine.State.Rules with
        {
            Research = false, Construction = false, Expansion = false,
        }, false, false);
        return fixture;
    }
}
