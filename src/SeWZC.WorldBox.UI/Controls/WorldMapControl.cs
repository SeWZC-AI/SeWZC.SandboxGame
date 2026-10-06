using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI.Controls;

/// <summary>绘制世界地图、实体和行动动画，并处理镜头、选择及地图工具交互。</summary>
public sealed partial class WorldMapControl : Control
{
    private const int TilePixels = 8;
    private const int ChunkTiles = 32;
    private static readonly IBrush OceanBrush = Brush(0xFF122D3D);

    private static readonly Typeface MapTypeface = new(new FontFamily(
        "avares://SeWZC.WorldBox.UI/Assets/Fonts/NotoSansSC.otf#Noto Sans CJK SC"));

    private static readonly IBrush LabelBrush = Brush(0xFFF4F1D8);
    private static readonly IBrush LabelShadow = Brush(0xDB152B2C);

    private static readonly IBrush[] ResidentBrushes =
        [Brush(0xFFE2BD69), Brush(0xFFBBE4B6), Brush(0xFFE7A376), Brush(0xFF83B576)];

    private static readonly IBrush HeadBrush = Brush(0xFFF2D9AA);
    private static readonly IBrush ShadowBrush = Brush(0x603B3427);
    private static readonly IBrush WallBrush = Brush(0xFFE3D1A0);
    private static readonly IBrush RoofBrush = Brush(0xFFAB6850);
    private static readonly IBrush WoodBrush = Brush(0xFF604B36);
    private static readonly IBrush FarmBrush = Brush(0xFFADA058);
    private static readonly Pen HoverPen = new(Brush(0xEAF9E4A7), 1.4);
    private static readonly Pen SelectionPen = new(Brush(0xFFEAC77C), 1.8);
    private static readonly IBrush ScaleBrush = Brush(0xAFDAE6D5);
    private static readonly Pen ScalePen = new(ScaleBrush);

    private static readonly IBrush FlameOuter = Brush(0xFFF0793D);
    private static readonly IBrush FlameInner = Brush(0xFFFFD575);

    private readonly Dictionary<BuildingKind, FormattedText> _buildingLabelText = [];
    private readonly Dictionary<(int X, int Y), MapChunk> _chunks = [];
    private readonly List<(int X, int Y)> _fires = [];
    private readonly Dictionary<int, IBrush> _nationBrushes = [];
    private readonly StreamGeometry?[] _residents = new StreamGeometry?[4];
    private readonly Dictionary<int, (string Name, double Size, FormattedText Text)> _settlementLabels = [];
    private readonly Dictionary<IPointer, Point> _touches = [];

    private readonly List<Resident> _visibleResidents = [];
    private MapTool _activeTool = MapTool.Inspect;
    private WorldState? _cachedState;
    private bool _cameraReady;

    private long _chunkRefreshTick = -1;
    private (int Left, int Right, int Top, int Bottom) _chunkViewport;
    private bool _dragging;
    private WorldEngine? _engine;
    private bool _gestureMoved;
    private StreamGeometry? _heads;
    private Point? _hover;
    private Settlement[] _labelSettlements = [];
    private (int X, int Y)? _lastPaint;
    private Point _lastPosition;
    private Point _origin;
    private bool _panning;
    private bool _pinching;
    private bool _preparingWorldEdit;
    private Point _pressPosition;
    private (int Left, int Right, int Top, int Bottom) _residentViewport;
    private FormattedText? _scaleLabel;
    private int _scaleLabelTiles;
    private (int X, int Y)? _selection;
    private bool _showBorders = true;
    private long _visibleResidentTick = -1;
    private double _zoom = 0.4;

    /// <summary>创建可聚焦、裁剪边界且使用清晰像素缩放的地图控件。</summary>
    public WorldMapControl()
    {
        ClipToBounds = true;
        Focusable = true;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
    }

    /// <summary>当前显示的世界引擎；切换时清除选择、动画与地图缓存。</summary>
    public WorldEngine? Engine
    {
        get => _engine;
        set
        {
            if (ReferenceEquals(_engine, value)) return;
            CancelPlacement();
            _touches.Clear();
            _dragging = false;
            _pinching = false;
            _lastPaint = null;
            PickingLocation = false;
            SelectedNationId = 0;
            SelectedSettlementId = 0;
            DisposeChunks();
            ResetMotion();
            _settlementLabels.Clear();
            _engine = value;
            _selection = null;
            _cameraReady = false;
            RefreshWorld(true);
        }
    }

    /// <summary>当前地图工具。</summary>
    public MapTool ActiveTool
    {
        get => _activeTool;
        set
        {
            CancelPlacement();
            _activeTool = value ?? throw new ArgumentNullException(nameof(value));
            _lastPaint = null;
            Cursor = new Cursor(IsNavigationTool ? StandardCursorType.Arrow : StandardCursorType.Cross);
            InvalidateVisual();
        }
    }

    /// <summary>地形和领土笔刷半径，以地格为单位。</summary>
    public int BrushRadius { get; set; } = 2;

    /// <summary>领土绘制等工具当前使用的国家 ID。</summary>
    public int SelectedNationId { get; set; }

    /// <summary>建设工具当前使用的归属聚落 ID。</summary>
    public int SelectedSettlementId { get; set; }

    /// <summary>是否绘制国家领土边界。</summary>
    public bool ShowBorders
    {
        get => _showBorders;
        set
        {
            _showBorders = value;
            InvalidateVisual();
        }
    }

    private bool IsNavigationTool => ActiveTool.IsNavigation;

    private double FitZoom => Engine is null
        ? .4
        : Math.Max(.025,
            Math.Min((Bounds.Width - 36) / (Engine.State.Width * TilePixels),
                (Bounds.Height - 36) / (Engine.State.Height * TilePixels)));

    /// <summary>本次地形缓存检查扫描的地格数，用于呈现诊断。</summary>
    public int TerrainTilesScanned { get; private set; }

    /// <summary>执行地图编辑前异步准备暂停和撤销恢复点的回调。</summary>
    public Func<Task>? PrepareWorldEdit { get; set; }

    /// <summary>捕获地图选择及跟随设置，用于界面导航返回。</summary>
    public MapSelectionState CaptureMapSelection()
    {
        return new MapSelectionState(SelectedResidentId, SelectedBuildingId,
            _selection, FollowSelectedResident, SelectedNationId, SelectedSettlementId);
    }

    /// <summary>恢复地图选择和跟随设置，优先恢复居民或建筑选择。</summary>
    /// <param name="selection">此前捕获的地图选择和跟随设置。</param>
    public void RestoreMapSelection(MapSelectionState selection)
    {
        ClearMapSelection();
        SelectedNationId = selection.NationId;
        SelectedSettlementId = selection.SettlementId;
        if (selection.ResidentId is { } resident)
            SelectResident(resident, selection.Follow);
        else if (selection.BuildingId is { } building)
            SelectMapBuilding(building);
        else if (selection.Tile is { } tile)
            SelectMapTile(tile.X, tile.Y);
    }

