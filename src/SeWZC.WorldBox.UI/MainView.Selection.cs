using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.ViewModels.Details;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    private bool _expandedInspector { get => _viewModel.InspectorExpanded; set => _viewModel.InspectorExpanded = value; }

    private void SelectMapObject(string kind, int id = 0, int x = 0, int y = 0)
    {
        if (_mapPick is not null)
            return;
        _mapSelectionKind = kind;
        _selectedResidentId = kind == "resident" ? id : 0;
        _selectedBuildingId = kind == "building" ? id : 0;
        _selectedTile = kind == "resident" ? null : (x, y);
        _toolsOpen = false;
        CloseInspector();
        RefreshSelectionSummary();
    }

    private void RefreshSelectionSummary()
    {
        if (!_viewModel.RefreshSelection() && _mapSelectionKind is not null)
            ClearMapSelection();
    }

    private void ViewMapSelection()
    {
        if (_mapPick is not null)
            return;
        if (_mapSelectionKind == "resident")
            OpenResident(_selectedResidentId);
        else if (_mapSelectionKind == "building")
            OpenInspector("building");
        else if (_selectedTile is not null)
            OpenInspector("tile");
        RefreshSelectionSummary();
    }

    private void ClearMapSelection()
    {
        if (_mapPick is not null)
            return;
        if (!_mobilePanel)
        {
            _navigation.Clear();
            _inspectorNavigationGeneration++;
        }

        _mapSelectionKind = null;
        _selectedBuildingId = 0;
        _viewModel.RefreshSelection();
        if (!_mobilePanel)
        {
            _selectedResidentId = 0;
            _selectedTile = null;
        }

        _map.ClearMapSelection();
    }

    private void ShowLandProject(int x, int y)
    {
        var towns = _engine.State.Settlements.OrderBy(t => Math.Abs(t.X - x) + Math.Abs(t.Y - y)).ToArray();
        if (towns.Length == 0)
        {
            SetStatus("需要先有聚落与居民。");
            return;
        }

        var terrain = _engine.State.Tiles[y * _engine.State.Width + x].Terrain;
        var kind = terrain == TerrainType.Mountain ? BuildingKind.MountainPass
            : terrain is TerrainType.Water or TerrainType.River or TerrainType.Lake or TerrainType.Stream
                or TerrainType.LargeRiver ? BuildingKind.Bridge : BuildingKind.Farm;
        var panel = ModalPanel("安排居民改造地块", $"{WorldEngine.BuildingName(kind)}\n投入材料后，由居民到场施工；桥梁和山路完工后才可通行。");
        var town = ObjectField(panel, "负责聚落", towns.Select(t => (t.Id, t.Name)), towns[0].Id, "land-town");
        var bridgeOptions = new StackNode { IsVisible = kind == BuildingKind.Bridge };
        panel.Children.Add(bridgeOptions);
        var direction = EnumField(bridgeOptions, "桥梁方向", BridgeDirection.Horizontal, WorldEngine.BridgeDirectionName,
            "land-bridge-direction");
        direction.IsVisible = kind == BuildingKind.Bridge;
        var level = ObjectField(bridgeOptions, "桥梁等级",
            [(1, "1 级：离岸 2 格"), (2, "2 级：离岸 4 格"), (3, "3 级：离岸 6 格")], 1, "land-bridge-level");
        level.IsVisible = kind == BuildingKind.Bridge;
        panel.Children.Add(LiveText(() =>
            "材料：" + StockLabel(WorldEngine.FacilityCost(kind, kind == BuildingKind.Bridge ? Integer(level) : 1))));
        panel.Children.Add(Named(Button("开始居民施工", () => RunEdit(() =>
        {
            _engine.BuildFacility(Integer(town), kind, x, y,
                kind == BuildingKind.Bridge ? (BridgeDirection?)direction.SelectedItem : null,
                kind == BuildingKind.Bridge ? Integer(level) : 1);
            CloseModal();
        }, "改造已立项，继续模拟后居民会到场施工")), "land-apply"));
        OpenModal(panel);
    }
}
