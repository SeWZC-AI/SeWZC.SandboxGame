namespace SeWZC.WorldBox.Core.Tests;

/// <summary>同一步中重复文化报告及转化后接触记录的即时读取。</summary>
public sealed class CultureContactTests
{
    /// <summary>接触程度即时更新，转化后复评读取重置后的值，重复报告不能绕过冷却。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Received_reports_read_current_contact_after_update_or_conversion(bool convert)
    {
        var fixture = new WorldFixture();
        var engine = fixture.Engine;
        engine.ConfigureWorld(engine.State.Rules with
        {
            Births = false,
            Aging = false,
            Hunger = false,
            Thirst = false,
            Disease = false,
            Construction = false,
            Expansion = false,
            Research = false,
            Wars = false,
        }, false, false);
        var ownCulture = fixture.Resident.CultureId;
        var foreign = engine.Current.Society.Cultures.First() with { Id = engine.Current.NextId++, Name = "邻村文化" };
        var initialExposure = convert ? 9d : 1;
        engine.Current.Society = engine.Current.Society with
        {
            Cultures = engine.Current.Society.Cultures.Add(foreign),
            CulturalContacts =
            [
                new CulturalContact
                {
                    ResidentId = fixture.ResidentId,
                    CultureId = foreign.Id,
                    Exposure = initialExposure,
                    LastContactTick = -12,
                },
                new CulturalContact
                {
                    ResidentId = fixture.ResidentId, CultureId = ownCulture, Exposure = 3, LastContactTick = -12,
                },
            ],
        };
        engine.Current.Tick = SimulationTime.WakeTick;
        fixture.Resident.X = fixture.Resident.Y = 16;
        fixture.Resident.Replace(fixture.Resident.Value with { FrozenUntilTick = 10 });
        fixture.Resident.Agent = fixture.Resident.Agent with
        {
            NextThinkTick = 100, Personality = fixture.Resident.Agent.Personality with { Sociability = .75 },
        };
        engine.Current.PendingMessages = engine.Current.PendingMessages.Add(new PendingMessage
        {
            SenderId = fixture.ResidentId,
            RecipientId = fixture.ResidentId,
            DeliverTick = SimulationTime.WakeTick + 1,
            Facts = [Report(foreign.Id), Report(foreign.Id), Report(ownCulture)],
        });
        var before = engine.State;

        engine.Step();

        var contacts = engine.State.Society.CulturalContacts;
        Assert.Equal(convert ? foreign.Id : ownCulture, fixture.Resident.CultureId);
        Assert.Equal(convert ? 0 : initialExposure + .5 + fixture.Resident.Agent.Personality.Sociability,
            contacts[0].Exposure, 8);
        Assert.Equal(convert ? 0 : 3, contacts[1].Exposure);
        Assert.Equal(SimulationTime.WakeTick + 1, contacts[0].LastContactTick);
        Assert.Equal(initialExposure, before.Society.CulturalContacts[0].Exposure);
        Assert.Equal(engine.ExportJson(), WorldEngine.ImportJson(engine.ExportJson()).ExportJson());

        AgentFact Report(int cultureId)
        {
            return new AgentFact
            {
                Id = engine.Current.NextId++,
                Kind = AgentFactKind.Culture,
                SubjectId = fixture.Town.Value.Id,
                X = 16,
                Y = 16,
                Value = cultureId,
                ObservedTick = SimulationTime.WakeTick,
                LearnedTick = SimulationTime.WakeTick,
                OriginResidentId = fixture.ResidentId,
                SourceResidentId = fixture.ResidentId,
                OriginProfession = fixture.Resident.Profession,
            };
        }
    }
}
