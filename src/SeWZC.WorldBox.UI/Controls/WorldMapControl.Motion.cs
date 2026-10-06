using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SeWZC.WorldBox.UI.Controls;

public sealed partial class WorldMapControl
{
    private readonly Dictionary<int, EntityMotionTrack> _armyMotion = [];
    private readonly List<int> _expiredMotion = [];
    private readonly Stopwatch _presentationClock = Stopwatch.StartNew();
    private readonly Dictionary<int, Point> _renderedResidentPoints = [];
    private readonly Dictionary<int, EntityMotionTrack> _residentMotion = [];
    private bool _followSelectedResident;
    private bool _framePending;
    private double _frozenPresentationTime;
    private Point _geometryOrigin;
    private double _geometryZoom;
    private double _lastAnimatedFrameTime;
    private Point? _lastResidentClick;
    private bool _motionAttached;
    private int _motionEpoch;
    private long _motionRevision;
    private double _renderFrameTime;
    private double _renderMotionTime;
    private int _residentClickCycle;
    private bool _residentGeometryDirty = true;
    private double _resumedAt;
    private double _simulationAnchorTick, _simulationAnchorTime;
    private bool _simulationPaused;
    private double _simulationTickDurationSeconds = .2;
    private long _snapshotTick = -1;

    /// <summary>下一模拟日已积累的时间比例，用于确定移动插值的时间基准。</summary>
    public double SimulationTickFraction { get; set; }

    private double MotionTime => _simulationAnchorTick + Math.Min(
        1 - (_simulationAnchorTick - Math.Floor(_simulationAnchorTick)),
        Math.Max(0, PresentationTime - _simulationAnchorTime) / SimulationTickDurationSeconds);

    /// <summary>是否冻结呈现时钟和移动动画。</summary>
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

    /// <summary>当前速度下，移动插值使用的每模拟日实际秒数。</summary>
    public double SimulationTickDurationSeconds
    {
        get => _simulationTickDurationSeconds;
        set
        {
            if (!double.IsFinite(value)) return;
            var next = Math.Clamp(value, .016, 1.5);
            if (Math.Abs(next - _simulationTickDurationSeconds) < .000001) return;
            // 先按旧速度记录当前插值时刻，再调整速度，避免移动位置突然跳变。
            _simulationAnchorTick = MotionTime;
            _simulationAnchorTime = PresentationTime;
            _simulationTickDurationSeconds = next;
            RequestMotionFrame();
        }
    }

    /// <summary>当前选中的居民 ID，空值表示未选中居民。</summary>
    public int? SelectedResidentId { get; private set; }
    /// <summary>当前选中的建筑 ID，空值表示未选中建筑。</summary>
    public int? SelectedBuildingId { get; private set; }

    /// <summary>镜头是否随所选居民的显示位置移动。</summary>
    public bool FollowSelectedResident
    {
        get => _followSelectedResident;
        set
        {
            _followSelectedResident = value && SelectedResidentId.HasValue;
            if (_followSelectedResident)
            {
                FollowResident();
                RequestMotionFrame();
            }

            InvalidateVisual();
        }
    }

    private double PresentationTime => _frozenPresentationTime +
                                       (_simulationPaused ? 0 : _presentationClock.Elapsed.TotalSeconds - _resumedAt);

    /// <summary>通过地图交互选中居民时发出其 ID。</summary>
    public event Action<int>? ResidentSelected;
    /// <summary>通过地图交互选中建筑时发出其 ID。</summary>
    public event Action<int>? BuildingSelected;

    /// <summary>清除居民、建筑和地格选择，并停止跟随。</summary>
    public void ClearMapSelection()
    {
        ClearResidentSelection();
        SelectedBuildingId = null;
        _selection = null;
        InvalidateVisual();
    }

    /// <summary>将地格中心转换为地图控件内的显示坐标。</summary>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    public Point GetTileScreenPosition(int x, int y)
    {
        return ToScreen((x + .5) * TilePixels, (y + .5) * TilePixels);
    }

    /// <summary>读取最近一帧实际绘制的居民位置，未绘制时返回失败。</summary>
    /// <param name="residentId">待操作居民的稳定 ID。</param>
    /// <param name="position">最近一帧实际绘制的位置，单位为控件布局坐标；失败时为默认值。</param>
    public bool TryGetResidentScreenPosition(int residentId, out Point position)
    {
        // 读取最近实际绘制的几何位置，避免仅查看详情或诊断就推动显示位置。
        return _renderedResidentPoints.TryGetValue(residentId, out position);
    }

