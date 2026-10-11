using System.Diagnostics;
using static SeWZC.WorldBox.UI.ViewModels.WorldPresentationModel;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;
using SeWZC.WorldBox.UI.Platform;
using SeWZC.WorldBox.UI.Services;
namespace SeWZC.WorldBox.UI.ViewModels;
/// <summary>组合各界面区域，协调模拟推进、保存和世界替换。</summary>
internal sealed class GameSessionViewModel : ViewModelBase
{
    private const double SimulationTickSeconds = SimulationScheduler.TickSeconds;
    private const int UnlimitedSpeed = SimulationScheduler.UnlimitedSpeed;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly SimulationScheduler _simulation = new();
    private bool _ready, _wasBackground;
    private double _previousTime, _lastUi, _lastPerformanceUpdate;
    private int _focusedEventId;
    private Task? _initialization;
    /// <summary>组合游戏的地图端口、状态和平台存储。</summary>
    /// <param name="state">当前世界及 HUD 状态。</param>
    /// <param name="map">地图显示与输入端口。</param>
    /// <param name="storage">读取平台存储。</param>
    internal GameSessionViewModel(MainViewModel state, IGameMap map, Func<IWorldStorage?> storage)
    {
        State = state;
        Map = map;
        Storage = new(() => State.Engine, storage, () => _clock.Elapsed.TotalSeconds);
        Edits = new(State, Storage, CurrentEditSession);
        Layout = new(this);
        Navigation = new(this);
        Tools = new(this);
        Interaction = new(this);
        Dialogs = new(this);
        (Inspector, Editors) = CreatePresentationModels();
        ZoomInCommand = new(_ => Map.ZoomIn());
        ZoomOutCommand = new(_ => Map.ZoomOut());
        FitWorldCommand = new(_ => Map.FitWorld());
        Edits.StateChanged += () => { Map.IsSimulationPaused = WorldTimeStopped; UpdateUndoButtons(); };
        Edits.Completed += () => { ResetTimeAnchor(); Map.IsSimulationPaused = WorldTimeStopped; RefreshUi(true); };
        Edits.Feedback += SetStatus;
        Storage.Feedback += SetStatus;
        Storage.TimeAnchorChanged += ResetTimeAnchor;
        Storage.CaptureChanged += () => { Map.IsSimulationPaused = WorldTimeStopped; RefreshTimeState(); };
        State.ViewRequested += HandleViewRequest;
        State.PauseChanged += () =>
        {
            if (!State.IsPaused) Edits.Checkpoint = null;
            Map.IsSimulationPaused = WorldTimeStopped;
            RefreshUi(true);
        };
        State.SpeedChanged += (previous, speed) =>
        {
            if (previous == UnlimitedSpeed || speed == UnlimitedSpeed) { _simulation.ClearPendingTime(); ResetTimeAnchor(); }
            Map.SimulationTickDurationSeconds = speed == UnlimitedSpeed ? .016 : SimulationTickSeconds / speed;
            ScheduleNextTick();
            RefreshTimeState();
        };
        Map.Engine = State.Engine;
        Map.GiftBuildings = true;
        Map.PrepareWorldEdit = PrepareEditAsync;
        Map.WorldMutationStarting += (_, _) => { Storage.CancelCapture(); Storage.EditRevision++; };
        Map.WorldEdited += (_, _) =>
        {
            Storage.DeferAutosaveAfterEdit();
            RefreshUi(true);
            SetStatus("世界已更新并暂停，可撤销本轮编辑");
        };
        Map.TileSelected += (x, y) =>
        {
            if (Interaction.IsPickingLocation) Interaction.FinishMapPick(x, y);
            else Navigation.SelectMapObject("tile", x: x, y: y);
        };
        Map.ResidentSelected += id => Navigation.SelectMapObject("resident", id);
        Map.BuildingSelected += id =>
        {
            var building = State.Engine.State.Society.Buildings.First(building => building.Id == id);
            Navigation.SelectMapObject("building", id, building.X, building.Y);
        };
        Map.ToolError += message => SetStatus("工具未应用：" + message);
        Map.PlacementChanged += Interaction.PlacementChanged;
        Tools.SetCategory(ToolCategory.Terrain);
        State.ToolsVisible = false;
        Layout.Resize(1440, 920, 1440);
        RefreshUi(true);
    }
    public MainViewModel State { get; }
    public InspectorNavigationViewModel Navigation { get; }
    public ToolPaletteViewModel Tools { get; }
    public MapInteractionViewModel Interaction { get; }
    public DialogHostViewModel Dialogs { get; }
    public ShellLayoutViewModel Layout { get; }
    public bool IsReady { get => _ready; internal set => Set(ref _ready, value); }
    public UiCommand ZoomInCommand { get; }
    public UiCommand ZoomOutCommand { get; }
    public UiCommand FitWorldCommand { get; }
    internal IGameMap Map { get; }
    internal WorldSaveCoordinator Storage { get; }
    internal WorldEditCoordinator Edits { get; }
    internal InspectorViewModel Inspector { get; }
    internal WorldEditorViewModel Editors { get; }
    internal event Action? FocusRequested;
    internal event Action? WorldMenuRequested;
    internal event Action? ScheduleChanged;
    internal event Action<Action>? LayoutRestorationRequested;
    internal bool EditCaptureActive => Edits.CaptureActive;
    internal bool WorldTimeStopped => State.IsPaused || Dialogs.IsVisible || Interaction.IsPickingLocation ||
        EditCaptureActive || Storage.Capture is not null || Storage.IsBackground;
    private EditSession CurrentEditSession() => new(Dialogs.Generation, Dialogs.IsVisible, Navigation.Generation, Interaction.IsPickingLocation);
    internal Task StartAsync() => _initialization ??= InitializeAsync();
    internal void ResetTimeAnchor() => _previousTime = _clock.Elapsed.TotalSeconds;
    internal void RequestFocus() => FocusRequested?.Invoke();
    internal void AfterLayout(Action action) => LayoutRestorationRequested?.Invoke(action);
    internal void SetStatus(string text) { State.Feedback = text; Dialogs.SetStatus(text); }
    private void RefreshTimeState() => State.RefreshTime(Dialogs.IsVisible, Interaction.IsPickingLocation,
        EditCaptureActive, Storage.Capture is not null, Storage.IsBackground, _simulation.IsCatchingUp);
    private Task PrepareEditAsync()
    {
        var source = State.Engine;
        var tool = Map.ActiveTool;
        var generation = Dialogs.Generation;
        return Edits.PrepareMapEditAsync(() => ReferenceEquals(source, State.Engine) && tool == Map.ActiveTool &&
            generation == Dialogs.Generation && !Dialogs.IsVisible && !Interaction.IsPickingLocation && !Map.PickingLocation);
    }
    internal Task<bool> SubmitEditAsync(Action command, bool replaceCheckpoint = false) =>
        Edits.SubmitAsync(command, replaceCheckpoint, Dialogs.LockSubmissionInputs);
    private async void RunEdit(Action command, string message)
    {
        await SubmitEditAsync(() => { command(); Map.RefreshWorld(); RefreshUi(true); SetStatus(message); });
    }
    private void HandleViewRequest(string request)
    {
        switch (request)
        {
            case "menu": WorldMenuRequested?.Invoke(); break;
            case "tools": Tools.ToggleTools(); break;
            case "pan": Tools.SuspendMapTool(true); break;
            case "selection": Navigation.ViewMapSelection(); break;
            case "clear": Navigation.ClearMapSelection(); break;
            case "locate": Navigation.LocateMapSelection(); break;
            case "return-observation": Navigation.ReturnToObservation(); break;
            case "undo": RestoreCheckpoint(); break;
            case "storage": Editors.ShowStorage(); break;
            case "rules": Editors.ShowRules(); break;
            case "new-world": Editors.ShowNewWorld(); break;
            case "event":
                if (State.Engine.State.Events.FirstOrDefault(entry => entry.Id == _focusedEventId) is { } entry)
                    Navigation.FocusEvent(entry);
                break;
            default: Navigation.OpenInspector(request); break;
        }
    }

