/// <summary>标记聚焦且有界的命令、查询或规则检查，供快速单元套件筛选。</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class UnitTestAttribute : Attribute;
