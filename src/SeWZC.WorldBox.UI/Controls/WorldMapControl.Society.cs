using System;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI.Controls;

public sealed partial class WorldMapControl
{
    private static readonly IBrush StoneBrush = Brush(0xFFB7BDAD);
    private static readonly IBrush AcademyBrush = Brush(0xFF86AFC6);
    private static readonly IBrush ArcaneBrush = Brush(0xFFA793D2);
    private static readonly IBrush HealingBrush = Brush(0xFFD6EDBE);
    private static readonly IBrush CargoBrush = Brush(0xFFD6A255);
    private static readonly IBrush MessageBrush = Brush(0xFFFFF0C4);
    private static readonly IBrush ProgressBrush = Brush(0xFFB6D59F);
    private StreamGeometry? _cargoGeometry;
    private StreamGeometry? _messageGeometry;
    private StreamGeometry? _magicGeometry;

    private void DrawBuildings(DrawingContext context, WorldState state)
    {
        foreach (var building in state.Society.Buildings)
        {
            var x = (building.X + .5) * TilePixels;
            var y = (building.Y + .5) * TilePixels;
            if (!Visible(new Rect(x - 9, y - 16, 18, 22))) continue;
            var town = state.Settlements.FirstOrDefault(s => s.Id == building.SettlementId);
            var roof = town is null ? RoofBrush : NationBrush(town.NationId);
            void Box(IBrush brush, double dx, double dy, double width, double height) =>
                context.DrawRectangle(brush, null, new Rect(x + dx, y + dy, width, height));
            Box(ShadowBrush, -4, 2, 10, 3);
            if (!building.IsCompleted)
            {
                Box(StoneBrush, -4, -2, 8, 5);
                Box(WoodBrush, -4, -8, 1, 10);
                Box(WoodBrush, 3, -8, 1, 10);
                Box(WoodBrush, -4, -7, 8, 1);
                Box(WoodBrush, -4, -3, 8, 1);
                var progress = Math.Clamp(building.ConstructionProgress / (double)Math.Max(1, building.ConstructionRequired), 0, 1);
                Box(WoodBrush, -4, 5, 8, 1.5);
                Box(ProgressBrush, -4, 5, 8 * progress, 1.5);
                continue;
            }
            switch (building.Kind)
            {
                case BuildingKind.Farm:
                    Box(WoodBrush, -4, -4, 8, 8);
                    Box(FarmBrush, -3, -3, 6, 6);
                    for (var row = 0; row < 3; row++) Box(ProgressBrush, -3, -3 + row * 2.5, 6, 1);
                    break;
                case BuildingKind.Workshop:
                    DrawHouse(context, x - 3, y - 3, RoofBrush, 1);
                    Box(StoneBrush, 2, -9, 2, 6);
                    Box(WoodBrush, -1, 1, 4, 2);
                    break;
                case BuildingKind.Academy:
                    Box(StoneBrush, -4, -4, 8, 8);
                    Box(AcademyBrush, -5, -6, 10, 2);
                    Box(AcademyBrush, -3, -8, 6, 2);
                    Box(WallBrush, -3, -2, 1, 5);
                    Box(WallBrush, 2, -2, 1, 5);
                    Box(WoodBrush, -1, 0, 2, 4);
                    break;
                case BuildingKind.Waystation:
                    DrawHouse(context, x - 3, y - 3, roof, 1);
                    Box(WoodBrush, 5, -6, 1, 9);
                    Box(MessageBrush, 4, -6, 4, 2);
                    break;
                case BuildingKind.SignalTower:
                    Box(StoneBrush, -2, -11, 4, 15);
                    Box(WoodBrush, -4, -11, 8, 2);
                    Box(roof, -3, -13, 6, 2);
                    Box(WoodBrush, 0, -18, 1, 6);
                    Box(MessageBrush, 1, -18, 4, 3);
                    break;
                case BuildingKind.ArcaneSanctum:
                    Box(StoneBrush, -4, 0, 8, 4);
                    Box(ArcaneBrush, -2, -7, 4, 8);
                    Box(ArcaneBrush, -1, -10, 2, 3);
                    Box(MessageBrush, -1, -6, 1, 5);
                    break;
                case BuildingKind.Infirmary:
                    DrawHouse(context, x - 3, y - 3, HealingBrush, 1);
                    Box(HealingBrush, -1, -1, 3, 1);
                    Box(HealingBrush, 0, -2, 1, 3);
                    break;
            }
        }
    }

    private void DrawResidentGoal(DrawingContext context)
    {
        if (SelectedResidentId is not { } id || Engine is null) return;
        var resident = Engine.State.Residents.FirstOrDefault(r => r.Id == id);
        if (resident is null || resident.Agent.Goal.Kind == AgentGoalKind.Idle) return;
        var goal = resident.Agent.Goal;
        if (goal.TargetX < 0 || goal.TargetY < 0 || goal.TargetX >= Engine.State.Width || goal.TargetY >= Engine.State.Height) return;
        var point = GetTileScreenPosition(goal.TargetX, goal.TargetY);
        // A destination marker is honest about the actual goal. It deliberately does not
        // invent a straight route through impassable terrain when the path is not exposed.
        var size = Math.Max(5, _zoom * TilePixels * .65);
        var pen = new Pen(Brush(0xB9B9DDC4), 1, dashStyle: DashStyle.Dash);
        context.DrawRectangle(null, pen, new Rect(point.X - size, point.Y - size, size * 2, size * 2), 2, 2);
    }

    private bool TryApplyConstructionTool(string tool, (int X, int Y) tile, out bool edited)
    {
        edited = false;
        var isRoad = tool.Equals("Road", StringComparison.OrdinalIgnoreCase);
        var isBuilding = ActiveTool.StartsWith("build:", StringComparison.OrdinalIgnoreCase) &&
                         Enum.TryParse<BuildingKind>(tool, true, out _);
        if (!isRoad && !isBuilding) return false;
        if (Engine is null || !Engine.State.Settlements.Any(town => town.Id == SelectedSettlementId))
        {
            if (_lastPaint is null) ToolError?.Invoke("请先选择负责建设的聚落。");
            _lastPaint = tile;
            return true;
        }
        if (!isRoad && _lastPaint is not null) return true;
        try
        {
            if (_lastPaint is null) WorldEditing?.Invoke(this, EventArgs.Empty);
            if (isRoad)
                Stroke(tile, (x, y) => Engine.BuildRoad(SelectedSettlementId, x, y, Math.Clamp(BrushRadius, 0, 4)));
            else
                Engine.BuildFacility(SelectedSettlementId, Enum.Parse<BuildingKind>(tool, true), tile.X, tile.Y);
            edited = true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            // A road stroke may contain completed segments before a later tile is rejected.
            // Keep those real edits visible and report the concrete simulation constraint.
            edited = isRoad;
            ToolError?.Invoke(exception.Message);
        }
        _lastPaint = tile;
        return true;
    }
}
