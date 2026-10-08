using System.Collections.Immutable;
using Avalonia;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI.Controls;

/// <summary>研究树中从前置研究到后续研究的折线连接。</summary>
public sealed record ResearchTreeEdge
{
    /// <summary>研究树中从前置研究到后续研究的折线连接。</summary>
    /// <param name="From">作为前置的研究节点。</param>
    /// <param name="To">依赖该前置的后续研究节点。</param>
    /// <param name="Points">在未缩放画布中的连接折线顶点。</param>
    public ResearchTreeEdge(Advancement From, Advancement To, Point[] Points)
    {
        this.From = From;
        this.To = To;
        this.Points = [.. Points];
    }

    /// <summary>依赖连线起点的研究项目。</summary>
    public Advancement From { get; }

    /// <summary>依赖连线终点的研究项目。</summary>
    public Advancement To { get; }

    /// <summary>在未缩放画布中的连接折线顶点。</summary>
    public ImmutableArray<Point> Points { get; }
}
