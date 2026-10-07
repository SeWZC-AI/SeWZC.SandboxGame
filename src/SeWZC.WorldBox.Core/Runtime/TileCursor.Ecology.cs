namespace SeWZC.WorldBox.Core.Runtime;

internal sealed partial class TileCursor
{
    private byte _edibleLand = byte.MaxValue, _edibleWater = byte.MaxValue;
    private WildlifeKind _landCapacityKind, _waterCapacityKind;
    private double _landCapacity, _waterCapacity;
    internal Action<TileCursor, global::SeWZC.WorldBox.Core.Tile, global::SeWZC.WorldBox.Core.Tile>? Changed { get; set; }

    protected override void OnReplace(in global::SeWZC.WorldBox.Core.Tile before, in global::SeWZC.WorldBox.Core.Tile after)
    {
        if (before.Wildlife != after.Wildlife || before.WildlifePopulation != after.WildlifePopulation || before.OtherWildlife != after.OtherWildlife)
            _edibleLand = _edibleWater = byte.MaxValue;
        if (before.Terrain != after.Terrain || before.Fertility != after.Fertility || before.Plants != after.Plants
            || before.NaturalWaterYield != after.NaturalWaterYield || before.Improvement != after.Improvement
            || (before.SettlementId != 0) != (after.SettlementId != 0)
            || (before.DroughtTicks > 0) != (after.DroughtTicks > 0) || (before.FireTicks > 0) != (after.FireTicks > 0)
            || Math.Min(100, before.ResourceAmount) != Math.Min(100, after.ResourceAmount))
            _landCapacityKind = _waterCapacityKind = WildlifeKind.None;
        Changed?.Invoke(this, before, after);
    }
    public double AnimalPopulation(WildlifeKind kind) => Value.AnimalPopulation(kind);
    internal void CopyAnimalPopulations(Span<double> destination) => Value.CopyAnimalPopulations(destination);
    internal void SetAnimalPopulation(WildlifeKind kind, double population) => Replace(Value.WithAnimalPopulation(kind, population));
    internal WildlifeKind EdibleAnimal(bool aquatic)
    {
        ref var cached = ref aquatic ? ref _edibleWater : ref _edibleLand;
        if (cached == byte.MaxValue) cached = (byte)Value.EdibleAnimal(aquatic);
        return (WildlifeKind)cached;
    }

    internal double EnvironmentalCapacity(WildlifeKind kind)
    {
        if (kind == WildlifeKind.None) return 0;
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
}
