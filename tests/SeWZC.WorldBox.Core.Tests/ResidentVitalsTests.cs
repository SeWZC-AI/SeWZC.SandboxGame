using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.Core.Tests;

/// <summary>居民基础生命状态的纯转换及伤害次序。</summary>
public sealed class ResidentVitalsTests
{
    /// <summary>老龄、火灾与疾病连续伤害保留首先致死的原因。</summary>
    [Fact]
    public void Vitals_preserve_the_first_lethal_damage_and_the_source()
    {
        var before = new Resident { Age = 999, Health = .4, SicknessTicks = 1 };

        var after = before.AdvanceVitals(new WorldRules(), new Tile { FireTicks = 1 }, 10, before.Profession, 0);

        Assert.Equal(.4, before.Health);
        Assert.Equal(1, before.SicknessTicks);
        Assert.Equal(0, after.Health);
        Assert.Equal(DeathCause.OldAge, after.DeathCause);
        Assert.Equal(10, after.DeathTick);
        Assert.Equal(190, after.DiseaseImmuneUntilTick);
        Assert.Equal(before.Activity, after.Activity);
    }

    /// <summary>传入新的感染时记录病程并进入生病活动，旧居民保持健康。</summary>
    [Fact]
    public void Infection_is_an_explicit_input_to_the_transition()
    {
        var before = new Resident { Age = 20, Profession = Profession.Farmer };

        var after = before.AdvanceVitals(new WorldRules(), new Tile(), 12, before.Profession, 80);

        Assert.Equal(0, before.SicknessTicks);
        Assert.Equal(80, after.SicknessTicks);
        Assert.Equal(ResidentActivity.Sick, after.Activity);
    }

    /// <summary>关闭衰老不增加年龄，传入的职业仍用于成年转换。</summary>
    [Fact]
    public void Disabled_aging_preserves_age()
    {
        var before = new Resident { Age = 14, Health = 50, Profession = Profession.Child };

        var after = before.AdvanceVitals(new WorldRules { Aging = false }, new Tile(), 1, Profession.Builder, 0);

        Assert.Equal(14, after.Age);
        Assert.Equal(50.15, after.Health);
        Assert.Equal(Profession.Builder, after.Profession);
        Assert.Equal(Profession.Child, before.Profession);
    }
}
