using System.Text.Json;
using System.Text.Json.Nodes;

namespace SeWZC.WorldBox.Core.Tests;

/// <summary>当前保存格式、必需字段和异步取消的检查。</summary>
public sealed class WorldPersistenceTests
{
    /// <summary>紧凑信息保留所有字段，包括转述来源、精确时间和军事关联。</summary>
    [Fact]
    public void Compact_fact_preserves_every_field()
    {
        var fact = new AgentFact
        {
            Id = 123,
            Kind = AgentFactKind.WarOrder,
            SubjectId = 456,
            X = 12,
            Y = 34,
            Value = 1.23456789012345,
            ObservedTick = 98_765_432_100,
            LearnedTick = 98_765_432_111,
            OriginResidentId = 789,
            OriginProfession = Profession.Soldier,
            SourceResidentId = 890,
            Confidence = .123456789012345,
            Hops = 7,
            Text = "转述的防御命令",
            EventId = 91,
            CampaignEventId = 92,
            WarObjective = WarObjective.DefendHomeland,
            TargetNationId = 93,
        };
        var context = new WorldJsonContext(new JsonSerializerOptions(WorldJsonContext.Default.Options)
        {
            Converters = { new AgentFactJsonConverter() },
        });

        var json = JsonSerializer.Serialize(fact, context.AgentFact);

        Assert.Equal(fact, JsonSerializer.Deserialize(json, context.AgentFact));
    }

    /// <summary>紧凑信息拒绝字段缺失、多余、错误类型及未知枚举。</summary>
    [Theory]
    [InlineData("short")]
    [InlineData("extra")]
    [InlineData("text")]
    [InlineData("enum")]
    [InlineData("object")]
    public void Import_rejects_malformed_compact_fact(string corruption)
    {
        var fixture = new WorldFixture();
        var document = JsonNode.Parse(fixture.Engine.ExportJson())!;
        var memory = document["Residents"]![0]!["Agent"]!["Memory"]!.AsArray();
        var fact = memory[0]!.AsArray();
        switch (corruption)
        {
            case "short":
                fact.RemoveAt(fact.Count - 1);
                break;
            case "extra":
                fact.Add(0);
                break;
            case "text":
                fact[13] = null;
                break;
            case "enum":
                fact[1] = 999;
                break;
            case "object":
                memory[0] = new JsonObject();
                break;
        }

        Assert.Throws<ArgumentException>(() => WorldEngine.ImportJson(document.ToJsonString()));
    }

