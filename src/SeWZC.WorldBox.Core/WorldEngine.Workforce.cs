namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private void BalanceLocalWorkforce()
    {
        foreach (var town in State.Settlements)
        {
            if (town.FoundationPending || !_citizens.TryGetValue(town.Id, out var citizens)) continue;
            var adults = citizens.Where(r => r.Age >= 16 && r.Health > 0 && r.ArmyId == 0).OrderBy(r => r.Id).ToArray();
            var messengerLimit = adults.Length < 16 ? 0 : Math.Max(1, (adults.Length + 79) / 80);
            var retained = 0;
            foreach (var person in adults)
            {
                if (person.Profession == Profession.Messenger && ++retained > messengerLimit && Available(person)) Change(person, Profession.Farmer);
                if (person.Profession == Profession.Representative && person.Id != town.RepresentativeId && Available(person)) Change(person, Profession.Farmer);
            }
            if (adults.Count(r => r.Profession == Profession.Messenger) < messengerLimit)
            {
                var recruit = adults.FirstOrDefault(r => r.Profession == Profession.Farmer && Available(r));
                if (recruit is not null) Change(recruit, Profession.Messenger);
            }
            var coastal = Circle(town.X, town.Y, 6).Any(i => EdibleAnimal(State.Tiles[i], aquatic: true) != WildlifeKind.None);
            if (coastal && adults.Length >= 8 && adults.All(r => r.Profession != Profession.Fisher))
            {
                var recruit = adults.FirstOrDefault(r => r.Profession == Profession.Farmer && Available(r));
                if (recruit is not null) Change(recruit, Profession.Fisher);
            }
        }
        bool Available(Resident person) => !person.Agent.Goal.PlayerDirected && person.Agent.DestinationSettlementId == 0
            && person.TravelMode == TravelMode.Foot && (State.Tick == 0 || State.Tick - person.Agent.JobChangedTick >= 120);
        void Change(Resident person, Profession profession)
        {
            person.Profession = profession; person.Agent.JobChangedTick = State.Tick;
            person.Agent.Goal = new() { Kind = AgentGoalKind.Idle, TargetX = person.X, TargetY = person.Y };
            person.Agent.NextThinkTick = State.Tick;
        }
    }
}
