using Avalonia.Media;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;

namespace SeWZC.WorldBox.UI.ViewModels;

/// <summary>地图显示设置、人物跟随与图例素材的呈现接口。</summary>
internal interface IMapPresentation
{
    int InfrastructureTownId { get; set; }
    int Overlay { get; set; }
    int SelectedSettlementId { get; set; }
    int SelectedNationId { get; set; }
    BuildingKind? InfrastructureKind { get; set; }
    bool HighlightBuildings { get; set; }
    bool HighlightRoads { get; set; }
    bool FollowSelectedResident { get; set; }
    bool ShowResidentRoute { get; set; }
    bool ShowBorders { get; set; }
    bool ShowWildlife { get; set; }
    bool ShowPlants { get; set; }
    bool ShowBuildingNames { get; set; }
    ResourceVisibility ResourceVisibility { get; set; }
    MapTool ActiveTool { get; set; }
    HashSet<ResourceKind> VisibleResources { get; }
    void RefreshWorld(bool resetCamera = false, bool deferAnimation = false);
    void InvalidateVisual();
    void SelectResident(int id, bool follow = false);
    IImage ActivityPreview(ResidentTaskIcon kind);
    IImage ResidentPreview(RaceKind race, Profession profession);
    IImage AnimalPreview(WildlifeKind kind);
    IImage PlantPreview(PlantKind kind);
}