    /// <summary>选择具有运动轨迹的居民，并可启用镜头跟随。</summary>
    /// <param name="residentId">待操作居民的稳定 ID。</param>
    /// <param name="follow">是否启用镜头跟随所选居民。</param>
    public void SelectResident(int residentId, bool follow = false)
    {
        if (!_residentMotion.ContainsKey(residentId))
        {
            ClearResidentSelection();
            return;
        }

        SelectedResidentId = residentId;
        SelectedBuildingId = null;
        CaptureSelectedRoute();
        _selection = null;
        FollowSelectedResident = follow;
        InvalidateVisual();
    }

    /// <summary>选择居民并将其插值显示位置移到镜头中心。</summary>
    /// <param name="residentId">待操作居民的稳定 ID。</param>
    public void FocusResident(int residentId)
    {
        if (!_residentMotion.TryGetValue(residentId, out var motion)) return;
        SelectResident(residentId, FollowSelectedResident);
        var position = motion.Position(MotionTime);
        _zoom = Math.Max(_zoom, 2.4);
        _origin = new Point(Bounds.Width / 2 - (position.X + .5) * TilePixels * _zoom,
            Bounds.Height / 2 - (position.Y + .5) * TilePixels * _zoom);
        _cameraReady = true;
        InvalidateVisual();
    }

