using System.ComponentModel;
using System.Runtime.CompilerServices;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI.ViewModels;

/// <summary>游戏主界面的可观察状态及命令，不持有控件或平台存储。</summary>
internal sealed class MainViewModel : INotifyPropertyChanged
{
    private bool _isPaused, _inspectorVisible, _toolsVisible, _canUndo;
    private int _speed = 1, _dismissedEventId;
    private string _worldDate = "", _timeHold = "", _selectionTitle = "", _selectionDescription = "";
    private string _inspectorTitle = "世界概览", _performanceText = "FPS 0\n0 日/秒";
    private bool _inspectorExpanded, _showPerformance = true;
    private Details.VisualNode? _inspectorContent;
    private string _eventTitle = "", _eventText = "", _feedback = "正在载入世界…";
    private bool _hasSelection, _hasEvent, _placementPending, _canChangeTime = true;
    private int _eventId;
    private bool _limited, _pickingLocation, _canNavigate = true, _canReturnObservation, _isCompactHud, _isShortHud;

    internal MainViewModel(WorldEngine engine)
    {
        Engine = engine;
        PauseCommand = new UiCommand(_ =>
        {
            IsPaused = !IsPaused;
            PauseChanged?.Invoke();
        }, () => CanChangeTime);
        SpeedCommand = new UiCommand(value => Speed = Convert.ToInt32(value), () => CanChangeTime);
        NavigateCommand = new UiCommand(value => ViewRequested?.Invoke((string)value!), () => _canNavigate);
        UndoCommand = new UiCommand(_ => ViewRequested?.Invoke("undo"), () => CanUndo);
        DismissEventCommand = new UiCommand(_ =>
        {
            _dismissedEventId = _eventId;
            HasEvent = false;
        });
        RefreshWorld();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    internal event Action? PauseChanged;
    internal event Action<int, int>? SpeedChanged;
    internal event Action<string>? ViewRequested;

    internal WorldEngine Engine { get; set; }
    internal int SelectedResidentId { get; set; }
    internal int SelectedNationId { get; set; }
    internal int SelectedBuildingId { get; set; }
    internal (int X, int Y)? SelectedTile { get; set; }
    internal string? SelectionKind { get; set; }
    internal string InspectorMode { get; set; } = "overview";

    public UiCommand PauseCommand { get; }
    public UiCommand SpeedCommand { get; }
    public UiCommand NavigateCommand { get; }
    public UiCommand UndoCommand { get; }
    public UiCommand DismissEventCommand { get; }
    public string WorldDate { get => _worldDate; private set => Set(ref _worldDate, value); }
    public string TimeState => _timeHold.Length > 0 ? _timeHold : IsPaused ? "已暂停"
        : _limited ? "运行中（设备限速）" : Speed == 0 ? "运行中（不限速）" : $"运行中（{Speed} 倍）";
    public string PauseLabel => IsPaused ? "继续" : "暂停";
    public string InspectorTitle { get => _inspectorTitle; internal set => Set(ref _inspectorTitle, value); }
    public Details.VisualNode? InspectorContent { get => _inspectorContent; internal set => Set(ref _inspectorContent, value); }
    public string PerformanceText { get => _performanceText; internal set => Set(ref _performanceText, value); }
    public bool ShowPerformance { get => _showPerformance; internal set => Set(ref _showPerformance, value); }
    public bool InspectorExpanded
    {
        get => _inspectorExpanded;
        internal set
        {
            if (Set(ref _inspectorExpanded, value))
                Notify(nameof(InspectorExpandLabel));
        }
    }
    public string InspectorExpandLabel => InspectorExpanded ? "收起" : "展开";
    public string Viewpoint => "上帝视角";
    public string SelectionTitle { get => _selectionTitle; private set => Set(ref _selectionTitle, value); }
    public string SelectionDescription { get => _selectionDescription; private set => Set(ref _selectionDescription, value); }
    public bool SelectionDescriptionVisible => !IsShortHud;
    public string EventTitle { get => _eventTitle; private set => Set(ref _eventTitle, value); }
    public string EventText { get => _eventText; private set => Set(ref _eventText, value); }
    public string Feedback { get => _feedback; set => Set(ref _feedback, value); }
    public bool HasEvent
    {
        get => _hasEvent;
        private set
        {
            if (Set(ref _hasEvent, value))
                Notify(nameof(EventVisible));
        }
    }
    public bool EventVisible => HasEvent && !PlacementPending && !_pickingLocation
        && !IsShortHud && !(IsCompactHud && (InspectorVisible || ToolsVisible));
    public bool PlacementPending
    {
        get => _placementPending;
        internal set
        {
            if (Set(ref _placementPending, value))
                Notify(nameof(EventVisible));
        }
    }
    public bool IsCompactHud
    {
        get => _isCompactHud;
        internal set
        {
            if (Set(ref _isCompactHud, value))
                Notify(nameof(EventVisible));
        }
    }
    public bool IsShortHud
    {
        get => _isShortHud;
        internal set
        {
            if (Set(ref _isShortHud, value))
                Notify(nameof(EventVisible), nameof(SelectionDescriptionVisible));
        }
    }
    public bool SelectionVisible => _hasSelection && !InspectorVisible && !ToolsVisible && !_pickingLocation;
    public bool CanChangeTime => _canChangeTime;
    public bool IsNormalSpeed => Speed == 1;
    public bool IsDoubleSpeed => Speed == 2;
    public bool IsFastSpeed => Speed == 5;
    public bool IsUnlimitedSpeed => Speed == 0;
    public bool CanReturnObservation { get => _canReturnObservation; internal set => Set(ref _canReturnObservation, value); }

    public bool IsPaused
    {
        get => _isPaused;
        internal set
        {
            if (Set(ref _isPaused, value))
                Notify(nameof(PauseLabel), nameof(TimeState));
        }
    }

    public int Speed
    {
        get => _speed;
        private set
        {
            var previous = _speed;
            if (!Set(ref _speed, value))
                return;
            Notify(nameof(IsNormalSpeed), nameof(IsDoubleSpeed), nameof(IsFastSpeed), nameof(IsUnlimitedSpeed), nameof(TimeState));
            SpeedChanged?.Invoke(previous, value);
        }
    }

    public bool InspectorVisible
    {
        get => _inspectorVisible;
        internal set
        {
            if (Set(ref _inspectorVisible, value))
                Notify(nameof(SelectionVisible), nameof(EventVisible));
        }
    }

    public bool ToolsVisible
    {
        get => _toolsVisible;
        internal set
        {
            if (Set(ref _toolsVisible, value))
                Notify(nameof(SelectionVisible), nameof(EventVisible));
        }
    }

    public bool CanUndo
    {
        get => _canUndo;
        internal set
        {
            if (Set(ref _canUndo, value))
            {
                UndoCommand.Refresh();
                Notify(nameof(UndoHint));
            }
        }
    }

    public string UndoHint => CanUndo ? "恢复到本轮编辑之前；继续模拟后清除恢复点" : "应用修改后可撤销；继续模拟后清除恢复点";

    internal void RefreshWorld()
    {
        WorldDate = GameText.Date(Engine.State.Tick);
    }

    internal void RefreshTime(bool modal, bool mapPick, bool editing, bool saving, bool background, bool limited)
    {
        var available = !modal && !mapPick && !editing && !saving && !background;
        if (Set(ref _canChangeTime, available, nameof(CanChangeTime)))
        {
            PauseCommand.Refresh();
            SpeedCommand.Refresh();
        }
        if (Set(ref _pickingLocation, mapPick))
            Notify(nameof(SelectionVisible), nameof(EventVisible));
        if (Set(ref _canNavigate, !mapPick && !editing))
            NavigateCommand.Refresh();
        var hold = mapPick ? "选取位置，时间暂时停止"
            : editing ? "准备编辑，时间暂时停止"
            : modal ? "窗口操作，时间暂时停止"
            : saving ? "正在保存，时间暂时停止"
            : background ? "后台停表" : "";
        if (Set(ref _timeHold, hold) | Set(ref _limited, limited))
            Notify(nameof(TimeState));
    }

    internal bool RefreshSelection()
    {
        var resident = SelectionKind == "resident"
            ? Engine.State.Residents.FirstOrDefault(person => person.Id == SelectedResidentId) : null;
        var building = SelectionKind == "building"
            ? Engine.State.Society.Buildings.FirstOrDefault(item => item.Id == SelectedBuildingId) : null;
        var tile = SelectedTile is { } point ? Engine.State.Tiles[point.Y * Engine.State.Width + point.X] : null;
        var valid = SelectionKind switch
        {
            "resident" => resident is not null,
            "building" => building is not null,
            "tile" => tile is not null,
            _ => false,
        };
        SelectionTitle = resident is not null ? $"{resident.Name}   生命 {resident.Health:0.#}/100"
            : building is not null ? $"{WorldEngine.BuildingName(building.Kind)}   完好 {building.Health:0.#}/100"
            : tile is not null ? $"{GameText.Terrain(tile.Terrain)}   资源 {tile.ResourceAmount:0.#}/{WorldEngine.NaturalResourceCapacity(tile):0.#}" : "";
        SelectionDescription = resident is not null ? ResidentActionBrief(resident.Id)
            : building is not null ? !building.IsCompleted ? $"施工 {building.ConstructionProgress / building.ConstructionRequired:P0}"
                : !building.Enabled ? "已停用" : Engine.GetProductionStatus(building.Id).Split('\n')[0]
            : tile is not null && SelectedTile is { } location ? $"地格（{location.X}, {location.Y}）" : "";
        if (_hasSelection != valid)
        {
            _hasSelection = valid;
            Notify(nameof(SelectionVisible));
        }
        return valid;
    }

    private string ResidentActionBrief(int id)
    {
        var lines = Engine.GetResidentActionSummary(id).Split('\n');
        // 摘要保留当前行动；任务目标、后续安排和行动依据在详情中展示。
        return lines.FirstOrDefault(line => line.StartsWith("当前活动：", StringComparison.Ordinal)
            || line.StartsWith("当前劳作：", StringComparison.Ordinal)) ?? lines[0];
    }

    internal void ShowEvent(EventGroup? group)
    {
        _eventId = group?.Latest.Id ?? 0;
        EventTitle = group is null ? "" : $"{GameText.Date(group.Latest.Tick)}";
        EventText = group is null ? "" : group.Latest.Message + (group.Count > 1 ? $"（同类 {group.Count} 次）" : "");
        HasEvent = _eventId != 0 && _eventId != _dismissedEventId;
    }

    internal void ResetWorldPresentation()
    {
        _dismissedEventId = 0;
        CanReturnObservation = false;
        ShowEvent(null);
        SelectionKind = null;
        SelectedResidentId = SelectedBuildingId = SelectedNationId = 0;
        SelectedTile = null;
        RefreshSelection();
        RefreshWorld();
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    private void Notify(params string[] names)
    {
        foreach (var name in names)
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
