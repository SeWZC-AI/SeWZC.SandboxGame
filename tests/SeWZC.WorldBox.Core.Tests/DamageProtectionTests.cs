namespace SeWZC.WorldBox.Core.Tests;

/// <summary>结界、护甲消耗和城镇护盾范围的检查。</summary>
public sealed class DamageProtectionTests
{
    /// <summary>个人结界仅消耗实际吸收的伤害。</summary>
    [Theory]
    [InlineData(3, 10, 0, 7)]
    [InlineData(15, 10, 5, 0)]
    public void Personal_ward_absorbs_damage_and_consumes_its_strength(
        double damage, double ward, double remaining, double remainingWard)
    {
        var fixture = new WorldFixture();
        fixture.Resident.PersonalWard = ward;
        fixture.Resident.Armor = 0;

        var result = fixture.Engine.TryAbsorbShieldDamage(fixture.Resident, damage);

        Assert.Equal(remaining, result);
        Assert.Equal(remainingWard, fixture.Resident.PersonalWard);
    }

    /// <summary>护甲耗损量等于本次吸收的伤害。</summary>
    [Fact]
    public void Armor_consumes_only_the_damage_it_absorbs()
    {
        var fixture = new WorldFixture();
        fixture.Resident.Armor = 10;
        fixture.Resident.PersonalWard = 0;

        var remaining = fixture.Engine.TryAbsorbShieldDamage(fixture.Resident, 20);

        Assert.Equal(13, remaining);
        Assert.Equal(3, fixture.Resident.Armor);
    }

    /// <summary>负伤害不会反向增加结界或护甲。</summary>
    [Fact]
    public void Negative_damage_does_not_change_protection()
    {
        var fixture = new WorldFixture();
        fixture.Resident.PersonalWard = 10;
        fixture.Resident.Armor = 10;

        Assert.Equal(0, fixture.Engine.TryAbsorbShieldDamage(fixture.Resident, -5));

        Assert.Equal(10, fixture.Resident.PersonalWard);
        Assert.Equal(10, fixture.Resident.Armor);
    }

    /// <summary>城镇护盾只保护局部范围内的居民。</summary>
    [Theory]
    [InlineData(21, 12)]
    [InlineData(22, 20)]
    public void Town_shield_applies_only_within_its_range(int x, double expectedDamage)
    {
        var fixture = new WorldFixture();
        fixture.Town.ShieldTicks = 10;
        fixture.Resident.X = x;
        fixture.Resident.Y = fixture.Town.Y;
        fixture.Resident.PersonalWard = 0;
        fixture.Resident.Armor = 0;

        Assert.Equal(expectedDamage, fixture.Engine.TryAbsorbShieldDamage(fixture.Resident, 20));
    }
}
