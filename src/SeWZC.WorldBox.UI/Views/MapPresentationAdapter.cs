using Avalonia.Media;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;
using SeWZC.WorldBox.UI.ViewModels;
namespace SeWZC.WorldBox.UI.Views;

/// <summary>将地图显示请求接入实际绘图控件。</summary>
internal sealed class MapPresentationAdapter(WorldMapControl map) : IGameMap
{
    public int InfrastructureTownId { get => map.InfrastructureTownId; set => map.InfrastructureTownId = value; }
    public int Overlay { get => map.Overlay; set => map.Overlay = value; }
    public int SelectedSettlementId { get => map.SelectedSettlementId; set => map.SelectedSettlementId = value; }
    public int SelectedNationId { get => map.SelectedNationId; set => map.SelectedNationId = value; }
    public BuildingKind? InfrastructureKind { get => map.InfrastructureKind; set => map.InfrastructureKind = value; }
    public bool HighlightBuildings { get => map.HighlightBuildings; set => map.HighlightBuildings = value; }
    public bool HighlightRoads { get => map.HighlightRoads; set => map.HighlightRoads = value; }
    public bool FollowSelectedResident { get => map.FollowSelectedResident; set => map.FollowSelectedResident = value; }
    public bool ShowResidentRoute { get => map.ShowResidentRoute; set => map.ShowResidentRoute = value; }
    public bool ShowBorders { get => map.ShowBorders; set => map.ShowBorders = value; }
    public bool ShowWildlife { get => map.ShowWildlife; set => map.ShowWildlife = value; }
    public bool ShowPlants { get => map.ShowPlants; set => map.ShowPlants = value; }
    public bool ShowBuildingNames { get => map.ShowBuildingNames; set => map.ShowBuildingNames = value; }
    public ResourceVisibility ResourceVisibility { get => map.ResourceVisibility; set => map.ResourceVisibility = value; }
    public MapTool ActiveTool { get => map.ActiveTool; set => map.ActiveTool = value; }
    public HashSet<ResourceKind> VisibleResources => map.VisibleResources;
    public void RefreshWorld(bool resetCamera = false, bool deferAnimation = false) => map.RefreshWorld(resetCamera, deferAnimation);
    public void InvalidateVisual() => map.InvalidateVisual();
    public void SelectResident(int id, bool follow = false) => map.SelectResident(id, follow);
    public IImage ActivityPreview(ResidentTaskIcon kind) => map.ActivityPreview(kind);
    public IImage ResidentPreview(RaceKind race, Profession profession) => map.ResidentPreview(race, profession);
    public IImage AnimalPreview(WildlifeKind kind) => map.AnimalPreview(kind);
    public IImage PlantPreview(PlantKind kind) => map.PlantPreview(kind);
    public WorldEngine? Engine { get => map.Engine; set => map.Engine = value; }
    public bool IsSimulationPaused { get => map.IsSimulationPaused; set => map.IsSimulationPaused = value; }
    public bool PickingLocation { get => map.PickingLocation; set => map.PickingLocation = value; }
    public bool GiftBuildings { get => map.GiftBuildings; set => map.GiftBuildings = value; }
    public double SimulationTickDurationSeconds { get => map.SimulationTickDurationSeconds; set => map.SimulationTickDurationSeconds = value; }
    public double SimulationTickFraction { get => map.SimulationTickFraction; set => map.SimulationTickFraction = value; }
    public double ScaleBottomInset { get => map.ScaleBottomInset; set => map.ScaleBottomInset = value; }
    public BridgeDirection ConstructionBridgeDirection { get => map.ConstructionBridgeDirection; set => map.ConstructionBridgeDirection = value; }
    public int ConstructionBridgeLevel { get => map.ConstructionBridgeLevel; set => map.ConstructionBridgeLevel = value; }
    public int? SelectedResidentId => map.SelectedResidentId;
    public bool HasPendingPlacement => map.HasPendingPlacement;
    public Func<Task>? PrepareWorldEdit { get => map.PrepareWorldEdit; set => map.PrepareWorldEdit = value; }
    public WorldMapControl.ViewportState CaptureViewport() => map.CaptureViewport();
    public WorldMapControl.MapSelectionState CaptureMapSelection() => map.CaptureMapSelection();
    public void RestoreViewport(WorldMapControl.ViewportState viewport) => map.RestoreViewport(viewport);
    public void RestoreMapSelection(WorldMapControl.MapSelectionState selection) => map.RestoreMapSelection(selection);
    public void RestoreObservationSelection(WorldMapControl.MapSelectionState selection) => map.RestoreObservationSelection(selection);
    public void SelectMapTile(int x, int y) => map.SelectMapTile(x, y);
    public void SelectMapBuilding(int id) => map.SelectMapBuilding(id);
    public void ClearMapSelection() => map.ClearMapSelection();
    public void FocusTile(int x, int y) => map.FocusTile(x, y);
    public bool FocusResident(int id) => map.FocusResident(id);
    public void ZoomIn() => map.ZoomIn();
    public void ZoomOut() => map.ZoomOut();
    public void FitWorld() => map.FitWorld();
    public void ConfirmPlacement() => map.ConfirmPlacement();
    public void CancelPlacement() => map.CancelPlacement();
    public int GetRecentFrameCount(long timestamp) => map.GetRecentFrameCount(timestamp);
    public void ApplyBrush(ToolCategory category, int index) => category.SetBrush(map, index);
    public event EventHandler? WorldMutationStarting { add => map.WorldMutationStarting += value; remove => map.WorldMutationStarting -= value; }
    public event EventHandler? WorldEdited { add => map.WorldEdited += value; remove => map.WorldEdited -= value; }
    public event Action<int, int>? TileSelected { add => map.TileSelected += value; remove => map.TileSelected -= value; }
    public event Action<int>? ResidentSelected { add => map.ResidentSelected += value; remove => map.ResidentSelected -= value; }
    public event Action<int>? BuildingSelected { add => map.BuildingSelected += value; remove => map.BuildingSelected -= value; }
    public event Action<string>? ToolError { add => map.ToolError += value; remove => map.ToolError -= value; }
    public event Action<string>? PlacementChanged { add => map.PlacementChanged += value; remove => map.PlacementChanged -= value; }
}
