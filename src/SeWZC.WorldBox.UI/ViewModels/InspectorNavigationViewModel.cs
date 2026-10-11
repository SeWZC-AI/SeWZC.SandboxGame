using Avalonia;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;
namespace SeWZC.WorldBox.UI.ViewModels;
/// <summary>详情导航、地图选择与观察位置的返回历史。</summary>
internal sealed class InspectorNavigationViewModel : ViewModelBase
{
    private readonly GameSessionViewModel _session;
    private readonly Stack<InspectorLocation> _navigation = new();
    private Vector _scrollOffset;
    /// <summary>连接导航使用的会话状态。</summary>
    /// <param name="session">当前游戏会话。</param>
    internal InspectorNavigationViewModel(GameSessionViewModel session)
    {
        _session = session;
        BackCommand = new(_ => GoBack());
        CloseCommand = new(_ => CloseInspector());
        ExpandCommand = new(_ =>
        {
            State.InspectorExpanded = !State.InspectorExpanded;
            Layout.Refresh();
            _session.Inspector.RefreshInspector();
        });
    }
    public MainViewModel State => _session.State;
    public ShellLayoutViewModel Layout => _session.Layout;
    public Vector ScrollOffset { get => _scrollOffset; set => Set(ref _scrollOffset, value); }
    public UiCommand BackCommand { get; }
    public UiCommand CloseCommand { get; }
    public UiCommand ExpandCommand { get; }
    internal void ClearHistory() { _navigation.Clear(); Generation++; }
    internal void ClearObservation() { _returnViewport = null; }

    internal int Generation { get; private set; }
    private WorldMapControl.ViewportState? _returnViewport;
    private WorldMapControl.MapSelectionState _returnSelection;

    internal void RememberObservation()
    {
        RememberObservation(_session.Map.CaptureViewport(), _session.Map.CaptureMapSelection());
    }

    internal void RememberObservation(WorldMapControl.ViewportState viewport, WorldMapControl.MapSelectionState selection)
    {
        if (_returnViewport is not null)
            return;
        _returnViewport = viewport;
        _returnSelection = selection;
        _session.State.CanReturnObservation = true;
        _session.Layout.Refresh();
    }

    internal void FocusMapTile(int x, int y)
    {
        RememberObservation();
        _session.Map.FocusTile(x, y);
        _session.SetStatus($"已定位地格（{x}, {y}），可返回观察位置");
    }

    internal bool FocusMapResident(int id)
    {
        var viewport = _session.Map.CaptureViewport();
        var selection = _session.Map.CaptureMapSelection();
        if (!_session.Map.FocusResident(id))
        {
            _session.SetStatus("这位居民已不在地图上，无法定位");
            return false;
        }
        _session.State.SelectedResidentId = id;
        _session.State.SelectedBuildingId = 0;
        _session.State.SelectedTile = null;
        _session.State.SelectionKind = "resident";
        RememberObservation(viewport, selection);
        RefreshSelectionSummary();
        _session.SetStatus("已定位居民，可返回观察位置");
        return true;
    }

    internal void ReturnToObservation()
    {
        if (_returnViewport is not { } viewport)
            return;
        CloseInspector();
        _session.Map.RestoreObservationSelection(_returnSelection);
        _session.Map.RestoreViewport(viewport);
        _session.State.SelectedResidentId = _returnSelection.ResidentId ?? 0;
        _session.State.SelectedBuildingId = _returnSelection.BuildingId ?? 0;
        _session.State.SelectedTile = _returnSelection.Tile;
        _session.State.SelectionKind = _returnSelection.ResidentId is not null ? "resident"
            : _returnSelection.BuildingId is not null ? "building" : _returnSelection.Tile is not null ? "tile" : null;
        _returnViewport = null;
        _session.State.CanReturnObservation = false;
        _session.Layout.Refresh();
    }

    internal void RememberLocation()
    {
        if (_session.Interaction.IsPickingLocation || !_session.State.InspectorVisible)
            return;
        var location = new InspectorLocation(_session.State.InspectorMode, _session.State.SelectedNationId, _session.State.SelectedResidentId,
            _session.Inspector._inspectorSettlementId, _session.State.SelectedTile, _session.State.SelectedBuildingId, _session.Inspector._eventDetailId,
            ScrollOffset, _session.State.SelectionKind, _session.State.InspectorExpanded, _session.Inspector._researchExpanded,
            _session.Map.CaptureMapSelection(), _session.Map.CaptureViewport(), new Dictionary<string, bool>(_session.Inspector._expandedDetails),
            new ResearchViewState(_session.Inspector._selectedResearch, _session.Inspector._researchRoute, _session.Inspector._researchBranch,
                _session.Inspector._civilizationDetails, _session.State.InspectorMode == "research"
                    ? _session.Inspector.ResearchGraph?.CaptureViewport()
                    : null));
        if (_navigation.Count >= 32)
        {
            var recent = _navigation.Take(31).Reverse().ToArray();
            _navigation.Clear();
            foreach (var item in recent)
                _navigation.Push(item);
        }

        _navigation.Push(location);
    }

