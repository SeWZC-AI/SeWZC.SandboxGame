namespace SeWZC.WorldBox.Core;

public sealed partial record Tile
{
    private WildlifeStorage _wildlife = WildlifeStorage.Empty;

    internal bool SameOtherWildlife(Tile other) =>
        ReferenceEquals(_wildlife, other._wildlife) || _wildlife.Populations.Equals(other._wildlife.Populations);

    // 取水、道路与灾害更新共享未改变的种群，避免每次复制全部三十一种动物数量。
    private sealed record WildlifeStorage
    {
        internal static readonly WildlifeStorage Empty = new(default(WildlifePopulations));
        internal readonly WildlifePopulations Populations;

        internal WildlifeStorage(WildlifePopulations populations) => Populations = populations;
    }
}
