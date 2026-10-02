using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI.Controls;

/// <summary>
/// A single map surface: cached pixel chunks, batched resident geometry, and no per-entity controls.
/// The camera uses artwork pixels; the simulation always uses tile coordinates.
/// </summary>
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
    private readonly Dictionary<(int X, int Y), MapChunk> _chunks = [];
    private readonly Dictionary<IPointer, Point> _touches = [];
    private readonly Dictionary<int, IBrush> _nationBrushes = [];
    private readonly List<(int X, int Y)> _fires = [];
    private readonly Dictionary<int, (string Name, double Size, FormattedText Text)> _settlementLabels = [];
    private Settlement[] _labelSettlements = [];
    private FormattedText? _scaleLabel;
    private int _scaleLabelTiles;
    private static readonly IBrush ScaleBrush = Brush(0xAFDAE6D5);
    private static readonly Pen ScalePen = new(ScaleBrush, 1);
    private readonly StreamGeometry?[] _residents = new StreamGeometry?[4];
    private StreamGeometry? _heads;
    private WorldEngine? _engine;
    private WorldState? _cachedState;
    private double _zoom = 0.4;
    private Point _origin;
    private bool _cameraReady;
    private Point? _hover;
    private (int X, int Y)? _selection;
    private Point _pressPosition;
    private Point _lastPosition;
    private bool _dragging;
    private bool _panning;
    private bool _gestureMoved;
    private bool _pinching;
    private (int X, int Y)? _lastPaint;
    private string _activeTool = "inspect";
    private bool _showBorders = true;

    public WorldMapControl()
    {
        ClipToBounds = true;
        Focusable = true;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
    }

    public WorldEngine? Engine
    {
        get => _engine;
        set
        {
            if (ReferenceEquals(_engine, value)) return;
            CancelPlacement();
            _touches.Clear(); _dragging = false; _pinching = false; _lastPaint = null;
            PickingLocation = false; SelectedNationId = 0; SelectedSettlementId = 0;
            DisposeChunks();
            ResetMotion();
            _settlementLabels.Clear();
            _engine = value;
            _selection = null;
            _cameraReady = false;
            RefreshWorld(true);
        }
    }

    public string ActiveTool
    {
        get => _activeTool;
        set
        {
            CancelPlacement(); _activeTool = value ?? "inspect";
            Cursor = new Cursor(IsNavigationTool ? StandardCursorType.Arrow : StandardCursorType.Cross);
            InvalidateVisual();
        }
    }

    public int BrushRadius { get; set; } = 2;
    public int SelectedNationId { get; set; }
    public int SelectedSettlementId { get; set; }

    public bool ShowBorders
    {
        get => _showBorders;
        set { _showBorders = value; InvalidateVisual(); }
    }

    /// <summary>Raised once before a tool stroke, allowing the shell to pause and capture an undo snapshot.</summary>
    public event EventHandler? WorldEditing;
    public event EventHandler? WorldEdited;
    public event Action<int, int>? TileSelected;
    public event Action<string>? ToolError;

    private bool IsNavigationTool => ActiveTool.Equals("inspect", StringComparison.OrdinalIgnoreCase) ||
                                     ActiveTool.Equals("pan", StringComparison.OrdinalIgnoreCase);

    public void RefreshWorld(bool resetCamera = false)
    {
        if (Engine is null) { InvalidateVisual(); return; }
        if (!ReferenceEquals(_cachedState, Engine.State))
        {
            DisposeChunks();
            ResetMotion();
            _settlementLabels.Clear();
            _effects.Clear(); _seenVisualSequence = Engine.VisualSequence;
            _cachedState = Engine.State;
            _cameraReady = false;
        }
        RebuildChangedChunks();
        _labelSettlements = Engine.State.Settlements.OrderByDescending(settlement => settlement.Population).ToArray();
        CaptureEffects();
        CaptureSelectedRoute();
        CaptureMotionSnapshots();
        if (resetCamera || !_cameraReady) FitWorld();
        InvalidateVisual();
    }

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

    public void ZoomIn() => ZoomAt(new Point(Bounds.Width / 2, Bounds.Height / 2), _zoom * 1.3);
    public void ZoomOut() => ZoomAt(new Point(Bounds.Width / 2, Bounds.Height / 2), _zoom / 1.3);

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

    private double FitZoom => Engine is null ? .4 : Math.Max(.025,
        Math.Min((Bounds.Width - 36) / (Engine.State.Width * TilePixels),
                 (Bounds.Height - 36) / (Engine.State.Height * TilePixels)));

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != BoundsProperty) return;
        if (!_cameraReady) FitWorld();
        else if (change.OldValue is Rect oldBounds && change.NewValue is Rect newBounds)
            _origin += new Vector((newBounds.Width - oldBounds.Width) / 2, (newBounds.Height - oldBounds.Height) / 2);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.DrawRectangle(OceanBrush, null, new Rect(Bounds.Size));
        if (Engine is null || !_cameraReady) return;
        var state = Engine.State;
        _renderFrameTime = PresentationTime;
        RenderedEffectCount = 0; RenderedRouteSegmentCount = 0;
        FollowResident(_renderFrameTime);
        if (_residentGeometryDirty || _geometryZoom != _zoom || _geometryOrigin != _origin)
            RebuildResidents();
        using (context.PushClip(new Rect(Bounds.Size)))
        {
            using (context.PushTransform(Matrix.CreateScale(_zoom, _zoom) * Matrix.CreateTranslation(_origin.X, _origin.Y)))
            {
                foreach (var chunk in _chunks.Values)
                {
                    if (!Visible(chunk.Bounds)) continue;
                    if (chunk.Terrain is not null) context.DrawImage(chunk.Terrain, chunk.TerrainBounds);
                    if (ShowBorders && chunk.Territory is not null) context.DrawImage(chunk.Territory, chunk.Bounds);
                }
                DrawTerrainDetails(context, state);
                foreach (var settlement in state.Settlements)
                    if (Visible(new Rect(settlement.X * TilePixels - 28, settlement.Y * TilePixels - 28, 56, 56)))
                        DrawSettlement(context, settlement);
                DrawBuildings(context, state);
                DrawMapOverlay(context, state);
                for (var race = 0; _zoom < 3 && race < _residents.Length; race++)
                    if (_residents[race] is { } body) context.DrawGeometry(ResidentBrushes[race], null, body);
                if (_zoom >= .7 && _zoom < 3 && _heads is not null) context.DrawGeometry(HeadBrush, null, _heads);
                if (_zoom >= .35)
                {
                    if (_cargoGeometry is not null) context.DrawGeometry(CargoBrush, null, _cargoGeometry);
                    if (_messageGeometry is not null) context.DrawGeometry(MessageBrush, null, _messageGeometry);
                    if (_magicGeometry is not null) context.DrawGeometry(ArcaneBrush, null, _magicGeometry);
                }
                DrawCloseDetails(context, state);
                DrawFires(context, _renderFrameTime);
                DrawEffects(context);
                foreach (var army in state.Armies)
                {
                    var position = _armyMotion.TryGetValue(army.Id, out var motion) ? motion.Position(_renderFrameTime) : new Point(army.X, army.Y);
                    var x = position.X * TilePixels + 4;
                    var y = position.Y * TilePixels;
                    context.DrawRectangle(WoodBrush, null, new Rect(x, y - 10, 1.3, 11));
                    context.DrawRectangle(NationBrush(army.NationId), null, new Rect(x + 1.3, y - 10, 7, 5));
                }
            }
            DrawLabels(context, state);
            DrawSelection(context);
            DrawResidentSelection(context);
            DrawResidentGoal(context);
            DrawScale(context);
        }
    }

    private bool Visible(Rect world) => new Rect(_origin.X + world.X * _zoom, _origin.Y + world.Y * _zoom,
        world.Width * _zoom, world.Height * _zoom).Intersects(new Rect(Bounds.Size));

    private void RebuildChangedChunks()
    {
        var state = Engine!.State;
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
        for (var cy = 0; cy < state.Height; cy += ChunkTiles)
        for (var cx = 0; cx < state.Width; cx += ChunkTiles)
        {
            uint terrainHash = 2166136261;
            uint territoryHash = colorHash;
            var containsTerritory = false;
            // Include a one-tile apron so edited coastlines and borders invalidate their neighbours.
            for (var y = Math.Max(0, cy - 1); y < Math.Min(state.Height, cy + ChunkTiles + 1); y++)
            for (var x = Math.Max(0, cx - 1); x < Math.Min(state.Width, cx + ChunkTiles + 1); x++)
            {
                var tile = state.Tiles[y * state.Width + x];
                terrainHash = unchecked((terrainHash ^ ((uint)tile.Terrain + (tile.DroughtTicks > 0 ? 16u : 0u) + (uint)tile.RoadLevel * 64 + (uint)Math.Clamp((int)(tile.ResourceAmount / 25), 0, 4) * 256)) * 16777619);
                territoryHash = unchecked((territoryHash ^ (uint)tile.NationId) * 16777619);
                containsTerritory |= tile.NationId != 0 && x >= cx && x < cx + ChunkTiles && y >= cy && y < cy + ChunkTiles;
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
                // Adjacent opaque images otherwise expose hairline background seams when
                // the camera maps chunk edges to fractional screen pixels. A one-art-pixel
                // gutter repeats actual neighbouring terrain at its original scale.
                // Transparent territory overlays deliberately do not overlap.
                var left = cx > 0 ? 1 : 0;
                var top = cy > 0 ? 1 : 0;
                var right = cx + ChunkTiles < state.Width ? 1 : 0;
                var bottom = cy + ChunkTiles < state.Height ? 1 : 0;
                chunk.TerrainBounds = new Rect(chunk.Bounds.X - left, chunk.Bounds.Y - top,
                    chunk.Bounds.Width + left + right, chunk.Bounds.Height + top + bottom);
                var canvas = new PixelCanvas((int)chunk.TerrainBounds.Width, (int)chunk.TerrainBounds.Height);
                for (var y = Math.Max(0, cy - 1); y < Math.Min(state.Height, cy + ChunkTiles + 1); y++)
                for (var x = Math.Max(0, cx - 1); x < Math.Min(state.Width, cx + ChunkTiles + 1); x++)
                {
                    DrawTerrainTile(canvas, state, x, y, (x - cx) * TilePixels + left, (y - cy) * TilePixels + top);
                    DrawRoadTile(canvas, state, x, y, (x - cx) * TilePixels + left, (y - cy) * TilePixels + top);
                }
                chunk.Terrain?.Dispose();
                chunk.Terrain = MakeBitmap(canvas, opaque: true);
                chunk.TerrainHash = terrainHash;
            }
            if (!chunk.TerritoryCached || chunk.TerritoryHash != territoryHash)
            {
                chunk.Territory?.Dispose();
                chunk.Territory = null;
                chunk.TerritoryHash = territoryHash;
                chunk.TerritoryCached = true;
                // Unclaimed chunks need no transparent GPU texture. A mostly natural
                // 256×256 world saves nearly 16 MiB compared with allocating every overlay.
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
                    if (x == 0 || state.Tiles[y * state.Width + x - 1].NationId != nationId) canvas.Rect(px, py, 1, 8, edge);
                    if (y == 0 || state.Tiles[(y - 1) * state.Width + x].NationId != nationId) canvas.Rect(px, py, 8, 1, edge);
                    if (x == state.Width - 1 || state.Tiles[y * state.Width + x + 1].NationId != nationId) canvas.Rect(px + 7, py, 1, 8, edge);
                    if (y == state.Height - 1 || state.Tiles[(y + 1) * state.Width + x].NationId != nationId) canvas.Rect(px, py + 7, 8, 1, edge);
                }
                chunk.Territory = MakeBitmap(canvas, opaque: false);
            }
        }
    }

    private static void DrawTerrainTile(PixelCanvas canvas, WorldState state, int x, int y, int px, int py)
    {
        var tile = state.Tiles[y * state.Width + x];
        var noise = PixelCanvas.Noise(x, y, state.Seed);
        var variation = (int)(noise % 11) - 5;
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
            TerrainType.Tundra => 0x99A88CFF,
            _ => 0x719262FF
        };
        if (tile.DroughtTicks > 0 && tile.Terrain is TerrainType.Grass or TerrainType.Forest or TerrainType.Sand or TerrainType.Hills or TerrainType.Wetland)
            color = 0xB59D62FF;
        color = PixelCanvas.Shade(color, variation);
        canvas.Rect(px, py, 8, 8, color);
        var nx = (int)((noise >> 5) % 6) + 1;
        var ny = (int)((noise >> 10) % 6) + 1;
        if (tile.Terrain is TerrainType.DeepWater or TerrainType.Water or TerrainType.River)
        {
            if (noise % 7 == 0) canvas.Rect(px + nx - 1, py + ny, 3, 1, PixelCanvas.Shade(color, 11));
            bool LandAt(int tx, int ty) => tx >= 0 && tx < state.Width && ty >= 0 && ty < state.Height &&
                state.Tiles[ty * state.Width + tx].Terrain is not TerrainType.DeepWater and not TerrainType.Water and not TerrainType.River;
            const uint coast = 0x74A29AFF;
            if (LandAt(x, y - 1)) canvas.Rect(px, py, 8, 1, coast);
            if (LandAt(x - 1, y)) canvas.Rect(px, py, 1, 8, coast);
            if (LandAt(x, y + 1)) canvas.Rect(px, py + 7, 8, 1, coast);
            if (LandAt(x + 1, y)) canvas.Rect(px + 7, py, 1, 8, coast);
            return;
        }
        canvas.Rect(px + nx, py + ny, noise % 2 == 0 ? 2 : 1, 1, PixelCanvas.Shade(color, -10));
        if (tile.Terrain == TerrainType.Forest && tile.ResourceAmount < 25)
        {
            canvas.Rect(px + 3, py + 4, 2, 3, 0x755A3DFF);
            canvas.Rect(px + 2, py + 4, 4, 1, 0xC3A174FF);
        }
        else if (tile.Terrain == TerrainType.Forest)
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
        else if (tile.Terrain is TerrainType.Mountain or TerrainType.Snow)
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
        bool RoadAt(int tx, int ty) => tx >= 0 && ty >= 0 && tx < state.Width && ty < state.Height &&
            state.Tiles[ty * state.Width + tx].RoadLevel > 0;
        var road = tile.RoadLevel >= 2 ? 0xB4B29BFFu : 0xB49A6FFFu;
        var verge = tile.RoadLevel >= 2 ? 0x858E7FFFu : 0x8A795AFFu;
        canvas.Rect(px + 2, py + 2, 4, 4, verge);
        canvas.Rect(px + 3, py + 3, 2, 2, road);
        if (RoadAt(x - 1, y)) { canvas.Rect(px, py + 2, 4, 4, verge); canvas.Rect(px, py + 3, 4, 2, road); }
        if (RoadAt(x + 1, y)) { canvas.Rect(px + 4, py + 2, 4, 4, verge); canvas.Rect(px + 4, py + 3, 4, 2, road); }
        if (RoadAt(x, y - 1)) { canvas.Rect(px + 2, py, 4, 4, verge); canvas.Rect(px + 3, py, 2, 4, road); }
        if (RoadAt(x, y + 1)) { canvas.Rect(px + 2, py + 4, 4, 4, verge); canvas.Rect(px + 3, py + 4, 2, 4, road); }
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
            for (var y = 0; y < canvas.Height; y++)
                Marshal.Copy(canvas.Pixels, y * canvas.Width * 4, pixels.Address + y * pixels.RowBytes, canvas.Width * 4);
        return bitmap;
    }

    private void RebuildResidents()
    {
        if (Engine is null) return;
        var now = _renderFrameTime;
        _renderedResidentPoints.Clear();
        var heads = _zoom >= .7 ? new StreamGeometry() : null;
        using var headContext = heads?.Open();
        var cargo = new StreamGeometry();
        var messages = new StreamGeometry();
        var magic = new StreamGeometry();
        using var cargoContext = cargo.Open();
        using var messageContext = messages.Open();
        using var magicContext = magic.Open();
        var contexts = new StreamGeometryContext[4];
        for (var race = 0; race < 4; race++)
        {
            _residents[race] = new StreamGeometry();
            contexts[race] = _residents[race]!.Open();
        }
        try
        {
            foreach (var resident in Engine.State.Residents)
            {
                var position = _residentMotion.TryGetValue(resident.Id, out var motion) ? motion.Position(now) : new Point(resident.X, resident.Y);
                var x = (position.X + .5) * TilePixels - .9;
                var y = (position.Y + .5) * TilePixels;
                _renderedResidentPoints[resident.Id] = ToScreen((position.X + .5) * TilePixels, (position.Y + .5) * TilePixels);
                if (!Visible(new Rect(x - 2, y - 3, 6, 7))) continue;
                var race = Math.Clamp((int)resident.Race, 0, 3);
                GeometryRect(contexts[race], x, y, resident.Profession == Profession.Soldier ? 2.6 : 1.8, 2.4);
                if (headContext is not null) GeometryRect(headContext, x, y - 1.4, 1.8, 1.4);
                if (_zoom < .35) continue;
                if (resident.Inventory.Food + resident.Inventory.Wood + resident.Inventory.Stone + resident.Inventory.Ore > 0)
                    GeometryRect(cargoContext, x + 1.8, y + .5, 2.3, 2);
                if (resident.Agent.CarriedMessages.Count > 0)
                    GeometryRect(messageContext, x + 1.7, y - 2.3, 2.8, 1.6);
                if (resident.Profession == Profession.Mage)
                    GeometryRect(magicContext, x - .8, y - 2.5, 3.4, 1.1);
            }
        }
        finally { foreach (var draw in contexts) draw.Dispose(); }
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
        context.BeginFigure(new Point(x, y), true);
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
        // The town hall marks the settlement; farms and other facilities are rendered only
        // from real construction entities, so decorations cannot imply nonexistent production.
        context.DrawRectangle(WoodBrush, null, new Rect(x - 6, y + 2, 14, 3));
        DrawHouse(context, x - 3, y - 5, nationBrush, settlement.Level >= 2 ? 1.5 : 1.2);
        context.DrawRectangle(WoodBrush, null, new Rect(x + 2, y - 19, 1, 11));
        context.DrawRectangle(nationBrush, null, new Rect(x + 3, y - 19, 6, 4));
        if (settlement.FertilityBoostTicks > 0)
            context.DrawEllipse(null, new Pen(HealingBrush, .8), new Point(x, y), 10, 6);
        if (settlement.ShieldTicks > 0)
            context.DrawEllipse(null, new Pen(ArcaneBrush, 1), new Point(x, y - 5), 13, 16);
    }

    private static void DrawHouse(DrawingContext context, double x, double y, IBrush roof, double scale)
    {
        void Box(IBrush brush, double dx, double dy, double w, double h) =>
            context.DrawRectangle(brush, null, new Rect(x + dx * scale, y + dy * scale, w * scale, h * scale));
        Box(ShadowBrush, -1, 6, 10, 3);
        Box(WallBrush, 0, 1, 7, 6);
        Box(roof, -1, -1, 9, 3);
        Box(roof, 1, -3, 5, 2);
        Box(WoodBrush, 3, 4, 2, 3);
        Box(WoodBrush, 0.8, 3, 1, 1);
    }

    private static readonly IBrush FlameOuter = Brush(0xFFF0793D);
    private static readonly IBrush FlameInner = Brush(0xFFFFD575);
    private void DrawFires(DrawingContext context, double time)
    {
        foreach (var (tx, ty) in _fires)
        {
            var x = tx * TilePixels + 4;
            var y = ty * TilePixels + 6;
            if (!Visible(new Rect(x - 5, y - 16, 12, 22))) continue;
            RenderedEffectCount++; RenderedEffectTime = time;
            var phase = time * 8 + tx * 1.7 + ty;
            var sway = Math.Sin(phase) * 1.2;
            using (context.PushOpacity(.18)) context.DrawEllipse(FlameOuter, null, new Point(x, y - 2), 7, 8);
            Triangle(context, FlameOuter, new(x - 3, y), new(x + 3, y), new(x + sway, y - 9 - Math.Sin(phase * .7) * 2));
            Triangle(context, FlameInner, new(x - 1.7, y), new(x + 1.8, y), new(x - sway * .4, y - 5));
            for (var i = 0; i < 2; i++)
            {
                var rise = (time * 5 + i * 5 + tx % 3) % 10;
                using (context.PushOpacity((1 - rise / 10) * .4))
                    context.DrawEllipse(StoneBrush, null, new Point(x + Math.Sin(phase * .2 + i) * 2, y - 8 - rise), 1.4 + rise * .12, 1.2);
            }
        }
    }

    private void DrawLabels(DrawingContext context, WorldState state)
    {
        if (_zoom < .22) return;
        // Keep tiny mobile maps legible by rejecting overlapping labels.
        var occupied = new List<Rect>();
        foreach (var settlement in _labelSettlements)
        {
            var position = ToScreen((settlement.X + .5) * TilePixels, settlement.Y * TilePixels - 24);
            var size = _zoom > .6 ? 12 : 10;
            if (!_settlementLabels.TryGetValue(settlement.Id, out var cached) || cached.Name != settlement.Name || cached.Size != size)
            {
                cached = (settlement.Name, size, new FormattedText(settlement.Name, CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, MapTypeface, size, LabelBrush));
                _settlementLabels[settlement.Id] = cached;
            }
            var text = cached.Text;
            var rect = new Rect(position.X - text.Width / 2 - 5, position.Y - text.Height - 3,
                text.Width + 10, text.Height + 5);
            if (!rect.Intersects(new Rect(Bounds.Size)) || occupied.Any(other => other.Intersects(rect.Inflate(4)))) continue;
            occupied.Add(rect);
            context.DrawRectangle(LabelShadow, null, rect, 3, 3);
            context.DrawText(text, new Point(rect.X + 5, rect.Y + 2));
        }
    }

    private void DrawSelection(DrawingContext context)
    {
        if (_selection is { } selected)
        {
            var center = ToScreen((selected.X + .5) * TilePixels, (selected.Y + .5) * TilePixels);
            var size = Math.Max(7, TilePixels * _zoom + 2);
            context.DrawRectangle(null, SelectionPen, new Rect(center.X - size / 2, center.Y - size / 2, size, size), 2, 2);
        }
        var hover = _pendingPlacement is { } pending ? GetTileScreenPosition(pending.X, pending.Y) : _hover;
        if (hover is not { } position || IsNavigationTool || !TryTile(position, out var tile)) return;
        var valid = PlacementError(tile.X, tile.Y) is null;
        var previewPen = new Pen(Brush(valid ? 0xFFB8E9BCu : 0xFFF08060u), 2);
        var previewFill = Brush(valid ? 0x448CDDABu : 0x55F08060u);
        var point = ToScreen((tile.X + .5) * TilePixels, (tile.Y + .5) * TilePixels);
        if (ActiveTool.StartsWith("build:", StringComparison.OrdinalIgnoreCase))
        {
            var size = Math.Max(6, TilePixels * _zoom);
            context.DrawRectangle(previewFill, previewPen,
                new Rect(point.X - size / 2, point.Y - size / 2, size, size), 1, 1);
            return;
        }
        var brushTiles = ActiveTool.StartsWith("road:", StringComparison.OrdinalIgnoreCase)
            ? 0 : Math.Clamp(BrushRadius, 0, 16);
        var toolName = ActiveTool.Contains(':') ? ActiveTool[(ActiveTool.IndexOf(':') + 1)..] : ActiveTool;
        if (Enum.TryParse<DisasterKind>(toolName, true, out _)) brushTiles = DisasterRadius;
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

    private IBrush NationBrush(int nationId) => _nationBrushes.TryGetValue(nationId, out var brush) ? brush : RoofBrush;
    private static IBrush Brush(uint argb) => new SolidColorBrush(Color.FromUInt32(argb));
    private Point ToScreen(double x, double y) => new(_origin.X + x * _zoom, _origin.Y + y * _zoom);
    private static double Distance(Point first, Point second) =>
        Math.Sqrt((first.X - second.X) * (first.X - second.X) + (first.Y - second.Y) * (first.Y - second.Y));

    private bool TryTile(Point position, out (int X, int Y) tile)
    {
        tile = ((int)Math.Floor((position.X - _origin.X) / (_zoom * TilePixels)),
                (int)Math.Floor((position.Y - _origin.Y) / (_zoom * TilePixels)));
        return Engine is not null && tile.X >= 0 && tile.Y >= 0 && tile.X < Engine.State.Width && tile.Y < Engine.State.Height;
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

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        ZoomAt(e.GetPosition(this), _zoom * Math.Pow(1.18, e.Delta.Y));
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var point = e.GetPosition(this);
        e.Pointer.Capture(this);
        if (e.Pointer.Type == PointerType.Touch)
        {
            _touches[e.Pointer] = point;
            if (_touches.Count > 1) { _pinching = true; _gestureMoved = true; }
            else { _pressPosition = point; _lastPosition = point; _gestureMoved = false; _pinching = false; }
        }
        else
        {
            _pressPosition = point;
            _lastPosition = point;
            _gestureMoved = false;
            _dragging = true;
            var properties = e.GetCurrentPoint(this).Properties;
            _panning = IsNavigationTool || properties.IsMiddleButtonPressed || properties.IsRightButtonPressed;
            _lastPaint = null;
            if (!_panning) ApplyTool(point);
        }
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        _hover = point;
        if (!HasPendingPlacement) PreviewPlacement(point);
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
            if (_panning) { _followSelectedResident = false; _origin += point - _lastPosition; }
            else ApplyTool(point);
            _lastPosition = point;
            e.Handled = true;
        }
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var point = e.GetPosition(this);
        if (e.Pointer.Type == PointerType.Touch)
        {
            // A capture-lost/canceled touch can deliver a late release. Only a still-active,
            // single-finger tap may apply a tool; a pinch remains navigation until all fingers lift.
            if (_touches.ContainsKey(e.Pointer) && !_gestureMoved && !_pinching)
            {
                if (IsNavigationTool || Enum.TryParse<TerrainType>(ActiveTool, out _) || ActiveTool == "territory" || ActiveTool.StartsWith("road:")) ApplyTool(point);
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

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _touches.Remove(e.Pointer);
        _dragging = false;
        _lastPaint = null;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = null;
        if (!HasPendingPlacement) SetPlacementMessage("");
        InvalidateVisual();
    }

    private void SelectTile(Point point)
    {
        if (!PickingLocation && SelectResidentAt(point)) return;
        if (!TryTile(point, out var tile)) return;
        ClearResidentSelection();
        _selection = tile;
        TileSelected?.Invoke(tile.X, tile.Y);
        InvalidateVisual();
    }

    private void ApplyTool(Point point)
    {
        if (Engine is null || !TryTile(point, out var tile)) return;
        if (IsNavigationTool) { SelectTile(point); return; }
        if (_lastPaint == tile) return;
        if (PlacementError(tile.X, tile.Y) is { } placementError)
        { SetPlacementMessage("无法放置：" + placementError); ToolError?.Invoke(placementError); return; }
        var tool = ActiveTool;
        var prefix = tool.IndexOf(':');
        if (prefix >= 0) tool = tool[(prefix + 1)..];
        var edited = false;
        if (TryApplyConstructionTool(tool, tile, out var constructionEdited))
        {
            if (constructionEdited) { RefreshWorld(); WorldEdited?.Invoke(this, EventArgs.Empty); }
            return;
        }
        if (tool.Equals("territory", StringComparison.OrdinalIgnoreCase))
        {
            if (!Engine.State.Nations.Any(nation => nation.Id == SelectedNationId)) return;
            if (_lastPaint is null) WorldEditing?.Invoke(this, EventArgs.Empty);
            Stroke(tile, (x, y) => Engine.TransferTerritory(x, y, SelectedNationId, Math.Clamp(BrushRadius, 0, 16)));
            edited = true;
        }
        else if (Enum.TryParse<TerrainType>(tool, true, out var terrain))
        {
            if (_lastPaint is null) WorldEditing?.Invoke(this, EventArgs.Empty);
            Stroke(tile, (x, y) => Engine.PaintTerrain(x, y, terrain, Math.Clamp(BrushRadius, 0, 16)));
            edited = true;
        }
        else if (Enum.TryParse<RaceKind>(tool, true, out var race))
        {
            // Each stroke deposits one group. Dragging a species tool should not create thousands of residents.
            if (_lastPaint is null)
            {
                WorldEditing?.Invoke(this, EventArgs.Empty);
                Engine.SpawnResidents(tile.X, tile.Y, race, SpawnCount);
                edited = true;
            }
        }
        else if (Enum.TryParse<DisasterKind>(tool, true, out var disaster))
        {
            if (_lastPaint is null)
            {
                WorldEditing?.Invoke(this, EventArgs.Empty);
                Engine.TriggerDisaster(tile.X, tile.Y, disaster, DisasterRadius);
                edited = true;
            }
        }
        _lastPaint = tile;
        if (!edited) return;
        RefreshWorld();
        WorldEdited?.Invoke(this, EventArgs.Empty);
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

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _motionAttached = true;
        if (Engine is not null) RefreshWorld();
    }

    private void DisposeChunks()
    {
        foreach (var chunk in _chunks.Values) { chunk.Terrain?.Dispose(); chunk.Territory?.Dispose(); }
        _chunks.Clear();
    }

    private sealed class MapChunk(Rect bounds)
    {
        public Rect Bounds { get; } = bounds;
        public Rect TerrainBounds { get; set; } = bounds;
        public WriteableBitmap? Terrain { get; set; }
        public WriteableBitmap? Territory { get; set; }
        public uint TerrainHash { get; set; }
        public uint TerritoryHash { get; set; }
        public bool TerritoryCached { get; set; }
    }
}
