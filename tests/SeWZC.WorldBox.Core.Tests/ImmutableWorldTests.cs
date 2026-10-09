using System.Text.Json;

namespace SeWZC.WorldBox.Core.Tests;

/// <summary>世界快照、日内转换和异步保存的隔离边界。</summary>
public sealed class ImmutableWorldTests
{
    /// <summary>日内仓库连续记账即时供后续居民读取，快照冻结合并库存并保留此前世界。</summary>
    [Fact]
    public void Warehouse_batches_preserve_snapshots_and_other_settlement_updates()
    {
        var fixture = new WorldFixture();
        var before = fixture.Engine.State;
        var town = fixture.Town;
        using var updates = fixture.Engine.Current.Settlements.BeginUpdates();
        town.BeginResourceUpdates();
        town.Resources = town.Resources with { Food = 7, Water = 3 };
        var middle = fixture.Engine.State;
        town.Resources = town.Resources with { Food = 5 };
        town.Housing = 100;
        Assert.Equal(5, town.Resources.Food);
        var current = fixture.Engine.Current.Settlements.Snapshot.Single();
        town.EndResourceUpdates();

        Assert.Equal(7, middle.Settlements.Single().Resources.Food);
        Assert.Equal(5, current.Resources.Food);
        Assert.Equal(100, current.Housing);
        Assert.Equal(3, current.Resources.Water);
        Assert.Equal(5, fixture.Engine.State.Settlements.Single().Resources.Food);
        Assert.Equal(Serialize(before), Serialize(WorldEngine.ImportJson(Serialize(before)).State));
    }

