using Avalonia;

namespace SeWZC.WorldBox.UI.Controls;

/// <summary>按模拟时间保存并采样已提交的移动区段，不依赖界面快照频率。</summary>
/// <param name="position">初始逻辑位置，以地格坐标计。</param>
internal sealed class EntityMotionTrack(Point position)
{
    private double _duration;
    private Point _from = position;
    private long _started = -1;

    /// <summary>当前已提交移动区段的终点，以地格坐标计。</summary>
    public Point Target { get; private set; } = position;

    /// <summary>最近一次世界呈现快照检查到此实体的版本号。</summary>
    public long SeenRevision { get; set; }

    /// <summary>按模拟时间对已提交的移动轨迹插值，位置限制在轨迹两端之间。</summary>
    /// <param name="simulationTime">用于采样的模拟日序，允许小数表示日内进度。</param>
    public Point Position(double simulationTime)
    {
        if (_duration <= 0) return Target;
        var fraction = Math.Clamp((simulationTime - _started) / _duration, 0, 1);
        return new Point(_from.X + (Target.X - _from.X) * fraction,
            _from.Y + (Target.Y - _from.Y) * fraction);
    }

    /// <summary>判断指定模拟时刻是否尚未到达移动区段的终点。</summary>
    /// <param name="simulationTime">用于判断的模拟日序，允许小数。</param>
    public bool IsMoving(double simulationTime)
    {
        return _duration > 0 && simulationTime < _started + _duration;
    }

    /// <summary>根据逻辑状态更新移动区段；需要立即定位时直接显示目标位置。</summary>
    /// <param name="from">已提交移动区段的起点，以地格坐标计。</param>
    /// <param name="target">已提交移动区段的终点，以地格坐标计。</param>
    /// <param name="startedTick">移动区段开始的模拟日序。</param>
    /// <param name="durationTicks">移动区段所需的模拟日数，至少按一日处理。</param>
    /// <param name="snap">是否立即显示终点而不插值。</param>
    public void Update(Point from, Point target, long startedTick, int durationTicks, bool snap)
    {
        if (!snap && target == Target && startedTick == _started) return;
        _from = from;
        Target = target;
        _started = startedTick;
        _duration = snap ? 0 : Math.Max(1, durationTicks);
    }
}
