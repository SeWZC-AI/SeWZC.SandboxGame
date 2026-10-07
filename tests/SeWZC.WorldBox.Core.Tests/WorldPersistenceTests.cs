using System.Text.Json.Nodes;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.Core.Tests;

/// <summary>当前保存格式、必需字段和异步取消的检查。</summary>
public sealed class WorldPersistenceTests
{
    /// <summary>输入为空或格式损坏时拒绝导入。</summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("{}")]
    public void Import_rejects_invalid_documents(string json)
    {
        Assert.Throws<ArgumentException>(() => WorldEngine.ImportJson(json));
    }

    /// <summary>当前格式的合法零值在保存后恢复。</summary>
    [Fact]
    public void Round_trip_preserves_explicit_zero_resident_values()
    {
        var fixture = new WorldFixture();
        fixture.Engine.EditResident(fixture.ResidentId, new ResidentEdit { Health = 0, Mana = 0, Age = 0 });

        var restored = WorldEngine.ImportJson(fixture.Engine.ExportJson()).GetResident(fixture.ResidentId)!;

        Assert.Equal(0, restored.Health);
        Assert.Equal(0, restored.Mana);
        Assert.Equal(0, restored.Age);
    }

    /// <summary>版本与地图必需字段缺失时拒绝恢复。</summary>
    [Theory]
    [InlineData("FormatVersion")]
    [InlineData("SimulationVersion")]
    [InlineData("Width")]
    [InlineData("Height")]
    [InlineData("Tiles")]
    public void Import_requires_explicit_world_metadata(string field)
    {
        var fixture = new WorldFixture();
        var saved = JsonNode.Parse(fixture.Engine.ExportJson())!.AsObject();
        saved.Remove(field);

        Assert.Throws<ArgumentException>(() => WorldEngine.ImportJson(saved.ToJsonString()));
    }

    /// <summary>旧版与未来版本均不能当作当前世界使用。</summary>
    [Theory]
    [InlineData("FormatVersion", -1)]
    [InlineData("FormatVersion", 1)]
    [InlineData("SimulationVersion", -1)]
    [InlineData("SimulationVersion", 1)]
    public void Import_rejects_unsupported_versions(string field, int offset)
    {
        var fixture = new WorldFixture();
        var saved = JsonNode.Parse(fixture.Engine.ExportJson())!;
        saved[field] = saved[field]!.GetValue<int>() + offset;

        Assert.Throws<ArgumentException>(() => WorldEngine.ImportJson(saved.ToJsonString()));
    }

    /// <summary>预先取消的捕获不会返回不完整结果。</summary>
    [Fact]
    public async Task Export_observes_preexisting_cancellation()
    {
        var fixture = new WorldFixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var before = fixture.Engine.ExportJson();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Engine.ExportJsonChunksAsync(
            _ => ValueTask.CompletedTask, cancellation.Token));

        Assert.Equal(before, fixture.Engine.ExportJson());
    }

    /// <summary>异步分块保存与同步保存表示同一个世界。</summary>
    [Fact]
    public async Task Chunked_export_matches_synchronous_export()
    {
        var fixture = new WorldFixture();
        var expected = fixture.Engine.ExportJson();

        var chunks = await fixture.Engine.ExportJsonChunksAsync(_ => ValueTask.CompletedTask);

        Assert.Equal(expected, string.Concat(chunks));
        Assert.Equal(expected, fixture.Engine.ExportJson());
    }
    /// <summary>保存恢复后仍按原始观察时间和议题规则计算可信度。</summary>
    [Fact]
    public void Round_trip_preserves_fact_snapshot_and_topic_behavior()
    {
        var fixture = new WorldFixture();
        var fact = new AgentFact
        {
            Id = fixture.Engine.Current.NextId++,
            Kind = AgentFactKind.Danger,
            X = 16,
            Y = 16,
            Confidence = 0.8,
            Text = "现场危险观察",
        };
        fixture.Resident.Agent.Memory.Add(fact);
        fixture.Engine.Current.PendingMessages.Add(new PendingMessage
        {
            SenderId = fixture.ResidentId,
            RecipientId = fixture.ResidentId,
            DeliverTick = 1,
            Facts = [fact],
        });

        var restoredWorld = WorldEngine.ImportJson(fixture.Engine.ExportJson());
        var savedFact = restoredWorld.GetResident(fixture.ResidentId)!.Agent.Memory.Last();

        Assert.Equal(fact, Assert.Single(Assert.Single(restoredWorld.Current.PendingMessages).Facts));
        Assert.Equal(fact, savedFact);
        Assert.Equal(0.4, savedFact.ReliabilityAt(12), 10);
    }

    /// <summary>空消息内容必须使用集合，不能用空引用替代。</summary>
    [Fact]
    public void Import_rejects_null_message_facts()
    {
        var fixture = new WorldFixture();
        fixture.Engine.Current.PendingMessages.Add(new PendingMessage
        {
            SenderId = fixture.ResidentId,
            RecipientId = fixture.ResidentId,
            DeliverTick = 1,
        });
        var saved = JsonNode.Parse(fixture.Engine.ExportJson())!;
        saved["PendingMessages"]![0]!["Facts"] = null;

        Assert.Throws<ArgumentException>(() => WorldEngine.ImportJson(saved.ToJsonString()));
    }

}
