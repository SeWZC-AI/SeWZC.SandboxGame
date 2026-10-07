namespace SeWZC.WorldBox.Core.Runtime;

internal sealed partial class TileCursor
{
    private byte _edibleLand = byte.MaxValue, _edibleWater = byte.MaxValue;
    internal Action<global::SeWZC.WorldBox.Core.Tile, global::SeWZC.WorldBox.Core.Tile>? Changed { get; set; }

    protected override void OnReplace(global::SeWZC.WorldBox.Core.Tile before, global::SeWZC.WorldBox.Core.Tile after)
    {
        if (before.Wildlife != after.Wildlife || before.WildlifePopulation != after.WildlifePopulation || before.OtherWildlife != after.OtherWildlife)
            _edibleLand = _edibleWater = byte.MaxValue;
        Changed?.Invoke(before, after);
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
}
