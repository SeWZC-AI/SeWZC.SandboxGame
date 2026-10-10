using Avalonia;
using System.Collections.Immutable;

namespace SeWZC.WorldBox.UI.Controls;

/// <summary>实体的显示移动轨迹，按模拟时间插值位置。</summary>
/// <param name="position">初始逻辑位置，以地格坐标计。</param>
internal sealed class EntityMotionTrack(Point position)
{
    private double _duration;
    private Point _from = position;
    private long _started = -1;
    private ImmutableArray<int> _route = [];
    private int _mapWidth;

    /// <summary>当前已提交移动区段的终点，以地格坐标计。</summary>
    public Point Target { get; private set; } = position;

    /// <summary>最近一次世界呈现快照检查到此实体的版本号。</summary>
    public long SeenRevision { get; set; }

    /// <summary>按模拟时间对已提交的移动轨迹插值，位置限制在轨迹两端之间。</summary>
    /// <param name="simulationTime">用于采样的模拟 tick 序，允许小数表示日内进度。</param>
    public Point Position(double simulationTime)
    {
        if (_duration <= 0)
            return Target;
        var fraction = Math.Clamp((simulationTime - _started) / _duration, 0, 1);
        if (_route.Length > 1 && _mapWidth > 0)
        {
            var progress = fraction * (_route.Length - 1);
            var segment = Math.Min((int)progress, _route.Length - 2);
            var first = _route[segment];
            var second = _route[segment + 1];
            var offset = progress - segment;
            return new Point(first % _mapWidth + (second % _mapWidth - first % _mapWidth) * offset,
                first / _mapWidth + (second / _mapWidth - first / _mapWidth) * offset);
        }
        return new Point(_from.X + (Target.X - _from.X) * fraction,
            _from.Y + (Target.Y - _from.Y) * fraction);
    }

    /// <summary>判断指定模拟时刻是否尚未到达移动区段的终点。</summary>
    /// <param name="simulationTime">用于判断的模拟 tick 序，允许小数。</param>
    public bool IsMoving(double simulationTime)
    {
        return _duration > 0 && simulationTime < _started + _duration;
    }

    /// <summary>根据逻辑状态更新移动区段；需要立即定位时直接显示目标位置。</summary>
    /// <param name="from">已提交移动区段的起点，以地格坐标计。</param>
    /// <param name="target">已提交移动区段的终点，以地格坐标计。</param>
    /// <param name="startedTick">移动区段开始的模拟 tick 序。</param>
    /// <param name="durationTicks">移动区段所需的模拟 tick 数，至少按一 tick 处理。</param>
    /// <param name="snap">是否立即显示终点而不插值。</param>
    /// <param name="route">本次移动经过的相邻地格，包含起点和终点。</param>
    /// <param name="mapWidth">地图宽度，用于还原地格坐标。</param>
    public void Update(Point from, Point target, long startedTick, int durationTicks, bool snap,
        ImmutableArray<int> route = default, int mapWidth = 0)
    {
        if (!snap && target == Target && startedTick == _started)
            return;
        _from = from;
        Target = target;
        _started = startedTick;
        _duration = snap ? 0 : Math.Max(1, durationTicks);
        _route = route.IsDefault ? [] : route;
        _mapWidth = mapWidth;
    }
}
