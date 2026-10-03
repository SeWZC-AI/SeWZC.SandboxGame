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
            DrawBuilding(context, building);
    }

    private static double BuildingHeight(BuildingKind kind) => kind switch
    {
        BuildingKind.SignalTower => 16, BuildingKind.TownCenter or BuildingKind.PowerPlant => 13,
        BuildingKind.Academy or BuildingKind.Foundry or BuildingKind.AetherForge => 12,
        BuildingKind.Farm or BuildingKind.AutomatedFarm => 6.4,
        BuildingKind.Bridge or BuildingKind.MountainPass => 4, _ => 9.6
    };

    private static Rect BuildingBounds(Building building)
    {
        var height = building.IsCompleted ? BuildingHeight(building.Kind) : 8;
        return new Rect(building.X * TilePixels, (building.Y + .5) * TilePixels + 2 - height, 8, height);
    }

    private void DrawBuilding(DrawingContext context, Building building)
    {
        var x = (building.X + .5) * TilePixels; var y = (building.Y + .5) * TilePixels;
        var bounds = BuildingBounds(building);
        if (!Visible(bounds)) return;
        var race = _settlementStyles.GetValueOrDefault(building.SettlementId);
        if (!building.IsCompleted)
        {
            context.DrawRectangle(StoneBrush, null, new Rect(x - 3, y - 1, 6, 3));
            context.DrawLine(new Pen(WoodBrush, .4), new(x - 3, y + 2), new(x - 3, y - 6));
            context.DrawLine(new Pen(WoodBrush, .4), new(x + 3, y + 2), new(x + 3, y - 6));
            context.DrawLine(new Pen(WoodBrush, .4), new(x - 3, y - 5), new(x + 3, y - 5));
        }
        else
        {
            using var opacity = context.PushOpacity(building.Enabled && building.Health > 0 ? 1 : .55);
            context.DrawImage(BuildingIcon(race, building.Kind), bounds);
        }
        if (_zoom >= 3 && (!building.IsCompleted || building.Health < 100))
        {
            var fraction = building.IsCompleted ? building.Health / 100 : building.ConstructionProgress / Math.Max(1, building.ConstructionRequired);
            context.DrawRectangle(WoodBrush, null, new Rect(x - 3, y + 2, 6, .45));
            context.DrawRectangle(building.IsCompleted && building.Health < 50 ? FlameOuter : ProgressBrush, null, new Rect(x - 3, y + 2, 6 * Math.Clamp(fraction, 0, 1), .45));
            if (building.IsCompleted && building.Health < 50) context.DrawLine(new Pen(WoodBrush, .25), new(x - 1, y - 3), new(x + 1, y + 1));
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
        if (ShowResidentRoute && _selectedRoute.Count > 1)
        {
            var routePen = new Pen(HealingBrush, 1.8, dashStyle: DashStyle.Dash);
            var previous = TryGetResidentScreenPosition(id, out var drawn) ? drawn : GetTileScreenPosition(resident.X, resident.Y);
            foreach (var step in _selectedRoute.Skip(1))
            {
                var next = GetTileScreenPosition(step.X, step.Y);
                context.DrawLine(routePen, previous, next);
                RenderedRouteSegmentCount++;
                context.DrawEllipse(HealingBrush, null, next, 2, 2);
                previous = next;
            }
        }
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
                Stroke(tile, (x, y) => Engine.BuildRoad(SelectedSettlementId, x, y, 0));
            else
            {
                if (GiftBuildings) Engine.GrantFacility(SelectedSettlementId, Enum.Parse<BuildingKind>(tool, true), tile.X, tile.Y);
                else Engine.BuildFacility(SelectedSettlementId, Enum.Parse<BuildingKind>(tool, true), tile.X, tile.Y);
            }
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
