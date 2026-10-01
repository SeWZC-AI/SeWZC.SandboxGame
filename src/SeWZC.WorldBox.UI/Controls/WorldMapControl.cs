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
public sealed class WorldMapControl : Control
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
            DisposeChunks();
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
            _activeTool = value ?? "inspect";
            Cursor = new Cursor(IsNavigationTool ? StandardCursorType.Arrow : StandardCursorType.Cross);
            InvalidateVisual();
        }
    }

    public int BrushRadius { get; set; } = 2;
    public int SelectedNationId { get; set; }

    public bool ShowBorders
    {
        get => _showBorders;
        set { _showBorders = value; InvalidateVisual(); }
    }

    /// <summary>Raised once before a tool stroke, allowing the shell to pause and capture an undo snapshot.</summary>
    public event EventHandler? WorldEditing;
    public event EventHandler? WorldEdited;
    public event Action<int, int>? TileSelected;

    private bool IsNavigationTool => ActiveTool.Equals("inspect", StringComparison.OrdinalIgnoreCase) ||
                                     ActiveTool.Equals("pan", StringComparison.OrdinalIgnoreCase);

    public void RefreshWorld(bool resetCamera = false)
    {
        if (Engine is null) { InvalidateVisual(); return; }
        if (!ReferenceEquals(_cachedState, Engine.State))
        {
            DisposeChunks();
            _cachedState = Engine.State;
            _cameraReady = false;
        }
        RebuildChangedChunks();
        RebuildResidents();
        if (resetCamera || !_cameraReady) FitWorld();
        InvalidateVisual();
    }

    public void FitWorld()
    {
        if (Engine is null || Bounds.Width < 1 || Bounds.Height < 1) return;
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
                DrawTrade(context, state);
                foreach (var settlement in state.Settlements)
                    if (Visible(new Rect(settlement.X * TilePixels - 28, settlement.Y * TilePixels - 28, 56, 56)))
                        DrawSettlement(context, settlement);
                for (var race = 0; race < _residents.Length; race++)
                    if (_residents[race] is { } body) context.DrawGeometry(ResidentBrushes[race], null, body);
                if (_zoom >= .7 && _heads is not null) context.DrawGeometry(HeadBrush, null, _heads);
                DrawFires(context, state.Tick);
                foreach (var army in state.Armies)
                {
                    var x = army.X * TilePixels + 4;
                    var y = army.Y * TilePixels;
                    context.DrawRectangle(WoodBrush, null, new Rect(x, y - 10, 1.3, 11));
                    context.DrawRectangle(NationBrush(army.NationId), null, new Rect(x + 1.3, y - 10, 7, 5));
                }
            }
            DrawLabels(context, state);
            DrawSelection(context);
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
                terrainHash = unchecked((terrainHash ^ ((uint)tile.Terrain + (tile.DroughtTicks > 0 ? 16u : 0u))) * 16777619);
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
                    DrawTerrainTile(canvas, state, x, y, (x - cx) * TilePixels + left, (y - cy) * TilePixels + top);
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
            _ => 0x719262FF
        };
        if (tile.DroughtTicks > 0 && tile.Terrain is TerrainType.Grass or TerrainType.Forest or TerrainType.Sand)
            color = 0xB59D62FF;
        color = PixelCanvas.Shade(color, variation);
        canvas.Rect(px, py, 8, 8, color);
        var nx = (int)((noise >> 5) % 6) + 1;
        var ny = (int)((noise >> 10) % 6) + 1;
        if (tile.Terrain is TerrainType.DeepWater or TerrainType.Water)
        {
            if (noise % 7 == 0) canvas.Rect(px + nx - 1, py + ny, 3, 1, PixelCanvas.Shade(color, 11));
            bool LandAt(int tx, int ty) => tx >= 0 && tx < state.Width && ty >= 0 && ty < state.Height &&
                state.Tiles[ty * state.Width + tx].Terrain is not TerrainType.DeepWater and not TerrainType.Water;
            const uint coast = 0x74A29AFF;
            if (LandAt(x, y - 1)) canvas.Rect(px, py, 8, 1, coast);
            if (LandAt(x - 1, y)) canvas.Rect(px, py, 1, 8, coast);
            if (LandAt(x, y + 1)) canvas.Rect(px, py + 7, 8, 1, coast);
            if (LandAt(x + 1, y)) canvas.Rect(px + 7, py, 1, 8, coast);
            return;
        }
        canvas.Rect(px + nx, py + ny, noise % 2 == 0 ? 2 : 1, 1, PixelCanvas.Shade(color, -10));
        if (tile.Terrain == TerrainType.Forest)
        {
            var shift = (int)(noise % 2);
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
        var heads = new StreamGeometry();
        using (var headContext = heads.Open())
        {
            for (var race = 0; race < 4; race++)
            {
                var geometry = new StreamGeometry();
                using (var draw = geometry.Open())
                {
                    foreach (var resident in Engine.State.Residents)
                    {
                        if ((int)resident.Race != race) continue;
                        var x = resident.X * TilePixels + 2 + resident.Id % 4;
                        var y = resident.Y * TilePixels + 3 + (resident.Id / 4) % 3;
                        GeometryRect(draw, x, y, resident.Profession == Profession.Soldier ? 2.6 : 1.8, 2.4);
                        GeometryRect(headContext, x, y - 1.4, 1.8, 1.4);
                    }
                }
                _residents[race] = geometry;
            }
        }
        _heads = heads;
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
        // Cultivated strips and original tiny buildings make settlements readable before labels.
        context.DrawRectangle(FarmBrush, null, new Rect(x - 17, y + 5, 10, 7));
        for (var row = 0; row < 3; row++)
            context.DrawRectangle(WoodBrush, null, new Rect(x - 16, y + 6 + row * 2, 8, .7));
        DrawHouse(context, x - 12, y - 5, nationBrush, 1);
        DrawHouse(context, x + 8, y + 3, RoofBrush, 1);
        if (settlement.Population > 35) DrawHouse(context, x + 9, y - 11, nationBrush, .8);
        if (settlement.Population > 70) DrawHouse(context, x - 9, y - 16, RoofBrush, .9);
        DrawHouse(context, x - 3, y - 5, nationBrush, settlement.Level >= 2 ? 1.5 : 1.2);
        context.DrawRectangle(WoodBrush, null, new Rect(x + 2, y - 19, 1, 11));
        context.DrawRectangle(nationBrush, null, new Rect(x + 3, y - 19, 6, 4));
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
    private void DrawFires(DrawingContext context, long tick)
    {
        foreach (var (tx, ty) in _fires)
        {
            var x = tx * TilePixels;
            var y = ty * TilePixels;
            if (!Visible(new Rect(x, y - 4, 8, 12))) continue;
            var offset = (tx + ty + tick) % 3;
            context.DrawRectangle(FlameOuter, null, new Rect(x + 1, y + 1 - offset, 5, 6));
            context.DrawRectangle(FlameOuter, null, new Rect(x + 3, y - 2 - offset, 2, 8));
            context.DrawRectangle(FlameInner, null, new Rect(x + 2, y + 3, 3, 4));
        }
    }

    private void DrawTrade(DrawingContext context, WorldState state)
    {
        if (_zoom < .3 || state.TradeRoutes.Count == 0) return;
        var settlements = state.Settlements.ToDictionary(s => s.Id);
        var pen = new Pen(Brush(0x458FAD92), 1, dashStyle: DashStyle.Dash);
        foreach (var route in state.TradeRoutes)
        {
            if (!settlements.TryGetValue(route.FromSettlementId, out var from) ||
                !settlements.TryGetValue(route.ToSettlementId, out var to)) continue;
            var start = new Point((from.X + .5) * TilePixels, (from.Y + .5) * TilePixels);
            var end = new Point((to.X + .5) * TilePixels, (to.Y + .5) * TilePixels);
            context.DrawLine(pen, start, end);
            // This is a relationship link, not a caravan trajectory. Transport uses the
            // simulation's validated land path; do not animate units through sea or mountains.
        }
    }

    private void DrawLabels(DrawingContext context, WorldState state)
    {
        if (_zoom < .22) return;
        // Keep tiny mobile maps legible by rejecting overlapping labels.
        var occupied = new List<Rect>();
        foreach (var settlement in state.Settlements.OrderByDescending(s => s.Population))
        {
            var position = ToScreen((settlement.X + .5) * TilePixels, settlement.Y * TilePixels - 24);
            var text = new FormattedText(settlement.Name, CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, MapTypeface, _zoom > .6 ? 12 : 10, LabelBrush);
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
        if (_hover is not { } hover || IsNavigationTool || !TryTile(hover, out var tile)) return;
        var point = ToScreen((tile.X + .5) * TilePixels, (tile.Y + .5) * TilePixels);
        var radius = Math.Max(3, (Math.Clamp(BrushRadius, 0, 16) + .5) * TilePixels * _zoom);
        context.DrawEllipse(Brush(0x18FFE5AE), HoverPen, point, radius, radius);
    }

    private void DrawScale(DrawingContext context)
    {
        var tiles = _zoom > 1 ? 5 : _zoom > .3 ? 10 : 25;
        var length = tiles * TilePixels * _zoom;
        var x = 18.0;
        var y = Bounds.Height - 22;
        var pen = new Pen(Brush(0xAFDAE6D5), 1);
        context.DrawLine(pen, new Point(x, y), new Point(x + length, y));
        context.DrawLine(pen, new Point(x, y - 3), new Point(x, y + 3));
        context.DrawLine(pen, new Point(x + length, y - 3), new Point(x + length, y + 3));
        var text = new FormattedText($"{tiles} 格", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            MapTypeface, 10, Brush(0xAFDAE6D5));
        context.DrawText(text, new Point(x, y - 18));
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
        var next = Math.Clamp(zoom, FitZoom * .55, 7);
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
        if (e.Pointer.Type == PointerType.Touch && _touches.ContainsKey(e.Pointer))
        {
            if (_touches.Count >= 2)
            {
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
            if (_panning) _origin += point - _lastPosition;
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
            if (_touches.ContainsKey(e.Pointer) && !_gestureMoved && !_pinching) ApplyTool(point);
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
        InvalidateVisual();
    }

    private void SelectTile(Point point)
    {
        if (!TryTile(point, out var tile)) return;
        _selection = tile;
        TileSelected?.Invoke(tile.X, tile.Y);
        InvalidateVisual();
    }

    private void ApplyTool(Point point)
    {
        if (Engine is null || !TryTile(point, out var tile)) return;
        if (IsNavigationTool) { SelectTile(point); return; }
        if (_lastPaint == tile) return;
        var tool = ActiveTool;
        var prefix = tool.IndexOf(':');
        if (prefix >= 0) tool = tool[(prefix + 1)..];
        var edited = false;
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
                Engine.SpawnResidents(tile.X, tile.Y, race, 12);
                edited = true;
            }
        }
        else if (Enum.TryParse<DisasterKind>(tool, true, out var disaster))
        {
            if (_lastPaint is null)
            {
                WorldEditing?.Invoke(this, EventArgs.Empty);
                Engine.TriggerDisaster(tile.X, tile.Y, disaster, Math.Max(2, BrushRadius));
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
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
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
