using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;
using SeWZC.WorldBox.UI.ViewModels.Details;
namespace SeWZC.WorldBox.UI.ViewModels;
/// <summary>地图放置确认、表单取点及 Escape 的操作优先级。</summary>
internal sealed class MapInteractionViewModel : ViewModelBase
{
    private readonly GameSessionViewModel _session;
    private string _placementText = "";
    /// <summary>连接地图与表单状态。</summary>
    /// <param name="session">当前游戏会话。</param>
    internal MapInteractionViewModel(GameSessionViewModel session)
    {
        _session = session;
        ConfirmPlacementCommand = new(_ => session.Map.ConfirmPlacement());
        CancelPlacementCommand = new(_ => session.Map.CancelPlacement());
        ReturnToFormCommand = new(_ => FinishMapPick());
    }
    public MainViewModel State => _session.State;
    public ShellLayoutViewModel Layout => _session.Layout;
    public bool IsPickingLocation => _mapPick is not null;
    public string PlacementText { get => _placementText; private set => Set(ref _placementText, value); }
    public UiCommand ConfirmPlacementCommand { get; }
    public UiCommand CancelPlacementCommand { get; }
    public UiCommand ReturnToFormCommand { get; }
    internal void PlacementChanged(string message)
    {
        PlacementText = message;
        State.PlacementPending = _session.Map.HasPendingPlacement;
    }
    internal void Escape()
    {
        if (IsPickingLocation) FinishMapPick();
        else if (_session.Map.HasPendingPlacement) _session.Map.CancelPlacement();
        else if (_session.Dialogs.IsVisible) _session.Dialogs.CloseModal();
        else if (State.InspectorVisible) _session.Navigation.CloseInspector();
        else _session.Tools.SuspendMapTool(true);
    }

    private Action<int, int>? _mapPick;
    private WorldEngine? _mapPickEngine;
    private int _mapPickGeneration;
    private bool _mapPickInspectorVisible, _mapPickToolsOpen;
    private ModalViewModel? _mapPickModal;
    private WorldMapControl.MapSelectionState _mapPickSelection;
    private MapTool _mapPickTool = MapTool.Pan;

    internal void StartMapPick(NumberNode x, NumberNode y, Action? onLocationPicked)
    {
        if (!_session.Dialogs.IsVisible || _session.Dialogs.Content is null)
            return;
        _mapPickGeneration = _session.Dialogs.Generation;
        _mapPickEngine = _session.State.Engine;
        _mapPickModal = _session.Dialogs.Content;
        _mapPickInspectorVisible = _session.State.InspectorVisible;
        _mapPickToolsOpen = _session.State.ToolsVisible;
        _mapPickTool = _session.Map.ActiveTool;
        _mapPickSelection = _session.Map.CaptureMapSelection();
        _mapPick = (xx, yy) =>
        {
            x.Value = xx;
            y.Value = yy;
            onLocationPicked?.Invoke();
        };
        Notify(nameof(IsPickingLocation));
        _session.Map.PickingLocation = true;
        _session.Map.ActiveTool = MapTool.Inspect;
        _session.Dialogs.IsVisible = false;
        _session.State.InspectorVisible = false;
        _session.State.ToolsVisible = false;
        _session.Map.IsSimulationPaused = true;
        _session.Layout.Refresh();
        _session.RefreshUi();
        _session.SetStatus("点选目标地格，按 Escape 返回表单；时间暂时停止");
    }

    internal void FinishMapPick(int? x = null, int? y = null)
    {
        if (_mapPick is null)
            return;
        var assignLocation = _mapPick;
        var current = _mapPickGeneration == _session.Dialogs.Generation && ReferenceEquals(_mapPickEngine, _session.State.Engine) &&
                      ReferenceEquals(_mapPickModal, _session.Dialogs.Content);
        CancelMapPick(current);
        if (!current)
            return;
        if (x.HasValue && y.HasValue)
            assignLocation(x.Value, y.Value);
        _session.Dialogs.IsVisible = true;
        _session.Map.IsSimulationPaused = true;
        _session.RefreshUi();
        _session.SetStatus(x.HasValue && y.HasValue ? "已选定位置，可继续编辑表单" : "已返回表单，位置未改变");
    }

    internal void CancelMapPick(bool restorePresentation = false)
    {
        if (_mapPick is null)
            return;
        _mapPick = null;
        Notify(nameof(IsPickingLocation));
        _mapPickEngine = null;
        _mapPickModal = null;
        _session.Map.PickingLocation = false;
        _session.Map.ActiveTool = restorePresentation ? _mapPickTool : MapTool.Pan;
        if (restorePresentation)
        {
            _session.Map.RestoreMapSelection(_mapPickSelection);
            _session.State.InspectorVisible = _mapPickInspectorVisible;
            _session.State.ToolsVisible = _mapPickToolsOpen;
            _session.Layout.Refresh();
        }
    }
}