    /// <summary>选择有效地格并清除其他对象选择，不发送点击通知。</summary>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    public void SelectMapTile(int x, int y)
    {
        ClearMapSelection();
        if (Engine is not null && (uint)x < Engine.State.Width && (uint)y < Engine.State.Height)
            _selection = (x, y);
        InvalidateVisual();
    }

    /// <summary>选择仍存在的建筑及其地格，不发送点击通知。</summary>
    /// <param name="id">要选择的建筑 ID。</param>
    public void SelectMapBuilding(int id)
    {
        ClearMapSelection();
        if (Engine?.State.Society.Buildings.FirstOrDefault(building => building.Id == id) is { } building)
        {
            SelectedBuildingId = building.Id;
            _selection = (building.X, building.Y);
        }

        InvalidateVisual();
    }

    /// <summary>工具操作开始时发出的通知，供主界面准备编辑状态。</summary>
    public event EventHandler? WorldEditing;

    /// <summary>实际修改世界之前发出的通知，供主界面取消失效的异步操作。</summary>
    public event EventHandler? WorldMutationStarting;

    /// <summary>地图工具完成世界修改后发出的通知。</summary>
    public event EventHandler? WorldEdited;

    /// <summary>通过地图交互选中地格时发出横向和纵向地格坐标。</summary>
    public event Action<int, int>? TileSelected;

    /// <summary>地图工具无法执行时发出的错误说明。</summary>
    public event Action<string>? ToolError;

    /// <summary>重新检查世界呈现缓存、运动轨迹和动画通知，刷新地图显示。</summary>
    /// <param name="resetCamera">是否同时将镜头恢复为全图视野。</param>
    /// <param name="deferAnimation">是否在近景已有动画帧等待时合并本次显示刷新。</param>
    public void RefreshWorld(bool resetCamera = false, bool deferAnimation = false)
    {
        if (Engine is null)
        {
            InvalidateVisual();
            return;
        }

        if (!ReferenceEquals(_cachedState, Engine.State))
        {
            DisposeChunks();
            ResetMotion();
            _settlementLabels.Clear();
            _effects.Clear();
            _seenVisualSequence = Engine.VisualSequence;
            _cachedState = Engine.State;
            _cameraReady = false;
        }

        if (resetCamera || !_cameraReady) FitWorld();
        _chunkRefreshTick = -1;
        _ecologyDirty = true;
        _visibleResidentTick = -1;
        _sceneBuildingsDirty = true;
        RebuildChangedChunks();
        _labelSettlements = Engine.State.Settlements.OrderByDescending(settlement => settlement.Population).ToArray();
        _relayOverlayDirty = true;
        _activityBuildings.Clear();
        foreach (var building in Engine.State.Society.Buildings) _activityBuildings[building.Id] = building;
        CaptureArchitecture(Engine.State);
        CaptureEffects();
        CaptureSelectedRoute();
        CaptureMotionSnapshots();
        // 五倍近景已有动画帧待执行时合并模拟刷新；编辑、停表及静止场景仍立即绘制。
        if (!deferAnimation || resetCamera || _zoom < 3 || IsSimulationPaused || !_framePending)
            InvalidateVisual();
    }

    /// <summary>调整镜头以容纳完整世界，并停止跟随居民。</summary>
    public void FitWorld()
    {
        if (Engine is null || Bounds.Width < 1 || Bounds.Height < 1) return;
        _followSelectedResident = false;
        _zoom = FitZoom;
        _origin = new Point((Bounds.Width - Engine.State.Width * TilePixels * _zoom) / 2,
            (Bounds.Height - Engine.State.Height * TilePixels * _zoom) / 2);
        _cameraReady = true;
        InvalidateVisual();
    }

    /// <summary>以当前视口中心为锚点放大地图。</summary>
    public void ZoomIn()
    {
        ZoomAt(new Point(Bounds.Width / 2, Bounds.Height / 2), _zoom * 1.3);
    }

    /// <summary>以当前视口中心为锚点缩小地图。</summary>
    public void ZoomOut()
    {
        ZoomAt(new Point(Bounds.Width / 2, Bounds.Height / 2), _zoom / 1.3);
    }

    /// <summary>将指定地格移到镜头中心，并放大到定位视野。</summary>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    public void FocusTile(int x, int y)
    {
        if (Engine is null) return;
        _followSelectedResident = false;
        _zoom = Math.Max(_zoom, 1.4);
        _origin = new Point(Bounds.Width / 2 - (x + .5) * TilePixels * _zoom,
            Bounds.Height / 2 - (y + .5) * TilePixels * _zoom);
        _selection = (x, y);
        _cameraReady = true;
        InvalidateVisual();
    }

