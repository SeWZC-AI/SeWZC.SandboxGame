namespace SeWZC.WorldBox.Core;

/// <summary>居民路线预览中的地格坐标。</summary>
/// <param name="X">路线节点的横向地格坐标。</param>
/// <param name="Y">路线节点的纵向地格坐标。</param>
public readonly record struct RoutePoint(int X, int Y);