    internal void OpenInspector(string mode, bool remember = true)
    {
        if (_session.Interaction.IsPickingLocation)
            return;
        if (mode == "rules")
        {
            _session.Editors.ShowRules();
            return;
        }

        if (_session.State.InspectorVisible && mode == _session.State.InspectorMode && _session.Inspector._inspectorKey == _session.Inspector.InspectorKey())
            return;
        if (remember)
            RememberLocation();
        Generation++;
        _session.State.InspectorMode = mode;
        _session.State.InspectorVisible = true;
        _session.State.ToolsVisible = false;
        _session.Tools.SuspendMapTool(false);
        SynchronizeInspectorSelection();
        _session.Layout.Refresh();
        _session.Inspector.RefreshInspector(true);
        _session.RequestFocus();
    }

    internal void GoBack()
    {
        if (_session.Interaction.IsPickingLocation)
            return;
        if (!_navigation.TryPop(out var view))
        {
            CloseInspector();
            return;
        }

        _session.State.SelectedNationId = view.Nation;
        _session.State.SelectedResidentId = view.Resident;
        _session.Inspector._inspectorSettlementId = view.Town;
        _session.State.SelectedTile = view.Tile;
        _session.State.SelectedBuildingId = view.Building;
        _session.Inspector._eventDetailId = view.Event;
        _session.State.SelectionKind = view.SelectionKind;
        _session.State.InspectorExpanded = view.Expanded;
        _session.Inspector._researchExpanded = view.ResearchExpanded;
        _session.Inspector._selectedResearch = view.Research.Selected;
        _session.Inspector._researchRoute = view.Research.Route;
        _session.Inspector._researchBranch = view.Research.Branch;
        _session.Inspector._civilizationDetails = view.Research.CivilizationDetails;
        _session.Inspector._expandedDetails.Clear();
        foreach (var detail in view.Details)
            _session.Inspector._expandedDetails[detail.Key] = detail.Value;
        _session.Map.RestoreMapSelection(view.MapSelection);
        _session.Map.RestoreViewport(view.Viewport);
        _session.Inspector.InvalidateInspector();
        OpenInspector(view.Mode, false);
        var generation = Generation;
        _session.AfterLayout(() =>
        {
            if (!_session.State.InspectorVisible || generation != Generation)
                return;
            ScrollOffset = view.Scroll;
            if (view.Research.Viewport is { } viewport &&
                _session.Inspector.ResearchGraph is { } graph)
            {
                graph.RestoreViewport(viewport, () => _session.State.InspectorVisible && generation == Generation);
                _session.Inspector.RefreshInspector();
            }
        });
    }

    internal void OpenResident(int id)
    {
        if (_session.Interaction.IsPickingLocation)
            return;
        if (_session.State.InspectorVisible && _session.State.InspectorMode == "resident" && _session.State.SelectedResidentId == id)
            return;
        RememberLocation();
        _session.State.SelectedResidentId = id;
        _session.State.SelectionKind = "resident";
        _session.State.SelectedBuildingId = 0;
        _session.State.SelectedTile = null;
        _session.Map.SelectResident(id, _session.Map.SelectedResidentId == id && _session.Map.FollowSelectedResident);
        OpenInspector("resident", false);
    }

    internal void OpenNation(int id)
    {
        if (_session.Interaction.IsPickingLocation)
            return;
        if (_session.State.InspectorVisible && _session.State.InspectorMode == "nation" && _session.State.SelectedNationId == id)
            return;
        RememberLocation();
        _session.State.SelectedNationId = id;
        OpenInspector("nation", false);
    }

    internal void OpenSettlement(int id, string mode = "settlement")
    {
        if (_session.Interaction.IsPickingLocation)
            return;
        if (_session.State.InspectorVisible && _session.State.InspectorMode == mode && _session.Inspector._inspectorSettlementId == id)
            return;
        RememberLocation();
        _session.Inspector._inspectorSettlementId = id;
        OpenInspector(mode, false);
    }

    internal void OpenTile(int x, int y, bool locate = false)
    {
        if (_session.Interaction.IsPickingLocation)
            return;
        if (_session.State.InspectorVisible && _session.State.InspectorMode == "tile" && _session.State.SelectedTile == (x, y))
            return;
        if (locate)
            RememberObservation();
        RememberLocation();
        _session.State.SelectedTile = (x, y);
        _session.State.SelectedResidentId = 0;
        _session.State.SelectedBuildingId = 0;
        _session.State.SelectionKind = "tile";
        _session.Map.SelectMapTile(x, y);
        if (locate)
            FocusMapTile(x, y);
        OpenInspector("tile", false);
    }

