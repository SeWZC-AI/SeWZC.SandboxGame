namespace SeWZC.WorldBox.Core.Tests;

/// <summary>结界、护甲消耗和城镇护盾范围的检查。</summary>
public sealed class DamageProtectionTests
{
    /// <summary>生命已归零的居民不能在归档前被治疗复生，死亡原因保持不变。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Healing_cannot_revive_a_resident_awaiting_archival(bool publicHealth)
    {
        var fixture = new WorldFixture();
        fixture.Engine.SpawnResidents(16, 16, RaceKind.Human, 1);
        var patient = fixture.Engine.Current.Residents.Single(person => person.Id != fixture.ResidentId);
        patient.Replace(patient.Value with
        {
            X = 16,
            Y = 16,
            Health = 0,
            DeathCause = DeathCause.Fire,
            DeathTick = 1,
        });
        fixture.Engine.Current.Tick = 1;
        fixture.Resident.Age = 25;
        fixture.Resident.Replace(fixture.Resident.Value with { MagicTalent = 60 });
        fixture.Resident.Replace(fixture.Resident.Value with { MagicTraining = 20 });
        fixture.Resident.Health = 100;
        fixture.Resident.Mana = 100;
        fixture.Resident.X = 16;
        fixture.Resident.Y = 16;

        if (publicHealth)
        {
            fixture.Engine.SetPolicy(fixture.Town.NationId, PolicyKind.PublicHealth);
            fixture.Engine.TickSociety();
        }
        else
            Assert.False(fixture.Engine.TryCastSpell(fixture.ResidentId, SpellKind.Heal, 16, 16));

        Assert.Equal(0, patient.Health);
        Assert.Equal(DeathCause.Fire, patient.DeathCause);
        Assert.Equal(100, fixture.Resident.Mana);
    }

    /// <summary>日内附近索引与直接施法都遵循实际位置，优先治疗生命最低的本国居民。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [InlineData(true, true)]
    public void Healing_prefers_the_most_injured_local_resident(bool automatic, bool outsideAutomaticRange = false)
    {
        var fixture = new WorldFixture();
        fixture.Engine.SpawnResidents(16, 16, RaceKind.Human, 2);
        fixture.Engine.ConfigureWorld(fixture.Engine.State.Rules with
        {
            Aging = false,
            Hunger = false,
            Thirst = false,
            Disease = false,
            Births = false,
            Construction = false,
            Expansion = false,
            Research = false,
            Wars = false,
        }, false, false);
        foreach (var person in fixture.Engine.Current.Residents)
        {
            person.Age = 25;
            person.X = 16;
            person.Y = 16;
            person.Replace(person.Value with { FrozenUntilTick = 100 });
            person.Agent = person.Agent with { Initialized = true, NextThinkTick = 100 };
        }

        fixture.Resident.Replace(fixture.Resident.Value with { MagicTalent = 60 });
        fixture.Resident.Replace(fixture.Resident.Value with { MagicTraining = 20 });
        fixture.Resident.Mana = 100;
        var patients = fixture.Engine.Current.Residents.Where(person => person.Id != fixture.ResidentId).ToArray();
        patients[0].Health = 50;
        patients[1].Health = 30;
        if (outsideAutomaticRange)
        {
            patients[1].X = 18;
            patients[1].Y = 18;
        }

        fixture.Engine.Current.Tick = SimulationTime.WakeTick
                                      + (11 - fixture.ResidentId % 12 - SimulationTime.WakeTick + 24) % 12;

        if (automatic)
            fixture.Engine.Step();
        else
            fixture.Engine.CastSpell(fixture.ResidentId, SpellKind.Heal, 16, 16);

        if (outsideAutomaticRange)
        {
            Assert.True(patients[0].Health > 50);
            Assert.Equal(30 + .15 / SimulationTime.TicksPerDay, patients[1].Health, 8);
        }
        else
        {
            Assert.Equal(50 + (automatic ? .15 / SimulationTime.TicksPerDay : 0), patients[0].Health, 8);
            Assert.True(patients[1].Health > 30);
        }

        Assert.True(fixture.Resident.Mana < 100);
    }

    /// <summary>个人结界仅消耗实际吸收的伤害。</summary>
    [Theory]
    [InlineData(3, 10, 0, 7)]
    [InlineData(15, 10, 5, 0)]
    public void Personal_ward_absorbs_damage_and_consumes_its_strength(
        double damage, double ward, double remaining, double remainingWard)
    {
        var fixture = new WorldFixture();
        fixture.Resident.Replace(fixture.Resident.Value with { PersonalWard = ward });
        fixture.Resident.Replace(fixture.Resident.Value with { Armor = 0 });

        var result = fixture.Engine.TryAbsorbShieldDamage(fixture.Resident.Value, damage);

        Assert.Equal(remaining, result);
        Assert.Equal(remainingWard, fixture.Resident.PersonalWard);
    }

    /// <summary>护甲耗损量等于本次吸收的伤害。</summary>
    [Fact]
    public void Armor_consumes_only_the_damage_it_absorbs()
    {
        var fixture = new WorldFixture();
        fixture.Resident.Replace(fixture.Resident.Value with { Armor = 10 });
        fixture.Resident.Replace(fixture.Resident.Value with { PersonalWard = 0 });

        var remaining = fixture.Engine.TryAbsorbShieldDamage(fixture.Resident.Value, 20);

        Assert.Equal(13, remaining);
        Assert.Equal(3, fixture.Resident.Armor);
    }

    /// <summary>负伤害不会反向增加结界或护甲。</summary>
    [Fact]
    public void Negative_damage_does_not_change_protection()
    {
        var fixture = new WorldFixture();
        fixture.Resident.Replace(fixture.Resident.Value with { PersonalWard = 10 });
        fixture.Resident.Replace(fixture.Resident.Value with { Armor = 10 });

        Assert.Equal(0, fixture.Engine.TryAbsorbShieldDamage(fixture.Resident.Value, -5));

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
        fixture.Town.Replace(fixture.Town.Value with { ShieldTicks = 10 });
        fixture.Resident.X = x;
        fixture.Resident.Y = fixture.Town.Y;
        fixture.Resident.Replace(fixture.Resident.Value with { PersonalWard = 0 });
        fixture.Resident.Replace(fixture.Resident.Value with { Armor = 0 });

        Assert.Equal(expectedDamage, fixture.Engine.TryAbsorbShieldDamage(fixture.Resident.Value, 20));
    }
}