    /// <summary>清除居民选择并停止跟随镜头。</summary>
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
        SimulationTickFraction = 0;
        _lastResidentClick = null;
        _residentClickCycle = 0;
        _motionEpoch++;
        _framePending = false;
        _residentGeometryDirty = true;
        ClearMapSelection();
    }

    private void CaptureMotionSnapshots()
    {
        if (Engine is null) return;
        var state = Engine.State;
        var now = PresentationTime;
        var elapsedTicks = Math.Max(0, state.Tick - _snapshotTick);
        var reset = _snapshotTick < 0 || state.Tick < _snapshotTick;
        var sameTick = state.Tick == _snapshotTick;
        if (!sameTick || reset)
        {
            _simulationAnchorTick = state.Tick + Math.Clamp(SimulationTickFraction, 0, .999999);
            _simulationAnchorTime = now;
        }

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
            // 编辑传送须直接定位，避免人物横扫整张地图；仅对有界相邻移动插值。
            var teleport = displacement > Math.Max(2, Math.Min(6, elapsedTicks * 2));
            var remainingTicks = Math.Max(0, Math.Max(1, moveDurationTicks) - (state.Tick - moveStartedTick));
            var committedStep = Math.Abs(x - fromX) + Math.Abs(y - fromY) == 1;
            track.Update(new Point(fromX, fromY), target, moveStartedTick, moveDurationTicks, reset || editedPosition ||
                teleport ||
                (displacement > 0 && (!committedStep || remainingTicks == 0)));
            track.SeenRevision = _motionRevision;
        }

        void RemoveExpired(Dictionary<int, EntityMotionTrack> tracks)
        {
            _expiredMotion.Clear();
            foreach (var pair in tracks)
                if (pair.Value.SeenRevision != _motionRevision)
                    _expiredMotion.Add(pair.Key);
            foreach (var id in _expiredMotion) tracks.Remove(id);
        }
    }

    private void RequestMotionFrame()
    {
        if (!_motionAttached || _framePending || IsSimulationPaused) return;
        var now = MotionTime;
        if (!(Engine is not null && VisibleResidents(Engine.State).Any(person =>
                _residentMotion.TryGetValue(person.Id, out var track) && track.IsMoving(now))) &&
            !_armyMotion.Values.Any(track => track.IsMoving(now)) && !HasAnimatedEffects(PresentationTime)) return;
        if (TopLevel.GetTopLevel(this) is not { } topLevel) return;
        _framePending = true;
        var epoch = _motionEpoch;
        topLevel.RequestAnimationFrame(_ =>
        {
            if (epoch != _motionEpoch) return;
            _framePending = false;
            if (!_motionAttached || IsSimulationPaused) return;
            var frameTime = PresentationTime;
            // 总览保留 15 Hz；五倍近景合并为 20 Hz，给真实模拟步留下绘制预算。
            var cadence = _zoom < 1 ? 1d / 15 : _zoom >= 3 && SimulationTickDurationSeconds <= .040001
                ? 1d / 20 : 1d / 30;
            if (frameTime - _lastAnimatedFrameTime >= cadence)
            {
                _lastAnimatedFrameTime = frameTime;
                _residentGeometryDirty = true;
                FollowResident();
                InvalidateVisual();
            }

            RequestMotionFrame();
        });
    }

    private void FollowResident(double? frameTime = null)
    {
        if (!_followSelectedResident || SelectedResidentId is not { } id ||
            !_residentMotion.TryGetValue(id, out var motion) || !_cameraReady) return;
        var position = motion.Position(frameTime ?? MotionTime);
        _origin = new Point(Bounds.Width / 2 - (position.X + .5) * TilePixels * _zoom,
            Bounds.Height / 2 - (position.Y + .5) * TilePixels * _zoom);
    }

    private bool SelectObjectAt(Point point)
    {
        if (!IsNavigationTool || Engine is null) return false;
        var hasTile = TryTile(point, out var tile);
        var worldPoint = new Point((point.X - _origin.X) / _zoom, (point.Y - _origin.Y) / _zoom);
        var radius = Math.Clamp(TilePixels * _zoom * .65, 7, 17);
        var candidates = new List<(int Kind, int Id, double Distance, double Depth, int X, int Y)>();
        foreach (var pair in _renderedResidentPoints)
        {
            var distance = Distance(point, pair.Value);
            if (distance <= radius) candidates.Add((0, pair.Key, distance, pair.Value.Y + 2.2 * _zoom, tile.X, tile.Y));
        }

        foreach (var building in Engine.State.Society.Buildings)
            if ((hasTile && building.X == tile.X && building.Y == tile.Y) ||
                (_zoom >= 3 && BuildingBounds(building).Contains(worldPoint)))
            {
                var ground = GetTileScreenPosition(building.X, building.Y);
                candidates.Add((1, building.Id, Distance(point, ground), ground.Y + 2 * _zoom, building.X, building.Y));
            }

        // 优先选择指针下的实际建筑地格，避免前方屋顶遮住后方建筑入口。
        var footprint = hasTile ? candidates.FindIndex(c => c.Kind == 1 && c.X == tile.X && c.Y == tile.Y) : -1;
        var directBuilding = footprint >= 0 ? candidates[footprint].Id : 0;
        candidates.Sort((a, b) => (a.Kind == 1 && a.Id == directBuilding) != (b.Kind == 1 && b.Id == directBuilding)
            ? a.Kind == 1 && a.Id == directBuilding ? -1 : 1
            : _zoom >= 3 && Math.Abs(a.Depth - b.Depth) > .01
                ? b.Depth.CompareTo(a.Depth)
                : a.Kind != b.Kind
                    ? a.Kind.CompareTo(b.Kind)
                    : Math.Abs(a.Distance - b.Distance) > .01
                        ? a.Distance.CompareTo(b.Distance)
                        : a.Id.CompareTo(b.Id));
        if (hasTile)
            candidates.Add((2, 0, 0, 0, tile.X, tile.Y)); // 循环选择保留地格和被遮挡居民的入口。
        if (candidates.Count == 0) return false;
        if (_lastResidentClick is { } previous && Distance(previous, point) <= 4) _residentClickCycle++;
        else _residentClickCycle = 0;
        _lastResidentClick = point;
        var selected = candidates[_residentClickCycle % candidates.Count];
        if (selected.Kind == 0)
        {
            SelectResident(selected.Id, FollowSelectedResident);
            ResidentSelected?.Invoke(selected.Id);
        }
        else
        {
            ClearResidentSelection();
            _selection = (selected.X, selected.Y);
            SelectedBuildingId = selected.Kind == 1 ? selected.Id : null;
            if (selected.Kind == 1) BuildingSelected?.Invoke(selected.Id);
            else TileSelected?.Invoke(tile.X, tile.Y);
        }

        InvalidateVisual();
        return true;
    }

    private Point ResidentMapPosition(int id, int x, int y)
    {
        var position = _residentMotion.TryGetValue(id, out var motion)
            ? motion.Position(_renderMotionTime)
            : new Point(x, y);
        return new Point((position.X + .5) * TilePixels, (position.Y + .5) * TilePixels);
    }

    private void DrawResidentSelection(DrawingContext context)
    {
        if (SelectedResidentId is not { } id || !TryGetResidentScreenPosition(id, out var point)) return;
        var radius = Math.Max(6, _zoom * 3.5);
        context.DrawEllipse(null, SelectionPen, point, radius, radius);
        context.DrawLine(SelectionPen, new Point(point.X, point.Y - radius - 5),
            new Point(point.X, point.Y - radius - 1));
    }
}
