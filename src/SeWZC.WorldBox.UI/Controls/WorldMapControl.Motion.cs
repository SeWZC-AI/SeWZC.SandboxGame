using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI.Controls;

public sealed partial class WorldMapControl
{
    private readonly Stopwatch _presentationClock = Stopwatch.StartNew();
    private readonly Dictionary<int, EntityMotionTrack> _residentMotion = [];
    private readonly Dictionary<int, EntityMotionTrack> _armyMotion = [];
    private readonly Dictionary<int, Point> _renderedResidentPoints = [];
    private readonly List<int> _expiredMotion = [];
    private double _frozenPresentationTime;
    private double _resumedAt;
    private bool _simulationPaused;
    private bool _framePending;
    private bool _motionAttached;
    private long _snapshotTick = -1;
    private long _motionRevision;
    private int _motionEpoch;
    private bool _residentGeometryDirty = true;
    private double _geometryZoom;
    private Point _geometryOrigin;
    private Point? _lastResidentClick;
    private int _residentClickCycle;
    private bool _followSelectedResident;
    private double _simulationTickDurationSeconds = .2;
    private double _renderFrameTime;

    public bool IsSimulationPaused
    {
        get => _simulationPaused;
        set
        {
            if (_simulationPaused == value) return;
            _frozenPresentationTime = PresentationTime;
            _resumedAt = _presentationClock.Elapsed.TotalSeconds;
            _simulationPaused = value;
            _residentGeometryDirty = true;
            if (!value) RequestMotionFrame();
            InvalidateVisual();
        }
    }

    public double SimulationTickDurationSeconds
    {
        get => _simulationTickDurationSeconds;
        set
        {
            if (!double.IsFinite(value)) return;
            var next = Math.Clamp(value, .016, 1.5);
            if (Math.Abs(next - _simulationTickDurationSeconds) < .000001) return;
            var ratio = next / _simulationTickDurationSeconds;
            var now = PresentationTime;
            foreach (var motion in _residentMotion.Values) motion.RescaleRemaining(now, ratio);
            foreach (var motion in _armyMotion.Values) motion.RescaleRemaining(now, ratio);
            _simulationTickDurationSeconds = next;
            RequestMotionFrame();
        }
    }
    public int? SelectedResidentId { get; private set; }
    public event Action<int>? ResidentSelected;

    public bool FollowSelectedResident
    {
        get => _followSelectedResident;
        set
        {
            _followSelectedResident = value && SelectedResidentId.HasValue;
            if (_followSelectedResident) { FollowResident(); RequestMotionFrame(); }
            InvalidateVisual();
        }
    }

    private double PresentationTime => _frozenPresentationTime +
        (_simulationPaused ? 0 : _presentationClock.Elapsed.TotalSeconds - _resumedAt);

    public Point GetTileScreenPosition(int x, int y) => ToScreen((x + .5) * TilePixels, (y + .5) * TilePixels);

    public bool TryGetResidentScreenPosition(int residentId, out Point position)
    {
        // Report the geometry used by the most recent Render, not a position extrapolated
        // merely because an inspector or automated observer happened to read the property.
        return _renderedResidentPoints.TryGetValue(residentId, out position);
    }

    public void SelectResident(int residentId, bool follow = false)
    {
        if (!_residentMotion.ContainsKey(residentId)) { ClearResidentSelection(); return; }
        SelectedResidentId = residentId;
        CaptureSelectedRoute();
        _selection = null;
        FollowSelectedResident = follow;
        InvalidateVisual();
    }

    public void FocusResident(int residentId)
    {
        if (!_residentMotion.TryGetValue(residentId, out var motion)) return;
        SelectResident(residentId, FollowSelectedResident);
        var position = motion.Position(PresentationTime);
        _zoom = Math.Max(_zoom, 2.4);
        _origin = new Point(Bounds.Width / 2 - (position.X + .5) * TilePixels * _zoom,
                            Bounds.Height / 2 - (position.Y + .5) * TilePixels * _zoom);
        _cameraReady = true;
        InvalidateVisual();
    }

    public void ClearResidentSelection()
    {
        SelectedResidentId = null;
        _followSelectedResident = false;
        InvalidateVisual();
    }

    private void ResetMotion()
    {
        _residentMotion.Clear();
        _armyMotion.Clear();
        _renderedResidentPoints.Clear();
        _snapshotTick = -1;
        _lastResidentClick = null;
        _residentClickCycle = 0;
        _motionEpoch++;
        _framePending = false;
        _residentGeometryDirty = true;
        ClearResidentSelection();
    }