    /// <summary>在控件尺寸变化时使地图视野缓存失效，并初始化镜头。</summary>
    /// <param name="change">发生变化的 Avalonia 属性及其新旧值。</param>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != BoundsProperty) return;
        if (!_cameraReady) FitWorld();
        else if (change.OldValue is Rect oldBounds && change.NewValue is Rect newBounds)
            _origin += new Vector((newBounds.Width - oldBounds.Width) / 2, (newBounds.Height - oldBounds.Height) / 2);
    }

    /// <summary>绘制地形缓存、实体、动画、地图图层和当前选择。</summary>
    /// <param name="context">本帧绘制使用的 Avalonia 绘图上下文。</param>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.DrawRectangle(OceanBrush, null, new Rect(Bounds.Size));
        if (Engine is null || !_cameraReady) return;
        var state = Engine.State;
        _renderFrameTime = PresentationTime;
        _renderMotionTime = MotionTime;
        RenderedEffectCount = 0;
        RenderedRouteSegmentCount = 0;
        FollowResident(_renderMotionTime);
        RebuildChangedChunks();
        if (_residentGeometryDirty || _geometryZoom != _zoom || _geometryOrigin != _origin)
            RebuildResidents();
        using (context.PushClip(new Rect(Bounds.Size)))
        {
            using (context.PushTransform(Matrix.CreateScale(_zoom, _zoom) *
                                         Matrix.CreateTranslation(_origin.X, _origin.Y)))
            {
                foreach (var chunk in _chunks.Values)
                {
                    if (!Visible(chunk.Bounds)) continue;
                    if (chunk.Terrain is not null) context.DrawImage(chunk.Terrain, chunk.TerrainBounds);
                    if (ShowBorders && chunk.Territory is not null) context.DrawImage(chunk.Territory, chunk.Bounds);
                }

                DrawEcology(context, state);
                foreach (var settlement in state.Settlements)
                    if (Visible(new Rect(settlement.X * TilePixels - 28, settlement.Y * TilePixels - 28, 56, 56)))
                        DrawSettlement(context, settlement);
                if (_zoom < 3) DrawBuildings(context, state);
                DrawMapOverlay(context, state);
                for (var race = 0; _zoom < 3 && race < _residents.Length; race++)
                    if (_residents[race] is { } body)
                        context.DrawGeometry(ResidentBrushes[race], null, body);
                if (_zoom >= .7 && _zoom < 3 && _heads is not null) context.DrawGeometry(HeadBrush, null, _heads);
                if (_zoom >= .7 && _zoom < 3)
                {
                    if (_cargoGeometry is not null) context.DrawGeometry(CargoBrush, null, _cargoGeometry);
                    if (_messageGeometry is not null) context.DrawGeometry(MessageBrush, null, _messageGeometry);
                    if (_magicGeometry is not null) context.DrawGeometry(ArcaneBrush, null, _magicGeometry);
                }

                if (_zoom >= 3) DrawNearScene(context, state);
                DrawTownEffects(context, state);
                DrawVehicles(context, state);
                DrawFires(context, _renderFrameTime);
                DrawEffects(context);
                foreach (var army in state.Armies)
                {
                    var position = _armyMotion.TryGetValue(army.Id, out var motion)
                        ? motion.Position(_renderMotionTime)
                        : new Point(army.X, army.Y);
                    var x = position.X * TilePixels + 4;
                    var y = position.Y * TilePixels;
                    context.DrawRectangle(WoodBrush, null, new Rect(x, y - 10, 1.3, 11));
                    context.DrawRectangle(NationBrush(army.NationId), null, new Rect(x + 1.3, y - 10, 7, 5));
                }
            }

            DrawLabels(context, state);
            DrawInfrastructureLegend(context);
            DrawSelection(context);
            DrawPlacementHint(context);
            DrawResidentSelection(context);
            DrawResidentGoal(context);
            DrawScale(context);
        }

        RequestMotionFrame();
    }

    private bool Visible(Rect world)
    {
        return new Rect(_origin.X + world.X * _zoom, _origin.Y + world.Y * _zoom,
            world.Width * _zoom, world.Height * _zoom).Intersects(new Rect(Bounds.Size));
    }

    private IReadOnlyList<Resident> VisibleResidents(WorldState state)
    {
        var viewport = VisibleTiles(state, 3);
        if (_visibleResidentTick == state.Tick && _residentViewport == viewport) return _visibleResidents;
        _visibleResidentTick = state.Tick;
        _residentViewport = viewport;
        _visibleResidents.Clear();

        bool Inside(int x, int y)
        {
            return x >= viewport.Left && x <= viewport.Right && y >= viewport.Top && y <= viewport.Bottom;
        }

        foreach (var person in state.Residents)
            if (Inside(person.X, person.Y) || (person.MoveStartedTick + person.MoveDurationTicks > state.Tick &&
                                               Inside(person.FromX, person.FromY)))
                _visibleResidents.Add(person);
        return _visibleResidents;
    }

    private (int Left, int Right, int Top, int Bottom) VisibleTiles(WorldState state, int margin = 2)
    {
        var size = Math.Max(.01, _zoom * TilePixels);
        return (Math.Clamp((int)Math.Floor(-_origin.X / size) - margin, 0, state.Width - 1),
            Math.Clamp((int)Math.Ceiling((Bounds.Width - _origin.X) / size) + margin, 0, state.Width - 1),
            Math.Clamp((int)Math.Floor(-_origin.Y / size) - margin, 0, state.Height - 1),
            Math.Clamp((int)Math.Ceiling((Bounds.Height - _origin.Y) / size) + margin, 0, state.Height - 1));
    }

    private void RebuildChangedChunks()
    {
        var state = Engine!.State;
        var tiles = VisibleTiles(state);
        var view = (tiles.Left / ChunkTiles, tiles.Right / ChunkTiles, tiles.Top / ChunkTiles,
            tiles.Bottom / ChunkTiles);
        if (_chunkRefreshTick == state.Tick && _chunkViewport == view) return;
        _chunkRefreshTick = state.Tick;
        _chunkViewport = view;
        TerrainTilesScanned = 0;
        TerrainTilesDrawn = 0;
        var colors = new Dictionary<int, uint>();
        uint colorHash = 0;
        _nationBrushes.Clear();
        foreach (var nation in state.Nations)
        {
            colors[nation.Id] = nation.ColorArgb;
            colorHash = unchecked(colorHash * 31 + nation.ColorArgb + (uint)nation.Id);
            _nationBrushes[nation.Id] = Brush(nation.ColorArgb);
        }

        _fires.Clear();
        for (var cy = view.Item3 * ChunkTiles; cy <= view.Item4 * ChunkTiles; cy += ChunkTiles)
        for (var cx = view.Item1 * ChunkTiles; cx <= view.Item2 * ChunkTiles; cx += ChunkTiles)
        {
            var terrainHash = 2166136261;
            var territoryHash = colorHash;
            var containsTerritory = false;
            // 缓存摘要包含一格邻域，使岸线和边界编辑也能令相邻分块失效。
            for (var y = Math.Max(0, cy - 1); y < Math.Min(state.Height, cy + ChunkTiles + 1); y++)
            for (var x = Math.Max(0, cx - 1); x < Math.Min(state.Width, cx + ChunkTiles + 1); x++)
            {
                TerrainTilesScanned++;
                var tile = state.Tiles[y * state.Width + x];
                // 地形图像只需关注森林变树桩的资源阈值；动物和植物数量由独立图层刷新，避免频繁重建地形。
                terrainHash = unchecked((terrainHash ^ TerrainImageInput(tile)) * 16777619);
                territoryHash = unchecked((territoryHash ^ (uint)tile.NationId) * 16777619);
                containsTerritory |= tile.NationId != 0 && x >= cx && x < cx + ChunkTiles && y >= cy &&
                                     y < cy + ChunkTiles;
                if (x >= cx && x < cx + ChunkTiles && y >= cy && y < cy + ChunkTiles && tile.FireTicks > 0)
                    _fires.Add((x, y));
            }

            var key = (cx, cy);
            if (!_chunks.TryGetValue(key, out var chunk))
            {
                chunk = new MapChunk(new Rect(cx * TilePixels, cy * TilePixels,
                    Math.Min(ChunkTiles, state.Width - cx) * TilePixels,
                    Math.Min(ChunkTiles, state.Height - cy) * TilePixels));
                _chunks[key] = chunk;
            }

            if (chunk.Terrain is null || chunk.TerrainHash != terrainHash)
            {
                // 分块边缘落在小数屏幕坐标时会露出细缝，须用一像素邻格图案补边；透明领地图层不能重叠以免加深颜色。
                var left = cx > 0 ? 1 : 0;
                var top = cy > 0 ? 1 : 0;
                var right = cx + ChunkTiles < state.Width ? 1 : 0;
                var bottom = cy + ChunkTiles < state.Height ? 1 : 0;
                chunk.TerrainBounds = new Rect(chunk.Bounds.X - left, chunk.Bounds.Y - top,
                    chunk.Bounds.Width + left + right, chunk.Bounds.Height + top + bottom);
                var canvas = UpdateTerrainCanvas(chunk, state, cx, cy, left, top);
                chunk.Terrain?.Dispose();
                chunk.Terrain = MakeBitmap(canvas, true);
                chunk.TerrainHash = terrainHash;
            }

            if (!chunk.TerritoryCached || chunk.TerritoryHash != territoryHash)
            {
                chunk.Territory?.Dispose();
                chunk.Territory = null;
                chunk.TerritoryHash = territoryHash;
                chunk.TerritoryCached = true;
                // 无归属分块不分配透明纹理，避免自然地形占多数时浪费显存。
                if (!containsTerritory) continue;
                var canvas = new PixelCanvas((int)chunk.Bounds.Width, (int)chunk.Bounds.Height);
                for (var y = cy; y < Math.Min(state.Height, cy + ChunkTiles); y++)
                for (var x = cx; x < Math.Min(state.Width, cx + ChunkTiles); x++)
                {
                    var nationId = state.Tiles[y * state.Width + x].NationId;
                    if (nationId == 0 || !colors.TryGetValue(nationId, out var argb)) continue;
                    var rgb = (argb & 0x00FFFFFF) << 8;
                    var px = (x - cx) * TilePixels;
                    var py = (y - cy) * TilePixels;
                    canvas.Rect(px, py, TilePixels, TilePixels, rgb | 27);
                    var edge = rgb | 190;
                    if (x == 0 || state.Tiles[y * state.Width + x - 1].NationId != nationId)
                        canvas.Rect(px, py, 1, 8, edge);
                    if (y == 0 || state.Tiles[(y - 1) * state.Width + x].NationId != nationId)
                        canvas.Rect(px, py, 8, 1, edge);
                    if (x == state.Width - 1 || state.Tiles[y * state.Width + x + 1].NationId != nationId)
                        canvas.Rect(px + 7, py, 1, 8, edge);
                    if (y == state.Height - 1 || state.Tiles[(y + 1) * state.Width + x].NationId != nationId)
                        canvas.Rect(px, py + 7, 8, 1, edge);
                }

                chunk.Territory = MakeBitmap(canvas, false);
            }
        }
    }

    private static void DrawTerrainTile(PixelCanvas canvas, WorldState state, int x, int y, int px, int py)
    {
        var tile = state.Tiles[y * state.Width + x];
        var noise = PixelCanvas.Noise(x, y, state.Seed);
        var variation = (int)(PixelCanvas.Noise(x / 4, y / 4, state.Seed) % 7) - 3 + (int)(noise % 3) - 1;
        uint color = tile.Terrain switch
        {
            TerrainType.DeepWater => 0x184254FF,
            TerrainType.Water => 0x2E6D7AFF,
            TerrainType.Sand => 0xC7B77EFF,
            TerrainType.Grass => 0x719262FF,
            TerrainType.Forest => 0x537A50FF,
            TerrainType.Mountain => 0x798575FF,
            TerrainType.Snow => 0xCBD7CAFF,
            TerrainType.Hills => 0x92905EFF,
            TerrainType.Wetland => 0x58887DFF,
            TerrainType.Desert => 0xCEAE75FF,
            TerrainType.River => 0x428E9CFF,
            TerrainType.Lake => 0x559BA8FF,
            TerrainType.DryFertile => 0xA3A66BFF,
            TerrainType.Tundra => 0x99A88CFF,
            TerrainType.Stream => 0x65A6A0FF,
            TerrainType.LargeRiver => 0x347FA0FF,
            TerrainType.Meadow => 0x8FAF65FF,
            TerrainType.Woodland => 0x77926AFF,
            TerrainType.Rainforest => 0x356F52FF,
            TerrainType.Savanna => 0xB0A767FF,
            TerrainType.Scrub => 0x929164FF,
            TerrainType.Floodplain => 0x78A679FF,
            TerrainType.AlpineMeadow => 0xA0AF7BFF,
            _ => 0x719262FF,
        };
        if (tile.DroughtTicks > 0 && tile.Terrain is TerrainType.Grass or TerrainType.Forest or TerrainType.Sand
                or TerrainType.Hills or TerrainType.Wetland)
            color = 0xB59D62FF;
        color = PixelCanvas.Shade(color, variation);
        canvas.Rect(px, py, 8, 8, color);
        var nx = (int)((noise >> 5) % 6) + 1;
        var ny = (int)((noise >> 10) % 6) + 1;
        if (WorldEngine.IsWaterTerrain(tile.Terrain))
        {
            if (noise % 7 == 0) canvas.Rect(px + nx - 1, py + ny, 3, 1, PixelCanvas.Shade(color, 11));

            bool LandAt(int tx, int ty)
            {
                return tx >= 0 && tx < state.Width && ty >= 0 && ty < state.Height &&
                       !WorldEngine.IsWaterTerrain(state.Tiles[ty * state.Width + tx].Terrain);
            }

            const uint coast = 0x74A29AFF;
            if (LandAt(x, y - 1)) canvas.Rect(px, py, 8, 1, coast);
            if (LandAt(x - 1, y)) canvas.Rect(px, py, 1, 8, coast);
            if (LandAt(x, y + 1)) canvas.Rect(px, py + 7, 8, 1, coast);
            if (LandAt(x + 1, y)) canvas.Rect(px + 7, py, 1, 8, coast);
            // 只圆化外岸，保持相连水域中心连续，避免河道出现拼接断口。
            if (LandAt(x, y - 1) && LandAt(x - 1, y)) canvas.Rect(px, py, 2, 2, coast);
            if (LandAt(x, y - 1) && LandAt(x + 1, y)) canvas.Rect(px + 6, py, 2, 2, coast);
            if (LandAt(x, y + 1) && LandAt(x - 1, y)) canvas.Rect(px, py + 6, 2, 2, coast);
            if (LandAt(x, y + 1) && LandAt(x + 1, y)) canvas.Rect(px + 6, py + 6, 2, 2, coast);
            if (tile.Terrain is TerrainType.River or TerrainType.Stream or TerrainType.LargeRiver)
            {
                var horizontal = !LandAt(x - 1, y) || !LandAt(x + 1, y);
                canvas.Line(px + 2, py + 3, px + (horizontal ? 6 : 2), py + (horizontal ? 3 : 6),
                    PixelCanvas.Shade(color, 16));
            }

            return;
        }

        if (tile.Terrain is TerrainType.Mountain or TerrainType.Hills)
        {
            bool RidgeAt(int tx, int ty)
            {
                return tx >= 0 && tx < state.Width && ty >= 0 && ty < state.Height
                       && state.Tiles[ty * state.Width + tx].Terrain is TerrainType.Mountain or TerrainType.Hills;
            }

            var light = tile.Terrain == TerrainType.Mountain ? 0xB2BEADFFu : 0xADB17BFFu;
            var dark = tile.Terrain == TerrainType.Mountain ? 0x596D67FFu : 0x7F875CFFu;
            // 相邻地格共享边缘高度，使山脊跨地格和分块连续。
            canvas.Rect(px, py + 5, 8, 3, PixelCanvas.Shade(color, -8));
            canvas.Line(px + 4, py + 2, px + 6, py + 6, dark, 2);
            canvas.Line(px + 4, py + 2, px + 2, py + 6, light, 2);
            if (RidgeAt(x - 1, y)) canvas.Line(px, py + 4, px + 4, py + 2, light);
            if (RidgeAt(x + 1, y)) canvas.Line(px + 4, py + 2, px + 7, py + 4, light);
            if (RidgeAt(x, y - 1)) canvas.Line(px + 4, py, px + 4, py + 2, light);
            if (RidgeAt(x, y + 1)) canvas.Line(px + 4, py + 2, px + 4, py + 7, dark);
            if (tile.Terrain == TerrainType.Mountain && noise % 4 == 0) canvas.Rect(px + 3, py + 1, 3, 2, 0xDEE5D5FF);
            return;
        }

        canvas.Rect(px + nx, py + ny, noise % 2 == 0 ? 2 : 1, 1, PixelCanvas.Shade(color, -10));
        if (WorldEngine.IsForestTerrain(tile.Terrain) && tile.ResourceAmount < 25)
        {
            canvas.Rect(px + 3, py + 4, 2, 3, 0x755A3DFF);
            canvas.Rect(px + 2, py + 4, 4, 1, 0xC3A174FF);
        }
        else if (WorldEngine.IsForestTerrain(tile.Terrain))
        {
            var shift = (int)(noise % 2);
            if (noise % 3 == 0)
            {
                canvas.Rect(px + 3, py + 4, 1, 4, 0x705D42FF);
                canvas.Rect(px + 1, py + 2, 6, 3, 0x426C46FF);
                canvas.Rect(px + 2, py + 1, 4, 2, 0x689254FF);
                return;
            }

            canvas.Rect(px + 2 + shift, py + 6, 4, 1, 0x3F6344FF);
            canvas.Rect(px + 3 + shift, py + 5, 1, 2, 0x755A3DFF);
            canvas.Rect(px + 1 + shift, py + 3, 5, 3, 0x345F43FF);
            canvas.Rect(px + 2 + shift, py + 1, 3, 4, 0x3F714BFF);
            canvas.Rect(px + 3 + shift, py, 1, 3, 0x58865AFF);
            canvas.Rect(px + 2 + shift, py + 2, 1, 2, 0x659160FF);
        }
        else if (tile.Terrain == TerrainType.Snow)
        {
            var light = tile.Terrain == TerrainType.Snow ? 0xE6EAD8FF : 0xA0AA92FF;
            var dark = tile.Terrain == TerrainType.Snow ? 0xA6B9AEFF : 0x586D67FF;
            canvas.Rect(px + 1, py + 6, 6, 1, dark);
            canvas.Rect(px + 2, py + 4, 4, 2, dark);
            canvas.Rect(px + 3, py + 2, 2, 4, dark);
            canvas.Rect(px + 3, py + 2, 1, 3, light);
            canvas.Rect(px + 2, py + 4, 1, 2, light);
            canvas.Rect(px + 1, py + 6, 1, 1, light);
            canvas.Pixel(px + 3, py + 1, tile.Terrain == TerrainType.Snow ? 0xF0F0DFFF : 0xCAD1B8FF);
        }
        else if (tile.Terrain == TerrainType.Grass && noise % 4 == 0)
        {
            canvas.Pixel(px + nx, py + ny - 1, PixelCanvas.Shade(color, 11));
            canvas.Pixel(px + nx + 1, py + ny, PixelCanvas.Shade(color, 8));
        }
        else if (tile.Terrain == TerrainType.Hills)
        {
            canvas.Rect(px + 1, py + 5, 6, 2, 0x777D52FF);
            canvas.Rect(px + 2, py + 3, 4, 2, 0xA1A170FF);
            canvas.Rect(px + 3, py + 2, 2, 1, 0xB1AF7EFF);
            canvas.Rect(px + 5, py + 4, 1, 2, 0x7C8155FF);
        }
        else if (tile.Terrain == TerrainType.Wetland)
        {
            canvas.Rect(px + 1, py + 4, 5, 2, 0x4B8588FF);
            canvas.Rect(px + 2, py + 4, 3, 1, 0x72A599FF);
            canvas.Rect(px + 5, py + 1, 1, 4, 0x9AA86AFF);
            canvas.Rect(px + 6, py + 2, 1, 3, 0xB7B981FF);
            canvas.Pixel(px + 4, py + 3, 0x365F50FF);
        }
        else if (tile.Terrain == TerrainType.Desert)
        {
            canvas.Rect(px + 1, py + 5, 5, 1, 0xC0A068FF);
            canvas.Rect(px + 2, py + 4, 4, 1, 0xE2C68EFF);
            canvas.Rect(px + 4, py + 3, 3, 1, 0xDABC83FF);
            if (noise % 9 == 0) canvas.Rect(px + 1, py + 1, 1, 2, 0x8C9462FF);
        }
        else if (tile.Terrain == TerrainType.Tundra)
        {
            canvas.Rect(px + 1, py + 5, 3, 1, 0x82987CFF);
            canvas.Pixel(px + 2, py + 4, 0x748D70FF);
            canvas.Rect(px + 5, py + 2, 2, 1, 0xD0D8BFFF);
            if (noise % 3 == 0) canvas.Rect(px + 4, py + 6, 3, 1, 0xBDCBAEFF);
        }
    }

    private static void DrawRoadTile(PixelCanvas canvas, WorldState state, int x, int y, int px, int py)
    {
        var tile = state.Tiles[y * state.Width + x];
        if (tile.RoadLevel == 0) return;

        bool RoadAt(int tx, int ty)
        {
            return tx >= 0 && ty >= 0 && tx < state.Width && ty < state.Height &&
                   state.Tiles[ty * state.Width + tx].RoadLevel > 0;
        }

        var road = tile.RoadLevel >= 2 ? 0xB4B29BFFu : 0xB49A6FFFu;
        var verge = tile.RoadLevel >= 2 ? 0x858E7FFFu : 0x8A795AFFu;
        canvas.Rect(px + 2, py + 2, 4, 4, verge);
        canvas.Rect(px + 3, py + 3, 2, 2, road);
        if (RoadAt(x - 1, y))
        {
            canvas.Rect(px, py + 2, 4, 4, verge);
            canvas.Rect(px, py + 3, 4, 2, road);
        }

        if (RoadAt(x + 1, y))
        {
            canvas.Rect(px + 4, py + 2, 4, 4, verge);
            canvas.Rect(px + 4, py + 3, 4, 2, road);
        }

        if (RoadAt(x, y - 1))
        {
            canvas.Rect(px + 2, py, 4, 4, verge);
            canvas.Rect(px + 3, py, 2, 4, road);
        }

        if (RoadAt(x, y + 1))
        {
            canvas.Rect(px + 2, py + 4, 4, 4, verge);
            canvas.Rect(px + 3, py + 4, 2, 4, road);
        }

        canvas.Pixel(px + 3, py + 3, PixelCanvas.Shade(road, 12));
    }

    private static WriteableBitmap MakeBitmap(PixelCanvas canvas, bool opaque)
    {
        var bitmap = new WriteableBitmap(new PixelSize(canvas.Width, canvas.Height), new Vector(96, 96),
            PixelFormat.Rgba8888, opaque ? AlphaFormat.Opaque : AlphaFormat.Unpremul);
        using var pixels = bitmap.Lock();
        if (pixels.RowBytes == canvas.Width * 4)
            Marshal.Copy(canvas.Pixels, 0, pixels.Address, canvas.Pixels.Length);
        else
        {
            for (var y = 0; y < canvas.Height; y++)
                Marshal.Copy(canvas.Pixels, y * canvas.Width * 4, pixels.Address + y * pixels.RowBytes,
                    canvas.Width * 4);
        }

        return bitmap;
    }

    private void RebuildResidents()
    {
        if (Engine is null) return;
        var now = _renderMotionTime;
        _renderedResidentPoints.Clear();
        var silhouettes = _zoom < 3;
        var details = silhouettes && _zoom >= .7;
        var heads = silhouettes && _zoom >= .7 ? new StreamGeometry() : null;
        using var headContext = heads?.Open();
        var cargo = details ? new StreamGeometry() : null;
        var messages = details ? new StreamGeometry() : null;
        var magic = details ? new StreamGeometry() : null;
        using var cargoContext = cargo?.Open();
        using var messageContext = messages?.Open();
        using var magicContext = magic?.Open();
        var contexts = new StreamGeometryContext?[4];
        for (var race = 0; race < 4; race++)
        {
            _residents[race] = silhouettes ? new StreamGeometry() : null;
            contexts[race] = _residents[race]?.Open();
        }

        try
        {
            foreach (var resident in VisibleResidents(Engine.State))
            {
                var position = _residentMotion.TryGetValue(resident.Id, out var motion)
                    ? motion.Position(now)
                    : new Point(resident.X, resident.Y);
                var x = (position.X + .5) * TilePixels - .9;
                var y = (position.Y + .5) * TilePixels;
                if (!Visible(new Rect(x - 2, y - 3, 6, 7))) continue;
                _renderedResidentPoints[resident.Id] =
                    ToScreen((position.X + .5) * TilePixels, (position.Y + .5) * TilePixels);
                // 近景精灵已包含职业细节，总览小图标又不足一个像素，因此无需另建重复几何。
                if (!silhouettes) continue;
                var race = Math.Clamp((int)resident.Race, 0, 3);
                if (ShowVehicle(resident)) continue;
                if (contexts[race] is { } silhouette)
                    GeometryRect(silhouette, x, y, resident.Profession == Profession.Soldier ? 2.6 : 1.8, 2.4);
                if (headContext is not null) GeometryRect(headContext, x, y - 1.4, 1.8, 1.4);
                if (!details) continue;
                if (resident.Inventory.Food + resident.Inventory.Wood + resident.Inventory.Stone +
                    resident.Inventory.Ore > 0)
                    GeometryRect(cargoContext!, x + 1.8, y + .5, 2.3, 2);
                if (resident.Agent.CarriedMessages.Count > 0)
                    GeometryRect(messageContext!, x + 1.7, y - 2.3, 2.8, 1.6);
                if (resident.Profession == Profession.Mage)
                    GeometryRect(magicContext!, x - .8, y - 2.5, 3.4, 1.1);
            }
        }
        finally
        {
            foreach (var draw in contexts) draw?.Dispose();
        }

        _heads = heads;
        _cargoGeometry = cargo;
        _messageGeometry = messages;
        _magicGeometry = magic;
        _residentGeometryDirty = false;
        _geometryZoom = _zoom;
        _geometryOrigin = _origin;
    }

    private static void GeometryRect(StreamGeometryContext context, double x, double y, double width, double height)
    {
        context.BeginFigure(new Point(x, y));
        context.LineTo(new Point(x + width, y));
        context.LineTo(new Point(x + width, y + height));
        context.LineTo(new Point(x, y + height));
        context.EndFigure(true);
    }

    private void DrawSettlement(DrawingContext context, Settlement settlement)
    {
        var x = settlement.X * TilePixels + 4;
        var y = settlement.Y * TilePixels + 4;
        var nationBrush = NationBrush(settlement.NationId);
        if (settlement.FertilityBoostTicks > 0)
            context.DrawEllipse(null, new Pen(HealingBrush, .8), new Point(x, y), 10, 6);
        if (settlement.ShieldTicks > 0)
            context.DrawEllipse(null, new Pen(ArcaneBrush), new Point(x, y - 5), 13, 16);
    }

    private static void DrawHouse(DrawingContext context, double x, double y, IBrush roof, double scale)
    {
        void Box(IBrush brush, double dx, double dy, double w, double h)
        {
            context.DrawRectangle(brush, null, new Rect(x + dx * scale, y + dy * scale, w * scale, h * scale));
        }

        Box(ShadowBrush, -1, 6, 10, 3);
        Box(WallBrush, 0, 1, 7, 6);
        Box(roof, -1, -1, 9, 3);
        Box(roof, 1, -3, 5, 2);
        Box(WoodBrush, 3, 4, 2, 3);
        Box(WoodBrush, 0.8, 3, 1, 1);
    }

    private void DrawFires(DrawingContext context, double time)
    {
        foreach (var (tx, ty) in _fires)
        {
            var x = tx * TilePixels + 4;
            var y = ty * TilePixels + 6;
            if (!Visible(new Rect(x - 5, y - 16, 12, 22))) continue;
            RenderedEffectCount++;
            RenderedEffectTime = time;
            var phase = time * 8 + tx * 1.7 + ty;
            var sway = Math.Sin(phase) * 1.2;
            using (context.PushOpacity(.18))
            {
                context.DrawEllipse(FlameOuter, null, new Point(x, y - 2), 7, 8);
            }

            Triangle(context, FlameOuter, new Point(x - 3, y), new Point(x + 3, y),
                new Point(x + sway, y - 9 - Math.Sin(phase * .7) * 2));
            Triangle(context, FlameInner, new Point(x - 1.7, y), new Point(x + 1.8, y),
                new Point(x - sway * .4, y - 5));
            for (var i = 0; i < 2; i++)
            {
                var rise = (time * 5 + i * 5 + tx % 3) % 10;
                using (context.PushOpacity((1 - rise / 10) * .4))
                {
                    context.DrawEllipse(StoneBrush, null, new Point(x + Math.Sin(phase * .2 + i) * 2, y - 8 - rise),
                        1.4 + rise * .12, 1.2);
                }
            }
        }
    }

    private void DrawLabels(DrawingContext context, WorldState state)
    {
        RenderedBuildingLabelCount = 0;
        if (_zoom < .22) return;
        // 拒绝重叠名称，使小尺寸地图上的标签仍可辨读。
        var occupied = new List<Rect>();
        foreach (var settlement in _labelSettlements)
        {
            var position = ToScreen((settlement.X + .5) * TilePixels, settlement.Y * TilePixels - 24);
            var size = _zoom > .6 ? 12 : 10;
            if (!_settlementLabels.TryGetValue(settlement.Id, out var cached) || cached.Name != settlement.Name ||
                cached.Size != size)
            {
                cached = (settlement.Name, size, new FormattedText(DisplayFormat.Text(settlement.Name),
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, MapTypeface, size, LabelBrush));
                _settlementLabels[settlement.Id] = cached;
            }

            var text = cached.Text;
            var rect = new Rect(position.X - text.Width / 2 - 5, position.Y - text.Height - 3,
                text.Width + 10, text.Height + 5);
            if (!rect.Intersects(new Rect(Bounds.Size)) ||
                occupied.Any(other => other.Intersects(rect.Inflate(4)))) continue;
            occupied.Add(rect);
            context.DrawRectangle(LabelShadow, null, rect, 3, 3);
            context.DrawText(text, new Point(rect.X + 5, rect.Y + 2));
        }

        if (!ShowBuildingNames || _zoom < 5) return;
        foreach (var building in state.Society.Buildings)
        {
            var position = ToScreen((building.X + .5) * TilePixels, BuildingBounds(building).Top);
            if (position.X < -40 || position.X > Bounds.Width + 40 || position.Y < -20 ||
                position.Y > Bounds.Height) continue;
            if (!_buildingLabelText.TryGetValue(building.Kind, out var text))
            {
                text = new FormattedText(WorldEngine.BuildingName(building.Kind), CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, MapTypeface, 10, LabelBrush);
                _buildingLabelText[building.Kind] = text;
            }

            var rect = new Rect(position.X - text.Width / 2 - 3, position.Y - text.Height - 2, text.Width + 6,
                text.Height + 3);
            if (building.Id != SelectedBuildingId && (RenderedBuildingLabelCount >= 40 ||
                                                      occupied.Any(other => other.Intersects(rect.Inflate(2)))))
                continue;
            occupied.Add(rect);
            context.DrawRectangle(LabelShadow, null, rect, 2, 2);
            context.DrawText(text, new Point(rect.X + 3, rect.Y + 1));
            RenderedBuildingLabelCount++;
        }
    }

    private void DrawSelection(DrawingContext context)
    {
        if (_selection is { } selected)
        {
            var center = ToScreen((selected.X + .5) * TilePixels, (selected.Y + .5) * TilePixels);
            var size = Math.Max(7, TilePixels * _zoom + 2);
            context.DrawRectangle(null, SelectionPen, new Rect(center.X - size / 2, center.Y - size / 2, size, size), 2,
                2);
        }

        var hover = _pendingPlacement is { } pending ? GetTileScreenPosition(pending.X, pending.Y) : _hover;
        if (hover is not { } position || IsNavigationTool || !TryTile(position, out var tile)) return;
        var valid = PlacementError(tile.X, tile.Y) is null;
        var previewPen = new Pen(Brush(valid ? 0xFFB8E9BCu : 0xFFF08060u), 2);
        var previewFill = Brush(valid ? 0x448CDDABu : 0x55F08060u);
        var point = ToScreen((tile.X + .5) * TilePixels, (tile.Y + .5) * TilePixels);
        if (ActiveTool.SquarePreview)
        {
            var size = Math.Max(6, TilePixels * _zoom);
            context.DrawRectangle(previewFill, previewPen,
                new Rect(point.X - size / 2, point.Y - size / 2, size, size), 1, 1);
            return;
        }

        var brushTiles = ActiveTool.PreviewRadius(this);
        var radius = Math.Max(3, (brushTiles + .5) * TilePixels * _zoom);
        context.DrawEllipse(previewFill, previewPen, point, radius, radius);
    }

    private void DrawScale(DrawingContext context)
    {
        var tiles = _zoom > 1 ? 5 : _zoom > .3 ? 10 : 25;
        var length = tiles * TilePixels * _zoom;
        var x = 18.0;
        var y = Bounds.Height - 22;
        context.DrawLine(ScalePen, new Point(x, y), new Point(x + length, y));
        context.DrawLine(ScalePen, new Point(x, y - 3), new Point(x, y + 3));
        context.DrawLine(ScalePen, new Point(x + length, y - 3), new Point(x + length, y + 3));
        if (_scaleLabel is null || _scaleLabelTiles != tiles)
        {
            _scaleLabel = new FormattedText($"{tiles} 格", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                MapTypeface, 10, ScaleBrush);
            _scaleLabelTiles = tiles;
        }

        context.DrawText(_scaleLabel, new Point(x, y - 18));
    }

    private IBrush NationBrush(int nationId)
    {
        return _nationBrushes.TryGetValue(nationId, out var brush) ? brush : RoofBrush;
    }

    private static IBrush Brush(uint argb)
    {
        return new SolidColorBrush(Color.FromUInt32(argb));
    }

    private Point ToScreen(double x, double y)
    {
        return new Point(_origin.X + x * _zoom, _origin.Y + y * _zoom);
    }

    private static double Distance(Point first, Point second)
    {
        return Math.Sqrt((first.X - second.X) * (first.X - second.X) + (first.Y - second.Y) * (first.Y - second.Y));
    }

    private bool TryTile(Point position, out (int X, int Y) tile)
    {
        tile = ((int)Math.Floor((position.X - _origin.X) / (_zoom * TilePixels)),
            (int)Math.Floor((position.Y - _origin.Y) / (_zoom * TilePixels)));
        return Engine is not null && tile.X >= 0 && tile.Y >= 0 && tile.X < Engine.State.Width &&
               tile.Y < Engine.State.Height;
    }

    private void ZoomAt(Point focus, double zoom)
    {
        if (Engine is null) return;
        var next = Math.Clamp(zoom, FitZoom * .55, 24);
        var factor = next / _zoom;
        _origin = new Point(focus.X - (focus.X - _origin.X) * factor, focus.Y - (focus.Y - _origin.Y) * factor);
        _zoom = next;
        InvalidateVisual();
    }

    /// <summary>以指针位置为锚点处理滚轮缩放。</summary>
    /// <param name="e">本次指针或可视树事件的参数。</param>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        ZoomAt(e.GetPosition(this), _zoom * Math.Pow(1.18, e.Delta.Y));
        e.Handled = true;
    }

    /// <summary>开始地图选择、镜头拖动或工具操作，并捕获指针。</summary>
    /// <param name="e">本次指针或可视树事件的参数。</param>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var point = e.GetPosition(this);
        e.Pointer.Capture(this);
        if (e.Pointer.Type == PointerType.Touch)
        {
            _touches[e.Pointer] = point;
            if (_touches.Count > 1)
            {
                _pinching = true;
                _gestureMoved = true;
            }
            else
            {
                _pressPosition = point;
                _lastPosition = point;
                _gestureMoved = false;
                _pinching = false;
            }
        }
        else
        {
            _pressPosition = point;
            _lastPosition = point;
            _gestureMoved = false;
            _dragging = true;
            var properties = e.GetCurrentPoint(this).Properties;
            _panning = PickingLocation || IsNavigationTool || properties.IsMiddleButtonPressed ||
                       properties.IsRightButtonPressed;
            _lastPaint = null;
            if (!_panning) ApplyTool(point);
        }

        e.Handled = true;
    }

    /// <summary>根据当前指针状态更新镜头拖动、触屏缩放和工具预览。</summary>
    /// <param name="e">本次指针或可视树事件的参数。</param>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        _hover = point;
        if (!PickingLocation && !HasPendingPlacement) PreviewPlacement(point);
        if (e.Pointer.Type == PointerType.Touch && _touches.ContainsKey(e.Pointer))
        {
            if (_touches.Count >= 2)
            {
                _followSelectedResident = false;
                var old = _touches.Values.Take(2).ToArray();
                _touches[e.Pointer] = point;
                var next = _touches.Values.Take(2).ToArray();
                var oldCenter = new Point((old[0].X + old[1].X) / 2, (old[0].Y + old[1].Y) / 2);
                var center = new Point((next[0].X + next[1].X) / 2, (next[0].Y + next[1].Y) / 2);
                var oldDistance = Distance(old[1], old[0]);
                var newDistance = Distance(next[1], next[0]);
                if (oldDistance > 2) ZoomAt(oldCenter, _zoom * newDistance / oldDistance);
                _origin += center - oldCenter;
                _gestureMoved = true;
            }
            else
            {
                if (Distance(point, _pressPosition) > 5) _followSelectedResident = false;
                _origin += point - _touches[e.Pointer];
                _touches[e.Pointer] = point;
                _gestureMoved |= Distance(point, _pressPosition) > 5;
            }

            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_dragging)
        {
            _gestureMoved |= Distance(point, _pressPosition) > 5;
            if (_panning)
            {
                _followSelectedResident = false;
                _origin += point - _lastPosition;
            }
            else ApplyTool(point);

            _lastPosition = point;
            e.Handled = true;
        }

        InvalidateVisual();
    }

    /// <summary>结束当前手势，并按点击或放置状态处理地图交互。</summary>
    /// <param name="e">本次指针或可视树事件的参数。</param>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var point = e.GetPosition(this);
        if (e.Pointer.Type == PointerType.Touch)
        {
            // 取消触摸后可能迟到释放事件，只有仍有效的单指轻点才执行工具；捏合须等全部手指离开才结束。
            if (_touches.ContainsKey(e.Pointer) && !_gestureMoved && !_pinching)
            {
                if (PickingLocation) SelectTile(point);
                else if (!ActiveTool.RequiresTouchConfirmation) ApplyTool(point);
                else PreviewPlacement(point, true);
            }

            _touches.Remove(e.Pointer);
            if (_touches.Count == 0) _pinching = false;
        }
        else if (_dragging && _panning && !_gestureMoved && e.InitialPressMouseButton == MouseButton.Left)
            SelectTile(point);

        _dragging = false;
        _lastPaint = null;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    /// <summary>指针捕获丢失时清除未完成的拖动及触屏手势。</summary>
    /// <param name="e">本次指针或可视树事件的参数。</param>
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _touches.Remove(e.Pointer);
        _dragging = false;
        _lastPaint = null;
    }

    /// <summary>指针离开地图时清除悬停预览。</summary>
    /// <param name="e">本次指针或可视树事件的参数。</param>
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = null;
        if (!HasPendingPlacement) SetPlacementMessage("");
        InvalidateVisual();
    }

    private void SelectTile(Point point)
    {
        if (!PickingLocation && SelectObjectAt(point)) return;
        if (!TryTile(point, out var tile)) return;
        ClearMapSelection();
        _selection = tile;
        TileSelected?.Invoke(tile.X, tile.Y);
        InvalidateVisual();
    }

    private async void ApplyTool(Point point)
    {
        if (Engine is null || !TryTile(point, out var tile)) return;
        if (PickingLocation || IsNavigationTool)
        {
            SelectTile(point);
            return;
        }

        if (_lastPaint == tile) return;
        if (PlacementError(tile.X, tile.Y) is { } placementError)
        {
            SetPlacementMessage("无法放置：" + placementError);
            ToolError?.Invoke(placementError);
            return;
        }

        if (_preparingWorldEdit) return;
        var editEngine = Engine;
        var editTool = ActiveTool;
        if (PrepareWorldEdit is not null)
        {
            _preparingWorldEdit = true;
            try
            {
                await PrepareWorldEdit();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception error)
            {
                ToolError?.Invoke("无法准备恢复点：" + error.Message);
                return;
            }
            finally
            {
                _preparingWorldEdit = false;
            }

            if (PickingLocation || !ReferenceEquals(editEngine, Engine) || editTool != ActiveTool) return;
        }

        // 每个落点先取消正在捕获的存档，包括同一笔连续绘制。
        WorldMutationStarting?.Invoke(this, EventArgs.Empty);
        var edited = ActiveTool.Apply(this, tile);
        _lastPaint = tile;
        if (!edited) return;
        RefreshWorld();
        WorldEdited?.Invoke(this, EventArgs.Empty);
    }

    internal bool PaintWithTool((int X, int Y) tile, Action<int, int> paint)
    {
        if (_lastPaint is null) WorldEditing?.Invoke(this, EventArgs.Empty);
        Stroke(tile, paint);
        return true;
    }

    internal bool PlaceWithTool(Action place)
    {
        if (_lastPaint is not null) return false;
        WorldEditing?.Invoke(this, EventArgs.Empty);
        place();
        return true;
    }

    private void Stroke((int X, int Y) tile, Action<int, int> paint)
    {
        var start = _lastPaint ?? tile;
        var steps = Math.Max(Math.Abs(tile.X - start.X), Math.Abs(tile.Y - start.Y));
        for (var step = 0; step <= steps; step++)
        {
            var t = steps == 0 ? 1 : step / (double)steps;
            paint((int)Math.Round(start.X + (tile.X - start.X) * t),
                (int)Math.Round(start.Y + (tile.Y - start.Y) * t));
        }
    }

    /// <summary>地图离开可视树时停止动画请求并清理手势状态。</summary>
    /// <param name="e">本次指针或可视树事件的参数。</param>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        DisposeChunks();
        _touches.Clear();
        _dragging = false;
        _motionAttached = false;
        _motionEpoch++;
        _framePending = false;
    }

    /// <summary>地图进入可视树时允许动画调度。</summary>
    /// <param name="e">本次指针或可视树事件的参数。</param>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _motionAttached = true;
        if (Engine is not null) RefreshWorld();
    }

    private void DisposeChunks()
    {
        foreach (var chunk in _chunks.Values)
        {
            chunk.Terrain?.Dispose();
            chunk.Territory?.Dispose();
        }

        _chunks.Clear();
    }

    /// <summary>地图当前选择和跟随设置，用于界面导航返回时恢复。</summary>
    /// <param name="ResidentId">选中的居民 ID，空值表示未选中。</param>
    /// <param name="BuildingId">选中的建筑 ID，空值表示未选中。</param>
    /// <param name="Tile">选中的地格坐标，空值表示未选中。</param>
    /// <param name="Follow">是否跟随所选居民。</param>
    /// <param name="NationId">工具选定的国家 ID。</param>
    /// <param name="SettlementId">工具选定的聚落 ID。</param>
    public readonly record struct MapSelectionState(
        int? ResidentId,
        int? BuildingId,
        (int X, int Y)? Tile,
        bool Follow,
        int NationId,
        int SettlementId);

    private sealed class MapChunk(Rect bounds)
    {
        public Rect Bounds { get; } = bounds;
        public Rect TerrainBounds { get; set; } = bounds;
        public WriteableBitmap? Terrain { get; set; }
        public WriteableBitmap? Territory { get; set; }
        public PixelCanvas? TerrainCanvas { get; set; }
        public uint[] TerrainInputs { get; } = new uint[(ChunkTiles + 2) * (ChunkTiles + 2)];
        public uint TerrainHash { get; set; }
        public uint TerritoryHash { get; set; }
        public bool TerritoryCached { get; set; }
    }
}
