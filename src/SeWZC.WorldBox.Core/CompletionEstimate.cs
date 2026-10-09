namespace SeWZC.WorldBox.Core;

/// <summary>项目完工时间的估算结果。</summary>
/// <param name="RemainingTicks">预计剩余模拟 tick 数，空值表示当前无法可靠估算。</param>
/// <param name="Explanation">估算依据或无法估算的原因。</param>
public readonly record struct CompletionEstimate(long? RemainingTicks, string Explanation);