    private async Task InitializeAsync()
    {
        try
        {
            if (await Storage.LoadAsync(() => true) is { } json)
            {
                State.Engine = WorldEngine.ImportJson(json);
                Map.Engine = State.Engine;
                _simulation.ClearActivity();
                Storage.MarkLoaded(State.Engine);
                SetStatus("已恢复本机世界，每 30 秒自动保存");
            }
            else
                SetStatus("新世界已诞生，可选择工具创造或观察文明演化");
        }
        catch (Exception ex)
        {
            Storage.AllowAutosave = false;
            SetStatus($"本机存档未载入，已暂停自动保存：{FriendlyError(ex)}");
        }

        IsReady = true;
        Storage.Ready = true;
        Storage.LastSave = _clock.Elapsed.TotalSeconds;
        Map.RefreshWorld(true);
        RefreshUi(true);
    }

    /// <summary>推进到期的模拟日，刷新界面并调度保存。</summary>
    internal void Tick()
    {
        var now = _clock.Elapsed.TotalSeconds;
        var elapsed = Math.Clamp(now - _previousTime, 0, .25);
        _previousTime = now;
        if (!IsReady)
            return;
        var hidden = Storage.IsBackground;
        var unlimited = State.Speed == UnlimitedSpeed;
        Map.IsSimulationPaused = WorldTimeStopped;
        Map.SimulationTickDurationSeconds = unlimited ? .016 : SimulationTickSeconds / State.Speed;
        if (hidden)
        {
            _simulation.ClearPendingTime();
            if (!_wasBackground)
            {
                _wasBackground = true;
                _ = Storage.SaveAsync(false);
            }

            ScheduleNextTick();
            return;
        }

        _wasBackground = false;
        if (!WorldTimeStopped)
        {
            var count = _simulation.Advance(State.Engine, State.Speed, elapsed);
            if (count > 0)
            {
                Map.SimulationTickFraction = _simulation.TickFraction;
                var mapStarted = Stopwatch.GetTimestamp();
                Map.RefreshWorld(deferAnimation: State.Speed == 5 || unlimited);
                _simulation.LastMapRefreshMilliseconds = Stopwatch.GetElapsedTime(mapStarted).TotalMilliseconds;
            }

            RefreshTimeState();
        }

        if (now - _lastPerformanceUpdate >= .25)
        {
            _lastPerformanceUpdate = now;
            RefreshPerformanceStats();
        }

        if (now - _lastUi > .7)
        {
            _lastUi = now;
            RefreshUi();
        }

        if (now - Storage.LastSave > 30 && !Storage.IsSaving && !Dialogs.IsVisible && !Interaction.IsPickingLocation && !EditCaptureActive)
        {
            Storage.LastSave = now;
            _ = Storage.SaveAsync(false);
        }

        ScheduleNextTick();
    }

