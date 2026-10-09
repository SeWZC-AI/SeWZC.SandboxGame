using System.Text.Json;
using System.Text.Json.Nodes;

namespace SeWZC.WorldBox.Core.Tests;

/// <summary>居民编辑的局部更新、输入拒绝与草稿隔离检查。</summary>
public sealed class ResidentEditingTests
{
    /// <summary>只编辑姓名时保留其他状态。</summary>
    [Fact]
    public void Rename_trims_input_and_preserves_unspecified_fields()
    {
        var fixture = new WorldFixture();
        var before = fixture.Resident;

        fixture.Engine.EditResident(fixture.ResidentId, new ResidentEdit { Name = "  新姓名  " });

        Assert.Equal("新姓名", fixture.Resident.Name);
        Assert.Equal(before.Age, fixture.Resident.Age);
        Assert.Equal(before.Profession, fixture.Resident.Profession);
        Assert.Equal(before.SettlementId, fixture.Resident.SettlementId);
        Assert.Equal(before.Health, fixture.Resident.Health);
    }

    /// <summary>显式零值不能被当作未指定。</summary>
    [Fact]
    public void Edit_accepts_explicit_zero_values()
    {
        var fixture = new WorldFixture();

        fixture.Engine.EditResident(fixture.ResidentId, new ResidentEdit
        {
            Age = 0,
            Health = 0,
            Hunger = 0,
            Thirst = 0,
            Mana = 0,
            MagicTalent = 0,
            MagicTraining = 0,
        });

        var resident = fixture.Resident;
        Assert.Equal(0, resident.Age);
        Assert.Equal(0, resident.Health);
        Assert.Equal(0, resident.Hunger);
        Assert.Equal(0, resident.Thirst);
        Assert.Equal(0, resident.Mana);
        Assert.Equal(0, resident.MagicTalent);
        Assert.Equal(0, resident.MagicTraining);
    }

    /// <summary>无效年龄不会连带提交同一次编辑中的姓名。</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Invalid_age_rejects_the_entire_patch(double age)
    {
        var fixture = new WorldFixture();
        var before = fixture.Engine.ExportJson();

        Assert.Throws<ArgumentException>(() => fixture.Engine.EditResident(fixture.ResidentId,
            new ResidentEdit { Name = "不应提交", Age = age }));

        Assert.Equal(before, fixture.Engine.ExportJson());
    }

    /// <summary>地图外的位置编辑不消耗编号或提交部分修改。</summary>
    [Theory]
    [InlineData(-1, 16)]
    [InlineData(32, 16)]
    [InlineData(16, -1)]
    [InlineData(16, 32)]
    public void Out_of_bounds_position_leaves_the_world_unchanged(int x, int y)
    {
        var fixture = new WorldFixture();
        var before = fixture.Engine.ExportJson();

        Assert.Throws<ArgumentException>(() => fixture.Engine.EditResident(fixture.ResidentId,
            new ResidentEdit { Name = "不应提交", X = x, Y = y }));

        Assert.Equal(before, fixture.Engine.ExportJson());
    }

    /// <summary>移入不可步行的水格时拒绝整个编辑。</summary>
    [Fact]
    public void Position_rejects_inaccessible_land()
    {
        var fixture = new WorldFixture();
        fixture.Engine.Current.Tiles[10 * 32 + 10].Terrain = TerrainType.DeepWater;
        var before = fixture.Engine.ExportJson();

        Assert.Throws<ArgumentException>(() => fixture.Engine.EditResident(fixture.ResidentId,
            new ResidentEdit { X = 10, Y = 10 }));

        Assert.Equal(before, fixture.Engine.ExportJson());
    }

    /// <summary>位置编辑同时重置呈现起点。</summary>
    [Fact]
    public void Position_resets_the_display_segment_to_the_new_location()
    {
        var fixture = new WorldFixture();

        fixture.Engine.EditResident(fixture.ResidentId, new ResidentEdit { X = 10, Y = 11 });

        Assert.Equal(10, fixture.Resident.X);
        Assert.Equal(11, fixture.Resident.Y);
        Assert.Equal(10, fixture.Resident.FromX);
        Assert.Equal(11, fixture.Resident.FromY);
        Assert.Equal(fixture.Engine.Current.Tick, fixture.Resident.MoveStartedTick);
    }

