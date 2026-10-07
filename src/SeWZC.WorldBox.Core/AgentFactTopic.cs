using SeWZC.WorldBox.Core.Runtime;
namespace SeWZC.WorldBox.Core;

/// <summary>信息议题的共享行为，供不可变快照和机构报告使用。</summary>
internal abstract class AgentFactTopic
{
    private static readonly AgentFactTopic FoodSupply = new FoodTopic();
    private static readonly AgentFactTopic Danger = new DangerTopic();
    private static readonly AgentFactTopic SettlementLocation = new LocationTopic();
    private static readonly AgentFactTopic ReliefRequest = new ReliefTopic();
    private static readonly AgentFactTopic Policy = new PolicyTopic();
    private static readonly AgentFactTopic Order = new OrderTopic();
    private static readonly AgentFactTopic Culture = new CultureTopic();
    private static readonly AgentFactTopic Research = new ResearchTopic();
    private static readonly AgentFactTopic Personal = new PersonalTopic();
    private static readonly AgentFactTopic DiplomaticNotice = new DiplomacyTopic();
    private static readonly AgentFactTopic WarReport = new WarReportTopic();
    private static readonly AgentFactTopic General = new GeneralTopic();

    // 枚举标识信息议题；议题规则在共享对象中实现。
    internal static AgentFactTopic For(AgentFactKind kind) => kind switch
    {
        AgentFactKind.FoodSupply => FoodSupply,
        AgentFactKind.Danger => Danger,
        AgentFactKind.SettlementLocation => SettlementLocation,
        AgentFactKind.ReliefRequest => ReliefRequest,
        AgentFactKind.Policy => Policy,
        AgentFactKind.WarOrder or AgentFactKind.PeaceOrder => Order,
        AgentFactKind.Culture => Culture,
        AgentFactKind.Research => Research,
        AgentFactKind.Personal => Personal,
        AgentFactKind.TradeExchange => General,
        AgentFactKind.DiplomaticNotice => DiplomaticNotice,
        AgentFactKind.WarReport => WarReport,
        AgentFactKind.WaterSource or AgentFactKind.FoundingSite => General,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    internal virtual double Lifetime => 600;
    internal virtual bool DistinguishesLocation => false;
    internal virtual bool OrdersSameDayById => false;
    internal virtual bool CreatesInstitutionReport => false;
    internal virtual bool PrioritizeMessage(bool relay) => false;
    internal virtual long RetentionBonus(AgentFact fact, int homeId) => 0;
    internal virtual PolicyKind SuggestedPolicy => PolicyKind.PublicHealth;
    internal virtual double Urgency(double value) => Math.Clamp(value, 0, 100);
    internal virtual void Receive(WorldEngine engine, SettlementCursor town, ResidentCursor carrier, AgentFact fact) { }

    private sealed class GeneralTopic : AgentFactTopic;

    private sealed class FoodTopic : AgentFactTopic
    {
        internal override double Lifetime => 180;
        internal override bool CreatesInstitutionReport => true;
        internal override PolicyKind SuggestedPolicy => PolicyKind.FoodSecurity;
        internal override double Urgency(double value) => 100 - Math.Clamp(value, 0, 100);
    }

    private sealed class DangerTopic : AgentFactTopic
    {
        internal override double Lifetime => 24;
        internal override bool DistinguishesLocation => true;
        internal override bool CreatesInstitutionReport => true;
        internal override bool PrioritizeMessage(bool relay) => !relay;
        internal override PolicyKind SuggestedPolicy => PolicyKind.Defense;
    }

    private sealed class LocationTopic : AgentFactTopic
    {
        internal override double Lifetime => 1200;
        internal override long RetentionBonus(AgentFact fact, int homeId) => fact.SubjectId == homeId ? 100000 : 0;
    }

    private sealed class ReliefTopic : AgentFactTopic
    {
        internal override double Lifetime => 180;
        internal override bool CreatesInstitutionReport => true;
        internal override bool PrioritizeMessage(bool relay) => true;
        internal override PolicyKind SuggestedPolicy => PolicyKind.FoodSecurity;
    }

    private sealed class PolicyTopic : AgentFactTopic
    {
        internal override long RetentionBonus(AgentFact fact, int homeId) => 60;
        internal override void Receive(WorldEngine engine, SettlementCursor town, ResidentCursor carrier, AgentFact fact) =>
            engine.ReceivePolicyFact(town, fact);
    }

    private sealed class OrderTopic : AgentFactTopic
    {
        internal override bool OrdersSameDayById => true;
        internal override bool PrioritizeMessage(bool relay) => true;
    }

    private sealed class CultureTopic : AgentFactTopic
    {
        internal override void Receive(WorldEngine engine, SettlementCursor town, ResidentCursor carrier, AgentFact fact) =>
            engine.ReceiveCultureFact(carrier, fact);
    }

    private sealed class ResearchTopic : AgentFactTopic
    {
        internal override bool CreatesInstitutionReport => true;
        internal override bool PrioritizeMessage(bool relay) => true;
        internal override long RetentionBonus(AgentFact fact, int homeId) => 60;
        internal override PolicyKind SuggestedPolicy => PolicyKind.Scholarship;
        internal override double Urgency(double value) => 40;
        internal override void Receive(WorldEngine engine, SettlementCursor town, ResidentCursor carrier, AgentFact fact) =>
            engine.ReceiveResearchFact(town, fact);
    }

    private sealed class PersonalTopic : AgentFactTopic
    {
        internal override bool DistinguishesLocation => true;
        internal override bool CreatesInstitutionReport => true;
    }

    private sealed class DiplomacyTopic : AgentFactTopic
    {
        internal override bool PrioritizeMessage(bool relay) => true;
        internal override void Receive(WorldEngine engine, SettlementCursor town, ResidentCursor carrier, AgentFact fact) =>
            engine.ReceiveDiplomaticNotice(town, fact);
    }

    private sealed class WarReportTopic : AgentFactTopic
    {
        internal override bool PrioritizeMessage(bool relay) => true;
        internal override void Receive(WorldEngine engine, SettlementCursor town, ResidentCursor carrier, AgentFact fact) =>
            engine.ReceiveWarReport(town, fact);
    }
}
