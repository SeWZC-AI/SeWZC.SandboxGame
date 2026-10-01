namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private void UpdateArmies()
    {
        if (State.Tick % 30 == 0)
        {
            foreach (var nation in State.Nations.ToArray())
            {
                if (State.Armies.Any(a => a.NationId == nation.Id)) continue;
                var enemy = State.Nations.FirstOrDefault(n => n.Id != nation.Id && GetDiplomacy(nation.Id, n.Id) == DiplomaticStatus.War);
                if (enemy is null) continue;
                var capital = State.Settlements.FirstOrDefault(s => s.Id == nation.CapitalId);
                if (capital is null) continue;
                var recruits = State.Residents.Where(r => r.NationId == nation.Id && r.ArmyId == 0 && r.Age >= 16 && r.Health > 50).ToArray();
                var count = Math.Min(50, recruits.Length / 3);
                if (count < 4) { nation.Decision = "战争中：人口不足，无法征募新军"; continue; }
                var provisions = Math.Min(capital.Resources.Food, count * 12);
                capital.Resources.Food -= provisions;
                var army = new Army { Id = NewId(), NationId = nation.Id, TargetNationId = enemy.Id, X = capital.X, Y = capital.Y, Soldiers = count, Supplies = provisions };
                State.Armies.Add(army);
                foreach (var resident in recruits.Take(count)) { resident.ArmyId = army.Id; resident.Profession = Profession.Soldier; resident.X = army.X; resident.Y = army.Y; }
                nation.Decision = $"与{enemy.Name}交战：征兵消耗劳动力与粮食";
                AddEvent(WorldEventKind.War, $"{nation.Name}征募 {count} 名士兵，向{enemy.Name}进军。", capital.X, capital.Y);
            }
        }
        foreach (var army in State.Armies.ToArray())
        {
            if (!State.Armies.Contains(army)) continue;
            var soldiers = State.Residents.Where(r => r.ArmyId == army.Id && r.Health > 0).ToArray();
            army.Soldiers = soldiers.Length;
            if (soldiers.Length == 0 || !_nations.ContainsKey(army.TargetNationId) || GetDiplomacy(army.NationId, army.TargetNationId) != DiplomaticStatus.War) { DisbandArmy(army); continue; }
            army.Supplies = Math.Max(0, army.Supplies - soldiers.Length * 0.08);
            var home = State.Settlements.Where(s => s.NationId == army.NationId && Distance(s.X, s.Y, army.X, army.Y) <= 6).OrderBy(s => Distance(s.X, s.Y, army.X, army.Y)).FirstOrDefault();
            if (home is not null && army.Supplies < soldiers.Length * 6)
            {
                var amount = Math.Min(home.Resources.Food * 0.1, soldiers.Length * 6 - army.Supplies);
                home.Resources.Food -= amount; army.Supplies += amount;
            }
            army.Morale = Math.Clamp(army.Morale + (army.Supplies > 0 ? 0.15 : -1.2), 0, 100);
            if (army.Supplies == 0) foreach (var soldier in soldiers) soldier.Health -= 0.3;
            if (army.Morale < 15) { AddEvent(WorldEventKind.War, $"{_nations[army.NationId].Name}的军队因补给耗尽而溃散。", army.X, army.Y); DisbandArmy(army); continue; }
            var opponent = State.Armies.FirstOrDefault(a => a.Id != army.Id && GetDiplomacy(army.NationId, a.NationId) == DiplomaticStatus.War && Distance(army.X, army.Y, a.X, a.Y) <= 2);
            if (opponent is not null)
            {
                army.Status = "交战";
                if (State.Tick % 3 == 0) ApplyDamage(State.Residents.Where(r => r.ArmyId == opponent.Id && r.Health > 0), soldiers.Length * 5 * army.Morale / 100);
                continue;
            }
            var target = State.Settlements.Where(s => s.NationId == army.TargetNationId).OrderBy(s => Distance(army.X, army.Y, s.X, s.Y)).FirstOrDefault();
            if (target is null) { DisbandArmy(army); continue; }
            if (_armyTargets.GetValueOrDefault(army.Id) != target.Id)
            {
                _armyPaths.Remove(army.Id); _armyTargets[army.Id] = target.Id;
            }
            if (Distance(army.X, army.Y, target.X, target.Y) <= 1)
            {
                army.Status = "围攻 " + target.Name;
                if (State.Tick % 3 == 0) Siege(army, target, soldiers);
                continue;
            }
            if (!_armyPaths.TryGetValue(army.Id, out var path) || path.Count == 0 && State.Tick % 30 == 0)
            {
                path = FindPath(army.X, army.Y, target.X, target.Y) ?? new Queue<int>();
                _armyPaths[army.Id] = path;
            }
            if (path.Count == 0) { army.Status = "道路受阻"; continue; }
            if (State.Tick % 2 != 0) continue;
            var next = path.Dequeue();
            if (!State.Tiles[next].IsWalkable) { _armyPaths.Remove(army.Id); army.Status = "重新寻路"; continue; }
            army.X = next % State.Width; army.Y = next / State.Width; army.Status = "行军";
            foreach (var soldier in soldiers) { soldier.X = army.X; soldier.Y = army.Y; }
        }
    }

    private static void ApplyDamage(IEnumerable<Resident> residents, double damage)
    {
        foreach (var resident in residents)
        {
            var dealt = Math.Min(resident.Health, damage); resident.Health -= dealt; damage -= dealt;
            if (damage <= 0) break;
        }
    }

    private void Siege(Army army, Settlement target, Resident[] soldiers)
    {
        var defenders = State.Residents.Where(r => r.SettlementId == target.Id && r.ArmyId == 0 && r.Age >= 14 && r.Health > 0).ToArray();
        if (defenders.Length == 0) { CaptureSettlement(army, target); return; }
        var attack = soldiers.Length * (6 + _nations[army.NationId].Technology) * army.Morale / 100;
        var defense = defenders.Length * 1.7 * (1 + target.Level * 0.1);
        ApplyDamage(defenders, attack); ApplyDamage(soldiers, defense);
        army.Morale = Math.Max(0, army.Morale - 0.2);
        if (defenders.Count(r => r.Health > 0) < Math.Max(2, soldiers.Length / 3)) CaptureSettlement(army, target);
    }

    private void CaptureSettlement(Army army, Settlement town)
    {
        var previous = town.NationId;
        if (!_nations.TryGetValue(previous, out var previousNation)) return;
        TransferSettlementOwnership(town, army.NationId);
        foreach (var index in Circle(town.X, town.Y, 15))
            if (State.Tiles[index].NationId == previous && (State.Tiles[index].SettlementId == 0 || State.Tiles[index].SettlementId == town.Id)) State.Tiles[index].NationId = army.NationId;
        ClaimTerritory(town, 8);
        AddEvent(WorldEventKind.War, $"{_nations[army.NationId].Name}占领了{previousNation.Name}的{town.Name}。", town.X, town.Y);
        _armyPaths.Remove(army.Id);
        Reindex(); RemoveEmptyNations();
    }

    private void DisbandArmy(Army army)
    {
        foreach (var soldier in State.Residents.Where(r => r.ArmyId == army.Id))
        {
            soldier.ArmyId = 0; soldier.Profession = AssignProfession();
            if (_settlements.TryGetValue(soldier.SettlementId, out var home)) { soldier.X = home.X; soldier.Y = home.Y; }
        }
        State.Armies.Remove(army); _armyPaths.Remove(army.Id); _armyTargets.Remove(army.Id);
    }

    private Queue<int>? FindPath(int startX, int startY, int endX, int endY)
    {
        if (!Walkable(startX, startY) || !Walkable(endX, endY)) return null;
        var start = Index(startX, startY); var goal = Index(endX, endY);
        if (start == goal) return new Queue<int>();
        var previous = new int[State.Tiles.Length]; Array.Fill(previous, -1);
        var queue = new Queue<int>(); queue.Enqueue(start); previous[start] = start;
        while (queue.TryDequeue(out var current))
        {
            var x = current % State.Width; var y = current / State.Width;
            foreach (var (dx, dy) in Directions)
            {
                var xx = x + dx; var yy = y + dy;
                if (!Walkable(xx, yy)) continue;
                var next = Index(xx, yy); if (previous[next] != -1) continue;
                previous[next] = current;
                if (next == goal)
                {
                    var path = new List<int>();
                    for (var i = goal; i != start; i = previous[i]) path.Add(i);
                    path.Reverse(); return new Queue<int>(path);
                }
                queue.Enqueue(next);
            }
        }
        return null;
    }

    private void RemoveSettlement(Settlement settlement, string reason)
    {
        AddEvent(WorldEventKind.Death, $"{settlement.Name}：{reason}。", settlement.X, settlement.Y);
        var destination = State.Settlements.FirstOrDefault(s => s.Id != settlement.Id && s.NationId == settlement.NationId);
        foreach (var resident in State.Residents.Where(r => r.SettlementId == settlement.Id).ToArray())
        {
            if (destination is null) { State.Residents.Remove(resident); continue; }
            resident.SettlementId = destination.Id; resident.X = destination.X; resident.Y = destination.Y;
        }
        var tile = State.Tiles[Index(settlement.X, settlement.Y)]; if (tile.SettlementId == settlement.Id) tile.SettlementId = 0;
        State.Settlements.Remove(settlement); _settlements.Remove(settlement.Id); _citizens.Remove(settlement.Id);
        State.TradeRoutes.RemoveAll(r => r.FromSettlementId == settlement.Id || r.ToSettlementId == settlement.Id);
        if (_nations.TryGetValue(settlement.NationId, out var nation) && nation.CapitalId == settlement.Id) nation.CapitalId = destination?.Id ?? 0;
    }

    private void RemoveEmptyNations()
    {
        foreach (var nation in State.Nations.Where(n => !State.Settlements.Any(s => s.NationId == n.Id)).ToArray())
        {
            AddEvent(WorldEventKind.War, $"{nation.Name}失去了最后的聚落，退出历史舞台。");
            State.Nations.Remove(nation); _nations.Remove(nation.Id);
            State.Diplomacies.RemoveAll(r => r.FirstNationId == nation.Id || r.SecondNationId == nation.Id);
            foreach (var tile in State.Tiles) if (tile.NationId == nation.Id) tile.NationId = 0;
            foreach (var army in State.Armies.Where(a => a.NationId == nation.Id || a.TargetNationId == nation.Id).ToArray()) DisbandArmy(army);
        }
    }
}
