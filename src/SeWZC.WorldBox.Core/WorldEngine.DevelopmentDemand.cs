namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    /// <summary>建设与研究规划使用的当地劳动力、设施和需求信号。</summary>
    private sealed record LocalDemand(Settlement Town, Resident[] Adults, IReadOnlyList<Building> Buildings, bool Defense, bool Patients,
        bool Water, bool Coast, bool Timber, bool Stone, bool Contacts, bool MagicTalent, bool Roads);

    /// <summary>汇总附近劳动力和资源条件，以及从居民记忆中推断的需求。</summary>
    private LocalDemand InspectLocalDemand(Settlement town, IReadOnlyList<Building> buildings)
    {
        var residents = _localWorkQueriesActive ? _localWorkResidents.GetValueOrDefault(town.Id) : _citizens.GetValueOrDefault(town.Id);
        var adults = residents?.Where(p => p.SettlementId == town.Id && p.Age >= 14 && p.Health > 0 && p.ArmyId == 0
            && Distance(p.X, p.Y, town.X, town.Y) <= 6).ToArray() ?? [];
        var defense = GetLocalPolicy(town.Id) == PolicyKind.Defense || adults.Any(p => p.Agent.Memory.Any(f => f.Kind is AgentFactKind.WarOrder or AgentFactKind.Danger
            && f.Value > 0 && State.Tick - f.ObservedTick < 120 && AgentFactReliability(f) >= .5));
        var coast = false; var timber = false; var stone = false; var roads = false;
        foreach (var index in Circle(town.X, town.Y, 6))
        {
            var tile = State.Tiles[index];
            coast |= !coast && IsFreshWater(tile) && tile.AnimalPopulation(WildlifeKind.Fish) + tile.AnimalPopulation(WildlifeKind.GrassCarp) >= .2;
            timber |= !timber && IsForestTerrain(tile.Terrain) && tile.ResourceAmount >= 10;
            stone |= !stone && TerrainRules.For(tile.Terrain).StoneYield + TerrainRules.For(tile.Terrain).OreYield > 0 && tile.ResourceAmount >= 10;
            roads |= tile.NationId == town.NationId && tile.RoadLevel > 0;
            if (coast && timber && stone && roads) break;
        }
        return new(town, adults, buildings, defense, adults.Any(p => p.Health < 90 || p.SicknessTicks > 0),
            State.Rules.Thirst && (town.Resources.Water < Math.Max(2, town.Population * .5) || adults.Any(p => p.Thirst > 20)),
            coast, timber, stone,
            adults.Any(p => p.Agent.Memory.Any(f => f.Kind == AgentFactKind.SettlementLocation && f.SubjectId != town.Id && AgentFactReliability(f) >= .5)),
            adults.Any(p => p.MagicTalent >= 35), roads);
    }

    private bool FacilityNeeded(LocalDemand d, BuildingKind kind)
    {
        var town = d.Town; var stock = town.Resources;
        bool Has(BuildingKind k) => d.Buildings.Any(b => b.Kind == k && b.Enabled && b.Health > 0);
        return kind switch
        {
            BuildingKind.Academy => State.Rules.Research,
            BuildingKind.Bridge or BuildingKind.MountainPass => false,
            BuildingKind.Farm => stock.Food < Math.Max(25, town.Population * 1.5),
            BuildingKind.Housing => town.Population >= GetHousingCapacity(town.Id),
            BuildingKind.Well => d.Water && !Has(BuildingKind.Reservoir),
            BuildingKind.Reservoir => d.Water,
            BuildingKind.Pasture => stock.Food < town.Population * 2 && d.Adults.Any(p => p.Profession == Profession.Farmer)
                && Circle(town.X, town.Y, 6).Any(i => HusbandryStockAt(i % State.Width, i / State.Width, false).Source >= 0),
            BuildingKind.Aquaculture => d.Coast && stock.Food < town.Population * 2,
            BuildingKind.Infirmary => d.Patients && !Has(BuildingKind.Hospital),
            BuildingKind.Hospital => d.Patients && (stock.Medicine >= .25 || Has(BuildingKind.Apothecary) || Has(BuildingKind.AlchemyLab)),
            BuildingKind.Apothecary or BuildingKind.AlchemyLab => stock.Medicine < 8 && (d.Patients || State.Rules.Disease && town.Population >= 60),
            BuildingKind.FireStation => d.Buildings.Any(b => b.Health is > 0 and < 90)
                || State.NaturalDisasters && town.Population >= 60 || d.Adults.Any(p => p.Agent.Memory.Any(f => f.Kind == AgentFactKind.Danger && f.Value > 0 && State.Tick - f.ObservedTick < 24)),
            BuildingKind.Library => State.Society.Research.First(r => r.SettlementId == town.Id).Completed.Count >= 3,
            BuildingKind.SurveyOffice => State.Rules.Expansion || d.Adults.Any(p => p.Profession is Profession.Miner or Profession.Trader or Profession.Messenger),
            BuildingKind.Arsenal => d.Defense && stock.Ammunition < 24,
            BuildingKind.Armory or BuildingKind.WardTower or BuildingKind.StormSpire or BuildingKind.Watchtower or BuildingKind.WarDrum => d.Defense,
            BuildingKind.MachineWorkshop => stock.Tools < 8 && d.Adults.Any(p => p.Profession is Profession.Builder or Profession.Engineer),
            BuildingKind.Dock or BuildingKind.Shipyard => d.Coast && stock.Boats < 2 && d.Adults.Any(p => p.Profession is Profession.Fisher or Profession.Trader or Profession.Messenger),
            BuildingKind.SignalTower or BuildingKind.Waystation or BuildingKind.Market or BuildingKind.TradeGuild => d.Contacts,
            BuildingKind.Waygate => d.Contacts && d.MagicTalent && State.Settlements.Any(t => t.Id != town.Id && t.NationId == town.NationId && Distance(t.X, t.Y, town.X, town.Y) <= 24),
            BuildingKind.GroveSanctuary => d.MagicTalent && !d.Timber && d.Adults.Any(p => p.Profession == Profession.Lumberjack),
            BuildingKind.LumberCamp => d.Timber && d.Adults.Any(p => p.Profession == Profession.Lumberjack) && stock.Wood < 40,
            BuildingKind.Quarry or BuildingKind.MiningHall => d.Stone && d.Adults.Any(p => p.Profession == Profession.Miner) && stock.Stone + stock.Ore < 60,
            BuildingKind.Granary => town.Population >= 60 && stock.Food >= town.Population,
            BuildingKind.HuntingCamp => stock.Food < town.Population && d.Adults.Any(p => p.Race == RaceKind.Orc),
            BuildingKind.ArcaneSanctum or BuildingKind.SacredGrove => d.MagicTalent,
            BuildingKind.HerbGarden => d.Patients,
            BuildingKind.AssemblyHall => town.Population >= 40,
            _ => true
        };
    }

    private double ResearchUtility(LocalDemand d, ResearchDefinition research)
    {
        var town = d.Town;
        if (research.Magic && !d.MagicTalent) return 0;
        var useful = research.Kind switch
        {
            ResearchKind.Ballistics or ResearchKind.ProtectiveEquipment or ResearchKind.Warding or ResearchKind.BattleMagic => d.Defense,
            ResearchKind.Elementalism => d.Defense || State.NaturalDisasters,
            ResearchKind.RailTransport => d.Roads,
            ResearchKind.SpatialMagic => FacilityNeeded(d, BuildingKind.Waygate),
            ResearchKind.FireEngineering => FacilityNeeded(d, BuildingKind.FireStation),
            ResearchKind.Medicine or ResearchKind.Sanitation or ResearchKind.Restoration => d.Patients || State.Rules.Disease,
            ResearchKind.Pharmacology or ResearchKind.Alchemy => FacilityNeeded(d, BuildingKind.Apothecary),
            ResearchKind.NatureBinding => FacilityNeeded(d, BuildingKind.GroveSanctuary) || State.NaturalDisasters,
            ResearchKind.Observation or ResearchKind.SignalNetwork => d.Contacts || d.Adults.Any(p => p.Profession == Profession.Miner),
            ResearchKind.EfficientSmelting => d.Buildings.Any(b => b.Kind is BuildingKind.Foundry or BuildingKind.DwarvenForge),
            ResearchKind.EnergyRecycling => d.Buildings.Any(b => b.Kind == BuildingKind.PowerPlant),
            ResearchKind.Leylines => d.Buildings.Any(b => b.Kind == BuildingKind.Crystallizer),
            _ => true
        };
        if (!useful) return 0;
        // Basic livelihoods and the next usable production chain outrank refinements.
        return research.Kind switch
        {
            ResearchKind.Agriculture => 100,
            ResearchKind.Logistics => 95,
            ResearchKind.Irrigation => town.Resources.Food < town.Population ? 90 : 55,
            ResearchKind.Forestry => town.Resources.Wood + town.Resources.Stone < 40 ? 85 : 50,
            ResearchKind.Medicine or ResearchKind.Sanitation or ResearchKind.Pharmacology => d.Patients ? 88 : 45,
            ResearchKind.FireEngineering => d.Buildings.Any(b => b.Health < 90) ? 88 : 40,
            _ => AdvancementRules.For(research.Kind) is not null ? 70 : 50
        };
    }
}
