namespace SeWZC.WorldBox.Core;

/// <summary>由不可变地格输入推导的生态查询缓存，不持有实体或状态提交入口。</summary>
internal struct TileQueryCache
{
    private byte _edibleLand, _edibleWater, _queries;
    private double _foodHarvestEfficiency, _woodHarvestEfficiency, _foodSiteYield, _woodSiteYield;
    private double _landCapacity, _waterCapacity;
    private WildlifeKind _landCapacityKind, _waterCapacityKind;
    private double _landHarvestEfficiency, _waterHarvestEfficiency;
    private WildlifeKind _landHarvestKind, _waterHarvestKind;

    internal void Invalidate(in TileChange change)
    {
        if (change.Plants)
            _queries &= 48;
        if (change.Population)
        {
            _queries &= 15;
            _landHarvestKind = _waterHarvestKind = WildlifeKind.None;
        }

        if (change.Habitat)
        {
            _landCapacityKind = _waterCapacityKind = WildlifeKind.None;
            _landHarvestKind = _waterHarvestKind = WildlifeKind.None;
        }
    }

    internal double PlantHarvestEfficiency(Tile tile, bool wood)
    {
        var flag = wood ? 2 : 1;
        ref var efficiency = ref wood ? ref _woodHarvestEfficiency : ref _foodHarvestEfficiency;
        if ((_queries & flag) == 0)
        {
            efficiency = tile.PlantHarvestEfficiency(wood);
            _queries |= (byte)flag;
        }

        return efficiency;
    }

    internal double PlantSiteYield(Tile tile, bool wood)
    {
        var flag = wood ? 8 : 4;
        ref var yield = ref wood ? ref _woodSiteYield : ref _foodSiteYield;
        if ((_queries & flag) == 0)
        {
            yield = tile.PlantSiteYield(wood, PlantHarvestEfficiency(tile, wood));
            _queries |= (byte)flag;
        }

        return yield;
    }

    internal WildlifeKind EdibleAnimal(Tile tile, bool aquatic)
    {
        ref var cached = ref aquatic ? ref _edibleWater : ref _edibleLand;
        var flag = aquatic ? 32 : 16;
        if ((_queries & flag) == 0)
        {
            cached = (byte)tile.EdibleAnimal(aquatic);
            _queries |= (byte)flag;
        }

        return (WildlifeKind)cached;
    }

    internal double EnvironmentalCapacity(Tile tile, WildlifeKind kind)
    {
        if (kind == WildlifeKind.None)
            return 0;
        var aquatic = AnimalRules.For(kind).Aquatic;
        ref var cachedKind = ref aquatic ? ref _waterCapacityKind : ref _landCapacityKind;
        ref var capacity = ref aquatic ? ref _waterCapacity : ref _landCapacity;
        if (cachedKind != kind)
        {
            capacity = AnimalRules.EnvironmentalCapacity(tile, kind);
            cachedKind = kind;
        }

        return capacity;
    }

    internal double HarvestEfficiency(Tile tile, WildlifeKind kind)
    {
        if (kind == WildlifeKind.None)
            return 0;
        var aquatic = AnimalRules.For(kind).Aquatic;
        ref var cachedKind = ref aquatic ? ref _waterHarvestKind : ref _landHarvestKind;
        ref var efficiency = ref aquatic ? ref _waterHarvestEfficiency : ref _landHarvestEfficiency;
        if (cachedKind != kind)
        {
            var density = Math.Min(1, tile.AnimalPopulation(kind) / Math.Max(.05, EnvironmentalCapacity(tile, kind)));
            efficiency = density * density;
            cachedKind = kind;
        }

        return efficiency;
    }
}
