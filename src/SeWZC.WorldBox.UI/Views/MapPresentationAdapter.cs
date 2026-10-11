using Avalonia.Media;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;
using SeWZC.WorldBox.UI.ViewModels;
namespace SeWZC.WorldBox.UI.Views;

/// <summary>将地图显示请求接入实际绘图控件。</summary>
internal sealed class MapPresentationAdapter(WorldMapControl map) : IMapPresentation
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
}
