using static SeWZC.WorldBox.UI.ViewModels.WorldPresentationModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;

namespace SeWZC.WorldBox.UI;

/// <summary>桌面与浏览器共用的游戏主界面。</summary>
public sealed partial class MainView : UserControl
{
    private const double SimulationTickSeconds = Services.SimulationScheduler.TickSeconds;
    private const int UnlimitedSpeed = 0;
    private static readonly IBrush Ink = Brush.Parse("#111E29");
    private static readonly IBrush Panel = Brush.Parse("#172632");
    private static readonly IBrush Line = Brush.Parse("#2A3C46");
    private static readonly IBrush Muted = Brush.Parse("#8FA8AE");
    private static readonly IBrush Mint = Brush.Parse("#B8E9BC");
    private readonly Grid _body;
    private readonly StackPanel _cameraBar;
    private readonly WrapPanel _bridgeSettings;
    private readonly ComboBox _brushPicker;
    private readonly ComboBox _buildMode;
    private readonly Border _inspector;
    private readonly ScrollViewer _inspectorScroll;
    private readonly WorldMapControl _map;
    private readonly Border _mapPickBar;
    private readonly Border _modal;
    private readonly Border _placementBar;
    private readonly TextBlock _placementText;
    private readonly Grid _shell;
    private readonly Views.GameHud _hud;
    private readonly Border _toolBar;
    private readonly ScrollViewer _toolScroll;
    private readonly UniformGrid _toolChoices;
    private readonly ComboBox _toolContext;
    private readonly TextBlock _toolHint;
    private readonly TextBlock _toolTitle;
    private readonly Button _toolPrevious;
    private readonly Button _toolNext;
    private readonly TextBlock _toolPageLabel;
    private readonly Flyout _worldMenuFlyout;
    private readonly List<Button> _categoryButtons = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Stack<InspectorLocation> _navigation = new();
    private readonly ViewModels.MainViewModel _viewModel;
    private readonly Services.WorldSaveCoordinator _storage;
    private readonly ViewModels.InspectorViewModel _inspectorModel;
    private readonly ViewModels.WorldEditorViewModel _editorModel;
    private readonly Services.SimulationScheduler _simulation = new();
    private readonly Services.WorldEditCoordinator _edits;
    private readonly MapTool?[] _slotTools = new MapTool?[8];
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly TextBlock[] _toolLabels = new TextBlock[8];
    private readonly Button[] _toolSlots = new Button[8];
    private readonly Border[] _toolSwatches = new Border[8];
    private readonly List<(MapTool Tool, Button Button)> _tools = [];
    private ToolCategory _category = ToolCategory.Terrain;
    private WorldState? _checkpoint { get => _edits.Checkpoint; set => _edits.Checkpoint = value; }
    private int[] _constructionTowns = [];
    private int _focusedEventId;
    private Task? _initialization;
    private bool _isCompact;
    private ViewModels.Details.TextNode? _modalFeedback;
    private int _modalGeneration;
    private bool _modalHasPrimary;
    private bool _ready, _wasBackground;
    private double _previousTime, _lastUi, _lastPerformanceUpdate;

    private bool _updatingToolContext;
    private bool _allowAutosave { get => _storage.AllowAutosave; set => _storage.AllowAutosave = value; }
    private bool _saving => _storage.IsSaving;
    private CancellationTokenSource? _saveCapture => _storage.Capture;
    private long _worldEditRevision { get => _storage.EditRevision; set => _storage.EditRevision = value; }
    private double _lastSave { get => _storage.LastSave; set => _storage.LastSave = value; }

    /// <summary>初始化游戏主界面。</summary>
    public MainView() : this(null)
    {
    }

