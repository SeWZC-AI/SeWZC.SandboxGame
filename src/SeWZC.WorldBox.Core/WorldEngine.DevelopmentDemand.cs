namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    /// <summary>汇总附近劳动力和资源条件，以及从居民记忆中推断的需求。</summary>
    /// <param name="town">需要评估发展需求的本地聚落。</param>
    /// <param name="buildings">本地已有设施，用于核对劳动与供给缺口。</param>
    private LocalDemand InspectLocalDemand(Settlement town, IReadOnlyList<Building> buildings)
    {
        var residents = _localWorkQueriesActive
            ? _localWorkResidents.GetValueOrDefault(town.Id)
            : _citizens.GetValueOrDefault(town.Id);
        var adults = residents?.Where(p => p.SettlementId == town.Id && p.Age >= 14 && p.Health > 0 && p.ArmyId == 0
                                           && Distance(p.X, p.Y, town.X, town.Y) <= 6).ToArray() ?? [];
        var defense = GetLocalPolicy(town.Id) == PolicyKind.Defense || adults.Any(p => p.Agent.Memory.Any(f =>
            f.Kind is AgentFactKind.Danger or AgentFactKind.WarOrder
            && f.Value > 0 && State.Tick - f.ObservedTick < 120 && AgentFactReliability(f) >= .5));
        var coast = false;
        var timber = false;
        var stone = false;
        var roads = false;
        foreach (var index in Circle(town.X, town.Y, 6))
        {
            var tile = State.Tiles[index];
            coast |= !coast && IsFreshWater(tile) &&
                     tile.AnimalPopulation(WildlifeKind.Fish) + tile.AnimalPopulation(WildlifeKind.GrassCarp) >= .2;
            timber |= !timber && IsForestTerrain(tile.Terrain) && tile.ResourceAmount >= 10;
            stone |= !stone &&
                     TerrainRules.For(tile.Terrain).StoneYield + TerrainRules.For(tile.Terrain).OreYield > 0 &&
                     tile.ResourceAmount >= 10;
            roads |= tile.NationId == town.NationId && tile.RoadLevel > 0;
            if (coast && timber && stone && roads)
                break;
        }

        return new LocalDemand(town, adults, buildings, defense, adults.Any(p => p.Health < 90 || p.SicknessTicks > 0),
            State.Rules.Thirst && (town.Resources.Water < Math.Max(2, town.Population * .5) ||
                                   adults.Any(p => p.Thirst > 20)),
            coast, timber, stone,
            adults.Any(p => p.Agent.Memory.Any(f =>
                f.Kind == AgentFactKind.SettlementLocation && f.SubjectId != town.Id && AgentFactReliability(f) >= .5)),
            adults.Any(p => p.MagicTalent >= 35), roads);
    }

    private bool FacilityNeeded(LocalDemand demand, BuildingKind kind)
    {
        var town = demand.Town;
        var stock = town.Resources;

        bool Has(BuildingKind k)
        {
            return demand.Buildings.Any(b => b.Kind == k && b.Enabled && b.Health > 0);
        }

        return kind switch
        {
            BuildingKind.Farm => stock.Food < Math.Max(25, town.Population * 1.5),
            BuildingKind.Academy => State.Rules.Research,
            BuildingKind.Waystation or BuildingKind.SignalTower or BuildingKind.Market
                or BuildingKind.TradeGuild => demand.Contacts,
            BuildingKind.ArcaneSanctum or BuildingKind.SacredGrove => demand.MagicTalent,
            BuildingKind.Infirmary => demand.Patients && !Has(BuildingKind.Hospital),
            BuildingKind.MountainPass or BuildingKind.Bridge => false,
            BuildingKind.Dock or BuildingKind.Shipyard => demand.Coast && stock.Boats < 2 && demand.Adults.Any(p =>
                p.Profession is Profession.Trader or Profession.Messenger or Profession.Fisher),
            BuildingKind.LumberCamp => demand.Timber && demand.Adults.Any(p => p.Profession == Profession.Lumberjack) &&
                                       stock.Wood < 40,
            BuildingKind.Quarry or BuildingKind.MiningHall => demand.Stone &&
                                                              demand.Adults.Any(p =>
                                                                  p.Profession == Profession.Miner) &&
                                                              stock.Stone + stock.Ore < 60,
            BuildingKind.Well => demand.Water && !Has(BuildingKind.Reservoir),
            BuildingKind.Granary => town.Population >= 60 && stock.Food >= town.Population,
            BuildingKind.Housing => town.Population >= GetHousingCapacity(town.Id),
            BuildingKind.Watchtower or BuildingKind.WarDrum or BuildingKind.Armory or BuildingKind.WardTower
                or BuildingKind.StormSpire => demand.Defense,
            BuildingKind.AssemblyHall => town.Population >= 40,
            BuildingKind.HerbGarden => demand.Patients,
            BuildingKind.HuntingCamp => stock.Food < town.Population && demand.Adults.Any(p => p.Race == RaceKind.Orc),
            BuildingKind.Reservoir => demand.Water,
            BuildingKind.Hospital => demand.Patients &&
                                     (stock.Medicine >= .25 || Has(BuildingKind.Apothecary) ||
                                      Has(BuildingKind.AlchemyLab)),
            BuildingKind.Apothecary or BuildingKind.AlchemyLab => stock.Medicine < 8 &&
                                                                  (demand.Patients || (State.Rules.Disease &&
                                                                      town.Population >= 60)),
            BuildingKind.FireStation => demand.Buildings.Any(b => b.Health is > 0 and < 90)
                                        || (State.NaturalDisasters && town.Population >= 60) || demand.Adults.Any(p =>
                                            p.Agent.Memory.Any(f =>
                                                f.Kind == AgentFactKind.Danger && f.Value > 0 &&
                                                State.Tick - f.ObservedTick < 24)),
            BuildingKind.Library => State.Society.Research.First(r => r.SettlementId == town.Id).Completed.Count >= 3,
            BuildingKind.SurveyOffice => State.Rules.Expansion || demand.Adults.Any(p =>
                p.Profession is Profession.Miner or Profession.Trader or Profession.Messenger),
            BuildingKind.MachineWorkshop => stock.Tools < 8 &&
                                            demand.Adults.Any(p =>
                                                p.Profession is Profession.Builder or Profession.Engineer),
            BuildingKind.Arsenal => demand.Defense && stock.Ammunition < 24,
            BuildingKind.GroveSanctuary => demand.MagicTalent && !demand.Timber &&
                                           demand.Adults.Any(p => p.Profession == Profession.Lumberjack),
            BuildingKind.Waygate => demand.Contacts && demand.MagicTalent && State.Settlements.Any(t =>
                t.Id != town.Id && t.NationId == town.NationId && Distance(t.X, t.Y, town.X, town.Y) <= 24),
            BuildingKind.Pasture => stock.Food < town.Population * 2 && demand.Adults.Any(p =>
                                                                         p.Profession == Profession.Farmer)
                                                                     && Circle(town.X, town.Y, 6).Any(i =>
                                                                         HusbandryStockAt(i % State.Width,
                                                                             i / State.Width, false).Source >= 0),
            BuildingKind.Aquaculture => demand.Coast && stock.Food < town.Population * 2,
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
            _ when research == Advancement.Elementalism => demand.Defense || State.NaturalDisasters,
            _ when research == Advancement.RailTransport => demand.Roads,
            _ when research == Advancement.SpatialMagic => FacilityNeeded(demand, BuildingKind.Waygate),
            _ when research == Advancement.FireEngineering => FacilityNeeded(demand, BuildingKind.FireStation),
            _ when research == Advancement.Medicine || research == Advancement.Sanitation
                                                    || research == Advancement.Restoration => demand.Patients ||
                State.Rules.Disease,
            _ when research == Advancement.Pharmacology || research == Advancement.Alchemy => FacilityNeeded(demand,
                BuildingKind.Apothecary),
            _ when research == Advancement.NatureBinding => FacilityNeeded(demand, BuildingKind.GroveSanctuary) ||
                                                            State.NaturalDisasters,
            _ when research == Advancement.Observation || research == Advancement.SignalNetwork => demand.Contacts ||
                demand.Adults.Any(p =>
                    p.Profession == Profession.Miner),
            _ when research == Advancement.EfficientSmelting => demand.Buildings.Any(b =>
                b.Kind is BuildingKind.Foundry or BuildingKind.DwarvenForge),
            _ when research == Advancement.EnergyRecycling => demand.Buildings.Any(b =>
                b.Kind == BuildingKind.PowerPlant),
            _ when research == Advancement.Leylines => demand.Buildings.Any(b => b.Kind == BuildingKind.Crystallizer),
            _ => true,
        };
        if (!useful)
            return 0;
        // 基本生计和下一条可用生产链优先，避免资源先被次要改进占用。
        return research switch
        {
            _ when research == Advancement.Agriculture => 100,
            _ when research == Advancement.Logistics => 95,
            _ when research == Advancement.Irrigation => town.Resources.Food < town.Population ? 90 : 55,
            _ when research == Advancement.Forestry => town.Resources.Wood + town.Resources.Stone < 40 ? 85 : 50,
            _ when research == Advancement.Medicine || research == Advancement.Sanitation
                                                    || research == Advancement.Pharmacology =>
                demand.Patients ? 88 : 45,
            _ when research == Advancement.FireEngineering => demand.Buildings.Any(b => b.Health < 90) ? 88 : 40,
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
        Settlement Town,
        Resident[] Adults,
        IReadOnlyList<Building> Buildings,
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
