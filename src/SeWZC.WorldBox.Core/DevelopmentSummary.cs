namespace SeWZC.WorldBox.Core;

/// <summary>供界面展示的聚落发展概况。</summary>
/// <param name="Stage">当前发展阶段名称。</param>
/// <param name="Goal">当前项目或发展计划的目标说明。</param>
/// <param name="Blocker">项目执行条件或发展阻碍说明。</param>
/// <param name="Progress">当前项目已完成的比例，没有项目时为零。</param>
public readonly record struct DevelopmentSummary(string Stage, string Goal, string Blocker, double Progress);
