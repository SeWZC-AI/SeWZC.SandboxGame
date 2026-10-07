namespace SeWZC.WorldBox.Core;

/// <summary>一次地图动画的呈现通知。</summary>
/// <param name="Sequence">该通知在当前引擎中的递增序号。</param>
/// <param name="Kind">行动特效类别。</param>
/// <param name="X">特效目标的横向地格坐标。</param>
/// <param name="Y">特效目标的纵向地格坐标。</param>
/// <param name="Radius">特效范围，以地格为单位。</param>
/// <param name="FromX">特效起点的横向地格坐标，-1 表示未指定。</param>
/// <param name="FromY">特效起点的纵向地格坐标，-1 表示未指定。</param>
public readonly record struct WorldVisual(
    long Sequence,
    WorldVisualKind Kind,
    int X,
    int Y,
    int Radius,
    int FromX,
    int FromY);