    /// <summary>身份状态拆分后，日常更新、迁居与训练仍保持旧快照、值相等和原有平面存档。</summary>
    [Fact]
    public void Resident_identity_remains_immutable_and_roundtrips_as_flat_fields()
    {
        var before = new Resident
        {
            Id = 12,
            Name = "林",
            Race = RaceKind.Elf,
            NationId = 3,
            SettlementId = 4,
            CultureId = 5,
            Trait = "好奇",
            Profession = Profession.Mage,
            MagicTalent = 40,
            MagicTraining = 20,
        };
        var after = before with { Age = 30, SettlementId = 8, MagicTraining = 24 };
        Assert.Equal(4, before.SettlementId);
        Assert.Equal(20, before.MagicTraining);
        var reverted = after with { Age = 0, SettlementId = 4, MagicTraining = 20 };
        Assert.Equal(before, reverted);
        Assert.Equal(before.GetHashCode(), reverted.GetHashCode());
        var json = JsonSerializer.Serialize(before, WorldJsonContext.Default.Resident);
        Assert.Equal(before, JsonSerializer.Deserialize(json, WorldJsonContext.Default.Resident));
        using var saved = JsonDocument.Parse(json);
        Assert.Equal(12, saved.RootElement.GetProperty("Id").GetInt32());
        Assert.Equal(20, saved.RootElement.GetProperty("MagicTraining").GetDouble());
        Assert.False(saved.RootElement.TryGetProperty("Identity", out _));
        Assert.Equal(25, new Resident().MagicTalent);
        var signedZero = before with { MagicTalent = -0d, MagicTraining = -0d };
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(signedZero.MagicTalent));
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(signedZero.MagicTraining));
    }

    /// <summary>日常认知转换共享性格，性格修改保留旧快照，存档仍使用原有字段。</summary>
    [Fact]
    public void Agent_personality_remains_immutable_and_roundtrips_as_flat_fields()
    {
        var before = new AgentState { Personality = new() { Diligence = .8 } };
        var daily = before with { Fatigue = 12, SocialNeed = 4 };
        var changed = daily with { Personality = daily.Personality with { Diligence = .2 } };

        Assert.Same(before.Personality, daily.Personality);
        Assert.Equal(.8, before.Personality.Diligence);
        Assert.Equal(.2, changed.Personality.Diligence);
        var reverted = changed with { Fatigue = 0, SocialNeed = 0, Personality = before.Personality };
        Assert.Equal(before, reverted);
        Assert.Equal(before.GetHashCode(), reverted.GetHashCode());

        var json = JsonSerializer.Serialize(changed, WorldJsonContext.Default.AgentState);
        Assert.Equal(changed, JsonSerializer.Deserialize(json, WorldJsonContext.Default.AgentState));
        using var saved = JsonDocument.Parse(json);
        Assert.Equal(.2, saved.RootElement.GetProperty("Personality").GetProperty("Diligence").GetDouble());
        Assert.False(saved.RootElement.TryGetProperty("Identity", out _));
    }

    private static string Serialize(WorldState state)
    {
        return JsonSerializer.Serialize(state, WorldJsonContext.Default.WorldState);
    }

    /// <summary>日内死亡、生成及位置改写合并冻结；中途读取的人口和快照仍准确。</summary>
    [Fact]
    public void Membership_changes_preserve_pending_body_and_agent_updates()
    {
        var fixture = new WorldFixture();
        fixture.Engine.SpawnResidents(16, 16, RaceKind.Human, 2);
        var list = fixture.Engine.Current.Residents;
        var before = fixture.Engine.State;
        var survivor = list[1];
        using (list.BeginUpdates())
        {
            survivor.Health = 60;
            list.RemoveAt(0);
            survivor.Agent.Fatigue = 20;
            Assert.Equal(2, fixture.Engine.Current.Population);
            var middle = fixture.Engine.State;
            Assert.Equal(60, middle.Residents[0].Health);
            Assert.Equal(20, middle.Residents[0].Agent.Fatigue);
            list.Add(new Resident { Id = 123, Health = 90 });
            survivor.Hunger = 40;
            list.RemoveAt(1);
            Assert.Equal(2, fixture.Engine.Current.Population);
            Assert.Equal(0, middle.Residents[0].Hunger);
        }

        var after = fixture.Engine.State;
        Assert.Equal([survivor.Id, 123], after.Residents.Select(p => p.Id));
        Assert.Equal(40, after.Residents[0].Hunger);
        Assert.Equal(90, after.Residents[1].Health);
        Assert.Equal(3, before.Population);
        Assert.NotEqual(60, before.Residents[1].Health);
    }

    /// <summary>连续动作立即读取草稿，读取实体及世界时冻结，后续更改不污染已返回快照。</summary>
    [Fact]
    public void Resident_drafts_freeze_agent_and_body_together_at_snapshot_boundaries()
    {
        var fixture = new WorldFixture();
        var before = fixture.Engine.State;
        var original = fixture.Resident.Value;
        using (fixture.Engine.Current.Residents.BeginUpdates())
        {
            fixture.Resident.Health = 70;
            fixture.Resident.X = 15;
            fixture.Resident.Inventory = new ResourceStock { Food = 3 };
            fixture.Resident.Agent.Fatigue = 40;
            Assert.Equal(70, fixture.Resident.Health);
            Assert.Equal(15, fixture.Resident.X);
            var middle = fixture.Engine.State;
            fixture.Resident.Health = 50;
            fixture.Resident.Agent.Goal = new AgentGoal { Kind = AgentGoalKind.Rest, TargetX = 15, TargetY = 16 };
            fixture.Resident.Profession = Profession.Laborer;
            Assert.Equal(70, middle.Residents[0].Health);
            Assert.Equal(40, middle.Residents[0].Agent.Fatigue);
            Assert.NotEqual(AgentGoalKind.Rest, middle.Residents[0].Agent.Goal.Kind);
            Assert.Equal(50, fixture.Resident.Value.Health);
            Assert.Equal(AgentGoalKind.Rest, fixture.Resident.Value.Agent.Goal.Kind);
            fixture.Resident.Health = 30;
            fixture.Resident.Replace(original);
            Assert.Equal(original, fixture.Resident.Value);
        }

        Assert.Equal(original, fixture.Engine.State.Residents[0]);
        Assert.Equal(original, before.Residents[0]);
    }

    /// <summary>共享种群存储不改变旧地格、值比较或平面存档；其他地格更新保留动物数量。</summary>
    [Fact]
    public void Tile_wildlife_storage_preserves_snapshots_and_serialization()
    {
        var before = new Tile
        {
            Wildlife = WildlifeKind.Deer,
            WildlifePopulation = 4,
            OtherWildlife = new WildlifePopulations { Rabbit = 2, Wolf = 1 },
        };
        var watered = before with { WaterDrawTick = 12, WaterDrawn = .025 };
        var changed = watered.WithAnimalPopulation(WildlifeKind.Rabbit, 3);
        var reverted = changed.WithAnimalPopulation(WildlifeKind.Rabbit, 2);

        Assert.Equal(2, before.AnimalPopulation(WildlifeKind.Rabbit));
        Assert.Equal(3, changed.AnimalPopulation(WildlifeKind.Rabbit));
        Assert.Equal(watered, reverted);
        Assert.Equal(watered.GetHashCode(), reverted.GetHashCode());
        var json = JsonSerializer.Serialize(changed, WorldJsonContext.Default.Tile);
        Assert.Equal(changed, JsonSerializer.Deserialize(json, WorldJsonContext.Default.Tile));
        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.TryGetProperty("OtherWildlife", out _));
        Assert.False(document.RootElement.TryGetProperty("WildlifeStorage", out _));
        var negativeZero = before with { OtherWildlife = new WildlifePopulations { Wolf = -0d } };
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(negativeZero.AnimalPopulation(WildlifeKind.Wolf)));
    }

    /// <summary>稀疏种群冻结后不持有可变生态缓冲，覆盖最高物种位和负零的值比较。</summary>
    [Fact]
    public void Sparse_animal_populations_own_their_buffer_and_preserve_all_species()
    {
        var populations = new double[32];
        populations[(int)WildlifeKind.Deer] = 4;
        populations[(int)WildlifeKind.SnowLeopard] = 2;
        populations[(int)WildlifeKind.Wolf] = -0d;
        var tile = new Tile().WithAnimalPopulations(WildlifeKind.Deer, populations);
        Array.Clear(populations);

        Assert.Equal(4, tile.AnimalPopulation(WildlifeKind.Deer));
        Assert.Equal(2, tile.AnimalPopulation(WildlifeKind.SnowLeopard));
        Assert.Equal((1 << (int)WildlifeKind.Deer) | (1 << (int)WildlifeKind.SnowLeopard), tile.WildlifeMask);
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(tile.OtherWildlife.Wolf));
        var positiveZero = tile.WithAnimalPopulation(WildlifeKind.Wolf, 0d);
        Assert.Equal(tile, positiveZero);
        Assert.Equal(tile.GetHashCode(), positiveZero.GetHashCode());
        var cleared = tile.WithAnimalPopulation(WildlifeKind.SnowLeopard, 0);
        Assert.Equal(2, tile.AnimalPopulation(WildlifeKind.SnowLeopard));
        Assert.Equal(0, cleared.AnimalPopulation(WildlifeKind.SnowLeopard));
        var promoted =
            new Tile { OtherWildlife = tile.OtherWildlife }.WithAnimalPopulation(WildlifeKind.SnowLeopard, 3);
        Assert.Equal(WildlifeKind.SnowLeopard, promoted.Wildlife);
        Assert.Equal(0, promoted.OtherWildlife.SnowLeopard);
    }

    /// <summary>日内 ID 和随机序列立即供后续行为读取，冻结时合并元数据并保留旧快照。</summary>
    [Fact]
    public void Scalar_updates_freeze_together_with_entity_changes()
    {
        var fixture = new WorldFixture();
        var initial = fixture.Engine.State;
        var current = fixture.Engine.Current;
        WorldState middle;
        using (current.BeginScalarUpdates())
        {
            current.Tick = 120;
            current.NextId += 2;
            current.RandomState = 123;
            fixture.Resident.Health = 80;
            Assert.Equal(2, current.Year);
            Assert.Equal(1, current.Day);
            middle = fixture.Engine.State;
            using (current.BeginScalarUpdates())
            {
                current.NextId++;
            }

            current.Tick++;
            current.RandomState = 456;
        }

        Assert.Equal(initial.NextId + 2, middle.NextId);
        Assert.Equal(123u, middle.RandomState);
        Assert.Equal(80, middle.Residents[0].Health);
        Assert.Equal(initial.NextId + 3, fixture.Engine.State.NextId);
        Assert.Equal(121, fixture.Engine.State.Tick);
        Assert.Equal(456u, fixture.Engine.State.RandomState);
        Assert.Equal(100, initial.Residents[0].Health);
        Assert.Equal(0, initial.Tick);
    }

    /// <summary>低频任务状态共享后仍按值比较，并使用原有平面存档字段。</summary>
    [Fact]
    public void Agent_identity_preserves_equality_and_flat_serialization()
    {
        var before = new AgentState
        {
            Initialized = true,
            DestinationSettlementId = 12,
            MissionOriginSettlementId = 3,
            MissionStartedTick = 20,
            MissionRetryTick = 24,
            ExplorationHeading = 6,
            LastConversationTick = 18,
            JobChangedTick = 9,
            MaterialPriority = ResourceKind.Stone,
            WorkplaceId = 2_000_001,
            WorkAreaIndex = 30,
        };
        var changed = before with { Fatigue = 20, DestinationSettlementId = 13 };
        var reverted = changed with { Fatigue = 0, DestinationSettlementId = 12 };

        Assert.Equal(before, reverted);
        Assert.Equal(before.GetHashCode(), reverted.GetHashCode());
        Assert.Equal(12, before.DestinationSettlementId);
        var json = JsonSerializer.Serialize(before, WorldJsonContext.Default.AgentState);
        Assert.Equal(before, JsonSerializer.Deserialize(json, WorldJsonContext.Default.AgentState));
        using var saved = JsonDocument.Parse(json);
        Assert.Equal(12, saved.RootElement.GetProperty("DestinationSettlementId").GetInt32());
        Assert.Equal(20, saved.RootElement.GetProperty("MissionStartedTick").GetInt64());
        Assert.False(saved.RootElement.TryGetProperty("Identity", out _));
        Assert.Equal(-120, new AgentState().JobChangedTick);
        Assert.Equal(2_000_001, saved.RootElement.GetProperty("WorkplaceId").GetInt32());
        Assert.Equal(-1, new AgentState().WorkAreaIndex);
    }

    /// <summary>低频身体状态仍按值比较并保存为原有平面字段，日常更新保留此前状态。</summary>
    [Fact]
    public void Resident_effects_preserve_value_equality_and_flat_serialization()
    {
        var before = new Resident
        {
            DiseaseImmuneUntilTick = 120,
            DeathCause = DeathCause.OldAge,
            DeathTick = 30,
            ArmyId = 12,
            Armor = 8,
            PersonalWard = 6,
            FrozenUntilTick = 24,
            LastRangedAttackTick = 20,
        };
        var changed = before with { Age = 60, PersonalWard = 3 };
        var restored = changed with { Age = before.Age, PersonalWard = 6 };

        Assert.Equal(before, restored);
        Assert.Equal(before.GetHashCode(), restored.GetHashCode());
        Assert.Equal(6, before.PersonalWard);
        Assert.Equal(3, changed.PersonalWard);
        Assert.Equal(120, changed.DiseaseImmuneUntilTick);
        var json = JsonSerializer.Serialize(before, WorldJsonContext.Default.Resident);
        var roundTrip = JsonSerializer.Deserialize(json, WorldJsonContext.Default.Resident);
        Assert.Equal(before, roundTrip);
        using var saved = JsonDocument.Parse(json);
        Assert.Equal(8, saved.RootElement.GetProperty("Armor").GetDouble());
        Assert.Equal(20, saved.RootElement.GetProperty("LastRangedAttackTick").GetInt64());
        Assert.False(saved.RootElement.TryGetProperty("Effects", out _));
    }

    /// <summary>低频双精度状态仍保留负零及 NaN 的输入位模式。</summary>
    [Fact]
    public void Resident_effects_preserve_double_bit_patterns()
    {
        var nanBits = 0x7ff8000000000001L;
        var before = new Resident();
        var changed = before with { Armor = -0d, PersonalWard = BitConverter.Int64BitsToDouble(nanBits) };

        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(changed.Armor));
        Assert.Equal(nanBits, BitConverter.DoubleToInt64Bits(changed.PersonalWard));
        Assert.Equal(0, BitConverter.DoubleToInt64Bits(before.Armor));
        Assert.Equal(-100, new Resident().LastRangedAttackTick);
        Assert.Equal(before, changed with { Armor = 0, PersonalWard = 0 });
    }

    /// <summary>日内更新立即用于后续劳动和查询，读取世界时冻结完整快照，继续更新不污染旧版本。</summary>
    [Fact]
    public void Deferred_updates_are_visible_and_snapshots_remain_independent()
    {
        var fixture = new WorldFixture();
        var initial = fixture.Engine.State;
        var tile = fixture.Engine.Current.Tiles[0];
        WorldState middle;
        using (fixture.Engine.Current.Tiles.BeginUpdates())
        using (fixture.Engine.Current.Residents.BeginUpdates())
        using (fixture.Engine.Current.Settlements.BeginUpdates())
        {
            fixture.Resident.Inventory = new ResourceStock { Food = 3 };
            fixture.Town.Resources = new ResourceStock { Food = 9 };
            tile.Fertility = 42;
            Assert.Equal(3, fixture.Resident.Inventory.Food);
            middle = fixture.Engine.State;
            fixture.Resident.Inventory = new ResourceStock { Food = 5 };
            fixture.Town.Resources = new ResourceStock { Food = 7 };
            tile.Fertility = 60;
        }

        Assert.Equal(3, middle.Residents[0].Inventory.Food);
        Assert.Equal(42, middle.Tiles[0].Fertility);
        Assert.Equal(9, middle.Settlements[0].Resources.Food);
        Assert.Equal(5, fixture.Engine.State.Residents[0].Inventory.Food);
        Assert.Equal(60, fixture.Engine.State.Tiles[0].Fertility);
        Assert.Equal(7, fixture.Engine.State.Settlements[0].Resources.Food);
        Assert.Equal(initial.Residents[0].Inventory.Food,
            WorldEngine.FromSnapshot(initial).State.Residents[0].Inventory.Food);
        Assert.NotEqual(42, initial.Tiles[0].Fertility);
    }

    /// <summary>批量写入、身体转换、成员增删和嵌套范围共用最新状态，不能遗失待提交更新。</summary>
    [Fact]
    public void Deferred_updates_survive_transforms_and_membership_changes()
    {
        var fixture = new WorldFixture();
        var before = fixture.Engine.State;
        var removed = fixture.Resident;
        using (fixture.Engine.Current.Residents.BeginUpdates())
        {
            using (fixture.Engine.Current.Residents.BeginUpdates())
            {
                fixture.Resident.Health = 80;
            }

            fixture.Engine.Current.Residents.Transform(person => person with { Hunger = 20 });
            fixture.Resident.Agent.Fatigue = 25;
            fixture.Engine.Current.Residents.Remove(fixture.Resident);
            fixture.Engine.Current.Residents.Add(new Resident { Id = 900, Name = "新居民" });
            fixture.Engine.Current.Residents[0].Inventory = new ResourceStock { Food = 7 };
        }

        var after = Assert.Single(fixture.Engine.State.Residents);
        Assert.Equal(900, after.Id);
        Assert.Equal(7, after.Inventory.Food);
        Assert.Equal(80, removed.Health);
        Assert.Equal(20, removed.Hunger);
        Assert.Equal(25, removed.Agent.Fatigue);
        Assert.Equal(100, before.Residents[0].Health);
        Assert.Equal(0, before.Residents[0].Agent.Fatigue);
    }

    /// <summary>退出失败的模拟范围仍提交已经发生的劳动，范围外继续写入正常发布。</summary>
    [Fact]
    public void Deferred_update_scope_flushes_on_exception()
    {
        var fixture = new WorldFixture();

        void Fail()
        {
            using var updates = fixture.Engine.Current.Residents.BeginUpdates();
            fixture.Resident.Health = 80;
            throw new InvalidOperationException();
        }

        Assert.Throws<InvalidOperationException>(Fail);

        Assert.Equal(80, fixture.Engine.State.Residents[0].Health);
        fixture.Resident.Hunger = 20;
        Assert.Equal(20, fixture.Engine.State.Residents[0].Hunger);
    }

    /// <summary>整批需求转换后，已有嵌套定位引用仍保留新身体和认知状态。</summary>
    [Fact]
    public void Batch_transition_synchronizes_existing_nested_cursors()
    {
        var fixture = new WorldFixture();
        var agent = fixture.Resident.Agent;
        var before = fixture.Engine.State;

        fixture.Engine.Current.Residents.Transform(person => person with
        {
            Health = 80, Agent = person.Agent with { Fatigue = 25 },
        });
        agent.Memory.Add(new AgentFact { SubjectId = 99 });

        Assert.Equal(80, fixture.Engine.State.Residents[0].Health);
        Assert.Equal(25, fixture.Engine.State.Residents[0].Agent.Fatigue);
        Assert.Equal(99, fixture.Engine.State.Residents[0].Agent.Memory[^1].SubjectId);
        Assert.Equal(100, before.Residents[0].Health);
        Assert.Equal(0, before.Residents[0].Agent.Fatigue);
    }

    /// <summary>移除后重新绑定归档集合的定位引用不会继续改写原集合。</summary>
    [Fact]
    public void Removed_cursor_can_be_rebound_to_another_collection()
    {
        var fixture = new WorldFixture();
        var person = fixture.Resident;
        var before = fixture.Engine.State;

        fixture.Engine.Current.Residents.Remove(person);
        fixture.Engine.Current.ArchivedResidents.Add(person);
        person.Name = "归档的新姓名";
        person.Inventory = new ResourceStock { Food = 3 };

        Assert.Empty(fixture.Engine.State.Residents);
        Assert.Equal("归档的新姓名", fixture.Engine.State.ArchivedResidents[0].Name);
        Assert.Equal(3, fixture.Engine.State.ArchivedResidents[0].Inventory.Food);
        Assert.NotEqual("归档的新姓名", before.Residents[0].Name);
    }

    /// <summary>直接恢复快照可共享不可变集合，两台引擎独立转换并得到相同的续演结果。</summary>
    [Fact]
    public void Restoring_a_snapshot_shares_values_and_isolates_subsequent_transitions()
    {
        var fixture = new WorldFixture();
        var before = fixture.Engine.State;
        var saved = Serialize(before);

        var restored = WorldEngine.FromSnapshot(before);

        Assert.Same(before.Tiles, restored.State.Tiles);
        Assert.Same(before.Residents, restored.State.Residents);
        fixture.Engine.Step();
        Assert.Equal(saved, restored.ExportJson());
        restored.Step();
        Assert.Equal(fixture.Engine.ExportJson(), restored.ExportJson());
        Assert.Equal(saved, Serialize(before));
    }

    /// <summary>直接恢复仍校验快照，拒绝无效地图并保留源世界。</summary>
    [Fact]
    public void Restoring_an_invalid_snapshot_is_rejected()
    {
        var fixture = new WorldFixture();
        var before = fixture.Engine.State;

        Assert.Throws<ArgumentException>(() => WorldEngine.FromSnapshot(before with { Width = 0 }));

        Assert.Equal(before, fixture.Engine.State);
    }

    /// <summary>一天模拟后，旧快照中的地格、居民认知、资源和集合均保持原值。</summary>
    [Fact]
    public void Tick_preserves_the_entire_previous_snapshot()
    {
        var fixture = new WorldFixture();
        var before = fixture.Engine.State;
        var saved = Serialize(before);

        fixture.Engine.Step();

        Assert.Equal(before.Tick + 1, fixture.Engine.State.Tick);
        Assert.Equal(saved, Serialize(before));
        Assert.NotSame(before.Residents[0], fixture.Engine.State.Residents[0]);
    }

    /// <summary>居民与世界编辑不能修改保留的旧实体和集合。</summary>
    [Fact]
    public void Commands_preserve_retained_entities_and_share_unchanged_tiles()
    {
        var fixture = new WorldFixture();
        var before = fixture.Engine.State;
        var saved = Serialize(before);
        var actor = fixture.Engine.GetResident(fixture.ResidentId)!;

        fixture.Engine.EditResident(actor.Id, new ResidentEdit { Name = "新姓名", Health = 80 });
        fixture.Engine.SetNationResources(fixture.Town.NationId, 10);

        Assert.Equal(saved, Serialize(before));
        Assert.NotEqual("新姓名", actor.Name);
        Assert.Equal(100, actor.Health);
        Assert.Same(before.Tiles[0], fixture.Engine.State.Tiles[0]);
        Assert.Equal("新姓名", fixture.Engine.GetResident(actor.Id)!.Name);
    }

    /// <summary>日内每次转换立即产生独立状态，后续转换不会覆盖先前版本。</summary>
    [Fact]
    public void Intra_day_updates_publish_independent_versions()
    {
        var fixture = new WorldFixture();
        var initial = fixture.Engine.State;
        var initialKnowledge = initial.Residents[0].Agent.Memory;
        var fact = new AgentFact { Id = fixture.Engine.Current.NextId++, Text = "新观察" };

        fixture.Resident.Agent.Memory.Add(fact);
        var observed = fixture.Engine.State;
        fixture.Resident.Inventory = new ResourceStock { Food = 3 };
        var supplied = fixture.Engine.State;

        Assert.Equal(initialKnowledge.Length + 1, observed.Residents[0].Agent.Memory.Length);
        Assert.Equal(initial.Residents[0].Inventory, observed.Residents[0].Inventory);
        Assert.Equal(3, supplied.Residents[0].Inventory.Food);
        Assert.Equal(initialKnowledge, initial.Residents[0].Agent.Memory);
        Assert.Same(observed.Residents[0].Agent, supplied.Residents[0].Agent);
        Assert.Equal(initial.Tick, supplied.Tick);
    }

    /// <summary>嵌套定位引用在父记录转换后仍指向当前状态，不能覆盖已经提交的需求。</summary>
    [Fact]
    public void Nested_locator_follows_a_parent_transition()
    {
        var fixture = new WorldFixture();
        var agent = fixture.Resident.Agent;
        var before = fixture.Engine.State;

        fixture.Resident.Replace(fixture.Resident.Value with
        {
            Agent = agent.Value with { Fatigue = 25 }, Activity = ResidentActivity.Working,
        });
        agent.SocialNeed = 30;

        Assert.Equal(25, fixture.Engine.State.Residents[0].Agent.Fatigue);
        Assert.Equal(30, fixture.Engine.State.Residents[0].Agent.SocialNeed);
        Assert.Equal(ResidentActivity.Working, fixture.Engine.State.Residents[0].Activity);
        Assert.NotEqual(25, before.Residents[0].Agent.Fatigue);
    }

    /// <summary>不可变种群和地形转换保留原地格，离开河道时清除宽度。</summary>
    [Fact]
    public void Tile_transitions_preserve_the_source_value()
    {
        var before = new Tile { Terrain = TerrainType.River, RiverWidth = 3, Wildlife = WildlifeKind.None };

        var after = before.WithTerrain(TerrainType.Grass).WithAnimalPopulation(WildlifeKind.Deer, 2);

        Assert.Equal(TerrainType.River, before.Terrain);
        Assert.Equal(3, before.RiverWidth);
        Assert.Equal(0, before.AnimalPopulation(WildlifeKind.Deer));
        Assert.Equal(0, after.RiverWidth);
        Assert.Equal(2, after.AnimalPopulation(WildlifeKind.Deer));
    }

    /// <summary>异步序列化期间发生编辑时，导出仍完整对应调用开始时的世界。</summary>
    [Fact]
    public async Task Async_save_uses_the_snapshot_captured_before_yielding()
    {
        var fixture = new WorldFixture();
        var expected = fixture.Engine.ExportJson();
        var changed = false;

        var saved = await fixture.Engine.ExportJsonAsync(_ =>
        {
            if (!changed)
            {
                changed = true;
                fixture.Engine.EditResident(fixture.ResidentId, new ResidentEdit { Name = "保存期间的新姓名" });
            }

            return ValueTask.CompletedTask;
        });

        Assert.True(changed);
        Assert.Equal(expected, saved);
        Assert.Equal("保存期间的新姓名", fixture.Engine.GetResident(fixture.ResidentId)!.Name);
        Assert.Equal(expected, WorldEngine.ImportJson(saved).ExportJson());
    }
}
