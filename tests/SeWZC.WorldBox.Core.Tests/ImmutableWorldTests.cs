using System.Text.Json;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.Core.Tests;

/// <summary>世界快照、日内转换和异步保存的隔离边界。</summary>
public sealed class ImmutableWorldTests
{
    private static string Serialize(WorldState state) => JsonSerializer.Serialize(state, WorldJsonContext.Default.WorldState);

    /// <summary>整批需求转换后，已有嵌套定位引用仍保留新身体和认知状态。</summary>
    [Fact]
    public void Batch_transition_synchronizes_existing_nested_cursors()
    {
        var fixture = new WorldFixture();
        var agent = fixture.Resident.Agent;
        var before = fixture.Engine.State;

        fixture.Engine.Current.Residents.Transform(person => person with
        {
            Health = 80,
            Agent = person.Agent with { Fatigue = 25 },
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

        Assert.Empty(fixture.Engine.State.Residents);
        Assert.Equal("归档的新姓名", fixture.Engine.State.ArchivedResidents[0].Name);
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
        fixture.Engine.SetNationResources(fixture.Town.NationId, food: 10);

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
            Agent = agent.Value with { Fatigue = 25 },
            Activity = ResidentActivity.Working,
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