    /// <summary>初始化游戏主界面。</summary>
    /// <param name="engine">初始世界引擎，空值时创建默认示例世界。</param>
    public MainView(WorldEngine? engine)
    {
        _viewModel = Design.IsDesignMode ? ViewModels.DesignData.Main
            : new ViewModels.MainViewModel(engine ?? WorldEngine.Create(73921));
        _storage = new Services.WorldSaveCoordinator(() => _engine, () => App.Storage, () => _clock.Elapsed.TotalSeconds);
        if (!Design.IsDesignMode)
            DataContext = _viewModel;
        AvaloniaXamlLoader.Load(this);
        _body = this.FindControl<Grid>("Body")!;
        _cameraBar = this.FindControl<StackPanel>("CameraBar")!;
        _bridgeSettings = this.FindControl<WrapPanel>("BridgeSettings")!;
        _brushPicker = this.FindControl<ComboBox>("BrushPicker")!;
        _buildMode = this.FindControl<ComboBox>("BuildMode")!;
        _inspector = this.FindControl<Border>("Inspector")!;
        _inspectorScroll = this.FindControl<ScrollViewer>("InspectorScroll")!;
        _map = this.FindControl<WorldMapControl>("Map")!;
        _mapPickBar = this.FindControl<Border>("MapPickBar")!;
        _modal = this.FindControl<Border>("Modal")!;
        _placementBar = this.FindControl<Border>("PlacementBar")!;
        _placementText = this.FindControl<TextBlock>("PlacementText")!;
        _shell = this.FindControl<Grid>("Shell")!;
        _hud = this.FindControl<Views.GameHud>("Hud")!;
        _toolBar = this.FindControl<Border>("ToolBar")!;
        _toolScroll = this.FindControl<ScrollViewer>("ToolScroll")!;
        _toolChoices = this.FindControl<UniformGrid>("ToolChoices")!;
        _toolContext = this.FindControl<ComboBox>("ToolContext")!;
        _toolHint = this.FindControl<TextBlock>("ToolHint")!;
        _toolTitle = this.FindControl<TextBlock>("ToolTitle")!;
        _toolPrevious = this.FindControl<Button>("ToolPrevious")!;
        _toolNext = this.FindControl<Button>("ToolNext")!;
        _toolPageLabel = this.FindControl<TextBlock>("ToolPageLabel")!;
        _worldMenuFlyout = (Flyout)Resources["WorldMenuFlyout"]!;
        _edits = new Services.WorldEditCoordinator(_viewModel, _storage,
            () => new Services.EditSession(_modalGeneration, _modal.IsVisible, _inspectorNavigationGeneration, _mapPick is not null));
        _edits.StateChanged += () =>
        {
            _map.IsSimulationPaused = WorldTimeStopped;
            UpdateUndoButtons();
        };
        _edits.Completed += () =>
        {
            _previousTime = _clock.Elapsed.TotalSeconds;
            _map.IsSimulationPaused = WorldTimeStopped;
            RefreshUi(true);
        };
        _edits.Feedback += SetStatus;
        (_inspectorModel, _editorModel) = CreatePresentationModels();
        InitializeViewControls();
        _storage.Feedback += SetStatus;
        _storage.TimeAnchorChanged += () => _previousTime = _clock.Elapsed.TotalSeconds;
        _storage.CaptureChanged += () =>
        {
            _map.IsSimulationPaused = WorldTimeStopped;
            RefreshTimeState();
        };
        _viewModel.ViewRequested += HandleViewRequest;
        _viewModel.PauseChanged += () =>
        {
            if (!_paused)
                _checkpoint = null;
            _map.IsSimulationPaused = WorldTimeStopped;
            RefreshUi(true);
        };
        _viewModel.SpeedChanged += (previous, speed) =>
        {
            if (previous == UnlimitedSpeed || speed == UnlimitedSpeed)
            {
                _simulation.ClearPendingTime();
                _previousTime = _clock.Elapsed.TotalSeconds;
            }
            _map.SimulationTickDurationSeconds = speed == UnlimitedSpeed ? .016 : SimulationTickSeconds / speed;
            UpdateSpeedButtons();
            RefreshTimeState();
        };
        _map.Engine = _engine;
        _map.PrepareWorldEdit = PrepareEditAsync;
        _map.WorldMutationStarting += (_, _) =>
        {
            _saveCapture?.Cancel();
            _worldEditRevision++;
        };
        _map.WorldEdited += (_, _) =>
        {
            DeferAutosaveAfterEdit();
            RefreshUi(true);
            SetStatus("世界已更新并暂停，可撤销本轮编辑");
        };
        _map.TileSelected += (x, y) =>
        {
            if (_mapPick is not null)
                FinishMapPick(x, y);
            else
                SelectMapObject("tile", x: x, y: y);
        };
        _map.ResidentSelected += id => SelectMapObject("resident", id);
        _map.BuildingSelected += id =>
        {
            var b = _engine.State.Society.Buildings.First(building => building.Id == id);
            SelectMapObject("building", id, b.X, b.Y);
        };
        _map.ToolError += message => SetStatus("工具未应用：" + message);

        SizeChanged += (_, _) => ApplyLayout();
        _body.SizeChanged += (_, _) => ApplyLayout();
        _map.SizeChanged += (_, _) => UpdateToolBarSize();
        KeyDown += OnKeyDown;
        _timer.Tick += OnTick;
        AttachedToVisualTree += async (_, _) =>
        {
            if (Design.IsDesignMode)
                return;
            await (_initialization ??= InitializeAsync());
            _previousTime = _clock.Elapsed.TotalSeconds;
            _timer.Start();
        };
        DetachedFromVisualTree += (_, _) => _timer.Stop();
        SetCategory(ToolCategory.Terrain);
        _toolsOpen = false;
        _shell.IsEnabled = Design.IsDesignMode;
        ApplyLayout();
        UpdateSpeedButtons();
        if (!Design.IsDesignMode)
            RefreshUi(true);
    }

}
