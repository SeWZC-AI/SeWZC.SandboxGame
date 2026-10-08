namespace SeWZC.WorldBox.Core.Runtime;

internal sealed partial class TileCursor
{
    private byte _edibleLand = byte.MaxValue, _edibleWater = byte.MaxValue;
    private double _foodHarvestEfficiency, _woodHarvestEfficiency, _foodSiteYield, _woodSiteYield;
    private double _landCapacity, _waterCapacity;
    private WildlifeKind _landCapacityKind, _waterCapacityKind;
    private double _landHarvestEfficiency, _waterHarvestEfficiency;
    private WildlifeKind _landHarvestKind, _waterHarvestKind;
    private byte _plantQueries;
    internal Action<TileCursor, Tile, Tile>? Changed { get; set; }

    protected override void OnReplace(in Tile before, in Tile after)
    {
        if (before.ResourceAmount != after.ResourceAmount || before.Plants != after.Plants
                                                          || before.Terrain != after.Terrain ||
                                                          before.Fertility != after.Fertility
                                                          || before.Improvement != after.Improvement ||
                                                          before.DroughtTicks > 0 != after.DroughtTicks > 0)
            _plantQueries = 0;
        var populationChanged = before.Wildlife != after.Wildlife ||
                                before.WildlifePopulation != after.WildlifePopulation ||
                                !before.SameOtherWildlife(after);
        if (populationChanged)
        {
            _edibleLand = _edibleWater = byte.MaxValue;
            _landHarvestKind = _waterHarvestKind = WildlifeKind.None;
        }

        var habitatChanged = before.Terrain != after.Terrain || before.Fertility != after.Fertility ||
                             before.Plants != after.Plants
                             || before.NaturalWaterYield != after.NaturalWaterYield ||
                             before.Improvement != after.Improvement
                             || before.SettlementId != 0 != (after.SettlementId != 0)
                             || before.DroughtTicks > 0 != after.DroughtTicks > 0 ||
                             before.FireTicks > 0 != after.FireTicks > 0
                             || Math.Min(100, before.ResourceAmount) != Math.Min(100, after.ResourceAmount);
        if (habitatChanged)
        {
            _landCapacityKind = _waterCapacityKind = WildlifeKind.None;
            _landHarvestKind = _waterHarvestKind = WildlifeKind.None;
        }

        Changed?.Invoke(this, before, after);
    }

    internal double PlantHarvestEfficiency(bool wood)
    {
        var flag = wood ? 2 : 1;
        ref var efficiency = ref wood ? ref _woodHarvestEfficiency : ref _foodHarvestEfficiency;
        if ((_plantQueries & flag) == 0)
        {
            var tile = Value;
            var farmland = tile.Improvement == LandImprovement.Farmland && !wood;
            var stock = tile.ResourceAmount * (farmland ? 1
                : wood ? tile.Plants.Trees
                : tile.Plants.Shrubs + tile.Plants.Grass + tile.Plants.Reeds);
            var density = Math.Clamp(stock / 20, 0, 1);
            efficiency = farmland ? 1 : density * density;
            _plantQueries |= (byte)flag;
        }

        return efficiency;
    }

    internal double PlantSiteYield(bool wood)
    {
        var flag = wood ? 8 : 4;
        ref var yield = ref wood ? ref _woodSiteYield : ref _foodSiteYield;
        if ((_plantQueries & flag) == 0)
        {
            var tile = Value;
            yield = wood
                ? tile.ResourceAmount > 0 && WorldEngine.IsForestTerrain(tile.Terrain)
                    ? TerrainRules.For(tile.Terrain).WoodYield * PlantHarvestEfficiency(true)
                    : 0
                : tile.ResourceAmount > 0 && tile.IsWalkable
                    ? Math.Min(1, TerrainRules.For(tile.Terrain).FoodYield / .7) * tile.Fertility / 100d
                      * (tile.DroughtTicks > 0 ? .15 : 1) * PlantHarvestEfficiency(false)
                    : 0;
            _plantQueries |= (byte)flag;
        }

        return yield;
    }

    public double AnimalPopulation(WildlifeKind kind)
    {
        return Value.AnimalPopulation(kind);
    }

    internal void CopyAnimalPopulations(Span<double> destination)
    {
        Value.CopyAnimalPopulations(destination);
    }

    internal void SetAnimalPopulation(WildlifeKind kind, double population)
    {
        Replace(Value.WithAnimalPopulation(kind, population));
    }

    internal WildlifeKind EdibleAnimal(bool aquatic)
    {
        ref var cached = ref aquatic ? ref _edibleWater : ref _edibleLand;
        if (cached == byte.MaxValue)
            cached = (byte)Value.EdibleAnimal(aquatic);
        return (WildlifeKind)cached;
    }

    internal double EnvironmentalCapacity(WildlifeKind kind)
    {
        if (kind == WildlifeKind.None)
            return 0;
        var aquatic = AnimalRules.For(kind).Aquatic;
        ref var cachedKind = ref aquatic ? ref _waterCapacityKind : ref _landCapacityKind;
        ref var capacity = ref aquatic ? ref _waterCapacity : ref _landCapacity;
        if (cachedKind != kind)
        {
            capacity = AnimalRules.EnvironmentalCapacity(Value, kind);
            cachedKind = kind;
        }

        return capacity;
    }

    internal double HarvestEfficiency(WildlifeKind kind)
    {
        if (kind == WildlifeKind.None)
            return 0;
        var aquatic = AnimalRules.For(kind).Aquatic;
        ref var cachedKind = ref aquatic ? ref _waterHarvestKind : ref _landHarvestKind;
        ref var efficiency = ref aquatic ? ref _waterHarvestEfficiency : ref _landHarvestEfficiency;
        if (cachedKind != kind)
        {
            var density = Math.Min(1, AnimalPopulation(kind) / Math.Max(.05, EnvironmentalCapacity(kind)));
            efficiency = density * density;
            cachedKind = kind;
        }

        return efficiency;
    }
}
