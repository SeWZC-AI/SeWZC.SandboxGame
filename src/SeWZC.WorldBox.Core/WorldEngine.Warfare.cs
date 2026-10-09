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
        return Armies.Any(a =>
            a.Value.Id == resident.ArmyId && a.Value.TargetNationId == targetNationId && a.Value.KnownDiplomacy == DiplomaticStatus.War);
    }

    private void PublishDiplomaticOrder(int nationId, int enemyId, DiplomaticStatus status, int? knownX = null,
        int? knownY = null, long? observedTick = null, int targetSettlementId = 0, int eventId = 0,
        WarObjective objective = WarObjective.OccupySettlement)
    {
        var capital = Settlements.FirstOrDefault(t => t.Value.Id == _nations[nationId].Value.CapitalId);
        var enemyCapital = Settlements.FirstOrDefault(t => t.Value.Id == _nations[enemyId].Value.CapitalId);
        if (capital is null || enemyCapital is null)
            return;
        if (objective == WarObjective.DefendHomeland)
        {
            knownX = capital.Value.X;
            knownY = capital.Value.Y;
            targetSettlementId = capital.Value.Id;
        }

        var witness =
            Residents.FirstOrDefault(r =>
                r.NationId == nationId && Distance(r.X, r.Y, capital.Value.X, capital.Value.Y) <= 4);
        var fact = new AgentFact
        {
            Id = NewId(),
            EventId = eventId,
            CampaignEventId = eventId,
            WarObjective = objective,
            Kind = status == DiplomaticStatus.War ? AgentFactKind.WarOrder : AgentFactKind.PeaceOrder,
            SubjectId = enemyId,
            TargetNationId = nationId,
            X = knownX ?? enemyCapital.Value.X,
            Y = knownY ?? enemyCapital.Value.Y,
            Value = targetSettlementId > 0 ? targetSettlementId : knownX.HasValue ? 0 : enemyCapital.Value.Id,
            ObservedTick = observedTick ?? SimulationTick,
            LearnedTick = SimulationTick,
            OriginResidentId = witness?.Id ?? 0,
            SourceResidentId = witness?.Id ?? 0,
            Text = status == DiplomaticStatus.War ? "首都宣布开战，征召当地志愿者" : "首都宣布停止敌对，前线须等待消息送达",
        };
        if (status == DiplomaticStatus.War)
        {
            _nations[nationId].Replace(_nations[nationId].Value with
            {
                Military = _nations[nationId].Value.Military with
            {
                CampaignEventId = eventId,
                EnemyNationId = enemyId,
                Objective = objective,
                TargetSettlementId = (int)fact.Value,
                TargetX = fact.X,
                TargetY = fact.Y,
                StartedTick = SimulationTick,
                ReportedOutcome = WarOutcome.None,
                LastReportEventId = 0,
                LastReportObservedTick = 0,
                LastReportReceivedTick = 0,
                Report = "尚未收到前线战报",
                }
            });
        }
        else
            _nations[nationId].Replace(_nations[nationId].Value with
            {
                Military = _nations[nationId].Value.Military with
                {
                    RecoveryUntilTick = Math.Max(_nations[nationId].Value.Military.RecoveryUntilTick,
                    SimulationTick + 3 * SimulationTime.TicksPerYear),
                }
            });

        capital.Replace(capital.Value with { PublicKnowledge = capital.Value.PublicKnowledge.RemoveAll(f =>
            f.SubjectId == enemyId && f.Kind is AgentFactKind.WarOrder or AgentFactKind.PeaceOrder) });
        AddPublicFact(capital, fact);
        if (capital.Value.PublicKnowledge.Count > 24)
            capital.Replace(capital.Value with { PublicKnowledge = capital.Value.PublicKnowledge.RemoveAt(0) });
        foreach (var person in Residents.Where(r =>
                     r.NationId == nationId && Distance(r.X, r.Y, capital.Value.X, capital.Value.Y) <= 4))
            RememberAgentFact(person, fact);
    }

    private void UpdateArmies()
    {
        if (SimulationTick % SimulationTime.TicksPerMonth == SimulationTime.WakeTick)
        {
            foreach (var nation in Nations.ToArray())
            {
                if (Armies.Any(a => a.Value.NationId == nation.Value.Id))
                    continue;
                var capital = Settlements.FirstOrDefault(s => s.Value.Id == nation.Value.CapitalId);
                if (capital is null)
                    continue;
                var order = capital.Value.PublicKnowledge.Where(f =>
                        f.SubjectId != nation.Value.Id && f.SubjectId > 0 &&
                        (f.TargetNationId == 0 || f.TargetNationId == nation.Value.Id) &&
                        f.Kind is AgentFactKind.WarOrder or AgentFactKind.PeaceOrder)
                    .OrderByDescending(f => f.ObservedTick).ThenByDescending(f => f.Id).FirstOrDefault();
                if (order is null || order.Kind != AgentFactKind.WarOrder ||
                    order.Id <= nation.Value.Military.LastMobilizedOrderId
                    || (order.WarObjective == WarObjective.OccupySettlement &&
                        SimulationTick < nation.Value.Military.RecoveryUntilTick))
                    continue;
                var recruits = Residents.Where(r =>
                    r.NationId == nation.Value.Id && r.ArmyId == 0 && r.Age >= 16 && r.Health > 50 &&
                    r.TravelMode == TravelMode.Foot
                    && Distance(r.X, r.Y, capital.Value.X, capital.Value.Y) <= 5).ToArray();
                var count = Math.Min(40, recruits.Length / 2);
                if (count < 3)
                {
                    nation.Replace(nation.Value with { Decision = "当地兵员尚未集结，等待居民返回聚落" });
                    continue;
                }

                var provisions = Math.Min(capital.Value.Resources.Food, count * 10);
                capital.Replace(capital.Value.WithResources(capital.Value.Resources with { Food = capital.Value.Resources.Food - provisions }));
                var waterProvisions = Math.Min(capital.Value.Resources.Water, count * 3);
                capital.Replace(capital.Value.WithResources(capital.Value.Resources with { Water = capital.Value.Resources.Water - waterProvisions }));
                nation.Replace(nation.Value with { Military = nation.Value.Military with { LastMobilizedOrderId = order.Id } });
                var army = new StateReference<Army>(new Army
                {
                    Id = NewId(),
                    CampaignEventId = order.CampaignEventId,
                    Objective = order.WarObjective,
                    InitialSoldiers = count,
                    StartedTick = SimulationTick,
                    NationId = nation.Value.Id,
                    TargetNationId = order.SubjectId,
                    TargetX = order.X,
                    TargetY = order.Y,
                    TargetSettlementId = (int)order.Value,
                    X = capital.Value.X,
                    Y = capital.Value.Y,
                    FromX = capital.Value.X,
                    FromY = capital.Value.Y,
                    Soldiers = count,
                    Supplies = provisions,
                    WaterSupplies = waterProvisions,
                    CommanderId = recruits[0].Id,
                    LastOrderTick = order.ObservedTick,
                    LastOrderFactId = order.Id,
                });
                Armies.Add(army);
                foreach (var resident in recruits.Take(count))
                {
                    resident.Replace(resident.Value with { ArmyId = army.Value.Id });
                    if (resident.Profession is not (Profession.Ranger or Profession.Battlemage))
                        resident.Replace(resident.Value with { Profession = Profession.Soldier });
                    resident.Agent = resident.Agent.WithGoal(new AgentGoal
                    {
                        Kind = AgentGoalKind.March,
                        TargetX = capital.Value.X,
                        TargetY = capital.Value.Y,
                        StartedTick = SimulationTick,
                        Reason = "听到当地征召，步行前往集结点",
                    });
                    RememberAgentFact(resident, order);
                }

                nation.Replace(nation.Value with { Decision = "根据首都已知命令征召当地居民，军队需要实地集结" });
                var recruitmentEvent = AddEvent(WorldEventKind.War, $"{nation.Value.Name}在首都征募 {count} 名居民，开始集结。", capital.Value.X,
                    capital.Value.Y, EventAction.Muster, capital.Value.Id, causeEventId: order.EventId);
                army.Replace(army.Value with { LastEventId = recruitmentEvent.Id });
                foreach (var recruit in recruits.Take(count))
                    RecordLife(recruit, "响应当地征召，开始" + ObjectiveName(army.Value.Objective) + "任务。", recruitmentEvent);
                recruitmentEvent = PublishEvent(recruitmentEvent with { SecondNationId = army.Value.TargetNationId });
            }
        }

        foreach (var army in Armies.ToArray())
        {
            if (!Armies.Contains(army))
                continue;
            var soldiers = Residents.Where(r => r.ArmyId == army.Value.Id && r.Health > 0).ToArray();
            army.Replace(army.Value with { Soldiers = soldiers.Length });
            if (soldiers.Length == 0)
            {
                DisbandArmy(army);
                continue;
            }

            var commander = soldiers.FirstOrDefault(r => r.Id == army.Value.CommanderId) ?? soldiers[0];
            army.Replace(army.Value with { CommanderId = commander.Id });
            // 离队士兵携带的军令须通过本地通信传给指挥官，避免军队即时共享信息。
            var received = commander.Agent.Memory
                .Where(f => (f.TargetNationId == 0 || f.TargetNationId == army.Value.NationId) &&
                            f.SubjectId == army.Value.TargetNationId &&
                            f.Kind is AgentFactKind.WarOrder or AgentFactKind.PeaceOrder)
                .OrderByDescending(f => f.ObservedTick).ThenByDescending(f => f.Id).FirstOrDefault();
            if (received is not null && (received.ObservedTick > army.Value.LastOrderTick ||
                                         (received.ObservedTick == army.Value.LastOrderTick &&
                                          received.Id > army.Value.LastOrderFactId)))
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
                else if (army.Value.Outcome is WarOutcome.None or WarOutcome.OrdersReceived)
                {
                    // 新递送的军令可替代停战命令，但不能取消损失、疲劳、补给或目标已完成导致的撤退。
                    army.Replace(army.Value with { Outcome = WarOutcome.None, Retreating = false });
                    if (received.CampaignEventId > 0 && received.CampaignEventId != army.Value.CampaignEventId)
                    {
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
            }

            foreach (var soldier in soldiers)
            {
                if (Rules.Thirst && Distance(soldier.X, soldier.Y, army.Value.X, army.Value.Y) <= 2 &&
                    soldier.Inventory.Water < .025)
                {
                    var water = Math.Min(army.Value.WaterSupplies, .125);
                    army.Replace(army.Value with { WaterSupplies = army.Value.WaterSupplies - (water) });
                    soldier.Inventory = soldier.Inventory with { Water = soldier.Inventory.Water + water };
                }

                DrinkCarriedWater(soldier);
                if (!Rules.Hunger)
                    soldier.Hunger = 0;
                else if (Distance(soldier.X, soldier.Y, army.Value.X, army.Value.Y) <= 2 &&
                         army.Value.Supplies >= 0.06 / SimulationTime.TicksPerDay)
                {
                    army.Replace(army.Value with { Supplies = army.Value.Supplies - (0.06 / SimulationTime.TicksPerDay) });
                    soldier.Hunger = Math.Max(0, soldier.Hunger - 3d / SimulationTime.TicksPerDay);
                }
                else if (soldier.Inventory.Food >= 0.05 / SimulationTime.TicksPerDay)
                {
                    soldier.Replace(soldier.Value with
                    {
                        Inventory = soldier.Inventory with
                        {
                            Food = soldier.Inventory.Food - 0.05 / SimulationTime.TicksPerDay,
                        },
                        Hunger = Math.Max(0, soldier.Hunger - 3d / SimulationTime.TicksPerDay),
                    });
                }
                else
                    soldier.Hunger = Math.Min(100, soldier.Hunger + .8 / SimulationTime.TicksPerDay);

                if (Rules.Hunger && soldier.Hunger > 80)
                    DamageResident(soldier, .30 / SimulationTime.TicksPerDay, DeathCause.Starvation);
            }

            var depot = Settlements.FirstOrDefault(s =>
                s.Value.NationId == army.Value.NationId && Distance(s.Value.X, s.Value.Y, army.Value.X, army.Value.Y) <= 1);
            if (depot is not null && army.Value.Supplies < soldiers.Length * 5)
            {
                var amount = Math.Min(depot.Value.Resources.Food, soldiers.Length * 5 - army.Value.Supplies);
                depot.Replace(depot.Value.WithResources(depot.Value.Resources with { Food = depot.Value.Resources.Food - amount }));
                army.Replace(army.Value with { Supplies = army.Value.Supplies + (amount) });
            }

            if (depot is not null && army.Value.WaterSupplies < soldiers.Length)
            {
                var water = Math.Min(depot.Value.Resources.Water, soldiers.Length * 3 - army.Value.WaterSupplies);
                depot.Replace(depot.Value.WithResources(depot.Value.Resources with { Water = depot.Value.Resources.Water - water }));
                army.Replace(army.Value with { WaterSupplies = army.Value.WaterSupplies + (water) });
            }

            army.Replace(army.Value with
            {
                Morale = Math.Clamp(army.Value.Morale + (army.Value.Supplies > 0 ? 0.15 : -1.2) / SimulationTime.TicksPerDay, 0,
                100)
            });
            if (army.Value.Outcome == WarOutcome.None)
            {
                if (army.Value.InitialSoldiers > 0 && soldiers.Length * 5 <= army.Value.InitialSoldiers * 3)
                    EndCampaign(army, WarOutcome.HeavyLosses, soldiers);
                else if ((Rules.Thirst && army.Value.WaterSupplies <= 0 && soldiers.Average(r => r.Thirst) > 60) ||
                         army.Value.Morale < 15 ||
                         (Rules.Hunger && army.Value.Supplies <= 0 && soldiers.Average(r => r.Hunger) > 40))
                    EndCampaign(army, WarOutcome.SupplyShortage, soldiers);
                else if (army.Value.BlockedTicks >= 5 * SimulationTime.TicksPerDay)
                    EndCampaign(army, WarOutcome.RouteBlocked, soldiers);
                else if (SimulationTick - army.Value.StartedTick >= 6 * SimulationTime.TicksPerYear)
                    EndCampaign(army, WarOutcome.Exhausted, soldiers);
            }

            var time = SimulationTime.TimeOfDay(SimulationTick);
            if ((time < SimulationTime.WakeTick || time >= SimulationTime.SleepTick)
                && !soldiers.Any(r => Tiles[Index(r.X, r.Y)].Value.FireTicks > 0)
                && !Armies.Any(a => a.Value.Id != army.Value.Id && a.Value.NationId == army.Value.TargetNationId
                                                            && Distance(army.Value.X, army.Value.Y, a.Value.X, a.Value.Y) <= 2))
            {
                army.Replace(army.Value with { Status = "夜间宿营休息" });
                foreach (var soldier in soldiers)
                {
                    soldier.Activity = ResidentActivity.Sleeping;
                    soldier.Agent = soldier.Agent with { Fatigue = Math.Max(0, soldier.Agent.Fatigue - 2.2) };
                }

                continue;
            }

            if (army.Value.Gathering && !army.Value.Retreating)
            {
                foreach (var soldier in soldiers)
                    MoveAgentTowards(soldier, army.Value.X, army.Value.Y);
                army.Replace(army.Value with { Status = "实地集结" });
                if (soldiers.Count(r => Distance(r.X, r.Y, army.Value.X, army.Value.Y) <= 1) >=
                    Math.Max(2, soldiers.Length * 3 / 4))
                    army.Replace(army.Value with { Gathering = false });
                continue;
            }

            if (army.Value.Retreating)
            {
                var home = _settlements.GetValueOrDefault(commander.SettlementId);
                if (home is null)
                {
                    DisbandArmy(army);
                    continue;
                }

                army.Replace(army.Value with { Status = OutcomeName(army.Value.Outcome) + "，正在返乡" });
                if (Distance(commander.X, commander.Y, home.Value.X, home.Value.Y) <= 1)
                {
                    DisbandArmy(army);
                    continue;
                }

                MoveArmy(army, commander, soldiers, home.Value.X, home.Value.Y);
                continue;
            }

            var opponent = Armies.FirstOrDefault(a =>
                a.Value.Id != army.Value.Id && a.Value.NationId == army.Value.TargetNationId && Distance(army.Value.X, army.Value.Y, a.Value.X, a.Value.Y) <= 2);
            foreach (var ranger in soldiers.Where(p => p.Profession == Profession.Ranger))
            {
                if (depot is not null && Distance(ranger.X, ranger.Y, depot.Value.X, depot.Value.Y) <= 1)
                {
                    var ammo = Math.Min(depot.Value.Resources.Ammunition, Math.Max(0, 8 - ranger.Inventory.Ammunition));
                    depot.Replace(depot.Value.WithResources(depot.Value.Resources with { Ammunition = depot.Value.Resources.Ammunition - ammo }));
                    ranger.Inventory = ranger.Inventory with { Ammunition = ranger.Inventory.Ammunition + ammo };
                }

                var enemy = LocalHostile(ranger, ranger.X, ranger.Y, 4);
                if (enemy is not null && RangedAttackError(ranger.Id, enemy.Id) is null)
                    RangedAttack(ranger.Id, enemy.Id);
            }

            if (opponent is not null)
            {
                army.Replace(army.Value with { Status = "交战" });
                RecordBattle(army, soldiers);
                if (SimulationTick % 3 == 0)
                {
                    ApplyDamage(
                        Residents.Where(r =>
                            r.ArmyId == opponent.Value.Id && r.Health > 0 && Distance(r.X, r.Y, army.Value.X, army.Value.Y) <= 3),
                        soldiers.Count(r => Distance(r.X, r.Y, army.Value.X, army.Value.Y) <= 2) * 5 * army.Value.Morale / 100);
                }

                continue;
            }

            if (army.Value.Objective == WarObjective.DefendHomeland)
            {
                var intruder = Armies.FirstOrDefault(a => a.Value.NationId == army.Value.TargetNationId
                                                                  && Distance(army.Value.X, army.Value.Y, a.Value.X, a.Value.Y) <= 6 &&
                                                                  Distance(a.Value.X, a.Value.Y, army.Value.TargetX, army.Value.TargetY) <= 8);
                army.Replace(army.Value with { Status = "保卫家园，依据当地观察巡守" });
                if (intruder is not null)
                    MoveArmy(army, commander, soldiers, intruder.Value.X, intruder.Value.Y);
                else if (Distance(army.Value.X, army.Value.Y, army.Value.TargetX, army.Value.TargetY) > 1)
                    MoveArmy(army, commander, soldiers, army.Value.TargetX, army.Value.TargetY);
                continue;
            }

            if (Distance(army.Value.X, army.Value.Y, army.Value.TargetX, army.Value.TargetY) <= 1)
            {
                var visibleTarget = Settlements.FirstOrDefault(t => t.Value.NationId == army.Value.TargetNationId
                                                                            && (army.Value.TargetSettlementId == 0 ||
                                                                                t.Value.Id == army.Value.TargetSettlementId)
                                                                            && Distance(t.Value.X, t.Value.Y, army.Value.TargetX,
                                                                                army.Value.TargetY) <= 1);
                if (visibleTarget is null)
                {
                    EndCampaign(army, WarOutcome.TargetChanged, soldiers);
                    continue;
                }

                army.Replace(army.Value with
                {
                    TargetSettlementId = visibleTarget.Value.Id, Status = "围攻既定目标 " + visibleTarget.Value.Name,
                });
                if (SimulationTick % 3 == 0)
                    Siege(army, visibleTarget, soldiers.Where(r => Distance(r.X, r.Y, army.Value.X, army.Value.Y) <= 2).ToArray());
                continue;
            }

            army.Replace(army.Value with { Status = "按已知军令行军" });
            MoveArmy(army, commander, soldiers, army.Value.TargetX, army.Value.TargetY);
        }
    }

    private void MoveArmy(StateReference<Army> army, ResidentCursor commander, ResidentCursor[] soldiers, int x, int y)
    {
        var previousX = commander.X;
        var previousY = commander.Y;
        MoveAgentTowards(commander, x, y);
        army.Replace(army.Value with
        {
            BlockedTicks = commander.X == previousX && commander.Y == previousY ? army.Value.BlockedTicks + 1 : 0,
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
                Agent = soldier.Agent.WithGoal(soldier.Agent.Goal with
                {
                    Kind = AgentGoalKind.March,
                    TargetX = x,
                    TargetY = y,
                    Reason = army.Value.Status,
                }),
            });
        }
    }

    private void ApplyDamage(IEnumerable<ResidentCursor> residents, double damage)
    {
        damage *= Rules.CombatDamageRate;
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

    private void Siege(StateReference<Army> army, StateReference<Settlement> target, ResidentCursor[] soldiers)
    {
        var defenders = Residents.Where(r =>
            r.SettlementId == target.Value.Id && r.ArmyId == 0 && r.Age >= 14 && r.Health > 0 &&
            Distance(r.X, r.Y, target.Value.X, target.Value.Y) <= 5).ToArray();
        if (defenders.Length == 0)
        {
            CaptureSettlement(army, target);
            return;
        }

        RecordBattle(army, soldiers, defenders);
        var attack = soldiers.Length * (6 + _nations[army.Value.NationId].Value.Technology) * army.Value.Morale / 100;
        var defense = defenders.Length * 1.7;
        ApplyDamage(defenders, attack);
        ApplyDamage(soldiers, defense);
        army.Replace(army.Value with { Morale = Math.Max(0, army.Value.Morale - 0.2) });
        if (defenders.Count(r => r.Health > 0) < Math.Max(2, soldiers.Length / 3))
            CaptureSettlement(army, target);
    }

    private void CaptureSettlement(StateReference<Army> army, StateReference<Settlement> town)
    {
        var previous = town.Value.NationId;
        if (!_nations.TryGetValue(previous, out var previousNation))
            return;
        TransferSettlementOwnership(town, army.Value.NationId);
        foreach (var index in Circle(town.Value.X, town.Value.Y, 15))
            if (Tiles[index].Value.NationId == previous &&
                (Tiles[index].Value.SettlementId == 0 || Tiles[index].Value.SettlementId == town.Value.Id))
                Tiles[index].Replace(Tiles[index].Value.WithNationId(army.Value.NationId));
        ClaimTerritory(town, 8);
        var occupationEvent = AddEvent(WorldEventKind.War,
            $"{_nations[army.Value.NationId].Value.Name}占领了{previousNation.Value.Name}的{town.Value.Name}。", town.Value.X, town.Value.Y, EventAction.Capture,
            town.Value.Id, causeEventId: army.Value.LastEventId);
        occupationEvent = PublishEvent(occupationEvent with { SecondNationId = previous });
        EndCampaign(army, WarOutcome.ObjectiveReached,
            Residents.Where(r => r.ArmyId == army.Value.Id && r.Health > 0).ToArray(), occupationEvent.Id);
        Reindex();
        RemoveEmptyNations();
    }

    private void DisbandArmy(StateReference<Army> army)
    {
        var veterans = Residents.Where(r => r.ArmyId == army.Value.Id).ToArray();
        var localDepot =
            Settlements.FirstOrDefault(s =>
                s.Value.NationId == army.Value.NationId && Distance(s.Value.X, s.Value.Y, army.Value.X, army.Value.Y) <= 1);
        if (localDepot is not null)
        {
            localDepot.Replace(localDepot.Value.WithResources(localDepot.Value.Resources with
            {
                Food = localDepot.Value.Resources.Food + army.Value.Supplies,
                Water = localDepot.Value.Resources.Water + army.Value.WaterSupplies,
            }));
            foreach (var veteran in veterans.Where(r => Distance(r.X, r.Y, localDepot.Value.X, localDepot.Value.Y) <= 2))
            foreach (var report in veteran.Agent.Memory.Where(f => f.Kind == AgentFactKind.WarReport).ToArray())
                ReceiveWarReport(localDepot, report);
        }
        else
        {
            var nearby = veterans.Where(r => Distance(r.X, r.Y, army.Value.X, army.Value.Y) <= 2).ToArray();
            foreach (var veteran in nearby)
                veteran.Inventory = veteran.Inventory with
                {
                    Food = Math.Min(1_000_000, veteran.Inventory.Food + army.Value.Supplies / nearby.Length),
                    Water = Math.Min(1_000_000, veteran.Inventory.Water + army.Value.WaterSupplies / nearby.Length),
                };
        }

        army.Replace(army.Value with { Supplies = 0, WaterSupplies = 0 });
        WorldEvent? homecoming = null;
        if (veterans.Length > 0)
        {
            homecoming = AddEvent(WorldEventKind.War, "军队解散，幸存居民恢复生活；未抵家者继续步行返乡。", army.Value.X, army.Value.Y,
                EventAction.Homecoming, localDepot?.Value.Id ?? 0, causeEventId: army.Value.LastEventId);
            homecoming =
                PublishEvent(homecoming with { NationId = army.Value.NationId, SecondNationId = army.Value.TargetNationId });
        }

        foreach (var soldier in veterans)
        {
            RecordLife(soldier, "结束军旅任务，恢复平民生活。", homecoming);
            soldier.Replace(soldier.Value with { ArmyId = 0 });
            if (soldier.Profession is not (Profession.Ranger or Profession.Battlemage))
                soldier.Replace(soldier.Value with { Profession = AssignProfession() });
            if (_settlements.TryGetValue(soldier.SettlementId, out var home))
            {
                soldier.Agent = soldier.Agent.WithGoal(new AgentGoal
                {
                    Kind = AgentGoalKind.ReturnHome,
                    TargetX = home.Value.X,
                    TargetY = home.Value.Y,
                    TargetSettlementId = home.Value.Id,
                    StartedTick = SimulationTick,
                    ReviewTick = SimulationTick + 200,
                    PlayerDirected = true,
                    Reason = "退伍后步行返回家园",
                });
            }
        }

        Armies.Remove(army);
    }

    private void RemoveSettlement(StateReference<Settlement> settlement, string reason)
    {
        AddEvent(WorldEventKind.Death, $"{settlement.Value.Name}：{reason}。", settlement.Value.X, settlement.Value.Y);
        var destination =
            Settlements.FirstOrDefault(s => s.Value.Id != settlement.Value.Id && s.Value.NationId == settlement.Value.NationId);
        foreach (var resident in Residents.Where(r => r.SettlementId == settlement.Value.Id).ToArray())
        {
            if (destination is null)
            {
                Residents.Remove(resident);
                continue;
            }

            resident.Replace(
                resident.Value with { SettlementId = destination.Value.Id, X = destination.Value.X, Y = destination.Value.Y });
        }

        var tile = Tiles[Index(settlement.Value.X, settlement.Value.Y)];
        if (tile.Value.SettlementId == settlement.Value.Id)
            tile.Replace(tile.Value.WithSettlementId(0));
        Conflicts = Conflicts.RemoveAll(c => c.SettlementId == settlement.Value.Id);
        foreach (var ground in Tiles)
            if (ground.Value.ClaimedSettlementId == settlement.Value.Id)
                ground.Replace(ground.Value with { ClaimedSettlementId = 0, NationId = 0 });

        Settlements.Remove(settlement);
        _settlements.Remove(settlement.Value.Id);
        _citizens.Remove(settlement.Value.Id);
        if (_nations.TryGetValue(settlement.Value.NationId, out var nation) && nation.Value.CapitalId == settlement.Value.Id)
            nation.Replace(nation.Value with { CapitalId = destination?.Value.Id ?? 0 });
    }

    private void RemoveEmptyNations()
    {
        foreach (var nation in Nations.Where(n => !Settlements.Any(s => s.Value.NationId == n.Value.Id)).ToArray())
        {
            AddEvent(WorldEventKind.War, $"{nation.Value.Name}失去了最后的聚落，退出历史舞台。");
            Nations.Remove(nation);
            _nations.Remove(nation.Value.Id);
            Diplomacies =
                Diplomacies.RemoveAll(r => r.FirstNationId == nation.Value.Id || r.SecondNationId == nation.Value.Id);
            foreach (var tile in Tiles)
                if (tile.Value.NationId == nation.Value.Id)
                    tile.Replace(tile.Value.WithNationId(0));
            foreach (var army in Armies.Where(a => a.Value.NationId == nation.Value.Id).ToArray())
                DisbandArmy(army);
        }
    }
}
