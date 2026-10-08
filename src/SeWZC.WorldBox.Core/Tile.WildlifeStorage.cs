using System.Numerics;

namespace SeWZC.WorldBox.Core;

public sealed partial record Tile
{
    private WildlifeStorage _wildlife = WildlifeStorage.Empty;

    internal bool SameOtherWildlife(Tile other)
    {
        return ReferenceEquals(_wildlife, other._wildlife) || _wildlife.Equals(other._wildlife);
    }

    // 日内生态缓冲只在构造时读取，种群存储持有自己新建的数组。
    internal Tile WithAnimalPopulations(WildlifeKind primary, ReadOnlySpan<double> populations)
    {
        return this with
        {
            Wildlife = primary,
            WildlifePopulation = populations[(int)primary],
            _wildlife = WildlifeStorage.Create(populations, primary),
        };
    }

    // 取水、道路与灾害更新共享未改变的种群，避免每次复制全部三十一种动物数量。
    private sealed record WildlifeStorage
    {
        internal static readonly WildlifeStorage Empty = new(0, []);
        internal readonly int ActiveMask;
        private readonly uint _mask;
        private readonly double[] _populations;

        /// <summary>接管种群紧凑数组；调用后不再写入该数组。</summary>
        /// <param name="mask">非正零种群的物种位掩码，不包含 None。</param>
        /// <param name="populations">按掩码内物种编号升序排列的数量，长度等于置位数。</param>
        private WildlifeStorage(uint mask, double[] populations)
        {
            _mask = mask;
            _populations = populations;
            var remaining = mask;
            var index = 0;
            while (remaining != 0)
            {
                var bit = remaining & (0u - remaining);
                if (populations[index++] > 0)
                    ActiveMask |= (int)bit;
                remaining &= remaining - 1;
            }
        }

        public bool Equals(WildlifeStorage? other)
        {
            if (ReferenceEquals(this, other))
                return true;
            if (other is null)
                return false;
            var remaining = _mask | other._mask;
            while (remaining != 0)
            {
                var species = (WildlifeKind)BitOperations.TrailingZeroCount(remaining);
                if (!Get(species).Equals(other.Get(species)))
                    return false;
                remaining &= remaining - 1;
            }

            return true;
        }

        internal static WildlifeStorage Create(WildlifePopulations populations)
        {
            Span<double> values = stackalloc double[AnimalRules.SpeciesCount];
            populations.CopyTo(values);
            return Create(values, WildlifeKind.None);
        }

        internal static WildlifeStorage Create(ReadOnlySpan<double> populations, WildlifeKind primary)
        {
            var mask = 0u;
            for (var species = 1; species < AnimalRules.SpeciesCount; species++)
                // 保存负零及非法值的原始位，不能让存档校验因压缩而漏掉非法值。
                if (species != (int)primary && BitConverter.DoubleToInt64Bits(populations[species]) != 0)
                    mask |= 1u << species;
            if (mask == 0)
                return Empty;
            var values = new double[BitOperations.PopCount(mask)];
            var remaining = mask;
            var index = 0;
            while (remaining != 0)
            {
                values[index++] = populations[BitOperations.TrailingZeroCount(remaining)];
                remaining &= remaining - 1;
            }

            return new WildlifeStorage(mask, values);
        }

        internal double Get(WildlifeKind kind)
        {
            if ((uint)kind is 0 or >= AnimalRules.SpeciesCount)
                return 0;
            var bit = 1u << (int)kind;
            return (_mask & bit) == 0 ? 0 : _populations[BitOperations.PopCount(_mask & (bit - 1))];
        }

        internal void CopyTo(Span<double> destination)
        {
            destination[..AnimalRules.SpeciesCount].Clear();
            var remaining = _mask;
            var index = 0;
            while (remaining != 0)
            {
                destination[BitOperations.TrailingZeroCount(remaining)] = _populations[index++];
                remaining &= remaining - 1;
            }
        }

        internal WildlifePopulations ToPopulations()
        {
            var result = new WildlifePopulations();
            var remaining = _mask;
            var index = 0;
            while (remaining != 0)
            {
                result = result.WithPopulation((WildlifeKind)BitOperations.TrailingZeroCount(remaining),
                    _populations[index++]);
                remaining &= remaining - 1;
            }

            return result;
        }

        internal WildlifeStorage WithPopulation(WildlifeKind kind, double population)
        {
            if ((uint)kind is 0 or >= AnimalRules.SpeciesCount
                || BitConverter.DoubleToInt64Bits(Get(kind)) == BitConverter.DoubleToInt64Bits(population))
                return this;
            Span<double> values = stackalloc double[AnimalRules.SpeciesCount];
            CopyTo(values);
            values[(int)kind] = population;
            return Create(values, WildlifeKind.None);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            for (var species = 1; species < AnimalRules.SpeciesCount; species++)
                hash.Add(Get((WildlifeKind)species));
            return hash.ToHashCode();
        }
    }
}
