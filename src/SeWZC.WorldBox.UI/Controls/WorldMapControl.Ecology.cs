using Avalonia;
using Avalonia.Media;
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

    private void DrawEcology(DrawingContext context, WorldState state)
    {
        RenderedWildlifeCount = 0;
        if (_zoom < 3) return;
        var left = Math.Clamp((int)(-_origin.X / (_zoom * TilePixels)) - 2, 0, state.Width - 1);
        var right = Math.Clamp((int)((Bounds.Width - _origin.X) / (_zoom * TilePixels)) + 2, 0, state.Width - 1);
        var top = Math.Clamp((int)(-_origin.Y / (_zoom * TilePixels)) - 2, 0, state.Height - 1);
        var bottom = Math.Clamp((int)((Bounds.Height - _origin.Y) / (_zoom * TilePixels)) + 2, 0, state.Height - 1);
        for (var y = top; y <= bottom; y++)
            for (var x = left; x <= right; x++)
            {
                var tile = state.Tiles[y * state.Width + x];
                if (tile.Deposit is { } resource && VisibleResources.Contains(resource) && Engine!.IsDepositVisible(tile, ResourceVisibility))
                {
                    var brush = resource == ResourceKind.Coal ? CoalMarkerBrush : resource == ResourceKind.Oil ? OilMarkerBrush : RareMarkerBrush;
                    var point = new Point((x + .8) * TilePixels, (y + .2) * TilePixels);
                    context.DrawEllipse(brush, new Pen(MessageBrush, .15), point, .8, .8);
                }
                if (!ShowWildlife || tile.Wildlife == WildlifeKind.None || tile.WildlifePopulation < .25) continue;
                var capacity = Math.Max(1, WorldEngine.WildlifeCapacity(tile, tile.Wildlife));
                var scale = .25 + .75 * Math.Clamp(tile.WildlifePopulation / capacity, 0, 1);
                var cx = (x + .5) * TilePixels; var cy = (y + .75) * TilePixels;
                var brushAnimal = tile.Wildlife is WildlifeKind.Fish or WildlifeKind.Waterfowl ? WaterAnimalBrush : AnimalBrush;
                void Box(double dx, double dy, double width, double height) => context.DrawRectangle(brushAnimal, null,
                    new Rect(cx + dx * scale, cy + dy * scale, width * scale, height * scale));
                context.DrawEllipse(ShadowBrush, null, new Point(cx, cy + 1.1 * scale), 3 * scale, .7 * scale);
                Box(-2.5, -1.1, 4, 1.9); Box(1, -2, 1.5, 1.5);
                if (tile.Wildlife == WildlifeKind.Fish) { Box(-3.5, -1.7, 1, 3); Box(-.7, -2, 1, 1); }
                else if (tile.Wildlife == WildlifeKind.Waterfowl) { Box(2, -1.8, 1.5, .5); Box(-1, -2, 1.8, .7); }
                else
                {
                    Box(-2, .7, .7, 1.2); Box(.5, .7, .7, 1.2);
                    if (tile.Wildlife == WildlifeKind.Rabbit) { Box(1.1, -4, .5, 2); Box(2, -3.6, .5, 1.6); }
                    else if (tile.Wildlife is WildlifeKind.Deer or WildlifeKind.Goat) { Box(.8, -3.5, .4, 1.5); Box(2, -3.5, .4, 1.5); Box(.2, -3.4, 2.8, .4); }
                    else if (tile.Wildlife == WildlifeKind.Wolf) { Box(1, -3, .6, 1); Box(-3.5, -1.3, 1.2, .5); }
                    else if (tile.Wildlife == WildlifeKind.Boar) { Box(2.2, -1.2, 1, .6); Box(-3.2, -.7, .8, .4); }
                }
                RenderedWildlifeCount++;
            }
    }

    private void DrawInfrastructureOverlay(DrawingContext context, WorldState state)
    {
        var roadPen = new Pen(MessageBrush, 1.2);
        for (var i = 0; i < state.Tiles.Length; i++)
        {
            if (state.Tiles[i].RoadLevel == 0) continue;
            var x = i % state.Width; var y = i / state.Width;
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
