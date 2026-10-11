using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;

namespace SeWZC.WorldBox.UI.ViewModels;

/// <summary>会话通过地图端口控制视野、选择及编辑，不依赖视图实例。</summary>
internal interface IGameMap : IMapPresentation
{
    WorldEngine? Engine { get; set; }
    bool IsSimulationPaused { get; set; }
    bool PickingLocation { get; set; }
    bool GiftBuildings { get; set; }
    double SimulationTickDurationSeconds { get; set; }
    double SimulationTickFraction { get; set; }
    double ScaleBottomInset { get; set; }
    BridgeDirection ConstructionBridgeDirection { get; set; }
    int ConstructionBridgeLevel { get; set; }
    int? SelectedResidentId { get; }
    bool HasPendingPlacement { get; }
    Func<Task>? PrepareWorldEdit { get; set; }
    WorldMapControl.ViewportState CaptureViewport();
    WorldMapControl.MapSelectionState CaptureMapSelection();
    void RestoreViewport(WorldMapControl.ViewportState viewport);
    void RestoreMapSelection(WorldMapControl.MapSelectionState selection);
    void RestoreObservationSelection(WorldMapControl.MapSelectionState selection);
    void SelectMapTile(int x, int y);
    void SelectMapBuilding(int id);
    void ClearMapSelection();
    void FocusTile(int x, int y);
    bool FocusResident(int id);
    void ZoomIn();
    void ZoomOut();
    void FitWorld();
    void ConfirmPlacement();
    void CancelPlacement();
    int GetRecentFrameCount(long timestamp);
    void ApplyBrush(ToolCategory category, int index);
    event EventHandler? WorldMutationStarting;
    event EventHandler? WorldEdited;
    event Action<int, int>? TileSelected;
    event Action<int>? ResidentSelected;
    event Action<int>? BuildingSelected;
    event Action<string>? ToolError;
    event Action<string>? PlacementChanged;
}
