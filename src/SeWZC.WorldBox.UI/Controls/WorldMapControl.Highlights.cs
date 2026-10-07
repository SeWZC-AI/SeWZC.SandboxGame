using Avalonia;
using Avalonia.Media;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI.Controls;

public sealed partial class WorldMapControl
{
    private static readonly uint[] TownHighlightColors =
        [0xAA5CAEE8, 0xAA9ADA72, 0xAAE1AE60, 0xAAC293E0, 0xAAE48090, 0xAA62CFBC];

    private static readonly IBrush[] TownHighlights = TownHighlightColors.Select(Brush).ToArray();

    private static readonly IBrush[] WaterHighlights =
        Enumerable.Range(0, 141).Select(i => Brush(((uint)(40 + i) << 24) | 0x52BDEB)).ToArray();

    private static readonly IBrush[] FertilityHighlights =
        Enumerable.Range(0, 101).Select(i => Brush(((uint)(40 + i * 1.4) << 24) | 0x89D773)).ToArray();

    private static readonly IBrush FireHighlight = Brush(0xBBF07858), DroughtHighlight = Brush(0xAADEB65C);

    private static readonly Pen WorkingHighlight = new(Brush(0xFF79D58F)),
        RestingHighlight = new(Brush(0xFFB6A2EF)),
        TravelHighlight = new(Brush(0xFF65C8FA));

    private void DrawExtraHighlights(DrawingContext context, WorldState state)
    {
        if (Overlay is 9 or 10)
        {
            foreach (var resident in VisibleResidents(state))
            {
                if (Overlay == 9 && resident.TravelMode != TravelMode.Boat &&
                    resident.Agent.Goal.Kind != AgentGoalKind.Fish)
                    continue;
                var point = ResidentMapPosition(resident.Id, resident.X, resident.Y);
                var pen = resident.Activity is ResidentActivity.Working or ResidentActivity.Studying ? WorkingHighlight
                    : resident.Activity == ResidentActivity.Resting ? RestingHighlight : TravelHighlight;
                context.DrawRectangle(null, pen, new Rect(point.X - 3, point.Y - 3, 6, 6));
                if (Overlay == 10)
                {
                    context.DrawImage(ActivityPreview(Engine!.GetResidentTaskIcon(resident)),
                        new Rect(point.X + 3, point.Y - 4, 3, 3));
                }
            }

            return;
        }

        var viewport = VisibleTiles(state);
        for (var y = viewport.Top; y <= viewport.Bottom; y++)
        for (var x = viewport.Left; x <= viewport.Right; x++)
        {
            var tile = state.Tiles[y * state.Width + x];
            IBrush brush;
            if (Overlay == 5)
            {
                if (tile.ClaimedSettlementId == 0)
                    continue;
                brush = TownHighlights[tile.ClaimedSettlementId % TownHighlights.Length];
            }
            else if (Overlay == 6)
            {
                if (!WorldEngine.IsWaterSource(tile))
                    continue;
                var abundance = Math.Clamp(WorldEngine.DailyWaterYield(tile) / .1, 0, 1);
                brush = WaterHighlights[(int)(abundance * 140)];
            }
            else if (Overlay == 7)
                brush = FertilityHighlights[tile.Fertility];
            else
            {
                if (tile.FireTicks == 0 && tile.DroughtTicks == 0)
                    continue;
                brush = tile.FireTicks > 0 ? FireHighlight : DroughtHighlight;
            }

            context.DrawRectangle(brush, null, new Rect(x * TilePixels, y * TilePixels, TilePixels, TilePixels));
        }
    }
}
