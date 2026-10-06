using Avalonia;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI.Controls;

/// <summary>研究树中从前置研究到后续研究的折线连接。</summary>
/// <param name="from">作为前置的研究节点。</param>
/// <param name="to">依赖该前置的后续研究节点。</param>
/// <param name="points">在未缩放画布中的连接折线顶点。</param>
public sealed class ResearchTreeEdge(ResearchKind from, ResearchKind to, Point[] points)
{
    /// <summary>作为前置的研究节点。</summary>
    public ResearchKind From { get; } = from;
    /// <summary>依赖该前置的后续研究节点。</summary>
    public ResearchKind To { get; } = to;
    /// <summary>在未缩放画布中的折线顶点，构造时复制。</summary>
    public IReadOnlyList<Point> Points { get; } = Array.AsReadOnly((Point[])points.Clone());
}
