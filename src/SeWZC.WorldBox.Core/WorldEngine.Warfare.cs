using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private bool IsKnownHostile(ResidentCursor resident, int targetNationId)
    {
        var order = resident.Agent.Memory.Where(f =>
                (f.TargetNationId == 0 || f.TargetNationId == resident.NationId) && f.SubjectId == targetNationId &&
                f.Kind is AgentFactKind.WarOrder or AgentFactKind.PeaceOrder)
            .OrderByDescending(f => f.ObservedTick).ThenByDescending(f => f.Id).FirstOrDefault();
        if (order is not null)
            return order.Kind == AgentFactKind.WarOrder;
        return Current.Armies.Any(a =>
            a.Id == resident.ArmyId && a.TargetNationId == targetNationId && a.KnownDiplomacy == DiplomaticStatus.War);
    }

    private void PublishDiplomaticOrder(int nationId, int enemyId, DiplomaticStatus status, int? knownX = null,
        int? knownY = null, long? observedTick = null, int targetSettlementId = 0, int eventId = 0,
        WarObjective objective = WarObjective.OccupySettlement)
    {
        var capital = Current.Settlements.FirstOrDefault(t => t.Id == _nations[nationId].CapitalId);
        var enemyCapital = Current.Settlements.FirstOrDefault(t => t.Id == _nations[enemyId].CapitalId);
        if (capital is null || enemyCapital is null)
            return;
        if (objective == WarObjective.DefendHomeland)
        {
            knownX = capital.X;
            knownY = capital.Y;
            targetSettlementId = capital.Id;
        }

        var witness =
            Current.Residents.FirstOrDefault(r =>
                r.NationId == nationId && Distance(r.X, r.Y, capital.X, capital.Y) <= 4);
        var fact = new AgentFact
        {
            Id = NewId(),
            EventId = eventId,
            CampaignEventId = eventId,
            WarObjective = objective,
            Kind = status == DiplomaticStatus.War ? AgentFactKind.WarOrder : AgentFactKind.PeaceOrder,
            SubjectId = enemyId,
            TargetNationId = nationId,
            X = knownX ?? enemyCapital.X,
            Y = knownY ?? enemyCapital.Y,
            Value = targetSettlementId > 0 ? targetSettlementId : knownX.HasValue ? 0 : enemyCapital.Id,
            ObservedTick = observedTick ?? Current.Tick,
            LearnedTick = Current.Tick,
            OriginResidentId = witness?.Id ?? 0,
            SourceResidentId = witness?.Id ?? 0,
            Text = status == DiplomaticStatus.War ? "首都宣布开战，征召当地志愿者" : "首都宣布停止敌对，前线须等待消息送达",
        };
        var military = _nations[nationId].Military;
        if (status == DiplomaticStatus.War)
            military.Replace(military.Value with
            {
                CampaignEventId = eventId,
                EnemyNationId = enemyId,
                Objective = objective,
                TargetSettlementId = (int)fact.Value,
                TargetX = fact.X,
                TargetY = fact.Y,
                StartedTick = Current.Tick,
                ReportedOutcome = WarOutcome.None,
                LastReportEventId = 0,
                LastReportObservedTick = 0,
                LastReportReceivedTick = 0,
                Report = "尚未收到前线战报",
            });
        else
            military.RecoveryUntilTick = Math.Max(military.RecoveryUntilTick, Current.Tick + 360);

        capital.PublicKnowledge.RemoveAll(f =>
            f.SubjectId == enemyId && f.Kind is AgentFactKind.WarOrder or AgentFactKind.PeaceOrder);
        AddPublicFact(capital, fact);
        if (capital.PublicKnowledge.Count > 24)
            capital.PublicKnowledge.RemoveAt(0);
        foreach (var person in Current.Residents.Where(r =>
                     r.NationId == nationId && Distance(r.X, r.Y, capital.X, capital.Y) <= 4))
            RememberAgentFact(person, fact);
    }

    private void UpdateArmies()
    {
        if (Current.Tick % 30 == 0)
        {
            foreach (var nation in Current.Nations.ToArray())
            {
                if (Current.Armies.Any(a => a.NationId == nation.Id))
                    continue;
                var capital = Current.Settlements.FirstOrDefault(s => s.Id == nation.CapitalId);
                if (capital is null)
                    continue;
                var order = capital.PublicKnowledge.Where(f =>
                        f.SubjectId != nation.Id && f.SubjectId > 0 &&
                        (f.TargetNationId == 0 || f.TargetNationId == nation.Id) &&
                        f.Kind is AgentFactKind.WarOrder or AgentFactKind.PeaceOrder)
                    .OrderByDescending(f => f.ObservedTick).ThenByDescending(f => f.Id).FirstOrDefault();
                if (order is null || order.Kind != AgentFactKind.WarOrder ||
                    order.Id <= nation.Military.LastMobilizedOrderId
                    || (order.WarObjective == WarObjective.OccupySettlement &&
                        Current.Tick < nation.Military.RecoveryUntilTick))
                    continue;
                var recruits = Current.Residents.Where(r =>
                    r.NationId == nation.Id && r.ArmyId == 0 && r.Age >= 16 && r.Health > 50 &&
                    r.TravelMode == TravelMode.Foot
                    && Distance(r.X, r.Y, capital.X, capital.Y) <= 5).ToArray();
                var count = Math.Min(40, recruits.Length / 2);
                if (count < 3)
                {
                    nation.Decision = "当地兵员尚未集结，等待居民返回聚落";
                    continue;
                }

                var provisions = Math.Min(capital.Resources.Food, count * 10);
                capital.Resources = capital.Resources with { Food = capital.Resources.Food - provisions };
                var waterProvisions = Math.Min(capital.Resources.Water, count * 3);
                capital.Resources = capital.Resources with { Water = capital.Resources.Water - waterProvisions };
                nation.Military.LastMobilizedOrderId = order.Id;
                var army = new ArmyCursor
                {
                    Id = NewId(),
                    CampaignEventId = order.CampaignEventId,
                    Objective = order.WarObjective,
                    InitialSoldiers = count,
                    StartedTick = Current.Tick,
                    NationId = nation.Id,
                    TargetNationId = order.SubjectId,
                    TargetX = order.X,
                    TargetY = order.Y,
                    TargetSettlementId = (int)order.Value,
                    X = capital.X,
                    Y = capital.Y,
                    FromX = capital.X,
                    FromY = capital.Y,
                    Soldiers = count,
                    Supplies = provisions,
                    WaterSupplies = waterProvisions,
                    CommanderId = recruits[0].Id,
                    LastOrderTick = order.ObservedTick,
                    LastOrderFactId = order.Id,
                };
                Current.Armies.Add(army);
                foreach (var resident in recruits.Take(count))
                {
                    resident.ArmyId = army.Id;
                    if (resident.Profession is not (Profession.Ranger or Profession.Battlemage))
                        resident.Profession = Profession.Soldier;
                    resident.Agent.Goal = new AgentGoal
                    {
                        Kind = AgentGoalKind.March,
                        TargetX = capital.X,
                        TargetY = capital.Y,
                        StartedTick = Current.Tick,
                        Reason = "听到当地征召，步行前往集结点",
                    };
                    RememberAgentFact(resident, order);
                }

                nation.Decision = "根据首都已知命令征召当地居民，军队需要实地集结";
                var recruitmentEvent = AddEvent(WorldEventKind.War, $"{nation.Name}在首都征募 {count} 名居民，开始集结。", capital.X,
                    capital.Y, EventAction.Muster, capital.Id, causeEventId: order.EventId);
                army.LastEventId = recruitmentEvent.Id;
                foreach (var recruit in recruits.Take(count))
                    RecordLife(recruit, "响应当地征召，开始" + ObjectiveName(army.Objective) + "任务。", recruitmentEvent);
                recruitmentEvent.SecondNationId = army.TargetNationId;
            }
        }

        foreach (var army in Current.Armies.ToArray())
        {
            if (!Current.Armies.Contains(army))
                continue;
            var soldiers = Current.Residents.Where(r => r.ArmyId == army.Id && r.Health > 0).ToArray();
            army.Soldiers = soldiers.Length;
            if (soldiers.Length == 0)
            {
                DisbandArmy(army);
                continue;
            }

            var commander = soldiers.FirstOrDefault(r => r.Id == army.CommanderId) ?? soldiers[0];
            army.CommanderId = commander.Id;
            // 离队士兵携带的军令须通过本地通信传给指挥官，避免军队即时共享信息。
            var received = commander.Agent.Memory
                .Where(f => (f.TargetNationId == 0 || f.TargetNationId == army.NationId) &&
                            f.SubjectId == army.TargetNationId &&
                            f.Kind is AgentFactKind.WarOrder or AgentFactKind.PeaceOrder)
                .OrderByDescending(f => f.ObservedTick).ThenByDescending(f => f.Id).FirstOrDefault();
            if (received is not null && (received.ObservedTick > army.LastOrderTick ||
                                         (received.ObservedTick == army.LastOrderTick &&
                                          received.Id > army.LastOrderFactId)))
            {
                army.Replace(army.Value with
                {
                    LastOrderTick = received.ObservedTick,
                    LastOrderFactId = received.Id,
                    KnownDiplomacy = received.Kind == AgentFactKind.WarOrder
                        ? DiplomaticStatus.War
                        : DiplomaticStatus.Neutral,
                });
                if (received.Kind == AgentFactKind.PeaceOrder)
                    EndCampaign(army, WarOutcome.OrdersReceived, soldiers, received.EventId);
                else if (army.Outcome is WarOutcome.None or WarOutcome.OrdersReceived)
                {
                    // 新递送的军令可替代停战命令，但不能取消损失、疲劳、补给或目标已完成导致的撤退。
                    army.Replace(army.Value with { Outcome = WarOutcome.None, Retreating = false });
                    if (received.CampaignEventId > 0 && received.CampaignEventId != army.CampaignEventId)
                        army.Replace(army.Value with
                        {
                            CampaignEventId = received.CampaignEventId,
                            LastEventId = received.EventId,
                            Objective = received.WarObjective,
                            TargetSettlementId = (int)received.Value,
                            TargetX = received.X,
                            TargetY = received.Y,
                            BattleRecorded = false,
                        });
                }
            }

            foreach (var soldier in soldiers)
            {
                if (Current.Rules.Thirst && Distance(soldier.X, soldier.Y, army.X, army.Y) <= 2 &&
                    soldier.Inventory.Water < .025)
                {
                    var water = Math.Min(army.WaterSupplies, .125);
                    army.WaterSupplies -= water;
                    soldier.Inventory = soldier.Inventory with { Water = soldier.Inventory.Water + water };
                }

                DrinkCarriedWater(soldier);
                if (!Current.Rules.Hunger)
                    soldier.Hunger = 0;
                else if (Distance(soldier.X, soldier.Y, army.X, army.Y) <= 2 && army.Supplies >= 0.06)
                {
                    army.Supplies -= 0.06;
                    soldier.Hunger = Math.Max(0, soldier.Hunger - 3);
                }
                else if (soldier.Inventory.Food >= 0.05)
                    soldier.Replace(soldier.Value with
                    {
                        Inventory = soldier.Inventory with { Food = soldier.Inventory.Food - 0.05 },
                        Hunger = Math.Max(0, soldier.Hunger - 3),
                    });
                else
                    soldier.Hunger = Math.Min(100, soldier.Hunger + .8);

                if (Current.Rules.Hunger && soldier.Hunger > 80)
                    DamageResident(soldier, .30, DeathCause.Starvation);
            }

            var depot = Current.Settlements.FirstOrDefault(s =>
                s.NationId == army.NationId && Distance(s.X, s.Y, army.X, army.Y) <= 1);
            if (depot is not null && army.Supplies < soldiers.Length * 5)
            {
                var amount = Math.Min(depot.Resources.Food, soldiers.Length * 5 - army.Supplies);
                depot.Resources = depot.Resources with { Food = depot.Resources.Food - amount };
                army.Supplies += amount;
            }

            if (depot is not null && army.WaterSupplies < soldiers.Length)
            {
                var water = Math.Min(depot.Resources.Water, soldiers.Length * 3 - army.WaterSupplies);
                depot.Resources = depot.Resources with { Water = depot.Resources.Water - water };
                army.WaterSupplies += water;
            }

            army.Morale = Math.Clamp(army.Morale + (army.Supplies > 0 ? 0.15 : -1.2), 0, 100);
            if (army.Outcome == WarOutcome.None)
            {
                if (army.InitialSoldiers > 0 && soldiers.Length * 5 <= army.InitialSoldiers * 3)
                    EndCampaign(army, WarOutcome.HeavyLosses, soldiers);
                else if ((Current.Rules.Thirst && army.WaterSupplies <= 0 && soldiers.Average(r => r.Thirst) > 60) ||
                         army.Morale < 15 ||
                         (Current.Rules.Hunger && army.Supplies <= 0 && soldiers.Average(r => r.Hunger) > 40))
                    EndCampaign(army, WarOutcome.SupplyShortage, soldiers);
                else if (army.BlockedTicks >= 120)
                    EndCampaign(army, WarOutcome.RouteBlocked, soldiers);
                else if (Current.Tick - army.StartedTick >= 720)
                    EndCampaign(army, WarOutcome.Exhausted, soldiers);
            }

            if (army.Gathering && !army.Retreating)
            {
                foreach (var soldier in soldiers)
                    MoveAgentTowards(soldier, army.X, army.Y);
                army.Status = "实地集结";
                if (soldiers.Count(r => Distance(r.X, r.Y, army.X, army.Y) <= 1) >=
                    Math.Max(2, soldiers.Length * 3 / 4))
                    army.Gathering = false;
                continue;
            }

            if (army.Retreating)
            {
                var home = _settlements.GetValueOrDefault(commander.SettlementId);
                if (home is null)
                {
                    DisbandArmy(army);
                    continue;
                }

                army.Status = OutcomeName(army.Outcome) + "，正在返乡";
                if (Distance(commander.X, commander.Y, home.X, home.Y) <= 1)
                {
                    DisbandArmy(army);
                    continue;
                }

                MoveArmy(army, commander, soldiers, home.X, home.Y);
                continue;
            }

            var opponent = Current.Armies.FirstOrDefault(a =>
                a.Id != army.Id && a.NationId == army.TargetNationId && Distance(army.X, army.Y, a.X, a.Y) <= 2);
            foreach (var ranger in soldiers.Where(p => p.Profession == Profession.Ranger))
            {
                if (depot is not null && Distance(ranger.X, ranger.Y, depot.X, depot.Y) <= 1)
                {
                    var ammo = Math.Min(depot.Resources.Ammunition, Math.Max(0, 8 - ranger.Inventory.Ammunition));
                    depot.Resources = depot.Resources with { Ammunition = depot.Resources.Ammunition - ammo };
                    ranger.Inventory = ranger.Inventory with { Ammunition = ranger.Inventory.Ammunition + ammo };
                }

                var enemy = LocalHostile(ranger, ranger.X, ranger.Y, 4);
                if (enemy is not null && RangedAttackError(ranger.Id, enemy.Id) is null)
                    RangedAttack(ranger.Id, enemy.Id);
            }

            if (opponent is not null)
            {
                army.Status = "交战";
                RecordBattle(army, soldiers);
                if (Current.Tick % 3 == 0)
                {
                    ApplyDamage(
                        Current.Residents.Where(r =>
                            r.ArmyId == opponent.Id && r.Health > 0 && Distance(r.X, r.Y, army.X, army.Y) <= 3),
                        soldiers.Count(r => Distance(r.X, r.Y, army.X, army.Y) <= 2) * 5 * army.Morale / 100);
                }

                continue;
            }

            if (army.Objective == WarObjective.DefendHomeland)
            {
                var intruder = Current.Armies.FirstOrDefault(a => a.NationId == army.TargetNationId
                                                                  && Distance(army.X, army.Y, a.X, a.Y) <= 6 &&
                                                                  Distance(a.X, a.Y, army.TargetX, army.TargetY) <= 8);
                army.Status = "保卫家园，依据当地观察巡守";
                if (intruder is not null)
                    MoveArmy(army, commander, soldiers, intruder.X, intruder.Y);
                else if (Distance(army.X, army.Y, army.TargetX, army.TargetY) > 1)
                    MoveArmy(army, commander, soldiers, army.TargetX, army.TargetY);
                continue;
            }

            if (Distance(army.X, army.Y, army.TargetX, army.TargetY) <= 1)
            {
                var visibleTarget = Current.Settlements.FirstOrDefault(t => t.NationId == army.TargetNationId
                                                                            && (army.TargetSettlementId == 0 ||
                                                                                t.Id == army.TargetSettlementId)
                                                                            && Distance(t.X, t.Y, army.TargetX,
                                                                                army.TargetY) <= 1);
                if (visibleTarget is null)
                {
                    EndCampaign(army, WarOutcome.TargetChanged, soldiers);
                    continue;
                }

                army.Replace(army.Value with
                {
                    TargetSettlementId = visibleTarget.Id, Status = "围攻既定目标 " + visibleTarget.Name,
                });
                if (Current.Tick % 3 == 0)
                    Siege(army, visibleTarget, soldiers.Where(r => Distance(r.X, r.Y, army.X, army.Y) <= 2).ToArray());
                continue;
            }

            army.Status = "按已知军令行军";
            MoveArmy(army, commander, soldiers, army.TargetX, army.TargetY);
        }
    }

    private void MoveArmy(ArmyCursor army, ResidentCursor commander, ResidentCursor[] soldiers, int x, int y)
    {
        var previousX = commander.X;
        var previousY = commander.Y;
        MoveAgentTowards(commander, x, y);
        army.Replace(army.Value with
        {
            BlockedTicks = commander.X == previousX && commander.Y == previousY ? army.BlockedTicks + 1 : 0,
            FromX = commander.FromX,
            FromY = commander.FromY,
            X = commander.X,
            Y = commander.Y,
            MoveStartedTick = commander.MoveStartedTick,
            MoveDurationTicks = commander.MoveDurationTicks,
        });
        foreach (var soldier in soldiers)
        {
            if (soldier.Id != commander.Id)
                MoveAgentTowards(soldier, commander.X, commander.Y);
            soldier.Replace(soldier.Value with
            {
                Activity = ResidentActivity.Marching,
                Agent = soldier.Agent.Value with
                {
                    Goal = soldier.Agent.Goal with
                    {
                        Kind = AgentGoalKind.March, TargetX = x, TargetY = y, Reason = army.Status,
                    },
                },
            });
        }
    }

    private void ApplyDamage(IEnumerable<ResidentCursor> residents, double damage)
    {
        damage *= Current.Rules.CombatDamageRate;
        foreach (var resident in residents)
        {
            EmitVisual(WorldVisualKind.Battle, resident.X, resident.Y);
            var incoming = Math.Min(resident.Health, damage);
            var dealt = TryAbsorbShieldDamage(resident, incoming);
            DamageResident(resident, dealt, DeathCause.Battle);
            damage -= incoming;
            if (damage <= 0)
                break;
        }
    }

    private void Siege(ArmyCursor army, SettlementCursor target, ResidentCursor[] soldiers)
    {
        var defenders = Current.Residents.Where(r =>
            r.SettlementId == target.Id && r.ArmyId == 0 && r.Age >= 14 && r.Health > 0 &&
            Distance(r.X, r.Y, target.X, target.Y) <= 5).ToArray();
        if (defenders.Length == 0)
        {
            CaptureSettlement(army, target);
            return;
        }

        RecordBattle(army, soldiers, defenders);
        var attack = soldiers.Length * (6 + _nations[army.NationId].Technology) * army.Morale / 100;
        var defense = defenders.Length * 1.7;
        ApplyDamage(defenders, attack);
        ApplyDamage(soldiers, defense);
        army.Morale = Math.Max(0, army.Morale - 0.2);
        if (defenders.Count(r => r.Health > 0) < Math.Max(2, soldiers.Length / 3))
            CaptureSettlement(army, target);
    }

    private void CaptureSettlement(ArmyCursor army, SettlementCursor town)
    {
        var previous = town.NationId;
        if (!_nations.TryGetValue(previous, out var previousNation))
            return;
        TransferSettlementOwnership(town, army.NationId);
        foreach (var index in Circle(town.X, town.Y, 15))
            if (Current.Tiles[index].NationId == previous &&
                (Current.Tiles[index].SettlementId == 0 || Current.Tiles[index].SettlementId == town.Id))
                Current.Tiles[index].NationId = army.NationId;
        ClaimTerritory(town, 8);
        var occupationEvent = AddEvent(WorldEventKind.War,
            $"{_nations[army.NationId].Name}占领了{previousNation.Name}的{town.Name}。", town.X, town.Y, EventAction.Capture,
            town.Id, causeEventId: army.LastEventId);
        occupationEvent.SecondNationId = previous;
        EndCampaign(army, WarOutcome.ObjectiveReached,
            Current.Residents.Where(r => r.ArmyId == army.Id && r.Health > 0).ToArray(), occupationEvent.Id);
        _armyPaths.Remove(army.Id);
        Reindex();
        RemoveEmptyNations();
    }

    private void DisbandArmy(ArmyCursor army)
    {
        var veterans = Current.Residents.Where(r => r.ArmyId == army.Id).ToArray();
        var localDepot =
            Current.Settlements.FirstOrDefault(s =>
                s.NationId == army.NationId && Distance(s.X, s.Y, army.X, army.Y) <= 1);
        if (localDepot is not null)
        {
            localDepot.Resources = localDepot.Resources with
            {
                Food = localDepot.Resources.Food + army.Supplies,
                Water = localDepot.Resources.Water + army.WaterSupplies,
            };
            foreach (var veteran in veterans.Where(r => Distance(r.X, r.Y, localDepot.X, localDepot.Y) <= 2))
            foreach (var report in veteran.Agent.Memory.Where(f => f.Kind == AgentFactKind.WarReport).ToArray())
                ReceiveWarReport(localDepot, report);
        }
        else
        {
            var nearby = veterans.Where(r => Distance(r.X, r.Y, army.X, army.Y) <= 2).ToArray();
            foreach (var veteran in nearby)
                veteran.Inventory = veteran.Inventory with
                {
                    Food = Math.Min(1_000_000, veteran.Inventory.Food + army.Supplies / nearby.Length),
                    Water = Math.Min(1_000_000, veteran.Inventory.Water + army.WaterSupplies / nearby.Length),
                };
        }

        army.Replace(army.Value with { Supplies = 0, WaterSupplies = 0 });
        WorldEventCursor? homecoming = null;
        if (veterans.Length > 0)
        {
            homecoming = AddEvent(WorldEventKind.War, "军队解散，幸存居民恢复生活；未抵家者继续步行返乡。", army.X, army.Y,
                EventAction.Homecoming, localDepot?.Id ?? 0, causeEventId: army.LastEventId);
            homecoming.Replace(homecoming.Value with
            {
                NationId = army.NationId, SecondNationId = army.TargetNationId,
            });
        }

        foreach (var soldier in veterans)
        {
            RecordLife(soldier, "结束军旅任务，恢复平民生活。", homecoming);
            soldier.ArmyId = 0;
            if (soldier.Profession is not (Profession.Ranger or Profession.Battlemage))
                soldier.Profession = AssignProfession();
            if (_settlements.TryGetValue(soldier.SettlementId, out var home))
            {
                soldier.Agent.Goal = new AgentGoal
                {
                    Kind = AgentGoalKind.ReturnHome,
                    TargetX = home.X,
                    TargetY = home.Y,
                    TargetSettlementId = home.Id,
                    StartedTick = Current.Tick,
                    ReviewTick = Current.Tick + 200,
                    PlayerDirected = true,
                    Reason = "退伍后步行返回家园",
                };
            }
        }

        Current.Armies.Remove(army);
        _armyPaths.Remove(army.Id);
        _armyTargets.Remove(army.Id);
    }

    private Queue<int>? FindPath(int startX, int startY, int endX, int endY)
    {
        if (!Walkable(startX, startY) || !Walkable(endX, endY))
            return null;
        var start = Index(startX, startY);
        var goal = Index(endX, endY);
        if (start == goal)
            return new Queue<int>();
        var previous = new int[Current.Tiles.Count];
        Array.Fill(previous, -1);
        var queue = new Queue<int>();
        queue.Enqueue(start);
        previous[start] = start;
        while (queue.TryDequeue(out var current))
        {
            var x = current % Current.Width;
            var y = current / Current.Width;
            foreach (var (dx, dy) in Directions)
            {
                var xx = x + dx;
                var yy = y + dy;
                if (!CanTraverseStep(x, y, xx, yy, TravelMode.Foot))
                    continue;
                var next = Index(xx, yy);
                if (previous[next] != -1)
                    continue;
                previous[next] = current;
                if (next == goal)
                {
                    var path = new List<int>();
                    for (var i = goal; i != start; i = previous[i])
                        path.Add(i);
                    path.Reverse();
                    return new Queue<int>(path);
                }

                queue.Enqueue(next);
            }
        }

        return null;
    }

    private void RemoveSettlement(SettlementCursor settlement, string reason)
    {
        AddEvent(WorldEventKind.Death, $"{settlement.Name}：{reason}。", settlement.X, settlement.Y);
        var destination =
            Current.Settlements.FirstOrDefault(s => s.Id != settlement.Id && s.NationId == settlement.NationId);
        foreach (var resident in Current.Residents.Where(r => r.SettlementId == settlement.Id).ToArray())
        {
            if (destination is null)
            {
                Current.Residents.Remove(resident);
                continue;
            }

            resident.Replace(
                resident.Value with { SettlementId = destination.Id, X = destination.X, Y = destination.Y });
        }

        var tile = Current.Tiles[Index(settlement.X, settlement.Y)];
        if (tile.SettlementId == settlement.Id)
            tile.SettlementId = 0;
        Current.Conflicts.RemoveAll(c => c.SettlementId == settlement.Id);
        foreach (var ground in Current.Tiles)
            if (ground.ClaimedSettlementId == settlement.Id)
                ground.Replace(ground.Value with { ClaimedSettlementId = 0, NationId = 0 });

        Current.Settlements.Remove(settlement);
        _settlements.Remove(settlement.Id);
        _citizens.Remove(settlement.Id);
        if (_nations.TryGetValue(settlement.NationId, out var nation) && nation.CapitalId == settlement.Id)
            nation.CapitalId = destination?.Id ?? 0;
    }

    private void RemoveEmptyNations()
    {
        foreach (var nation in Current.Nations.Where(n => !Current.Settlements.Any(s => s.NationId == n.Id)).ToArray())
        {
            AddEvent(WorldEventKind.War, $"{nation.Name}失去了最后的聚落，退出历史舞台。");
            Current.Nations.Remove(nation);
            _nations.Remove(nation.Id);
            Current.Diplomacies.RemoveAll(r => r.FirstNationId == nation.Id || r.SecondNationId == nation.Id);
            foreach (var tile in Current.Tiles)
                if (tile.NationId == nation.Id)
                    tile.NationId = 0;
            foreach (var army in Current.Armies.Where(a => a.NationId == nation.Id).ToArray())
                DisbandArmy(army);
        }
    }
}
