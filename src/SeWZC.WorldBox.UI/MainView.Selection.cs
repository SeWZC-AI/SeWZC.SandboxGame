using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    private readonly Border _selectionBar = new() { IsVisible = false, Background = Panel, Padding = new Thickness(6, 2), CornerRadius = new CornerRadius(7) };
    private readonly TextBlock _selectionText = Text("", 12, Mint);
    private string? _mapSelectionKind;
    private int _selectedBuildingId;
    private bool _expandedInspector;

    private Control BuildSelectionBar()
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 4 };
        _selectionText.TextTrimming = TextTrimming.CharacterEllipsis;
        _selectionText.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(_selectionText);
        var view = Named(Button("查看", ViewMapSelection, minWidth: 52), "selection-view");
        Grid.SetColumn(view, 1); row.Children.Add(view);
        var close = IconButton("close", ClearMapSelection, "取消选择", "selection-clear");
        Grid.SetColumn(close, 2); row.Children.Add(close);
        _selectionBar.Child = row;
        return Named(_selectionBar, "selection-summary");
    }

    private void SelectMapObject(string kind, int id = 0, int x = 0, int y = 0)
    {
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
        var resident = _mapSelectionKind == "resident" ? _engine.State.Residents.FirstOrDefault(r => r.Id == _selectedResidentId) : null;
        var building = _mapSelectionKind == "building" ? _engine.State.Society.Buildings.FirstOrDefault(b => b.Id == _selectedBuildingId) : null;
        if (_mapSelectionKind == "resident" && resident is null || _mapSelectionKind == "building" && building is null)
        { ClearMapSelection(); return; }
        _selectionBar.IsVisible = _mapSelectionKind is not null && !_mobilePanel && !_toolsOpen;
        _selectionText.Text = resident is not null ? $"{resident.Name}   {ProfessionName(resident.Profession)}\n{ResidentTask(resident)}"
            : building is not null ? $"{BuildingLabel(building)}\n{BuildingTask(building)}"
            : _selectedTile is { } p ? $"{TerrainName(_engine.State.Tiles[p.Y * _engine.State.Width + p.X].Terrain)}\n{_engine.GetTileProductionSummary(p.X, p.Y, _resourceVisibility).Split('\n')[0]}" : "";
        _selectionText.Text = DisplayFormat.Text(_selectionText.Text);
    }

    private void ViewMapSelection()
    {
        if (_mapSelectionKind == "resident") OpenResident(_selectedResidentId);
        else if (_mapSelectionKind == "building") OpenInspector("building");
        else if (_selectedTile is not null) OpenInspector("tile");
        RefreshSelectionSummary();
    }

    private void ClearMapSelection()
    {
        _mapSelectionKind = null; _selectedBuildingId = 0; _selectionBar.IsVisible = false;
        if (!_mobilePanel) { _selectedResidentId = 0; _selectedTile = null; }
        _map.ClearMapSelection();
    }

    private void ShowLandProject(int x, int y)
    {
        var towns = _engine.State.Settlements.OrderBy(t => Math.Abs(t.X - x) + Math.Abs(t.Y - y)).ToArray();
        if (towns.Length == 0) { SetStatus("需要先有聚落与居民。"); return; }
        var terrain = _engine.State.Tiles[y * _engine.State.Width + x].Terrain;
        var kind = terrain == TerrainType.Mountain ? BuildingKind.MountainPass
            : terrain is TerrainType.River or TerrainType.Stream or TerrainType.LargeRiver or TerrainType.Water or TerrainType.Lake ? BuildingKind.Bridge : BuildingKind.Farm;
        var panel = ModalPanel("安排居民改造地块", $"{WorldEngine.BuildingName(kind)}\n位置：{x}, {y}。投入材料后，由居民到场施工；桥梁和山路完工后才可通行。");
        var town = ObjectField(panel, "负责聚落", towns.Select(t => (t.Id, t.Name)), towns[0].Id, "land-town");
        var direction = EnumField(panel, "桥梁方向", BridgeDirection.Horizontal, WorldEngine.BridgeDirectionName, "land-bridge-direction"); direction.IsVisible = kind == BuildingKind.Bridge;
        var level = ObjectField(panel, "桥梁等级", new[] { (1, "1 级：离岸 2 格"), (2, "2 级：离岸 4 格"), (3, "3 级：离岸 6 格") }, 1, "land-bridge-level"); level.IsVisible = kind == BuildingKind.Bridge;
        panel.Children.Add(LiveText(() => "材料：" + StockLabel(WorldEngine.FacilityCost(kind, kind == BuildingKind.Bridge ? Integer(level) : 1))));
        panel.Children.Add(Named(Button("开始居民施工", () => RunEdit(() =>
        {
            _engine.BuildFacility(Integer(town), kind, x, y, kind == BuildingKind.Bridge ? (BridgeDirection?)direction.SelectedItem : null, kind == BuildingKind.Bridge ? Integer(level) : 1); CloseModal();
        }, "改造已立项，继续模拟后居民会到场施工")), "land-apply"));
        OpenModal(panel);
    }
}
