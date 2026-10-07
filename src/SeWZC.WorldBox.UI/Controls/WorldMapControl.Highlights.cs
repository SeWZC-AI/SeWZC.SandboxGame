using Avalonia;
using Avalonia.Media;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI.Controls;

public sealed partial class WorldMapControl
{
    private static readonly uint[] TownHighlightColors =
        [0xAA5CAEE8, 0xAA9ADA72, 0xAAE1AE60, 0xAAC293E0, 0xAAE48090, 0xAA62CFBC];

    private readonly Dictionary<uint, IBrush> _tileHighlightBrushes = [];

    private static readonly Pen WorkingHighlight = new(Brush(0xFF79D58F)),
        RestingHighlight = new(Brush(0xFFB6A2EF)),
        TravelHighlight = new(Brush(0xFF65C8FA));

    private void DrawExtraHighlights(DrawingContext context, WorldState state)
    {
        if (Overlay is 9 or 10)
        {
            _residentMarkers.Clear();
            foreach (var resident in VisibleResidents(state))
            {
                if (Overlay == 9 && resident.TravelMode != TravelMode.Boat &&
                    resident.Agent.Goal.Kind != AgentGoalKind.Fish)
                    continue;
                var point = ResidentMapPosition(resident.Id, resident.X, resident.Y);
                var screen = ToScreen(point.X, point.Y);
                if (!_residentMarkers.Add(((int)Math.Floor(screen.X / 6), (int)Math.Floor(screen.Y / 6))) &&
                    resident.Id != SelectedResidentId)
                    continue;
                var pen = resident.Activity is ResidentActivity.Working or ResidentActivity.Studying ? WorkingHighlight
                    : resident.Activity == ResidentActivity.Resting ? RestingHighlight : TravelHighlight;
                context.DrawRectangle(null, pen, new Rect(point.X - 3, point.Y - 3, 6, 6));
                if (Overlay == 10 && (Detail.ActivityBadges || resident.Id == SelectedResidentId))
                {
                    context.DrawImage(ActivityPreview(Engine!.GetResidentTaskIcon(resident)),
                        new Rect(point.X + 3, point.Y - 4, 3, 3));
                }
            }

            return;
        }

        // 全图用分块位图减少逐格命令；放大后可见格数很少，矩形填充也适合软件回退。
        if (Detail.TileSize >= TilePixels)
        {
            var viewport = VisibleTiles(state, 0);
            for (var y = viewport.Top; y <= viewport.Bottom; y++)
                for (var x = viewport.Left; x <= viewport.Right; x++)
                {
                    var color = TileHighlightColor(state.Tiles[y * state.Width + x]);
                    if (color == 0)
                        continue;
                    if (!_tileHighlightBrushes.TryGetValue(color, out var brush))
                        _tileHighlightBrushes[color] = brush = Brush(color);
                    context.DrawRectangle(brush, null, new Rect(x * TilePixels, y * TilePixels,
                        TilePixels, TilePixels));
                }
            return;
        }

        foreach (var chunk in _chunks.Values)
        {
            if (!Visible(chunk.Bounds))
                continue;
            if (chunk.HighlightTileRevision != _tileRevision || chunk.HighlightOverlay != Overlay)
                UpdateTileHighlight(chunk, state);
            if (chunk.Highlight is not null)
            {
                var viewport = new Rect(-_origin.X / _zoom, -_origin.Y / _zoom,
                    Bounds.Width / _zoom, Bounds.Height / _zoom);
                var target = chunk.Bounds.Intersect(viewport);
                var source = new Rect((target.X - chunk.Bounds.X) / TilePixels,
                    (target.Y - chunk.Bounds.Y) / TilePixels, target.Width / TilePixels, target.Height / TilePixels);
                context.DrawImage(chunk.Highlight, source, target);
            }
        }
    }

    private void UpdateTileHighlight(MapChunk chunk, WorldState state)
    {
        var first = chunk.HighlightCanvas is null;
        var canvas = chunk.HighlightCanvas ??= new PixelCanvas((int)chunk.Bounds.Width / TilePixels,
            (int)chunk.Bounds.Height / TilePixels);
        var changed = first;
        var left = (int)chunk.Bounds.X / TilePixels;
        var top = (int)chunk.Bounds.Y / TilePixels;
        for (var y = 0; y < canvas.Height; y++)
            for (var x = 0; x < canvas.Width; x++)
            {
                var tile = state.Tiles[(top + y) * state.Width + left + x];
                var argb = TileHighlightColor(tile);
                var rgba = (argb << 8) | (argb >> 24);
                var offset = (y * canvas.Width + x) * 4;
                if (canvas.Pixels[offset] == (byte)(rgba >> 24) &&
                    canvas.Pixels[offset + 1] == (byte)(rgba >> 16) &&
                    canvas.Pixels[offset + 2] == (byte)(rgba >> 8) && canvas.Pixels[offset + 3] == (byte)rgba)
                    continue;
                canvas.Pixel(x, y, rgba);
                changed = true;
            }
        if (changed)
        {
            chunk.Highlight?.Dispose();
            chunk.Highlight = MakeBitmap(canvas, false);
        }
        chunk.HighlightTileRevision = _tileRevision;
        chunk.HighlightOverlay = Overlay;
    }

    private uint TileHighlightColor(Tile tile)
    {
        return Overlay switch
        {
            5 => tile.ClaimedSettlementId == 0 ? 0u :
                TownHighlightColors[tile.ClaimedSettlementId % TownHighlightColors.Length],
            6 => !WorldEngine.IsWaterSource(tile) ? 0u :
                ((uint)(40 + (int)(Math.Clamp(WorldEngine.DailyWaterYield(tile) / .1, 0, 1) * 140)) << 24) |
                0x52BDEBu,
            7 => ((uint)(40 + tile.Fertility * 1.4) << 24) | 0x89D773u,
            _ => tile.FireTicks > 0 ? 0xBBF07858u : tile.DroughtTicks > 0 ? 0xAADEB65Cu : 0u,
        };
    }
}
