using Avalonia;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI.Controls;

/// <summary>研究树中从前置研究到后续研究的折线连接。</summary>
/// <param name="From">作为前置的研究节点。</param>
/// <param name="To">依赖该前置的后续研究节点。</param>
/// <param name="Points">在未缩放画布中的连接折线顶点。</param>
public sealed record ResearchTreeEdge(ResearchKind From, ResearchKind To, Point[] Points);
