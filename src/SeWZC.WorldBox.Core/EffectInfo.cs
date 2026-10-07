namespace SeWZC.WorldBox.Core;

/// <summary>供详情界面展示的一项效果说明。</summary>
/// <param name="Name">效果名称。</param>
/// <param name="Effect">实际效果或限制的说明。</param>
/// <param name="Source">产生效果的来源。</param>
/// <param name="RemainingDays">剩余模拟日数，空值表示没有明确期限。</param>
/// <param name="Active">效果当前是否实际生效。</param>
public readonly record struct EffectInfo(
    string Name,
    string Effect,
    string Source,
    long? RemainingDays = null,
    bool Active = true)
{
    /// <summary>返回效果的详情文字。</summary>
    public override string ToString()
    {
        return $"{Name}：{Effect}{(string.IsNullOrWhiteSpace(Source) ? "" : $"\n来源：{Source}")}" +
               (RemainingDays.HasValue ? $"   剩余 {RemainingDays} 日" : "");
    }
}
