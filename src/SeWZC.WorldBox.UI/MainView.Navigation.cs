using Avalonia;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    private int _inspectorNavigationGeneration;

    private string InspectorKey()
    {
        return
            $"{_inspectorMode}:{_selectedNationId}:{_selectedResidentId}:{_selectedTile}:{_eventDetailId}:{_inspectorSettlementId}:{_selectedBuildingId}";
    }

    private void RememberLocation()
    {
        if (_mapPick is not null || !_mobilePanel) return;
        var location = new InspectorLocation(_inspectorMode, _selectedNationId, _selectedResidentId,
            _inspectorSettlementId, _selectedTile, _selectedBuildingId, _eventDetailId,
            _inspectorScroll.Offset, _mapSelectionKind, _expandedInspector, _researchExpanded,
            _map.CaptureMapSelection(), new Dictionary<string, bool>(_expandedDetails),
            new ResearchViewState(_selectedResearch, _researchRoute, _researchBranch,
                _civilizationDetails, _inspectorMode == "research"
                    ? _inspectorScroll.GetVisualDescendants().OfType<ResearchGraphControl>().FirstOrDefault()
                        ?.CaptureViewport()
                    : null));
        // 导航历史达到上限时保留最近地点，避免返回操作先跳到久远位置。
        if (_navigation.Count >= 32)
        {
            var recent = _navigation.Take(31).Reverse().ToArray();
            _navigation.Clear();
            foreach (var item in recent) _navigation.Push(item);
        }

        _navigation.Push(location);
    }

    private void OpenInspector(string mode, bool remember = true)
    {
        if (_mapPick is not null) return;
        if (mode == "rules")
        {
            ShowRules();
            return;
        }

        if (_mobilePanel && mode == _inspectorMode && _inspectorKey == InspectorKey()) return;
        if (remember) RememberLocation();
        _inspectorNavigationGeneration++;
        _inspectorMode = mode;
        _mobilePanel = true;
        _toolsOpen = false;
        SuspendMapTool(false);
        SynchronizeInspectorSelection();
        ApplyLayout();
        RefreshInspector(true);
    }

    private void GoBack()
    {
        if (_mapPick is not null) return;
        if (!_navigation.TryPop(out var view))
        {
            CloseInspector();
            return;
        }

        _selectedNationId = view.Nation;
        _selectedResidentId = view.Resident;
        _inspectorSettlementId = view.Town;
        _selectedTile = view.Tile;
        _selectedBuildingId = view.Building;
        _eventDetailId = view.Event;
        _mapSelectionKind = view.SelectionKind;
        _expandedInspector = view.Expanded;
        _researchExpanded = view.ResearchExpanded;
        _selectedResearch = view.Research.Selected;
        _researchRoute = view.Research.Route;
        _researchBranch = view.Research.Branch;
        _civilizationDetails = view.Research.CivilizationDetails;
        _expandedDetails.Clear();
        foreach (var detail in view.Details) _expandedDetails[detail.Key] = detail.Value;
        _map.RestoreMapSelection(view.MapSelection);
        InvalidateInspector();
        OpenInspector(view.Mode, false);
        var generation = _inspectorNavigationGeneration;
        Dispatcher.UIThread.Post(() =>
        {
            if (!_mobilePanel || generation != _inspectorNavigationGeneration) return;
            _inspectorScroll.Offset = view.Scroll;
            if (view.Research.Viewport is { } viewport &&
                _inspectorScroll.GetVisualDescendants().OfType<ResearchGraphControl>().FirstOrDefault() is { } graph)
            {
                graph.RestoreViewport(viewport, () => _mobilePanel && generation == _inspectorNavigationGeneration);
                RefreshInspector();
            }
        }, DispatcherPriority.Loaded);
    }

    private void OpenResident(int id)
    {
        if (_mapPick is not null) return;
        if (_mobilePanel && _inspectorMode == "resident" && _selectedResidentId == id) return;
        RememberLocation();
        _selectedResidentId = id;
        _mapSelectionKind = "resident";
        _selectedBuildingId = 0;
        _selectedTile = null;
        _map.SelectResident(id, _map.SelectedResidentId == id && _map.FollowSelectedResident);
        OpenInspector("resident", false);
    }

    private void OpenNation(int id)
    {
        if (_mapPick is not null) return;
        if (_mobilePanel && _inspectorMode == "nation" && _selectedNationId == id) return;
        RememberLocation();
        _selectedNationId = id;
        OpenInspector("nation", false);
    }

    private void OpenSettlement(int id, string mode = "settlement")
    {
        if (_mapPick is not null) return;
        if (_mobilePanel && _inspectorMode == mode && _inspectorSettlementId == id) return;
        RememberLocation();
        _inspectorSettlementId = id;
        OpenInspector(mode, false);
    }

    private void OpenTile(int x, int y, bool locate = false)
    {
        if (_mapPick is not null) return;
        if (_mobilePanel && _inspectorMode == "tile" && _selectedTile == (x, y)) return;
        RememberLocation();
        _selectedTile = (x, y);
        _selectedResidentId = 0;
        _selectedBuildingId = 0;
        _mapSelectionKind = "tile";
        _map.SelectMapTile(x, y);
        if (locate) _map.FocusTile(x, y);
        OpenInspector("tile", false);
    }

    private void SynchronizeInspectorSelection()
    {
        if (_mapPick is not null) return;
        if (_inspectorMode == "resident")
        {
            _mapSelectionKind = "resident";
            _selectedBuildingId = 0;
            _selectedTile = null;
            _map.SelectResident(_selectedResidentId,
                _map.SelectedResidentId == _selectedResidentId && _map.FollowSelectedResident);
        }
        else if (_inspectorMode == "building")
        {
            _mapSelectionKind = "building";
            _selectedResidentId = 0;
            _map.SelectMapBuilding(_selectedBuildingId);
        }
        else if (_inspectorMode == "tile" && _selectedTile is { } tile)
        {
            _mapSelectionKind = "tile";
            _selectedResidentId = 0;
            _selectedBuildingId = 0;
            _map.SelectMapTile(tile.X, tile.Y);
        }
    }

    private sealed record InspectorLocation(
        string Mode,
        int Nation,
        int Resident,
        int Town,
        (int X, int Y)? Tile,
        int Building,
        int Event,
        Vector Scroll,
        string? SelectionKind,
        bool Expanded,
        bool ResearchExpanded,
        WorldMapControl.MapSelectionState MapSelection,
        Dictionary<string, bool> Details,
        ResearchViewState Research);

    private sealed record ResearchViewState(
        ResearchKind Selected,
        ResearchRoute Route,
        ResearchBranch? Branch,
        bool CivilizationDetails,
        ResearchGraphControl.ViewportState? Viewport);
}
