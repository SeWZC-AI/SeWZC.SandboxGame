using static SeWZC.WorldBox.UI.ViewModels.WorldPresentationModel;
using Avalonia;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;
using SeWZC.WorldBox.UI.ViewModels;
using SeWZC.WorldBox.UI.ViewModels.Details;
namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{

    private HashSet<ObservedObject> _watched { get => _inspectorModel._watched; }
    private bool _historyWatchedOnly { get => _inspectorModel._historyWatchedOnly; set => _inspectorModel._historyWatchedOnly = value; }
    private int _eventDetailId { get => _inspectorModel._eventDetailId; set => _inspectorModel._eventDetailId = value; }
    private string? _inspectorKey { get => _inspectorModel._inspectorKey; set => _inspectorModel._inspectorKey = value; }
    private int _inspectorSettlementId { get => _inspectorModel._inspectorSettlementId; set => _inspectorModel._inspectorSettlementId = value; }
    private Dictionary<string, bool> _expandedDetails { get => _inspectorModel._expandedDetails; }
    private bool _civilizationDetails { get => _inspectorModel._civilizationDetails; set => _inspectorModel._civilizationDetails = value; }
    private ResearchBranch? _researchBranch { get => _inspectorModel._researchBranch; set => _inspectorModel._researchBranch = value; }
    private bool _researchExpanded { get => _inspectorModel._researchExpanded; set => _inspectorModel._researchExpanded = value; }
    private ResearchRoute _researchRoute { get => _inspectorModel._researchRoute; set => _inspectorModel._researchRoute = value; }
    private Advancement _selectedResearch { get => _inspectorModel._selectedResearch; set => _inspectorModel._selectedResearch = value; }
    private void RefreshInspector(bool force = false) => _inspectorModel.RefreshInspector(force);
    private void InvalidateInspector() => _inspectorModel.InvalidateInspector();
    private void ResetInfrastructureFilters() => _inspectorModel.ResetInfrastructureFilters();
    private bool IsWatched(WorldEvent item) => _inspectorModel.IsWatched(item);
    private void ShowStorage() => _editorModel.ShowStorage();
    private void ShowNewWorld() => _editorModel.ShowNewWorld();
    private void ShowNationEditor(int id) => _editorModel.ShowNationEditor(id);
    private void ShowBuildingEditor(int id) => _editorModel.ShowBuildingEditor(id);
    private void ShowSpellSelectionEditor(SpellKind spell, int? town = null) => _editorModel.ShowSpellSelectionEditor(spell, town);
    private void ShowTileEditor(int x, int y) => _editorModel.ShowTileEditor(x, y);
    private StackNode ModalPanel(string title, string description) => _editorModel.ModalPanel(title, description);
    private string NationName(int id) => _inspectorModel.NationName(id);
    private string TownName(int id) => _inspectorModel.TownName(id);
    private Task SaveAsync(bool manual) => _storage.SaveAsync(manual);
    private void DeferAutosaveAfterEdit() => _storage.DeferAutosaveAfterEdit();

    private (InspectorViewModel Inspector, WorldEditorViewModel Editor) CreatePresentationModels()
    {
        var requests = new PresentationRequests
        {
            State = _viewModel,
            Map = new Views.MapPresentationAdapter(_map),
            Storage = _storage,
            CanSubmitEdit = CanSubmitEdit,
            SubmitEdit = SubmitEditAsync,
            RunEdit = RunEdit,
            ModalVisible = () => _modal.IsVisible,
            ModalGeneration = () => _modalGeneration,
            PickingLocation = () => _mapPick is not null,
            OpenModal = OpenModal,
            CloseModal = CloseModal,
            CloseInspector = CloseInspector,
            RefreshPerformanceStats = RefreshPerformanceStats,
            RefreshUi = RefreshUi,
            SetStatus = SetStatus,
            ApplyLayout = ApplyLayout,
            RememberLocation = RememberLocation,
            OpenInspector = OpenInspector,
            OpenResident = OpenResident,
            OpenNation = OpenNation,
            OpenSettlement = OpenSettlement,
            OpenTile = OpenTile,
            FocusMapTile = FocusMapTile,
            FocusMapResident = FocusMapResident,
            OpenBuilding = OpenBuilding,
            FocusEvent = FocusEvent,
            SetCategory = SetCategory,
            StartMapPick = StartMapPick,
            ReplaceWorld = ReplaceWorld,
            InspectorContentChanged = content =>
            {
                _viewModel.InspectorContent = content;
                _inspectorScroll.Offset = default;
            },
            TerritoryEdit = nation =>
            {
                CloseModal();
                SetCategory(ToolCategory.Terrain);
                _map.SelectedNationId = nation;
                _map.ActiveTool = MapTool.Territory;
                _toolTitle.Text = "划定疆域";
                _toolHint.Text = "绘制陆地归属，覆盖聚落会一并转移";
                _mobilePanel = false;
                ApplyLayout();
            },
        };
        requests.Inspector = new InspectorViewModel(requests);
        requests.Editors = new WorldEditorViewModel(requests);
        return (requests.Inspector, requests.Editors);
    }
}
