using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core.Tests;

/// <summary>研究开始、知识接收和只读查询的命令边界。</summary>
public sealed class ResearchCommandsTests
{
    private static WorldFixture ReadyWorld()
    {
        var fixture = new WorldFixture();
        fixture.Town.Resources = new ResourceStock { Food = 100, Wood = 100, Stone = 100, Ore = 100 };
        fixture.Engine.Current.Society.Buildings.Add(new BuildingCursor
        {
            Id = fixture.Engine.Current.NextId++,
            SettlementId = fixture.Town.Id,
            Kind = BuildingKind.Academy,
            X = 17,
            Y = 16,
            ConstructionProgress = 30,
        });
        return fixture;
    }

    /// <summary>开始研究仅扣除一次成本，登记实际工作量。</summary>
    [Fact]
    public void Start_spends_the_local_cost_and_creates_an_unfinished_project()
    {
        var fixture = ReadyWorld();
        var before = fixture.Town.Resources;
        var project = Advancement.Agriculture;

        fixture.Engine.StartResearch(fixture.Town.Id, project);

        foreach (var kind in ResourceStock.Kinds)
            Assert.Equal(before.Get(kind) - project.Cost.Get(kind), fixture.Town.Resources.Get(kind));
        var research = fixture.Engine.Current.Society.Research.Single();
        Assert.Same(project, research.ActiveProject);
        Assert.Equal(0, research.Progress);
        Assert.Equal(project.Work, research.RequiredProgress);
        Assert.DoesNotContain(project, research.Completed);
    }

    /// <summary>缺少完成的学舍时不扣费。</summary>
    [Fact]
    public void Start_requires_a_completed_academy()
    {
        var fixture = ReadyWorld();
        fixture.Engine.Current.Society.Buildings.RemoveAll(building => building.Kind == BuildingKind.Academy);
        var before = fixture.Engine.ExportJson();

        Assert.Throws<InvalidOperationException>(() => fixture.Engine.StartResearch(fixture.Town.Id,
            Advancement.Agriculture));

        Assert.Equal(before, fixture.Engine.ExportJson());
    }

    /// <summary>没有前置知识的项目不能启动。</summary>
    [Fact]
    public void Start_rejects_missing_prerequisites_without_spending()
    {
        var fixture = ReadyWorld();
        var before = fixture.Engine.ExportJson();

        Assert.Throws<InvalidOperationException>(() => fixture.Engine.StartResearch(fixture.Town.Id,
            Advancement.Irrigation));

        Assert.Equal(before, fixture.Engine.ExportJson());
    }

    /// <summary>材料不足时不会先扣除足够的其他材料。</summary>
    [Fact]
    public void Start_rejects_insufficient_materials_atomically()
    {
        var fixture = ReadyWorld();
        fixture.Town.Resources = fixture.Town.Resources with { Wood = 0 };
        var before = fixture.Engine.ExportJson();

        Assert.Throws<InvalidOperationException>(() => fixture.Engine.StartResearch(fixture.Town.Id,
            Advancement.Agriculture));

        Assert.Equal(before, fixture.Engine.ExportJson());
    }

    /// <summary>已有项目时不能通过再次启动重复扣费。</summary>
    [Fact]
    public void Start_rejects_a_second_project_without_spending_again()
    {
        var fixture = ReadyWorld();
        fixture.Engine.StartResearch(fixture.Town.Id, Advancement.Agriculture);
        var before = fixture.Engine.ExportJson();

        Assert.Throws<InvalidOperationException>(() => fixture.Engine.StartResearch(fixture.Town.Id,
            Advancement.Logistics));

        Assert.Equal(before, fixture.Engine.ExportJson());
    }

    /// <summary>知识接收重复调用不会产生重复记录或事件。</summary>
    [Fact]
    public void Receiving_the_same_knowledge_twice_is_idempotent()
    {
        var fixture = new WorldFixture();
        fixture.Engine.GrantReceivedResearch(fixture.Town.Id, Advancement.Agriculture);
        var before = fixture.Engine.ExportJson();

        fixture.Engine.GrantReceivedResearch(fixture.Town.Id, Advancement.Agriculture);

        Assert.Equal(before, fixture.Engine.ExportJson());
        Assert.Single(fixture.Engine.Current.Society.Research.Single().Completed);
    }

    /// <summary>查询不会授予知识、推进世界或消耗随机数。</summary>
    [Fact]
    public void HasResearch_is_read_only()
    {
        var fixture = new WorldFixture();
        var before = fixture.Engine.ExportJson();

        Assert.False(fixture.Engine.HasResearch(fixture.Town.Id, Advancement.Agriculture));

        Assert.Equal(before, fixture.Engine.ExportJson());
    }
}