    /// <summary>紧凑存档保留零可信度和非零默认值，中文直接写入 UTF-8，HTML 敏感字符仍转义。</summary>
    [Fact]
    public void Compact_save_preserves_zero_confidence_and_navigation_defaults()
    {
        var fixture = new WorldFixture();
        var fact = fixture.Resident.Value.Agent.Memory[0] with { Confidence = 0, Text = "中文 <html>" };
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with { Memory = [fact], Goal = new AgentGoal() }));

        var json = fixture.Engine.ExportJson();
        var restored = WorldEngine.ImportJson(json).GetResident(fixture.ResidentId)!;

        Assert.Contains("中文", json);
        Assert.Contains("\\u003Chtml\\u003E", json);
        var savedFact = JsonNode.Parse(json)!["Residents"]![0]!["Agent"]!["Memory"]![0]!.AsArray();
        Assert.Equal(18, savedFact.Count);
        Assert.Equal(fact, Assert.Single(restored.Agent.Memory));
        Assert.Equal(0, restored.Agent.Memory[0].Confidence);
        Assert.Equal(-1, restored.Agent.Goal.NavigationTarget);
        Assert.Equal(-1, restored.Agent.WorkAreaIndex);
    }

    /// <summary>工作范围须属于当前地图；旧岗位已拆除时可保留原地址，不能因此拒绝历史记录。</summary>
    [Theory]
    [InlineData(-1, 0, true)]
    [InlineData(1023, 2_000_001, true)]
    [InlineData(-2, 0, false)]
    [InlineData(1024, 0, false)]
    [InlineData(-1, -1, false)]
    public void Work_assignment_addresses_are_validated(int area, int workplace, bool valid)
    {
        var fixture = new WorldFixture();
        var before = fixture.Engine.State;
        var document = JsonNode.Parse(fixture.Engine.ExportJson())!;
        var agent = document["Residents"]![0]!["Agent"]!;
        agent["WorkAreaIndex"] = area;
        agent["WorkplaceId"] = workplace;

        if (valid)
        {
            var restored = WorldEngine.ImportJson(document.ToJsonString()).GetResident(fixture.ResidentId)!;
            Assert.Equal(area, restored.Agent.WorkAreaIndex);
            Assert.Equal(workplace, restored.Agent.WorkplaceId);
        }
        else
            Assert.Throws<ArgumentException>(() => WorldEngine.ImportJson(document.ToJsonString()));

        Assert.Equal(before, fixture.Engine.State);
    }

    /// <summary>短路线不能跳格、越出原视野或带有越界的进度，拒绝导入不改变原世界。</summary>
    [Theory]
    [InlineData(10 * 32 + 12, 1)]
    [InlineData(10 * 32 + 11, 3)]
    [InlineData(10 * 32 + 11, -1)]
    public void Import_rejects_invalid_short_routes(int next, int offset)
    {
        var fixture = new WorldFixture();
        var before = fixture.Engine.State;
        var document = JsonNode.Parse(fixture.Engine.ExportJson())!;
        var goal = document["Residents"]![0]!["Agent"]!["Goal"]!;
        goal["NavigationRoute"] = new JsonArray(10 * 32 + 10, next);
        goal["NavigationRouteOffset"] = offset;

        Assert.Throws<ArgumentException>(() => WorldEngine.ImportJson(document.ToJsonString()));

        Assert.Equal(before, fixture.Engine.State);
    }

    /// <summary>行动目标成为引用记录后，空目标仍须在导入阶段拒绝。</summary>
    [Fact]
    public void Null_goal_is_rejected_without_changing_the_source_world()
    {
        var fixture = new WorldFixture();
        var before = fixture.Engine.State;
        var document = JsonNode.Parse(fixture.Engine.ExportJson())!;
        document["Residents"]![0]!["Agent"]!["Goal"] = null;

        Assert.Throws<ArgumentException>(() => WorldEngine.ImportJson(document.ToJsonString()));

        Assert.Equal(before, fixture.Engine.State);
    }

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
            Id = fixture.Engine.NextId++,
            Kind = AgentFactKind.Danger,
            X = 16,
            Y = 16,
            Confidence = 0.8,
            Text = "现场危险观察",
        };
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with { Memory = fixture.Resident.Value.Agent.Memory.Add(fact) }));
        fixture.Engine.PendingMessages = fixture.Engine.PendingMessages.Add(new PendingMessage
        {
            SenderId = fixture.ResidentId, RecipientId = fixture.ResidentId, DeliverTick = 1, Facts = [fact],
        });

        var restoredWorld = WorldEngine.ImportJson(fixture.Engine.ExportJson());
        var savedFact = restoredWorld.GetResident(fixture.ResidentId)!.Agent.Memory.Last();

        Assert.Equal(fact, Assert.Single(Assert.Single(restoredWorld.PendingMessages).Facts));
        Assert.Equal(fact, savedFact);
        Assert.Equal(0.4, savedFact.ReliabilityAt(12), 10);
    }

    /// <summary>空消息内容必须使用集合，不能用空引用替代。</summary>
    [Fact]
    public void Import_rejects_null_message_facts()
    {
        var fixture = new WorldFixture();
        fixture.Engine.PendingMessages = fixture.Engine.PendingMessages.Add(new PendingMessage
        {
            SenderId = fixture.ResidentId, RecipientId = fixture.ResidentId, DeliverTick = 1,
        });
        var saved = JsonNode.Parse(fixture.Engine.ExportJson())!;
        saved["PendingMessages"]![0]!["Facts"] = null;

        Assert.Throws<ArgumentException>(() => WorldEngine.ImportJson(saved.ToJsonString()));
    }

    /// <summary>驻留进度不能超出完成等待的上限，也不能为负数。</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(AgentGoal.MaximumResidenceTicks + 1)]
    public void Import_rejects_invalid_residence_progress(int progress)
    {
        var fixture = new WorldFixture();
        var before = fixture.Engine.State;
        var saved = JsonNode.Parse(fixture.Engine.ExportJson())!;
        saved["Residents"]![0]!["Agent"]!["Goal"]!["WorkTicks"] = progress;

        Assert.Throws<ArgumentException>(() => WorldEngine.ImportJson(saved.ToJsonString()));
        Assert.Equal(before, fixture.Engine.State);
    }
}
