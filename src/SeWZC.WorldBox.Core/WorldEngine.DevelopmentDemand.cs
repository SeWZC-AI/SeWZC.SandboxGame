using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    /// <summary>汇总附近劳动力和资源条件，以及从居民记忆中推断的需求。</summary>
    /// <param name="town">需要评估发展需求的本地聚落。</param>
    /// <param name="buildings">本地已有设施，用于核对劳动与供给缺口。</param>
    private LocalDemand InspectLocalDemand(StateReference<Settlement> town, IReadOnlyList<StateReference<Building>> buildings)
    {
        var residents = _localWorkQueriesActive
            ? _localWorkResidents.GetValueOrDefault(town.Value.Id)
            : _citizens.GetValueOrDefault(town.Value.Id);
        var adults = residents?.Where(p => p.Value.SettlementId == town.Value.Id && p.Value.Age >= ResidentNeedsRules.MinimumWorkAge && p.Value.Health > 0 && p.Value.ArmyId == 0
                                           && Distance(p.Value.X, p.Value.Y, town.Value.X, town.Value.Y) <= 6).ToArray() ?? [];
        var defense = GetLocalPolicy(town.Value.Id) == PolicyKind.Defense || adults.Any(p => p.Value.Agent.Memory.Any(f =>
            f.Kind is AgentFactKind.Danger or AgentFactKind.WarOrder
            && f.Value > 0 && SimulationTick - f.ObservedTick < SimulationTime.TicksPerYear &&
            f.ReliabilityAt(SimulationTick) >= .5));
        var coast = false;
        var timber = false;
        var stone = false;
        var roads = false;
        foreach (var index in Circle(town.Value.X, town.Value.Y, 6))
        {
            var tile = Tiles[index];
            coast |= !coast && IsFreshWater(tile.Value) &&
                     tile.Value.AnimalPopulation(WildlifeKind.Fish) + tile.Value.AnimalPopulation(WildlifeKind.GrassCarp) >= .2;
            timber |= !timber && IsForestTerrain(tile.Value.Terrain) && tile.Value.ResourceAmount >= 10;
            stone |= !stone &&
                     TerrainRules.For(tile.Value.Terrain).StoneYield + TerrainRules.For(tile.Value.Terrain).OreYield > 0 &&
                     tile.Value.ResourceAmount >= 10;
            roads |= tile.Value.NationId == town.Value.NationId && tile.Value.RoadLevel > 0;
            if (coast && timber && stone && roads)
                break;
        }

        return new LocalDemand(town, adults, buildings, defense, adults.Any(p => p.Value.Health < 90 || p.Value.SicknessTicks > 0),
            Rules.Thirst && (town.Value.Resources.Water < Math.Max(2, town.Value.Population * .5) ||
                                     adults.Any(p => p.Value.Thirst > 20)),
            coast, timber, stone,
            adults.Any(p => p.Value.Agent.Memory.Any(f =>
                f.Kind == AgentFactKind.SettlementLocation && f.SubjectId != town.Value.Id &&
                f.ReliabilityAt(SimulationTick) >= .5)),
            adults.Any(p => p.Value.MagicTalent >= 35), roads);
    }

    private bool FacilityNeeded(LocalDemand demand, BuildingKind kind)
    {
        var town = demand.Town;
        var stock = town.Value.Resources;

        bool Has(BuildingKind k)
        {
            return demand.Buildings.Any(b => b.Value.Kind == k && b.Value.Enabled && b.Value.Health > 0);
        }

        return kind switch
        {
            BuildingKind.Farm => stock.Food < Math.Max(25, town.Value.Population * 1.5),
            BuildingKind.Academy => Rules.Research,
            BuildingKind.Waystation or BuildingKind.SignalTower or BuildingKind.Market
                or BuildingKind.TradeGuild => demand.Contacts,
            BuildingKind.ArcaneSanctum or BuildingKind.SacredGrove => demand.MagicTalent,
            BuildingKind.Infirmary => demand.Patients && !Has(BuildingKind.Hospital),
            BuildingKind.MountainPass or BuildingKind.Bridge => false,
            BuildingKind.Dock or BuildingKind.Shipyard => demand.Coast && stock.Boats < 2 && demand.Adults.Any(p =>
                p.Value.Profession is Profession.Trader or Profession.Messenger or Profession.Fisher),
            BuildingKind.LumberCamp => demand.Timber && demand.Adults.Any(p => p.Value.Profession == Profession.Lumberjack) &&
                                       stock.Wood < 40,
            BuildingKind.Quarry or BuildingKind.MiningHall => demand.Stone &&
                                                              demand.Adults.Any(p =>
                                                                  p.Value.Profession == Profession.Miner) &&
                                                              stock.Stone + stock.Ore < 60,
            BuildingKind.Well => demand.Water && !Has(BuildingKind.Reservoir),
            BuildingKind.Granary => town.Value.Population >= 60 && stock.Food >= town.Value.Population,
            BuildingKind.Housing => town.Value.Population >= GetHousingCapacity(town.Value.Id),
            BuildingKind.Watchtower or BuildingKind.WarDrum or BuildingKind.Armory or BuildingKind.WardTower
                or BuildingKind.StormSpire => demand.Defense,
            BuildingKind.AssemblyHall => town.Value.Population >= 40,
            BuildingKind.HerbGarden => demand.Patients,
            BuildingKind.HuntingCamp => stock.Food < town.Value.Population && demand.Adults.Any(p => p.Value.Race == RaceKind.Orc),
            BuildingKind.Reservoir => demand.Water,
            BuildingKind.Hospital => demand.Patients &&
                                     (stock.Medicine >= .25 || Has(BuildingKind.Apothecary) ||
                                      Has(BuildingKind.AlchemyLab)),
            BuildingKind.Apothecary or BuildingKind.AlchemyLab => stock.Medicine < 8 &&
                                                                  (demand.Patients || (Rules.Disease &&
                                                                      town.Value.Population >= 60)),
            BuildingKind.FireStation => demand.Buildings.Any(b => b.Value.Health is > 0 and < 90)
                                        || (NaturalDisasters && town.Value.Population >= 60) || demand.Adults.Any(p =>
                                            p.Value.Agent.Memory.Any(f =>
                                                f.Kind == AgentFactKind.Danger && f.Value > 0 &&
                                                SimulationTick - f.ObservedTick < 24)),
            BuildingKind.Library => Society.Research.First(r => r.SettlementId == town.Value.Id).Completed.Count >= 3,
            BuildingKind.SurveyOffice => Rules.Expansion || demand.Adults.Any(p =>
                p.Value.Profession is Profession.Miner or Profession.Trader or Profession.Messenger),
            BuildingKind.MachineWorkshop => stock.Tools < 8 &&
                                            demand.Adults.Any(p =>
                                                p.Value.Profession is Profession.Builder or Profession.Engineer),
            BuildingKind.Arsenal => demand.Defense && stock.Ammunition < 24,
            BuildingKind.GroveSanctuary => demand.MagicTalent && !demand.Timber &&
                                           demand.Adults.Any(p => p.Value.Profession == Profession.Lumberjack),
            BuildingKind.Waygate => demand.Contacts && demand.MagicTalent && Settlements.Any(t =>
                t.Value.Id != town.Value.Id && t.Value.NationId == town.Value.NationId && Distance(t.Value.X, t.Value.Y, town.Value.X, town.Value.Y) <= 24),
            BuildingKind.Pasture => stock.Food < town.Value.Population * 2 && demand.Adults.Any(p =>
                                                                         p.Value.Profession == Profession.Farmer)
                                                                     && Circle(town.Value.X, town.Value.Y, 6).Any(i =>
                                                                         HusbandryStockAt(i % Width,
                                                                             i / Width, false).Source >= 0),
            BuildingKind.Aquaculture => demand.Coast && stock.Food < town.Value.Population * 2,
            _ => true,
        };
    }

    private double ResearchUtility(LocalDemand demand, Advancement research)
    {
        var town = demand.Town;
        if (research.Magic && !demand.MagicTalent)
            return 0;
        var useful = research switch
        {
            _ when research == Advancement.Ballistics || research == Advancement.ProtectiveEquipment
                                                      || research == Advancement.Warding ||
                                                      research == Advancement.BattleMagic => demand.Defense,
            _ when research == Advancement.Elementalism => demand.Defense || NaturalDisasters,
            _ when research == Advancement.RailTransport => demand.Roads,
            _ when research == Advancement.SpatialMagic => FacilityNeeded(demand, BuildingKind.Waygate),
            _ when research == Advancement.FireEngineering => FacilityNeeded(demand, BuildingKind.FireStation),
            _ when research == Advancement.Medicine || research == Advancement.Sanitation
                                                    || research == Advancement.Restoration => demand.Patients ||
                Rules.Disease,
            _ when research == Advancement.Pharmacology || research == Advancement.Alchemy => FacilityNeeded(demand,
                BuildingKind.Apothecary),
            _ when research == Advancement.NatureBinding => FacilityNeeded(demand, BuildingKind.GroveSanctuary) ||
                                                            NaturalDisasters,
            _ when research == Advancement.Observation || research == Advancement.SignalNetwork => demand.Contacts ||
                demand.Adults.Any(p =>
                    p.Value.Profession == Profession.Miner),
            _ when research == Advancement.EfficientSmelting => demand.Buildings.Any(b =>
                b.Value.Kind is BuildingKind.Foundry or BuildingKind.DwarvenForge),
            _ when research == Advancement.EnergyRecycling => demand.Buildings.Any(b =>
                b.Value.Kind == BuildingKind.PowerPlant),
            _ when research == Advancement.Leylines => demand.Buildings.Any(b => b.Value.Kind == BuildingKind.Crystallizer),
            _ => true,
        };
        if (!useful)
            return 0;
        // 基本生计和下一条可用生产链优先，避免资源先被次要改进占用。
        return research switch
        {
            _ when research == Advancement.Agriculture => 100,
            _ when research == Advancement.Logistics => 95,
            _ when research == Advancement.Irrigation => town.Value.Resources.Food < town.Value.Population ? 90 : 55,
            _ when research == Advancement.Forestry => town.Value.Resources.Wood + town.Value.Resources.Stone < 40 ? 85 : 50,
            _ when research == Advancement.Medicine || research == Advancement.Sanitation
                                                    || research == Advancement.Pharmacology =>
                demand.Patients ? 88 : 45,
            _ when research == Advancement.FireEngineering => demand.Buildings.Any(b => b.Value.Health < 90) ? 88 : 40,
            _ => ProductionRules.For(research) is not null ? 70 : 50,
        };
    }

    /// <summary>聚落建设与研究规划的需求评估结果。</summary>
    /// <param name="Town">本次评估的本地聚落。</param>
    /// <param name="Adults">参与需求评估的本地成年居民。</param>
    /// <param name="Buildings">本地已有设施。</param>
    /// <param name="Defense">是否具有本地防御需求。</param>
    /// <param name="Patients">是否存在需要治疗的居民。</param>
    /// <param name="Water">是否具有饮水供给需求。</param>
    /// <param name="Coast">本地是否有可利用的岸线。</param>
    /// <param name="Timber">本地是否有可利用的木材来源。</param>
    /// <param name="Stone">本地是否有可利用的石矿来源。</param>
    /// <param name="Contacts">是否掌握外地聚落接触信息。</param>
    /// <param name="MagicTalent">是否有适合魔法发展的本地人才。</param>
    /// <param name="Roads">是否具有道路和交通发展需求。</param>
    private sealed record LocalDemand(
        StateReference<Settlement> Town,
        StateReference<Resident>[] Adults,
        IReadOnlyList<StateReference<Building>> Buildings,
        bool Defense,
        bool Patients,
        bool Water,
        bool Coast,
        bool Timber,
        bool Stone,
        bool Contacts,
        bool MagicTalent,
        bool Roads);
}
