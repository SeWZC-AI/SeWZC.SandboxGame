using Avalonia;
using Avalonia.Media;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;

namespace SeWZC.WorldBox.UI.ViewModels;

/// <summary>设计器使用的无绘图地图端口，不启动模拟或存储操作。</summary>
internal sealed class DesignMap : IGameMap
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

    public void SelectResident(int id, bool follow = false)
    {
        SelectedResidentId = id;
        FollowSelectedResident = follow;
    }

    public IImage ActivityPreview(ResidentTaskIcon kind)
    {
        return new DrawingImage();
    }

    public IImage ResidentPreview(RaceKind race, Profession profession)
    {
        return new DrawingImage();
    }

    public IImage AnimalPreview(WildlifeKind kind)
    {
        return new DrawingImage();
    }

    public IImage PlantPreview(PlantKind kind)
    {
        return new DrawingImage();
    }

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

    public event EventHandler? WorldMutationStarting
    {
        add { }
        remove { }
    }

    public event EventHandler? WorldEdited
    {
        add { }
        remove { }
    }

    public event Action<int, int>? TileSelected
    {
        add { }
        remove { }
    }

    public event Action<int>? ResidentSelected
    {
        add { }
        remove { }
    }

    public event Action<int>? BuildingSelected
    {
        add { }
        remove { }
    }

    public event Action<string>? ToolError
    {
        add { }
        remove { }
    }

    public event Action<string>? PlacementChanged
    {
        add { }
        remove { }
    }

    public WorldMapControl.ViewportState CaptureViewport()
    {
        return new WorldMapControl.ViewportState(new Point(128, 128), 1);
    }

    public WorldMapControl.MapSelectionState CaptureMapSelection()
    {
        return default;
    }

    public void RestoreViewport(WorldMapControl.ViewportState viewport) { }
    public void RestoreMapSelection(WorldMapControl.MapSelectionState selection) { }
    public void RestoreObservationSelection(WorldMapControl.MapSelectionState selection) { }
    public void SelectMapTile(int x, int y) { }
    public void SelectMapBuilding(int id) { }
    public void ClearMapSelection() { }
    public void FocusTile(int x, int y) { }

    public bool FocusResident(int id)
    {
        return true;
    }

    public void ZoomIn() { }
    public void ZoomOut() { }
    public void FitWorld() { }
    public void ConfirmPlacement() { }
    public void CancelPlacement() { }

    public int GetRecentFrameCount(long timestamp)
    {
        return 0;
    }

    public void ApplyBrush(ToolCategory category, int index) { }
}
