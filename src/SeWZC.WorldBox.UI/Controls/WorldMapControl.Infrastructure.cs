using Avalonia;
using Avalonia.Media;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI.Controls;

public sealed partial class WorldMapControl
{
    private static readonly (string Label, uint Color)[] InfrastructureLegend =
    [ ("农业", 0xFF83D67B), ("交通", 0xFF65C8FA), ("知识", 0xFFB99AFE), ("工业", 0xFFF6A86A),
      ("公共", 0xFF64DFCB), ("中心", 0xFFF5EFCD), ("道路", 0xFFEAD45F), ("施工", 0xFFFFC45E),
      ("受损", 0xFFF07878), ("停用", 0xFF949EA7) ];
    private FormattedText[]? _infrastructureLegendText;
    private static readonly (int X, int Y)[] InfrastructureDirections = [(1, 0), (0, 1)];
    public bool HighlightBuildings { get; set; } = true;
    public bool HighlightRoads { get; set; } = true;
    public int InfrastructureTownId { get; set; }
    public BuildingKind? InfrastructureKind { get; set; }
    public static uint InfrastructureColor(BuildingKind kind) => kind switch
    {
        BuildingKind.TownCenter => 0xFFF5EFCD,
        BuildingKind.Farm or BuildingKind.AutomatedFarm or BuildingKind.RunicGarden => 0xFF83D67B,
        BuildingKind.Dock or BuildingKind.Shipyard or BuildingKind.Bridge or BuildingKind.MountainPass or BuildingKind.Waystation or BuildingKind.Airfield => 0xFF65C8FA,
        BuildingKind.Academy or BuildingKind.SignalTower or BuildingKind.ArcaneSanctum or BuildingKind.Library or BuildingKind.SurveyOffice or BuildingKind.WardTower or BuildingKind.StormSpire or BuildingKind.Waygate or BuildingKind.AlchemyLab => 0xFFB99AFE,
        BuildingKind.Housing or BuildingKind.Granary or BuildingKind.Well or BuildingKind.Market or BuildingKind.Infirmary or BuildingKind.Watchtower or BuildingKind.Reservoir or BuildingKind.Hospital or BuildingKind.FireStation or BuildingKind.GroveSanctuary => 0xFF64DFCB,
        _ => 0xFFF6A86A
    };

    private void DrawInfrastructureLegend(DrawingContext context)
    {
        if (Overlay != 4) return;
        _infrastructureLegendText ??= InfrastructureLegend.Select(item => new FormattedText(item.Label,
            System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, MapTypeface, 11, LabelBrush)).ToArray();
        var columns = Math.Clamp((int)(Bounds.Width - 72) / 52, 1, 6);
        var rows = (InfrastructureLegend.Length + columns - 1) / columns;
        context.DrawRectangle(LabelShadow, null, new Rect(8, 8, columns * 52 + 8, rows * 19 + 8), 4, 4);
        for (var i = 0; i < InfrastructureLegend.Length; i++)
        {
            var x = 13 + i % columns * 52; var y = 12 + i / columns * 19;
            context.DrawRectangle(Brush(InfrastructureLegend[i].Color), null, new Rect(x, y + 4, 8, 8));
            context.DrawText(_infrastructureLegendText[i], new Point(x + 12, y));
        }
    }

    private void DrawInfrastructureColors(DrawingContext context, WorldState state)
    {
        var viewport = VisibleTiles(state); var roadBrush = Brush(0xFFEAD45F);
        var roadPen = new Pen(roadBrush, 1.5);
        if (HighlightRoads)
        for (var y = viewport.Top; y <= viewport.Bottom; y++)
        for (var x = viewport.Left; x <= viewport.Right; x++)
        {
            var tile = state.Tiles[y * state.Width + x];
            if (tile.RoadLevel == 0 || InfrastructureTownId != 0 && tile.ClaimedSettlementId != InfrastructureTownId) continue;
            var rect = new Rect(x * TilePixels, y * TilePixels, TilePixels, TilePixels);
            context.DrawRectangle(Brush(0x88EAD45F), null, rect);
            var center = rect.Center;
            context.DrawEllipse(roadBrush, null, center, 1.3, 1.3);
            foreach (var direction in InfrastructureDirections)
            {
                var xx = x + direction.X; var yy = y + direction.Y;
                if (xx >= state.Width || yy >= state.Height || state.Tiles[yy * state.Width + xx].RoadLevel == 0
                    || InfrastructureTownId != 0 && state.Tiles[yy * state.Width + xx].ClaimedSettlementId != InfrastructureTownId) continue;
                if (!Engine!.CanTraverseStep(x, y, xx, yy, TravelMode.Foot)) continue;
                context.DrawLine(roadPen, center, new Point(center.X + direction.X * TilePixels, center.Y + direction.Y * TilePixels));
            }
        }
        if (!HighlightBuildings) return;
        foreach (var building in state.Society.Buildings)
        {
            if (InfrastructureTownId != 0 && building.SettlementId != InfrastructureTownId
                || InfrastructureKind.HasValue && building.Kind != InfrastructureKind.Value) continue;
            var rect = new Rect(building.X * TilePixels, building.Y * TilePixels, TilePixels, TilePixels);
            if (!Visible(rect)) continue;
            var color = building.Health < 50 ? 0xFFF07878u : !building.Enabled ? 0xFF949EA7u
                : !building.IsCompleted || building.IsUpgrading ? 0xFFFFC45Eu : InfrastructureColor(building.Kind);
            context.DrawRectangle(Brush((color & 0xFFFFFF) | 0xBB000000), new Pen(Brush(color), 1.2), rect.Deflate(.5));
            if (building.Kind == BuildingKind.Bridge)
                context.DrawLine(new Pen(Brush(0xFF15333F), 1), building.Direction == BridgeDirection.Horizontal ? new Point(rect.Left + 1, rect.Center.Y) : new Point(rect.Center.X, rect.Top + 1),
                    building.Direction == BridgeDirection.Horizontal ? new Point(rect.Right - 1, rect.Center.Y) : new Point(rect.Center.X, rect.Bottom - 1));
        }
    }
}
