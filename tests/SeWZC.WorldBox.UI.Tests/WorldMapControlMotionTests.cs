using Avalonia;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;

namespace SeWZC.WorldBox.UI.Tests;

/// <summary>军旗与指挥官使用同一条实际行进路线。</summary>
public sealed class WorldMapControlMotionTests
{
    /// <summary>直行和转弯的一刻两格移动均在中点显示实际经过的地格。</summary>
    [Fact]
    public void Army_marker_follows_the_commanders_multi_tile_route()
    {
        foreach (var turn in new[] { false, true })
        {
            var engine = WorldEngine.Create(42, 32, 32, false);
            engine.PaintTerrain(16, 16, TerrainType.Grass, 32);
            engine.SpawnResidents(16, 16, RaceKind.Human, 1);
            engine.SpawnResidents(0, 0, RaceKind.Human, 1);
            if (turn)
            {
                engine.PaintTerrain(3, 2, TerrainType.Mountain, 0);
                engine.PaintTerrain(2, 4, TerrainType.Mountain, 0);
            }
            var snapshot = engine.State;
            var commander = snapshot.Residents[0];
            var army = new Army
            {
                Id = snapshot.NextId, NationId = commander.NationId,
                TargetNationId = snapshot.Nations.Single(n => n.Id != commander.NationId).Id,
                X = 2, Y = 2, FromX = 2, FromY = 2, TargetX = 16, TargetY = 16,
                CommanderId = commander.Id, Soldiers = 1, InitialSoldiers = 1,
                Gathering = false, Retreating = true, Supplies = 10, WaterSupplies = 10,
            };
            commander = commander with
            {
                X = 2, Y = 2, FromX = 2, FromY = 2, Age = 25, ArmyId = army.Id,
                IsInsideHome = false, Inventory = new ResourceStock { Food = 1, Water = 1 },
                Agent = commander.Agent with { Initialized = true },
            };
            engine = WorldEngine.FromSnapshot(snapshot with
            {
                Tick = SimulationTime.WakeTick, NextId = snapshot.NextId + 1,
                Residents = snapshot.Residents.SetItem(0, commander), Armies = snapshot.Armies.Add(army),
            });
            var map = new WorldMapControl { Engine = engine };

            engine.Step();
            map.RefreshWorld();

            commander = engine.GetResident(commander.Id)!;
            army = engine.State.Armies.Single();
            Assert.Equal(3, commander.MovementRoute.Length);
            Assert.Equal(turn, commander.X != commander.FromX && commander.Y != commander.FromY);
            var middle = commander.MovementRoute[1];
            Assert.Equal(new Point(middle % 32, middle / 32),
                map.ArmyPresentationPosition(army, commander.MoveStartedTick + .5));
        }
    }
}
