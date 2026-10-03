using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI.Controls;

public sealed partial class WorldMapControl
{
    public bool ShowWildlife { get; set; } = true;
    public ResourceVisibility ResourceVisibility { get; set; } = ResourceVisibility.Researched;
    public HashSet<ResourceKind> VisibleResources { get; } = [ResourceKind.Coal, ResourceKind.Oil, ResourceKind.RareEarth];
    public int RenderedWildlifeCount { get; private set; }
    private static readonly IBrush AnimalBrush = Brush(0xFFF3DBAA);
    private static readonly IBrush WaterAnimalBrush = Brush(0xFFBFE9F0);
    private static readonly IBrush CoalMarkerBrush = Brush(0xFF263547);
    private static readonly IBrush OilMarkerBrush = Brush(0xFF368BE8);
    private static readonly IBrush RareMarkerBrush = Brush(0xFFCC73ED);

    private bool _ecologyDirty = true;
    private (int Left, int Right, int Top, int Bottom) _ecologyViewport;
    private readonly Dictionary<WildlifeKind, WriteableBitmap> _animalIcons = [];
    private readonly List<(WriteableBitmap Icon, Rect Bounds)> _wildlifeDraws = [];
    private readonly List<(IBrush Brush, Point Point)> _depositDraws = [];
    private static readonly Pen DepositPen = new(MessageBrush, .15);

    private WriteableBitmap AnimalIcon(WildlifeKind kind)
    {
        if (_animalIcons.TryGetValue(kind, out var icon)) return icon;
        var canvas = new PixelCanvas(32, 32);
        var color = kind is WildlifeKind.Fish or WildlifeKind.Waterfowl ? 0xBFE9F0FFu : 0xF3DBAAFFu;
        void Box(double x, double y, double w, double h) => canvas.Rect((int)((x + 4) * 4), (int)((y + 4) * 4), Math.Max(1, (int)(w * 4)), Math.Max(1, (int)(h * 4)), color);
        Box(-2.5, -1.1, 4, 1.9); Box(1, -2, 1.5, 1.5);
        if (kind == WildlifeKind.Fish) { Box(-3.5, -1.7, 1, 3); Box(-.7, -2, 1, 1); }
        else if (kind == WildlifeKind.Waterfowl) { Box(2, -1.8, 1.5, .5); Box(-1, -2, 1.8, .7); }
        else
        {
            Box(-2, .7, .7, 1.2); Box(.5, .7, .7, 1.2);
            if (kind == WildlifeKind.Rabbit) { Box(1.1, -4, .5, 2); Box(2, -3.6, .5, 1.6); }
            else if (kind is WildlifeKind.Deer or WildlifeKind.Goat) { Box(.8, -3.5, .4, 1.5); Box(2, -3.5, .4, 1.5); Box(.2, -3.4, 2.8, .4); }
            else if (kind == WildlifeKind.Wolf) { Box(1, -3, .6, 1); Box(-3.5, -1.3, 1.2, .5); }
            else if (kind == WildlifeKind.Boar) { Box(2.2, -1.2, 1, .6); Box(-3.2, -.7, .8, .4); }
        }
        icon = MakeBitmap(canvas, opaque: false); _animalIcons[kind] = icon; return icon;
    }

    private void DrawEcology(DrawingContext context, WorldState state)
    {
        RenderedWildlifeCount = 0;
        if (_zoom < 3) return;
        var viewport = VisibleTiles(state);
        if (_ecologyDirty || viewport != _ecologyViewport)
        {
            _ecologyDirty = false; _ecologyViewport = viewport;
            _wildlifeDraws.Clear(); _depositDraws.Clear();
            for (var y = viewport.Top; y <= viewport.Bottom; y++)
                for (var x = viewport.Left; x <= viewport.Right; x++)
                {
                    var tile = state.Tiles[y * state.Width + x];
                    if (tile.Deposit is { } resource && VisibleResources.Contains(resource) && Engine!.IsDepositVisible(tile, ResourceVisibility))
                        _depositDraws.Add((resource == ResourceKind.Coal ? CoalMarkerBrush : resource == ResourceKind.Oil ? OilMarkerBrush : RareMarkerBrush,
                            new Point((x + .8) * TilePixels, (y + .2) * TilePixels)));
                    if (!ShowWildlife || tile.Wildlife == WildlifeKind.None || tile.WildlifePopulation < .25) continue;
                    var scale = .25 + .75 * Math.Clamp(tile.WildlifePopulation / Math.Max(1, WorldEngine.WildlifeCapacity(tile, tile.Wildlife)), 0, 1);
                    var cx = (x + .5) * TilePixels; var cy = (y + .75) * TilePixels;
                    _wildlifeDraws.Add((AnimalIcon(tile.Wildlife), new Rect(cx - 4 * scale, cy - 4 * scale, 8 * scale, 8 * scale)));
                }
        }
        foreach (var (brush, point) in _depositDraws) context.DrawEllipse(brush, DepositPen, point, .8, .8);
        foreach (var (icon, bounds) in _wildlifeDraws) context.DrawImage(icon, bounds);
        RenderedWildlifeCount = _wildlifeDraws.Count;
    }

    private void DrawInfrastructureOverlay(DrawingContext context, WorldState state)
    {
        var roadPen = new Pen(MessageBrush, 1.2);
        var viewport = VisibleTiles(state);
        for (var y = viewport.Top; y <= viewport.Bottom; y++)
        for (var x = viewport.Left; x <= viewport.Right; x++)
        {
            var i = y * state.Width + x;
            if (state.Tiles[i].RoadLevel == 0) continue;
            if (!Visible(new Rect(x * TilePixels, y * TilePixels, TilePixels, TilePixels))) continue;
            var center = new Point((x + .5) * TilePixels, (y + .5) * TilePixels);
            context.DrawEllipse(MessageBrush, null, center, 1.2, 1.2);
            if (x + 1 < state.Width && state.Tiles[i + 1].RoadLevel > 0) context.DrawLine(roadPen, center, new Point(center.X + TilePixels, center.Y));
            if (y + 1 < state.Height && state.Tiles[i + state.Width].RoadLevel > 0) context.DrawLine(roadPen, center, new Point(center.X, center.Y + TilePixels));
        }
        foreach (var b in state.Society.Buildings)
        {
            if (!Visible(new Rect(b.X * TilePixels, b.Y * TilePixels - 12, TilePixels, 20))) continue;
            context.DrawEllipse(null, new Pen(b.Kind == BuildingKind.TownCenter ? MessageBrush : ProgressBrush, 1.2),
                new Point((b.X + .5) * TilePixels, (b.Y + .5) * TilePixels - 3), 6, 7);
        }
    }
}
