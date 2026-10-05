using Avalonia;

namespace SeWZC.WorldBox.UI.Controls;

/// <summary>Samples the complete committed movement in simulation time, independent of snapshot cadence.</summary>
internal sealed class EntityMotionTrack(Point position)
{
    private double _duration;
    private Point _from = position;
    private long _started = -1;
    public Point Target { get; private set; } = position;
    public long SeenRevision { get; set; }

    /// <summary>按模拟时间对已提交的移动轨迹插值，位置限制在轨迹两端之间。</summary>
    public Point Position(double simulationTime)
    {
        if (_duration <= 0) return Target;
        var fraction = Math.Clamp((simulationTime - _started) / _duration, 0, 1);
        return new Point(_from.X + (Target.X - _from.X) * fraction,
            _from.Y + (Target.Y - _from.Y) * fraction);
    }

    public bool IsMoving(double simulationTime)
    {
        return _duration > 0 && simulationTime < _started + _duration;
    }

    /// <summary>根据逻辑状态更新移动区段；需要立即定位时直接显示目标位置。</summary>
    public void Update(Point from, Point target, long startedTick, int durationTicks, bool snap)
    {
        if (!snap && target == Target && startedTick == _started) return;
        _from = from;
        Target = target;
        _started = startedTick;
        _duration = snap ? 0 : Math.Max(1, durationTicks);
    }
}
