namespace SeWZC.WorldBox.UI.Controls;

/// <summary>研究树中一个分支的背景区域。</summary>
/// <param name="Name">分支显示名称。</param>
/// <param name="Left">分支背景在未缩放画布中的左边界。</param>
/// <param name="Width">分支背景的未缩放宽度。</param>
public sealed record ResearchTreeLane(string Name, double Left, double Width);
