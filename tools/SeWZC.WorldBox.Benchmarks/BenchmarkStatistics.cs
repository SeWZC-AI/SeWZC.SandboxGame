/// <summary>每日样本的平均值、最近秩 P95 和最大值。</summary>
/// <param name="Mean">所有样本的算术平均值。</param>
/// <param name="P95">排序后第 ceil(样本数 × 0.95) 个样本。</param>
/// <param name="Max">最大样本值。</param>
internal readonly record struct BenchmarkStatistics(double Mean, double P95, double Max)
{
    internal static BenchmarkStatistics From(IEnumerable<double> values)
    {
        var sorted = values.Order().ToArray();
        return new BenchmarkStatistics(sorted.Average(), sorted[(int)Math.Ceiling(sorted.Length * .95) - 1],
            sorted[^1]);
    }
}