    // 回调结束后才等待，将本轮工作耗时计入下一模拟日的期限。
    internal double NextDelayMilliseconds => _simulation.NextDelay(State.Speed,
        !IsReady || WorldTimeStopped || _wasBackground, (_clock.Elapsed.TotalSeconds - _previousTime) * 1000);
    private void ScheduleNextTick() => ScheduleChanged?.Invoke();

    internal void TogglePause()
    {
        if (EditCaptureActive || Dialogs.IsVisible || Interaction.IsPickingLocation)
            return;
        RefreshTimeState();
        State.PauseCommand.Execute(null);
    }

    internal void RestoreCheckpoint()
    {
        if (Interaction.IsPickingLocation || EditCaptureActive)
            return;
        if (Edits.Checkpoint is null)
        {
            SetStatus("暂无可撤销的编辑。开始绘制时会保存恢复点，继续模拟后清除。");
            return;
        }

        Storage.Capture?.Cancel();
        Dialogs.CloseModal();
        State.Engine = WorldEngine.FromSnapshot(Edits.Checkpoint.Value);
        State.ResetWorldPresentation();
        Navigation.ClearObservation();
        _simulation.ClearActivity();
        Edits.Checkpoint = null;
        State.IsPaused = true;
        Navigation.ClearMapSelection();
        Inspector.ResetInfrastructureFilters();
        State.SelectedTile = null;
        State.SelectedNationId = 0;
        State.SelectedResidentId = 0;
        State.InspectorMode = "overview";
        Navigation.ClearHistory();
        Inspector.InvalidateInspector();
        Map.Engine = State.Engine;
        Map.IsSimulationPaused = true;
        Map.RefreshWorld();
        Tools.UpdateToolContext();
        RefreshUi(true);
        SetStatus("已恢复到本轮编辑之前");
    }

