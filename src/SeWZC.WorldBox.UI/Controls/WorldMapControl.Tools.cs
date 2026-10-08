using System.Globalization;
using Avalonia;
using Avalonia.Media;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI.Controls;

public sealed partial class WorldMapControl
{
    private static readonly Pen CargoOverlayPen = new(CargoBrush, 1.5);
    private static readonly Pen RelayOverlayPen = new(MessageBrush);
    private readonly List<(Point From, Point To)> _relayOverlay = [];

    private string _drawnPlacementMessage = "";
    private FormattedText? _drawnPlacementText;
    private (int X, int Y)? _pendingPlacement;
    private string _placementMessage = "";
    private bool _relayOverlayDirty = true;

    /// <summary>建设工具选定的桥梁通行轴向。</summary>
    public BridgeDirection ConstructionBridgeDirection { get; set; }

    /// <summary>建设工具选定的桥梁等级。</summary>
    public int ConstructionBridgeLevel { get; set; } = 1;

    /// <summary>是否直接赐予完工建筑，关闭时按材料和施工规则建造。</summary>
    public bool GiftBuildings { get; set; } = true;

    /// <summary>每次投放工具创建的居民数量。</summary>
    public int SpawnCount { get; set; } = 12;

    /// <summary>灾害工具的作用半径，以地格为单位。</summary>
    public int DisasterRadius { get; set; } = 2;

    /// <summary>是否正在为表单选取地图地点。</summary>
    public bool PickingLocation { get; set; }

    /// <summary>是否存在等待触屏确认的放置地点。</summary>
    public bool HasPendingPlacement => _pendingPlacement.HasValue;

    /// <summary>当前地图高亮图层编号，0 表示关闭。</summary>
    public int Overlay { get; set; }

    /// <summary>待确认放置或鼠标预览说明变化时发出的通知。</summary>
    public event Action<string>? PlacementChanged;

    /// <summary>清除等待确认的放置地点和预览说明。</summary>
    public void CancelPlacement()
    {
        _pendingPlacement = null;
        SetPlacementMessage("");
        InvalidateVisual();
    }

    /// <summary>校验待确认地点，提交当前工具操作并清除放置预览。</summary>
    public void ConfirmPlacement()
    {
        if (_pendingPlacement is not { } tile)
            return;
        if (PlacementError(tile.X, tile.Y) is { } error)
        {
            SetPlacementMessage("无法放置：" + error);
            return;
        }

        _lastPaint = null;
        ApplyTool(GetTileScreenPosition(tile.X, tile.Y));
        CancelPlacement();
    }

    private void SetPlacementMessage(string text)
    {
        if (_placementMessage == text)
            return;
        _placementMessage = text;
        PlacementChanged?.Invoke(text);
    }

    private string? PlacementError(int x, int y)
    {
        if (Engine is null)
            return "世界尚未就绪";
        if (x < 0 || y < 0 || x >= Engine.State.Width || y >= Engine.State.Height)
            return "请选择世界范围内的地点";
        return ActiveTool.PlacementError(this, x, y);
    }

    private void PreviewPlacement(Point point, bool pending = false)
    {
        if (IsNavigationTool || !TryTile(point, out var tile))
            return;
        _hover = point;
        if (pending)
            _pendingPlacement = tile;
        var error = PlacementError(tile.X, tile.Y);
        var detail = ActiveTool.Describe(this);
        SetPlacementMessage(error is null ? detail : "无法放置：" + error);
        InvalidateVisual();
    }

    private void DrawPlacementHint(DrawingContext context)
    {
        if (HasPendingPlacement || IsNavigationTool || _hover is not { } hover || _placementMessage.Length == 0)
            return;
        if (_drawnPlacementText is null || _drawnPlacementMessage != _placementMessage)
        {
            _drawnPlacementMessage = _placementMessage;
            _drawnPlacementText = new FormattedText(_placementMessage, CultureInfo.CurrentCulture,
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
        if (Overlay == 0)
            return;
        if (Overlay == 4)
        {
            DrawInfrastructureColors(context, state);
            return;
        }

        if (Overlay >= 5)
        {
            DrawExtraHighlights(context, state);
            return;
        }

        if (Overlay == 1)
        {
            foreach (var town in state.Settlements)
            {
                var low = town.Resources.Food < town.Population;
                var center = new Point((town.X + .5) * TilePixels, (town.Y + .5) * TilePixels);
                context.DrawEllipse(Brush(low ? 0x55F08060u : 0x448CD3A0u),
                    new Pen(Brush(low ? 0xFFF08060u : 0xFF8CD3A0u), 1.5), center, 22, 22);
            }
        }
        else if (Overlay == 2)
        {
            _residentMarkers.Clear();
            foreach (var resident in VisibleResidents(state))
            {
                if (resident.Agent.DestinationSettlementId == 0)
                    continue;
                var point = ResidentMapPosition(resident.Id, resident.X, resident.Y);
                var screen = ToScreen(point.X, point.Y);
                if (_residentMarkers.Add(((int)Math.Floor(screen.X / 6), (int)Math.Floor(screen.Y / 6))) ||
                    resident.Id == SelectedResidentId)
                    context.DrawEllipse(null, CargoOverlayPen, point, 6, 6);
            }
        }
        else if (Overlay == 3)
        {
            if (_relayOverlayDirty)
            {
                _relayOverlay.Clear();
                for (var i = 0; i < state.Settlements.Count; i++)
                for (var j = i + 1; j < state.Settlements.Count; j++)
                {
                    var a = state.Settlements[i];
                    var b = state.Settlements[j];
                    if (Engine!.CanRelayInformation(a.Id, b.Id, out _))
                    {
                        _relayOverlay.Add((new Point((a.X + .5) * TilePixels, (a.Y + .5) * TilePixels),
                            new Point((b.X + .5) * TilePixels, (b.Y + .5) * TilePixels)));
                    }
                }

                _relayOverlayDirty = false;
            }

            foreach (var link in _relayOverlay)
                context.DrawLine(RelayOverlayPen, link.From, link.To);
            foreach (var building in state.Society.Buildings.Where(b =>
                         b.Kind == BuildingKind.SignalTower && b.IsCompleted))
            {
                var operational = Engine!.IsBuildingOperational(building);
                context.DrawEllipse(null, new Pen(operational ? MessageBrush : StoneBrush, 1.5),
                    new Point((building.X + .5) * TilePixels, (building.Y + .5) * TilePixels), 7, 7);
            }
        }
    }
}
