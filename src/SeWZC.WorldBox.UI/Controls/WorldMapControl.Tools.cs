using Avalonia;
using Avalonia.Media;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI.Controls;

public sealed partial class WorldMapControl
{
    private (int X, int Y)? _pendingPlacement;
    private string _placementMessage = "";
    public BridgeDirection ConstructionBridgeDirection { get; set; }
    public int ConstructionBridgeLevel { get; set; } = 1;
    public bool GiftBuildings { get; set; } = true;
    public int SpawnCount { get; set; } = 12;
    public int DisasterRadius { get; set; } = 2;
    public bool PickingLocation { get; set; }
    public bool HasPendingPlacement => _pendingPlacement.HasValue;
    public int Overlay { get; set; }
    private bool _relayOverlayDirty = true;
    private readonly System.Collections.Generic.List<(Point From, Point To)> _relayOverlay = [];
    private static readonly Pen CargoOverlayPen = new(CargoBrush, 1.5);
    private static readonly Pen RelayOverlayPen = new(MessageBrush, 1);
    public event Action<string>? PlacementChanged;

    public void CancelPlacement()
    {
        _pendingPlacement = null; SetPlacementMessage(""); InvalidateVisual();
    }

    public void ConfirmPlacement()
    {
        if (_pendingPlacement is not { } tile) return;
        if (PlacementError(tile.X, tile.Y) is { } error) { SetPlacementMessage("无法放置：" + error); return; }
        _lastPaint = null;
        ApplyTool(GetTileScreenPosition(tile.X, tile.Y));
        CancelPlacement();
    }

    private void SetPlacementMessage(string text)
    {
        if (_placementMessage == text) return;
        _placementMessage = text; PlacementChanged?.Invoke(text);
    }

    private string? PlacementError(int x, int y)
    {
        if (Engine is null) return "世界尚未就绪";
        if (x < 0 || y < 0 || x >= Engine.State.Width || y >= Engine.State.Height) return "请选择世界范围内的地点";
        if (ActiveTool.StartsWith("build:") && Enum.TryParse<BuildingKind>(ActiveTool[6..], out var kind))
            return Engine.FacilityPlacementError(SelectedSettlementId, kind, x, y, GiftBuildings, kind == BuildingKind.Bridge ? ConstructionBridgeDirection : null, kind == BuildingKind.Bridge ? ConstructionBridgeLevel : 1);
        if (ActiveTool.StartsWith("road:")) return Engine.RoadPlacementError(SelectedSettlementId, x, y, 0);
        var tile = Engine.State.Tiles[y * Engine.State.Width + x];
        if (Enum.TryParse<RaceKind>(ActiveTool, out _) && !tile.IsWalkable) return "居民需要可通行的陆地";
        if (Enum.TryParse<DisasterKind>(ActiveTool, out var disaster))
        {
            if (!tile.IsWalkable) return "请选择陆地上的灾害落点";
            if (disaster == DisasterKind.Plague && !Engine.State.Residents.Any(r => Math.Abs(r.X - x) + Math.Abs(r.Y - y) <= DisasterRadius)) return "作用范围内没有居民";
        }
        return null;
    }

    private void PreviewPlacement(Point point, bool pending = false)
    {
        if (IsNavigationTool || !TryTile(point, out var tile)) return;
        _hover = point;
        if (pending) _pendingPlacement = tile;
        var error = PlacementError(tile.X, tile.Y);
        var detail = ActiveTool.StartsWith("build:") ? GiftBuildings ? "建造方式：直接赐予\n无材料消耗，效果仍受建筑运营条件限制" : "建造方式：居民施工\n扣除当地材料后开工"
            : ActiveTool.StartsWith("road:") ? "修建道路\n每格木材：0.5\n每格石材：1"
            : Enum.TryParse<RaceKind>(ActiveTool, out _) ? $"投放 {SpawnCount} 位居民"
            : Enum.TryParse<DisasterKind>(ActiveTool, out _) ? $"单次释放\n范围：{DisasterRadius} 格" : $"绘制范围：{BrushRadius} 格";
        if (ActiveTool == "build:Bridge") detail += $"\n方向：{WorldEngine.BridgeDirectionName(ConstructionBridgeDirection)}\n等级 {ConstructionBridgeLevel}   离自然岸最多 {WorldEngine.BridgeShoreLimit(ConstructionBridgeLevel)} 格\n材料：木材 {WorldEngine.FacilityCost(BuildingKind.Bridge, ConstructionBridgeLevel).Wood:0.#}   石材 {WorldEngine.FacilityCost(BuildingKind.Bridge, ConstructionBridgeLevel).Stone:0.#}";
        SetPlacementMessage($"位置：{tile.X}, {tile.Y}\n{(error is null ? detail : "无法放置：" + error)}");
        InvalidateVisual();
    }

