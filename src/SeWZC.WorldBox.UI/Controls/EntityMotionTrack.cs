using System;
using Avalonia;

namespace SeWZC.WorldBox.UI.Controls;

/// <summary>
/// Presentation-only interpolation between authoritative simulation snapshots. It never
/// advances the simulation, guesses destinations, or consumes the world's random stream.
/// </summary>
internal sealed class EntityMotionTrack(Point position)
{
    private Point _from = position;
    private double _started;
    private double _duration;
    public Point Target { get; private set; } = position;
    public long SeenRevision { get; set; }

    public Point Position(double presentationTime)
    {
        if (_duration <= 0) return Target;
        var fraction = Math.Clamp((presentationTime - _started) / _duration, 0, 1);
        return new Point(_from.X + (Target.X - _from.X) * fraction,
                         _from.Y + (Target.Y - _from.Y) * fraction);
    }

    public bool IsMoving(double presentationTime) => _duration > 0 && presentationTime < _started + _duration;

    public void RescaleRemaining(double presentationTime, double ratio)
    {
        if (!IsMoving(presentationTime)) return;
        _from = Position(presentationTime);
        _duration = Math.Max(0, _started + _duration - presentationTime) * ratio;
        _started = presentationTime;
    }

    public void Update(Point target, double presentationTime, double duration, bool snap)
    {
        if (snap)
        {
            _from = Target = target;
            _duration = 0;
            return;
        }
        // Repeated snapshots of a resting entity must not restart an interpolation or drift.
        if (target == Target) return;
        _from = Position(presentationTime);
        Target = target;
        _started = presentationTime;
        _duration = double.IsFinite(duration) ? Math.Max(.001, duration) : 0;
    }
}
