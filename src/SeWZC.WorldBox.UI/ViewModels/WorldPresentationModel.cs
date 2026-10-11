using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;
using SeWZC.WorldBox.UI.ViewModels.Details;

namespace SeWZC.WorldBox.UI.ViewModels;

/// <summary>详情与编辑表单共用的字段呈现和交互请求。</summary>
internal abstract partial class WorldPresentationModel
{
    protected readonly PresentationRequests _requests;

    protected WorldPresentationModel(PresentationRequests requests)
    {
        _requests = requests;
    }

    protected MainViewModel _viewModel => _requests.State;
    protected WorldEngine _engine => _viewModel.Engine;
    protected IMapPresentation _map => _requests.Map;

    protected bool _mobilePanel
    {
        get => _viewModel.InspectorVisible;
        set => _viewModel.InspectorVisible = value;
    }

    protected bool _isCompact => _viewModel.IsCompactHud;

    protected bool _expandedInspector
    {
        get => _viewModel.InspectorExpanded;
        set => _viewModel.InspectorExpanded = value;
    }

    protected string _inspectorMode
    {
        get => _viewModel.InspectorMode;
        set => _viewModel.InspectorMode = value;
    }

    protected int _selectedResidentId
    {
        get => _viewModel.SelectedResidentId;
        set => _viewModel.SelectedResidentId = value;
    }

    protected int _selectedNationId
    {
        get => _viewModel.SelectedNationId;
        set => _viewModel.SelectedNationId = value;
    }

    protected int _selectedBuildingId
    {
        get => _viewModel.SelectedBuildingId;
        set => _viewModel.SelectedBuildingId = value;
    }

    protected (int X, int Y)? _selectedTile
    {
        get => _viewModel.SelectedTile;
        set => _viewModel.SelectedTile = value;
    }

    protected bool CanSubmitEdit()
    {
        return _requests.CanSubmitEdit();
    }

    protected Task<bool> SubmitEditAsync(Action command, bool replaceCheckpoint = false)
    {
        return _requests.SubmitEdit(command, replaceCheckpoint);
    }

    protected void RunEdit(Action command, string status)
    {
        _requests.RunEdit(command, status);
    }

    protected void OpenModal(StackNode content)
    {
        _requests.OpenModal(content);
    }

    protected void CloseModal()
    {
        _requests.CloseModal();
    }

    protected void CloseInspector()
    {
        _requests.CloseInspector();
    }

    protected void RefreshUi(bool force = false)
    {
        _requests.RefreshUi(force);
    }

    protected void SetStatus(string text)
    {
        _requests.SetStatus(text);
    }

    protected void ApplyLayout()
    {
        _requests.ApplyLayout();
    }

    protected void OpenInspector(string mode, bool remember = true)
    {
        _requests.OpenInspector(mode, remember);
    }

    protected void OpenResident(int id)
    {
        _requests.OpenResident(id);
    }

    protected void OpenNation(int id)
    {
        _requests.OpenNation(id);
    }

    protected void OpenSettlement(int id, string mode = "settlement")
    {
        _requests.OpenSettlement(id, mode);
    }

    protected void OpenTile(int x, int y, bool locate = false)
    {
        _requests.OpenTile(x, y, locate);
    }

    protected void OpenBuilding(Building building)
    {
        _requests.OpenBuilding(building);
    }

    protected void FocusEvent(WorldEvent item)
    {
        _requests.FocusEvent(item);
    }

    protected void FocusMapTile(int x, int y)
    {
        _requests.FocusMapTile(x, y);
    }

    protected bool FocusMapResident(int id)
    {
        return _requests.FocusMapResident(id);
    }

    protected void SetCategory(ToolCategory category)
    {
        _requests.SetCategory(category);
    }

    protected void AddMapPicker(StackNode panel, NumberNode x, NumberNode y, Action? onLocationPicked = null)
    {
        panel.Children.Add(Named(Button("从地图选点", () => _requests.StartMapPick(x, y, onLocationPicked)),
            "map-pick-" + x.Name));
    }
}
