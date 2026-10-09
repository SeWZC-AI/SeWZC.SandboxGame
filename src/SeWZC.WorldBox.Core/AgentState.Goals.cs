namespace SeWZC.WorldBox.Core;

public sealed partial record AgentState
{
    // 无变化的目标复用原认知值，避免日常动作反复复制相同状态。
    internal AgentState WithGoal(AgentGoal goal)
    {
        return ReferenceEquals(Goal, goal) || Goal.Equals(goal)
            ? this
            : this with { Goal = goal, DaytimeGoal = goal.Kind == AgentGoalKind.Sleep ? DaytimeGoal : null };
    }
}
