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

    /// <summary>版本缺失不能静默采用当前版本。</summary>
    [Theory]
    [InlineData("FormatVersion")]
    [InlineData("SimulationVersion")]
    public void Import_requires_explicit_versions(string field)
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
}
