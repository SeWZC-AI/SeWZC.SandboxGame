/// <summary>标记须显式运行的多种子、大世界或长程模拟回归检查。</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class LongRunningTestAttribute : Attribute;