    internal void SynchronizeInspectorSelection()
    {
        if (_session.Interaction.IsPickingLocation)
            return;
        if (_session.State.InspectorMode == "resident")
        {
            _session.State.SelectionKind = "resident";
            _session.State.SelectedBuildingId = 0;
            _session.State.SelectedTile = null;
            _session.Map.SelectResident(_session.State.SelectedResidentId,
                _session.Map.SelectedResidentId == _session.State.SelectedResidentId && _session.Map.FollowSelectedResident);
        }
        else if (_session.State.InspectorMode == "building")
        {
            _session.State.SelectionKind = "building";
            _session.State.SelectedResidentId = 0;
            _session.Map.SelectMapBuilding(_session.State.SelectedBuildingId);
        }
        else if (_session.State.InspectorMode == "tile" && _session.State.SelectedTile is { } tile)
        {
            _session.State.SelectionKind = "tile";
            _session.State.SelectedResidentId = 0;
            _session.State.SelectedBuildingId = 0;
            _session.Map.SelectMapTile(tile.X, tile.Y);
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
        WorldMapControl.ViewportState Viewport,
        Dictionary<string, bool> Details,
        ResearchViewState Research);

    private sealed record ResearchViewState(
        Advancement Selected,
        ResearchRoute Route,
        ResearchBranch? Branch,
        bool CivilizationDetails,
        ResearchGraphViewModel.ViewportState? Viewport);


    internal void SelectMapObject(string kind, int id = 0, int x = 0, int y = 0)
    {
        if (_session.Interaction.IsPickingLocation)
            return;
        _session.State.SelectionKind = kind;
        _session.State.SelectedResidentId = kind == "resident" ? id : 0;
        _session.State.SelectedBuildingId = kind == "building" ? id : 0;
        _session.State.SelectedTile = kind == "resident" ? null : (x, y);
        _session.State.ToolsVisible = false;
        CloseInspector();
        RefreshSelectionSummary();
    }

    internal void RefreshSelectionSummary()
    {
        if (!_session.State.RefreshSelection() && _session.State.SelectionKind is not null)
            ClearMapSelection();
    }

    internal void ViewMapSelection()
    {
        if (_session.Interaction.IsPickingLocation)
            return;
        if (_session.State.SelectionKind == "resident")
            OpenResident(_session.State.SelectedResidentId);
        else if (_session.State.SelectionKind == "building")
            OpenInspector("building");
        else if (_session.State.SelectedTile is not null)
            OpenInspector("tile");
        RefreshSelectionSummary();
    }

    internal void ClearMapSelection()
    {
        if (_session.Interaction.IsPickingLocation)
            return;
        if (!_session.State.InspectorVisible)
        {
            _navigation.Clear();
            Generation++;
        }

        _session.State.SelectionKind = null;
        _session.State.SelectedBuildingId = 0;
        _session.State.RefreshSelection();
        if (!_session.State.InspectorVisible)
        {
            _session.State.SelectedResidentId = 0;
            _session.State.SelectedTile = null;
        }

        _session.Map.ClearMapSelection();
    }



    internal void OpenBuilding(Building building)
    {
        if (_session.Interaction.IsPickingLocation)
            return;
        if (_session.State.InspectorVisible && _session.State.InspectorMode == "building" && _session.State.SelectedBuildingId == building.Id)
            return;
        RememberLocation();
        _session.State.SelectedBuildingId = building.Id;
        _session.State.SelectedTile = (building.X, building.Y);
        _session.State.SelectionKind = "building";
        OpenInspector("building", false);
    }


    internal void FocusEvent(WorldEvent item)
    {
        if (_session.Interaction.IsPickingLocation)
            return;
        if (_session.State.InspectorVisible && _session.State.InspectorMode == "event" && _session.Inspector._eventDetailId == item.Id)
            return;
        RememberLocation();
        _session.Inspector._eventDetailId = item.Id;
        OpenInspector("event", false);
    }
    internal void CloseInspector()
    {
        if (_session.Interaction.IsPickingLocation)
            return;
        _session.Edits.CancelPending();
        _navigation.Clear();
        Generation++;
        _session.State.InspectorVisible = false;
        _session.State.InspectorExpanded = false;
        _session.Inspector._researchExpanded = false;
        _session.Layout.Refresh();
        _session.RequestFocus();
    }
    internal void LocateMapSelection()
    {
        if (_session.State.SelectionKind == "resident")
            FocusMapResident(_session.State.SelectedResidentId);
        else if (_session.State.SelectedTile is { } tile)
            FocusMapTile(tile.X, tile.Y);
    }
}