    internal void UpdateUndoButtons()
    {
        var available = Edits.Checkpoint is not null && !EditCaptureActive && !Interaction.IsPickingLocation;
        State.CanUndo = available;
    }

    internal void RefreshPerformanceStats()
    {
        if (!State.ShowPerformance)
            return;
        var timestamp = Stopwatch.GetTimestamp();
        var text =
            $"FPS {Map.GetRecentFrameCount(timestamp)}\n{_simulation.DaysPerSecond(timestamp):0.##} 日/秒";
        State.PerformanceText = text;
    }

    internal void RefreshUi(bool force = false)
    {
        RefreshPerformanceStats();
        State.RefreshWorld();
        RefreshTimeState();
        UpdateUndoButtons();
        var spotlight = Inspector.SelectSpotlight();
        _focusedEventId = spotlight?.Latest.Id ?? 0;
        State.ShowEvent(spotlight);
        if (State.InspectorVisible || force)
            Inspector.RefreshInspector(force);
        Navigation.RefreshSelectionSummary();
    }

    internal void ReplaceWorld(WorldEngine engine)
    {
        Storage.Capture?.Cancel();
        Dialogs.CloseModal();
        Navigation.ClearMapSelection();
        Inspector.ResetInfrastructureFilters();
        Edits.Checkpoint ??= State.Engine.State;
        State.Engine = engine;
        State.ResetWorldPresentation();
        Navigation.ClearObservation();
        State.IsPaused = true;
        State.SelectedTile = null;
        State.SelectedNationId = 0;
        _simulation.Reset();
        Storage.AllowAutosave = true;
        State.InspectorMode = "overview";
        State.SelectedResidentId = 0;
        Navigation.ClearHistory();
        Inspector._watched.Clear();
        Inspector._historyWatchedOnly = false;
        Inspector._eventDetailId = 0;
        Inspector.InvalidateInspector();
        Map.Engine = engine;
        Map.IsSimulationPaused = true;
        Map.RefreshWorld(true);
        Tools.UpdateToolContext();
        RefreshUi(true);
    }
    private (InspectorViewModel Inspector, WorldEditorViewModel Editors) CreatePresentationModels()
    {
        var requests = new PresentationRequests
        {
            State = State,
            Map = Map,
            Storage = Storage,
            CanSubmitEdit = () => Edits.CanSubmit,
            SubmitEdit = SubmitEditAsync,
            RunEdit = RunEdit,
            ModalVisible = () => Dialogs.IsVisible,
            ModalGeneration = () => Dialogs.Generation,
            PickingLocation = () => Interaction.IsPickingLocation,
            OpenModal = Dialogs.OpenModal,
            CloseModal = Dialogs.CloseModal,
            CloseInspector = Navigation.CloseInspector,
            RefreshPerformanceStats = RefreshPerformanceStats,
            RefreshUi = RefreshUi,
            SetStatus = SetStatus,
            ApplyLayout = Layout.Refresh,
            RememberLocation = Navigation.RememberLocation,
            OpenInspector = Navigation.OpenInspector,
            OpenResident = Navigation.OpenResident,
            OpenNation = Navigation.OpenNation,
            OpenSettlement = Navigation.OpenSettlement,
            OpenTile = Navigation.OpenTile,
            FocusMapTile = Navigation.FocusMapTile,
            FocusMapResident = Navigation.FocusMapResident,
            OpenBuilding = Navigation.OpenBuilding,
            FocusEvent = Navigation.FocusEvent,
            SetCategory = Tools.SetCategory,
            StartMapPick = Interaction.StartMapPick,
            ReplaceWorld = ReplaceWorld,
            InspectorContentChanged = content =>
            {
                State.InspectorContent = content;
                Navigation.ScrollOffset = default;
            },
            TerritoryEdit = Tools.EditTerritory,
        };
        requests.Inspector = new InspectorViewModel(requests);
        requests.Editors = new WorldEditorViewModel(requests);
        return (requests.Inspector, requests.Editors);
    }
}
