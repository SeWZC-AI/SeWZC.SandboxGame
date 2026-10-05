using SeWZC.WorldBox.Core;

internal static class VisualFixture
{
    public static void Export(string path)
    {
        var engine = WorldEngine.Create(73, 64, 64, false);
        foreach (var tile in engine.State.Tiles)
        {
            tile.Terrain = TerrainType.Grass;
            tile.Rainfall = tile.NaturalWaterYield = .03;
            tile.ResourceAmount = 100;
            tile.Fertility = 90;
            tile.Wildlife = WildlifeKind.Rabbit;
            tile.WildlifePopulation = 4;
            tile.OtherWildlife = new WildlifePopulations { Deer = 1 };
        }

        var positions = new[] { (16, 16), (40, 16), (16, 40), (40, 40) };
        foreach (var race in Enum.GetValues<RaceKind>())
        {
            var (x, y) = positions[(int)race];
            engine.SpawnResidents(x, y, race);
            TestLand.ClaimAllTowns(engine);
            var town = engine.State.Settlements.Last();
            var people = engine.State.Residents.Where(r => r.SettlementId == town.Id).ToArray();
            for (var i = 0; i < people.Length; i++)
            {
                var p = people[i];
                p.X = p.FromX = x - 3 + i % 4;
                p.Y = p.FromY = y - 2 + i / 4;
                p.Age = 26;
                p.Profession = (Profession)(1 + i % (int)Profession.Mage);
                p.Activity = ResidentActivity.Working;
                p.MoveStartedTick = 0;
                p.MoveDurationTicks = 1;
                p.Agent.Goal = new AgentGoal
                {
                    Kind = AgentGoalKind.Work, TargetX = p.X, TargetY = p.Y, ReviewTick = 100, PlayerDirected = true,
                    Reason = "视觉夹具的现场工作",
                };
            }

            engine.GrantFacility(town.Id, BuildingKind.Waystation, x + 3, y + 1);
            var station = engine.State.Society.Buildings.Last();
            station.Health = 23;
            engine.GrantFacility(town.Id, BuildingKind.Infirmary, x + 3, y - 2);
            for (var iy = y - 3; iy <= y + 3; iy++)
            {
                var forest = engine.State.Tiles[iy * 64 + x + 4];
                forest.Terrain = TerrainType.Forest;
                forest.Wildlife = WildlifeKind.Deer;
                forest.WildlifePopulation = 3;
                forest.OtherWildlife = new WildlifePopulations { Boar = 2, Wolf = .5 };
                for (var ix = x + 5; ix <= x + 7; ix++)
                {
                    var tile = engine.State.Tiles[iy * 64 + ix];
                    tile.Terrain = TerrainType.Mountain;
                    tile.Elevation = 230;
                    tile.OtherWildlife = default;
                    tile.Wildlife = WildlifeKind.Goat;
                    tile.WildlifePopulation = 2;
                }

                var river = engine.State.Tiles[iy * 64 + x - 5];
                river.Terrain = TerrainType.River;
                river.OtherWildlife = new WildlifePopulations { Waterfowl = 2 };
                river.Wildlife = WildlifeKind.Fish;
                river.WildlifePopulation = 5;
            }
        }

        foreach (var town in engine.State.Settlements)
        {
            var wetland = Enumerable.Range(0, engine.State.Tiles.Length).First(i =>
                Math.Abs(i % 64 - town.X) + Math.Abs(i / 64 - town.Y) <= 6
                && engine.State.Tiles[i].Terrain == TerrainType.Grass &&
                !engine.State.Society.Buildings.Any(b => b.X == i % 64 && b.Y == i / 64));
            engine.State.Tiles[wetland].Terrain = TerrainType.Wetland;
            foreach (var kind in new[]
                     {
                         BuildingKind.Dock, BuildingKind.Shipyard, BuildingKind.LumberCamp, BuildingKind.Quarry,
                         BuildingKind.Well, BuildingKind.Granary, BuildingKind.Housing, BuildingKind.Market,
                         BuildingKind.Watchtower,
                     })
            {
                var site = Enumerable.Range(0, engine.State.Tiles.Length)
                    .Where(i => engine.FacilityPlacementError(town.Id, kind, i % 64, i / 64, true) is null)
                    .OrderByDescending(i => engine.BuildingSiteScore(town.Id, kind, i % 64, i / 64)).ThenBy(i => i)
                    .First();
                engine.GrantFacility(town.Id, kind, site % 64, site / 64);
            }

            var race = engine.State.Nations.Single(n => n.Id == town.NationId).FoundingRace;
            foreach (var kind in Enum.GetValues<BuildingKind>().Where(k => WorldEngine.BuildingRace(k) == race))
            {
                var site = Enumerable.Range(0, engine.State.Tiles.Length)
                    .Where(i => engine.FacilityPlacementError(town.Id, kind, i % 64, i / 64, true) is null)
                    .OrderByDescending(i => engine.BuildingSiteScore(town.Id, kind, i % 64, i / 64)).ThenBy(i => i)
                    .First();
                engine.GrantFacility(town.Id, kind, site % 64, site / 64);
            }

            for (var dx = 0; dx <= 3; dx++) engine.BuildRoad(town.Id, town.X + dx, town.Y, 0);
        }

        engine.ConfigureWorld(
            engine.State.Rules with { Births = false, Aging = false, Hunger = false, Thirst = false, Disease = false },
            false, true);
        engine.Step(2);
        var json = engine.ExportJson();
        _ = WorldEngine.ImportJson(json);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, json);
        Console.WriteLine(
            $"EXPORTED visual fixture: {path}; four races, waterfront facilities, specialist buildings, roads, damaged waystations and coexisting animals");
    }
}
