using Avalonia;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI.Controls;

/// <summary>研究树中从前置研究到后续研究的折线连接。</summary>
/// <param name="from">作为前置的研究节点。</param>
/// <param name="to">依赖该前置的后续研究节点。</param>
/// <param name="points">在未缩放画布中的连接折线顶点。</param>
public sealed class ResearchTreeEdge(Advancement from, Advancement to, Point[] points)
{
    /// <summary>依赖连线起点的研究项目。</summary>
    public Advancement From { get; } = from;

    /// <summary>依赖连线终点的研究项目。</summary>
    public Advancement To { get; } = to;

    /// <summary>在未缩放画布中的连接折线顶点。</summary>
    public IReadOnlyList<Point> Points { get; } = Array.AsReadOnly((Point[])points.Clone());
}
