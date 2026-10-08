using System.Collections.Immutable;
using System.Text.Json.Nodes;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.Core.Tests;

/// <summary>个人熟路记忆的容量、快照隔离及存档校验。</summary>
public sealed class RouteMemoryTests
{
    /// <summary>容量满后忘记最久未走的地格，旧快照保持原值。</summary>
    [Fact]
    public void Familiar_places_evict_the_oldest_visit_without_mutating_the_source()
    {
        var before = new AgentState { FamiliarTiles = Enumerable.Range(0, AgentState.MaximumFamiliarTiles).ToImmutableArray() };

        var after = before.RememberRouteTile(99);

        Assert.Equal(AgentState.MaximumFamiliarTiles, after.FamiliarTiles.Length);
        Assert.DoesNotContain(0, after.FamiliarTiles);
        Assert.Equal(99, after.FamiliarTiles[^1]);
        Assert.Equal(0, before.FamiliarTiles[0]);
    }

    /// <summary>重走既有地格只刷新顺序，不复制地点。</summary>
    [Fact]
    public void Revisiting_a_place_refreshes_recency_without_duplicates()
    {
        var before = new AgentState { FamiliarTiles = [1, 2, 3] };

        var after = before.RememberRouteTile(2);

        Assert.Equal<int>([1, 3, 2], after.FamiliarTiles);
        Assert.Equal<int>([1, 2, 3], before.FamiliarTiles);
        Assert.Same(after, after.RememberRouteTile(2));
    }

    /// <summary>保存恢复保留个人熟路，观察效果不改变世界或消耗随机数。</summary>
    [Fact]
    public void Saving_and_observing_preserve_familiar_places()
    {
        var fixture = new WorldFixture();
        fixture.Resident.Agent.Replace(fixture.Resident.Agent.Value with { FamiliarTiles = [528, 529] });
        var before = fixture.Engine.State;

        Assert.Contains(fixture.Engine.GetResidentEffects(fixture.ResidentId), effect => effect.Name == "路线习惯");
        var restored = WorldEngine.ImportJson(fixture.Engine.ExportJson());

        Assert.Equal<int>([528, 529], restored.GetResident(fixture.ResidentId)!.Agent.FamiliarTiles);
        Assert.Equal(before, fixture.Engine.State);
    }

    /// <summary>缺失熟路字段不能静默丢失决定未来路线的因果状态。</summary>
    [Fact]
    public void Import_requires_familiar_places()
    {
        var fixture = new WorldFixture();
        var saved = JsonNode.Parse(fixture.Engine.ExportJson())!;
        saved["Residents"]![0]!["Agent"]!.AsObject().Remove("FamiliarTiles");

        Assert.Throws<ArgumentException>(() => WorldEngine.ImportJson(saved.ToJsonString()));
    }

    /// <summary>拒绝重复、越界、超容量或空引用的熟路记录。</summary>
    [Theory]
    [InlineData("[1,1]")]
    [InlineData("[-1]")]
    [InlineData("[1024]")]
    [InlineData("null")]
    public void Import_rejects_invalid_familiar_places(string value)
    {
        var fixture = new WorldFixture();
        var before = fixture.Engine.State;
        var saved = JsonNode.Parse(fixture.Engine.ExportJson())!;
        saved["Residents"]![0]!["Agent"]!["FamiliarTiles"] = JsonNode.Parse(value);

        Assert.Throws<ArgumentException>(() => WorldEngine.ImportJson(saved.ToJsonString()));

        Assert.Equal(before, fixture.Engine.State);
    }

    /// <summary>熟路容量约束同样校验快照恢复，不依赖 JSON 解析器。</summary>
    [Fact]
    public void Snapshot_restore_rejects_oversized_familiar_places()
    {
        var fixture = new WorldFixture();
        var before = fixture.Engine.State;
        var person = before.Residents[0];
        var oversized = person with { Agent = person.Agent with
        {
            FamiliarTiles = Enumerable.Range(0, AgentState.MaximumFamiliarTiles + 1).ToImmutableArray(),
        } };

        Assert.Throws<ArgumentException>(() => WorldEngine.FromSnapshot(before with
        {
            Residents = before.Residents.SetItem(0, oversized),
        }));
        Assert.Equal(before, fixture.Engine.State);
    }
}