    /// <summary>提交后的随身资源不再受编辑草稿修改影响。</summary>
    [Fact]
    public void Inventory_is_copied_before_commit()
    {
        var fixture = new WorldFixture();
        var inventory = new ResourceStock { Food = 3.5 };

        fixture.Engine.EditResident(fixture.ResidentId, new ResidentEdit { Inventory = inventory });
        inventory = inventory with { Food = 100 };

        Assert.Equal(3.5, fixture.Resident.Inventory.Food);
    }

    /// <summary>空或损坏的心智草稿不能改变世界。</summary>
    [Theory]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("[]")]
    public void Mind_rejects_invalid_json_without_mutation(string json)
    {
        var fixture = new WorldFixture();
        var before = fixture.Engine.ExportJson();

        Assert.Throws<ArgumentException>(() => fixture.Engine.EditResidentMindJson(fixture.ResidentId, json));

        Assert.Equal(before, fixture.Engine.ExportJson());
    }

    /// <summary>无效心智目标不能部分替换居民认知。</summary>
    [Fact]
    public void Mind_rejects_an_invalid_goal_before_commit()
    {
        var fixture = new WorldFixture();
        var before = fixture.Engine.ExportJson();
        var mind = JsonNode.Parse(fixture.Engine.ExportResidentMind(fixture.ResidentId))!;
        mind["Goal"]!["TargetX"] = -1;

        Assert.Throws<ArgumentException>(() => fixture.Engine.EditResidentMindJson(fixture.ResidentId,
            mind.ToJsonString()));

        Assert.Equal(before, fixture.Engine.ExportJson());
    }

    /// <summary>修改经历只改变当前认知，不回算既有世界结果。</summary>
    [Fact]
    public void History_changes_personality_without_rewriting_past_results()
    {
        var fixture = new WorldFixture();
        var courage = fixture.Resident.Agent.Personality.Courage;
        var events = fixture.Engine.Current.Events.Select(entry => JsonSerializer.Serialize(entry)).ToArray();
        var resources = JsonSerializer.Serialize(fixture.Town.Resources);
        var history = new List<ResidentHistoryEntry>
        {
            new() { Tick = 0, Text = "经历困难", Experience = PersonalExperienceKind.Hardship, Impact = 1 },
        };

        fixture.Engine.EditResident(fixture.ResidentId, new ResidentEdit { History = history });

        Assert.True(fixture.Resident.Agent.Personality.Courage < courage);
        Assert.Equal(events, fixture.Engine.Current.Events.Take(events.Length)
            .Select(entry => JsonSerializer.Serialize(entry)));
        Assert.Equal(resources, JsonSerializer.Serialize(fixture.Town.Resources));
    }

    /// <summary>经历草稿与提交后的记录互不影响。</summary>
    [Fact]
    public void History_is_copied_before_commit()
    {
        var fixture = new WorldFixture();
        var history = new List<ResidentHistoryEntry> { new() { Tick = 0, Text = "原记录" } };

        fixture.Engine.EditResident(fixture.ResidentId, new ResidentEdit { History = history });
        history[0] = history[0] with { Text = "外部修改" };
        history.Clear();

        Assert.Equal("原记录", Assert.Single(fixture.Resident.History).Text);
    }

    /// <summary>编辑信息产生新身份，已公开的旧观察不被改写，决策依据同步修订。</summary>
    [Fact]
    public void Fact_revision_replaces_the_snapshot_and_updates_decision_evidence()
    {
        var fixture = new WorldFixture();
        var prior = fixture.Resident.Agent.Memory.First();
        fixture.Town.PublicKnowledge = fixture.Town.PublicKnowledge.Add(prior);
        fixture.Resident.Agent = fixture.Resident.Agent with
        {
            Decisions = fixture.Resident.Agent.Decisions.Add(new AgentDecision { EvidenceFactId = prior.Id })
        };
        var mind = JsonNode.Parse(fixture.Engine.ExportResidentMind(fixture.ResidentId))!;
        var memory = mind["Memory"]!.AsArray();
        memory.First(node => node!["Id"]!.GetValue<int>() == prior.Id)!["Text"] = "新的观察描述";

        fixture.Engine.EditResidentMindJson(fixture.ResidentId, mind.ToJsonString());

        var revised = fixture.Resident.Agent.Memory.First(fact => fact.Text == "新的观察描述");
        Assert.NotEqual(prior.Id, revised.Id);
        Assert.Contains(prior, fixture.Town.PublicKnowledge);
        Assert.Equal(revised.Id, fixture.Resident.Agent.Decisions.Last().EvidenceFactId);
        Assert.NotEqual("新的观察描述", prior.Text);
    }
}
