namespace SeWZC.WorldBox.Core.Tests;

/// <summary>国家编辑对本地权威状态的更新和拒绝检查。</summary>
public sealed class NationEditingTests
{
    /// <summary>资源编辑写入聚落仓库并更新国家汇总。</summary>
    [Fact]
    public void Resources_update_the_warehouse_and_nation_total()
    {
        var fixture = new WorldFixture();
        var wood = fixture.Town.Resources.Wood;

        fixture.Engine.SetNationResources(fixture.Town.NationId, 12.5);

        Assert.Equal(12.5, fixture.Town.Resources.Food);
        Assert.Equal(12.5, fixture.Engine.Current.Nations.Single().Resources.Food);
        Assert.Equal(wood, fixture.Town.Resources.Wood);
    }

    /// <summary>零库存是合法编辑值。</summary>
    [Fact]
    public void Resources_accept_zero()
    {
        var fixture = new WorldFixture();

        fixture.Engine.SetNationResources(fixture.Town.NationId, 0);

        Assert.Equal(0, fixture.Town.Resources.Food);
        Assert.Equal(0, fixture.Engine.Current.Nations.Single().Resources.Food);
    }

    /// <summary>非法金额不会先提交其他合法资源。</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(1_000_001)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Invalid_resources_reject_the_entire_edit(double invalid)
    {
        var fixture = new WorldFixture();
        var before = fixture.Engine.ExportJson();

        Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Engine.SetNationResources(
            fixture.Town.NationId, 9, invalid));

        Assert.Equal(before, fixture.Engine.ExportJson());
    }

    /// <summary>国家名称去除输入两端空白。</summary>
    [Fact]
    public void Rename_trims_the_name()
    {
        var fixture = new WorldFixture();

        fixture.Engine.RenameNation(fixture.Town.NationId, "  新国家  ");

        Assert.Equal("新国家", fixture.Engine.Current.Nations.Single().Name);
    }

    /// <summary>非法国名不会修改世界。</summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("名称\n内嵌换行")]
    [InlineData("12345678901234567890123456789012345678901")]
    public void Rename_rejects_invalid_names(string name)
    {
        var fixture = new WorldFixture();
        var before = fixture.Engine.ExportJson();

        Assert.Throws<ArgumentException>(() => fixture.Engine.RenameNation(fixture.Town.NationId, name));

        Assert.Equal(before, fixture.Engine.ExportJson());
    }

    /// <summary>古代工具等级编辑不授予聚落研究知识。</summary>
    [Fact]
    public void Basic_technology_does_not_grant_research()
    {
        var fixture = new WorldFixture();
        var completed = fixture.Engine.Current.Society.Research.Single().Completed.ToArray();

        fixture.Engine.SetNationTechnology(fixture.Town.NationId, 5);

        Assert.Equal(5, fixture.Engine.Current.Nations.Single().Technology);
        Assert.Equal(completed, fixture.Engine.Current.Society.Research.Single().Completed);
    }

    /// <summary>等级范围外的编辑不会提交。</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Basic_technology_rejects_out_of_range_levels(int level)
    {
        var fixture = new WorldFixture();
        var before = fixture.Engine.ExportJson();

        Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Engine.SetNationTechnology(
            fixture.Town.NationId, level));

        Assert.Equal(before, fixture.Engine.ExportJson());
    }
}
