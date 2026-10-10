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
        var patient = fixture.Engine.Residents.Single(person => person.Value.Id != fixture.ResidentId);
        patient.Replace(patient.Value with
        {
            X = 16,
            Y = 16,
            Health = 0,
            DeathCause = DeathCause.Fire,
            DeathTick = 1,
        });
        fixture.Engine.SimulationTick = 1;
        fixture.Resident.Replace(fixture.Resident.Value with { Age = 25 });
        fixture.Resident.Replace(fixture.Resident.Value with { MagicTalent = 60 });
        fixture.Resident.Replace(fixture.Resident.Value with { MagicTraining = 20 });
        fixture.Resident.Replace(fixture.Resident.Value.WithHealth(100));
        fixture.Resident.Replace(fixture.Resident.Value.WithMana(100));
        fixture.Resident.Replace(fixture.Resident.Value with { X = 16 });
        fixture.Resident.Replace(fixture.Resident.Value with { Y = 16 });

        if (publicHealth)
        {
            fixture.Engine.SetPolicy(fixture.Town.Value.NationId, PolicyKind.PublicHealth);
            fixture.Engine.TickSociety();
        }
        else
            Assert.False(fixture.Engine.TryCastSpell(fixture.ResidentId, SpellKind.Heal, 16, 16));

        Assert.Equal(0, patient.Value.Health);
        Assert.Equal(DeathCause.Fire, patient.Value.DeathCause);
        Assert.Equal(100, fixture.Resident.Value.Mana);
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
        foreach (var person in fixture.Engine.Residents)
        {
            person.Replace(person.Value with { Age = 25 });
            person.Replace(person.Value with { X = 16 });
            person.Replace(person.Value with { Y = 16 });
            person.Replace(person.Value with { FrozenUntilTick = 100 });
            person.Replace(person.Value.WithAgent(person.Value.Agent with { Initialized = true, NextThinkTick = 100 }));
        }

        fixture.Resident.Replace(fixture.Resident.Value with { MagicTalent = 60 });
        fixture.Resident.Replace(fixture.Resident.Value with { MagicTraining = 20 });
        fixture.Resident.Replace(fixture.Resident.Value.WithMana(100));
        var patients = fixture.Engine.Residents.Where(person => person.Value.Id != fixture.ResidentId).ToArray();
        patients[0].Replace(patients[0].Value.WithHealth(50));
        patients[1].Replace(patients[1].Value.WithHealth(30));
        if (outsideAutomaticRange)
        {
            patients[1].Replace(patients[1].Value with { X = 18 });
            patients[1].Replace(patients[1].Value with { Y = 18 });
        }

        fixture.Engine.SimulationTick = SimulationTime.WakeTick
                                      + (11 - fixture.ResidentId % 12 - SimulationTime.WakeTick + 24) % 12;

        if (automatic)
            fixture.Engine.Step();
        else
            fixture.Engine.CastSpell(fixture.ResidentId, SpellKind.Heal, 16, 16);

        if (outsideAutomaticRange)
        {
            Assert.True(patients[0].Value.Health > 50);
            Assert.Equal(30 + .15 / SimulationTime.TicksPerDay, patients[1].Value.Health, 8);
        }
        else
        {
            Assert.Equal(50 + (automatic ? .15 / SimulationTime.TicksPerDay : 0), patients[0].Value.Health, 8);
            Assert.True(patients[1].Value.Health > 30);
        }

        Assert.True(fixture.Resident.Value.Mana < 100);
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
        Assert.Equal(remainingWard, fixture.Resident.Value.PersonalWard);
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
        Assert.Equal(3, fixture.Resident.Value.Armor);
    }

    /// <summary>负伤害不会反向增加结界或护甲。</summary>
    [Fact]
    public void Negative_damage_does_not_change_protection()
    {
        var fixture = new WorldFixture();
        fixture.Resident.Replace(fixture.Resident.Value with { PersonalWard = 10 });
        fixture.Resident.Replace(fixture.Resident.Value with { Armor = 10 });

        Assert.Equal(0, fixture.Engine.TryAbsorbShieldDamage(fixture.Resident.Value, -5));

        Assert.Equal(10, fixture.Resident.Value.PersonalWard);
        Assert.Equal(10, fixture.Resident.Value.Armor);
    }

    /// <summary>城镇护盾只保护局部范围内的居民。</summary>
    [Theory]
    [InlineData(21, 12)]
    [InlineData(22, 20)]
    public void Town_shield_applies_only_within_its_range(int x, double expectedDamage)
    {
        var fixture = new WorldFixture();
        fixture.Town.Replace(fixture.Town.Value with { ShieldTicks = 10 });
        fixture.Resident.Replace(fixture.Resident.Value with { X = x });
        fixture.Resident.Replace(fixture.Resident.Value with { Y = fixture.Town.Value.Y });
        fixture.Resident.Replace(fixture.Resident.Value with { PersonalWard = 0 });
        fixture.Resident.Replace(fixture.Resident.Value with { Armor = 0 });

        Assert.Equal(expectedDamage, fixture.Engine.TryAbsorbShieldDamage(fixture.Resident.Value, 20));
    }
}
