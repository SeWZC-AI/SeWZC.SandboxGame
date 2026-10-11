using Avalonia;
using Avalonia.Media;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;
using SeWZC.WorldBox.UI.ViewModels;
namespace SeWZC.WorldBox.UI.Tests;
/// <summary>界面流程测试使用的地图状态与事件端口。</summary>
internal sealed class GameMapStub : IGameMap
{
    public int InfrastructureTownId { get; set; }
    public int Overlay { get; set; }
    public int SelectedSettlementId { get; set; }
    public int SelectedNationId { get; set; }
    public BuildingKind? InfrastructureKind { get; set; }
    public bool HighlightBuildings { get; set; }
    public bool HighlightRoads { get; set; }
    public bool FollowSelectedResident { get; set; }
    public bool ShowResidentRoute { get; set; }
    public bool ShowBorders { get; set; }
    public bool ShowWildlife { get; set; }
    public bool ShowPlants { get; set; }
    public bool ShowBuildingNames { get; set; }
    public ResourceVisibility ResourceVisibility { get; set; }
    public MapTool ActiveTool { get; set; } = MapTool.Pan;
    public HashSet<ResourceKind> VisibleResources { get; set; } = [];
    public void RefreshWorld(bool resetCamera = false, bool deferAnimation = false) { }
    public void InvalidateVisual() { }
    public void SelectResident(int id, bool follow = false) { SelectedResidentId = id; FollowSelectedResident = follow; }
    public IImage ActivityPreview(ResidentTaskIcon kind) => new DrawingImage();
    public IImage ResidentPreview(RaceKind race, Profession profession) => new DrawingImage();
    public IImage AnimalPreview(WildlifeKind kind) => new DrawingImage();
    public IImage PlantPreview(PlantKind kind) => new DrawingImage();
    public WorldEngine? Engine { get; set; }
    public bool IsSimulationPaused { get; set; }
    public bool PickingLocation { get; set; }
    public bool GiftBuildings { get; set; }
    public double SimulationTickDurationSeconds { get; set; }
    public double SimulationTickFraction { get; set; }
    public double ScaleBottomInset { get; set; }
    public BridgeDirection ConstructionBridgeDirection { get; set; }
    public int ConstructionBridgeLevel { get; set; }
    public int? SelectedResidentId { get; set; }
    public bool HasPendingPlacement { get; set; }
    public Func<Task>? PrepareWorldEdit { get; set; }
    public event EventHandler? WorldMutationStarting { add { } remove { } }
    public event EventHandler? WorldEdited { add { } remove { } }
    public event Action<int, int>? TileSelected;
    internal void PickTile(int x, int y) => TileSelected?.Invoke(x, y);
    public event Action<int>? ResidentSelected { add { } remove { } }
    public event Action<int>? BuildingSelected { add { } remove { } }
    public event Action<string>? ToolError { add { } remove { } }
    public event Action<string>? PlacementChanged { add { } remove { } }
    internal WorldMapControl.ViewportState Viewport { get; set; } = new(new Point(128, 128), 1);
    private int? _buildingId;
    private (int X, int Y)? _tile;
    internal (ToolCategory Category, int Index)? Brush { get; private set; }
    public WorldMapControl.ViewportState CaptureViewport() => Viewport;
    public WorldMapControl.MapSelectionState CaptureMapSelection() => new(SelectedResidentId, _buildingId, _tile, FollowSelectedResident, SelectedNationId, SelectedSettlementId);
    public void RestoreViewport(WorldMapControl.ViewportState viewport) { Viewport = viewport; }
    public void RestoreMapSelection(WorldMapControl.MapSelectionState selection)
    {
        SelectedResidentId = selection.ResidentId; _buildingId = selection.BuildingId; _tile = selection.Tile;
        FollowSelectedResident = selection.Follow; SelectedNationId = selection.NationId; SelectedSettlementId = selection.SettlementId;
    }
    public void RestoreObservationSelection(WorldMapControl.MapSelectionState selection) => RestoreMapSelection(selection);
    public void SelectMapTile(int x, int y) { ClearMapSelection(); _tile = (x, y); }
    public void SelectMapBuilding(int id) { ClearMapSelection(); _buildingId = id; }
    public void ClearMapSelection() { SelectedResidentId = null; _buildingId = null; _tile = null; FollowSelectedResident = false; }
    public void FocusTile(int x, int y) { Viewport = new(new Point(x, y), 1.4); }
    public bool FocusResident(int id) => false;
    public void ZoomIn() { }
    public void ZoomOut() { }
    public void FitWorld() { }
    public void ConfirmPlacement() { }
    public void CancelPlacement() { }
    public int GetRecentFrameCount(long timestamp) => 0;
    public void ApplyBrush(ToolCategory category, int index) { Brush = (category, index); }
}