    private void CaptureMotionSnapshots()
    {
        if (Engine is null) return;
        var state = Engine.State;
        var now = PresentationTime;
        var elapsedTicks = Math.Max(0, state.Tick - _snapshotTick);
        var reset = _snapshotTick < 0 || state.Tick < _snapshotTick;
        var sameTick = state.Tick == _snapshotTick;
        _motionRevision++;
        foreach (var resident in state.Residents)
            Capture(_residentMotion, resident.Id, resident.X, resident.Y,
                resident.FromX, resident.FromY, resident.MoveStartedTick, resident.MoveDurationTicks);
        foreach (var army in state.Armies)
            Capture(_armyMotion, army.Id, army.X, army.Y,
                army.FromX, army.FromY, army.MoveStartedTick, army.MoveDurationTicks);
        RemoveExpired(_residentMotion);
        RemoveExpired(_armyMotion);
        if (SelectedResidentId is { } selected && !_residentMotion.ContainsKey(selected)) ClearResidentSelection();
        _snapshotTick = state.Tick;
        _residentGeometryDirty = true;
        FollowResident();
        RequestMotionFrame();

        void Capture(Dictionary<int, EntityMotionTrack> tracks, int id, int x, int y,
            int fromX, int fromY, long moveStartedTick, int moveDurationTicks)
        {
            var target = new Point(x, y);
            if (!tracks.TryGetValue(id, out var track)) tracks[id] = track = new EntityMotionTrack(target);
            var displacement = Distance(track.Target, target);
            var editedPosition = sameTick && displacement > 0;
            // A local edit/teleport must never sweep a unit across the map. Snapshot gaps may
            // contain several real steps, but only bounded adjacent movement is interpolated.
            var teleport = displacement > Math.Max(2, Math.Min(6, elapsedTicks * 2));
            var remainingTicks = Math.Max(0, Math.Max(1, moveDurationTicks) - (state.Tick - moveStartedTick));
            var duration = remainingTicks * SimulationTickDurationSeconds;
            var committedStep = Math.Abs(x - fromX) + Math.Abs(y - fromY) == 1;
            track.Update(target, now, duration, reset || editedPosition || teleport ||
                displacement > 0 && (!committedStep || remainingTicks == 0));
            track.SeenRevision = _motionRevision;
        }

        void RemoveExpired(Dictionary<int, EntityMotionTrack> tracks)
        {
            _expiredMotion.Clear();
            foreach (var pair in tracks)
                if (pair.Value.SeenRevision != _motionRevision) _expiredMotion.Add(pair.Key);
            foreach (var id in _expiredMotion) tracks.Remove(id);
        }
    }

    private void RequestMotionFrame()
    {
        if (!_motionAttached || _framePending || IsSimulationPaused) return;
        var now = PresentationTime;
        if (!_residentMotion.Values.Any(track => track.IsMoving(now)) &&
            !_armyMotion.Values.Any(track => track.IsMoving(now)) && !HasAnimatedEffects(now)) return;
        if (TopLevel.GetTopLevel(this) is not { } topLevel) return;
        _framePending = true;
        var epoch = _motionEpoch;
        topLevel.RequestAnimationFrame(_ =>
        {
            if (epoch != _motionEpoch) return;
            _framePending = false;
            if (!_motionAttached || IsSimulationPaused) return;
            _residentGeometryDirty = true;
            FollowResident();
            InvalidateVisual();
            RequestMotionFrame();
        });
    }

    private void FollowResident(double? frameTime = null)
    {
        if (!_followSelectedResident || SelectedResidentId is not { } id ||
            !_residentMotion.TryGetValue(id, out var motion) || !_cameraReady) return;
        var position = motion.Position(frameTime ?? PresentationTime);
        _origin = new Point(Bounds.Width / 2 - (position.X + .5) * TilePixels * _zoom,
                            Bounds.Height / 2 - (position.Y + .5) * TilePixels * _zoom);
    }

    private bool SelectResidentAt(Point point)
    {
        if (!IsNavigationTool) return false;
        var radius = Math.Clamp(TilePixels * _zoom * .85, 8, 17);
        var candidates = new List<(int Id, double Distance)>();
        foreach (var pair in _renderedResidentPoints)
        {
            var distance = Distance(point, pair.Value);
            if (distance <= radius) candidates.Add((pair.Key, distance));
        }
        if (candidates.Count == 0) { _lastResidentClick = null; return false; }
        candidates.Sort((a, b) =>
        {
            var distance = a.Distance.CompareTo(b.Distance);
            return Math.Abs(a.Distance - b.Distance) < .01 ? a.Id.CompareTo(b.Id) : distance;
        });
        if (_lastResidentClick is { } previous && Distance(previous, point) <= 4) _residentClickCycle++;
        else _residentClickCycle = 0;
        _lastResidentClick = point;
        var selected = candidates[_residentClickCycle % candidates.Count].Id;
        SelectResident(selected, FollowSelectedResident);
        ResidentSelected?.Invoke(selected);
        return true;
    }

    private void DrawResidentSelection(DrawingContext context)
    {
        if (SelectedResidentId is not { } id || !TryGetResidentScreenPosition(id, out var point)) return;
        var radius = Math.Max(6, _zoom * 3.5);
        context.DrawEllipse(null, SelectionPen, point, radius, radius);
        context.DrawLine(SelectionPen, new Point(point.X, point.Y - radius - 5), new Point(point.X, point.Y - radius - 1));
    }
}