    private string _drawnPlacementMessage = "";
    private FormattedText? _drawnPlacementText;
    private void DrawPlacementHint(DrawingContext context)
    {
        if (HasPendingPlacement || IsNavigationTool || _hover is not { } hover || _placementMessage.Length == 0) return;
        if (_drawnPlacementText is null || _drawnPlacementMessage != _placementMessage)
        {
            _drawnPlacementMessage = _placementMessage;
            _drawnPlacementText = new FormattedText(_placementMessage, System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, MapTypeface, 11, LabelBrush) { MaxTextWidth = 210 };
        }
        var text = _drawnPlacementText;
        var x = Math.Clamp(hover.X + 16, 4, Math.Max(4, Bounds.Width - text.Width - 12));
        var y = Math.Clamp(hover.Y - text.Height - 18, 4, Math.Max(4, Bounds.Height - text.Height - 12));
        context.DrawRectangle(LabelShadow, null, new Rect(x - 4, y - 3, text.Width + 8, text.Height + 6), 4, 4);
        context.DrawText(text, new Point(x, y));
    }

    private void DrawMapOverlay(DrawingContext context, WorldState state)
    {
        if (Overlay == 0) return;
        if (Overlay == 4) { DrawInfrastructureColors(context, state); return; }
        if (Overlay >= 5) { DrawExtraHighlights(context, state); return; }
        if (Overlay == 1)
        {
            foreach (var town in state.Settlements)
            {
                var low = town.Resources.Food < town.Population;
                var center = new Point((town.X + .5) * TilePixels, (town.Y + .5) * TilePixels);
                context.DrawEllipse(Brush(low ? 0x55F08060u : 0x448CD3A0u), new Pen(Brush(low ? 0xFFF08060u : 0xFF8CD3A0u), 1.5), center, 22, 22);
            }
        }
        else if (Overlay == 2)
        {
            foreach (var resident in state.Residents.Where(r => r.Agent.DestinationSettlementId != 0))
                context.DrawEllipse(null, CargoOverlayPen, ResidentMapPosition(resident.Id, resident.X, resident.Y), 6, 6);
        }
        else if (Overlay == 3)
        {
            if (_relayOverlayDirty)
            {
                _relayOverlay.Clear();
                for (var i = 0; i < state.Settlements.Count; i++)
                    for (var j = i + 1; j < state.Settlements.Count; j++)
                    {
                        var a = state.Settlements[i]; var b = state.Settlements[j];
                        if (Engine!.CanRelayInformation(a.Id, b.Id, out _))
                            _relayOverlay.Add((new Point((a.X + .5) * TilePixels, (a.Y + .5) * TilePixels), new Point((b.X + .5) * TilePixels, (b.Y + .5) * TilePixels)));
                    }
                _relayOverlayDirty = false;
            }
            foreach (var link in _relayOverlay) context.DrawLine(RelayOverlayPen, link.From, link.To);
            foreach (var building in state.Society.Buildings.Where(b => b.Kind == BuildingKind.SignalTower && b.IsCompleted))
            {
                var operational = Engine!.IsBuildingOperational(building);
                context.DrawEllipse(null, new Pen(operational ? MessageBrush : StoneBrush, 1.5), new Point((building.X + .5) * TilePixels, (building.Y + .5) * TilePixels), 7, 7);
            }
        }
    }
}
