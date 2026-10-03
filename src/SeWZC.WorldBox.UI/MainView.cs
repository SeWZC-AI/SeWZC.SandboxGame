using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Automation;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView : UserControl
{
    private static readonly IBrush Ink = Brush.Parse("#111E29");
    private static readonly IBrush Panel = Brush.Parse("#172632");
    private static readonly IBrush Line = Brush.Parse("#2A3C46");
    private static readonly IBrush Muted = Brush.Parse("#8FA8AE");
    private static readonly IBrush Mint = Brush.Parse("#B8E9BC");
    private WorldEngine _engine;
    private readonly WorldMapControl _map = new();
    private readonly Grid _body = new();
    private readonly Border _rail = new();
    private readonly Border _inspector = new();
    private readonly ScrollViewer _inspectorScroll = new();
    private readonly StackPanel _inspectorNavigation = new() { Margin = new Thickness(8, 4), Spacing = 4 };
    private readonly UniformGrid _toolChoices = new() { Columns = 8, Rows = 1 };
    private readonly Border _toolBar = new();
    private readonly Border _timeStatus = new();
    private readonly ComboBox _brushPicker = new() { Width = 100, MinHeight = 36, FontSize = 11 };
    private readonly ComboBox _buildMode = new() { Width = 132, MinHeight = 36, FontSize = 11, ItemsSource = new[] { "直接赐予", "居民施工" }, SelectedIndex = 0 };
    private readonly TextBlock _eventText = Text("选择一个文明，观察它的发展", 11, Mint);
    private readonly TextBlock _placementText = Text("", 12);
    private readonly Border _placementBar = new();
    private bool _toolsOpen;
    private int _focusedEventId;
    private readonly Stack<(string Mode, int Nation, int Resident, int Town, (int X, int Y)? Tile, int Building, int Event, Vector Scroll)> _navigation = new();
    private readonly ComboBox _toolContext = new() { Width = 112, MinHeight = 36, FontSize = 10 };
    private bool _updatingToolContext;
    private int[] _constructionTowns = [];
    private readonly List<Button> _categoryButtons = [];
    private readonly Button[] _toolSlots = new Button[8];
    private readonly Border[] _toolSwatches = new Border[8];
    private readonly TextBlock[] _toolLabels = new TextBlock[8];
    private readonly string?[] _slotTools = new string?[8];
    private readonly TextBlock _toolTitle = Text("塑造山海", 13, Mint);
    private readonly TextBlock _toolHint = Text("选择一种地形，在世界上绘制", 11, Muted);
    private readonly TextBlock _worldTitle = Text("晨曦群岛", 22);
    private readonly TextBlock _worldSubtitle = Text("", 11, Muted);
    private readonly TextBlock _date = Text("", 13);
    private readonly TextBlock _population = Text("", 13, Mint);
    private readonly TextBlock _status = Text("正在唤醒世界…", 11, Muted);
    private readonly TextBlock _version = Text("众生纪元  v0.3", 10, Muted);
    private readonly TextBlock _simulationStatus = Text("世界正在演化", 11, Mint);
    private readonly Button _play;
    private TextBlock? _modalFeedback;
    private bool _modalHasPrimary;
    private readonly Border _modal = new() { IsVisible = false, Background = Brush.Parse("#BD071118"), ZIndex = 100 };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly List<(string Tool, Button Button)> _tools = [];
    private readonly List<(int Speed, Button Button)> _speeds = [];
    private double _previousTime, _accumulator, _lastUi, _lastSave;
    private bool _paused, _ready, _saving, _wasBackground, _mobilePanel;
    private bool _allowAutosave = true;
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private Task? _initialization;
    private Control? _shell;
    private int _speed = 1, _selectedNationId, _selectedResidentId;
    private int _modalGeneration;
    private string _category = "terrain", _inspectorMode = "overview";
    private (int X, int Y)? _selectedTile;
    private string? _checkpoint;
    private bool _isCompact;
    private readonly Control _headerStats;
    private readonly TextBlock _brandName = Text("SeWZC. WORLDBOX", 15, null, true);
    private readonly TextBlock _brandCaption = Text("众生与山海  /  ANCIENT WORLDS", 9, Muted);

    public MainView() : this(null) { }

    public MainView(WorldEngine? engine)
    {
        _engine = engine ?? WorldEngine.Create(73921, 256, 256, true);
        Background = Ink;
        Focusable = true;
        _map.Engine = _engine;
        _map.WorldEditing += (_, _) => BeginEdit();
        _map.WorldEdited += (_, _) => { RefreshUi(true); SetStatus("世界已更新并暂停，可撤销本轮编辑"); };
        _map.TileSelected += (x, y) => { if (_mapPick is not null) FinishMapPick(x, y); else SelectMapObject("tile", x: x, y: y); };
        _map.ResidentSelected += id => SelectMapObject("resident", id);
        _map.BuildingSelected += id => { var b = _engine.State.Society.Buildings.First(building => building.Id == id); SelectMapObject("building", id, b.X, b.Y); };
        _map.ToolError += message => SetStatus("工具未应用：" + message);

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(8, 0) };
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center };
        var logo = new Grid { Width = 28, Height = 28, ColumnDefinitions = new ColumnDefinitions("*,*"), RowDefinitions = new RowDefinitions("*,*") };
        var colors = new[] { "#B8E9BC", "#E4C889", "#558F88", "#88BBA0" };
        for (var i = 0; i < 4; i++) { var square = new Border { Background = Brush.Parse(colors[i]), Margin = new Thickness(1) }; Grid.SetColumn(square, i % 2); Grid.SetRow(square, i / 2); logo.Children.Add(square); }
        brand.Children.Add(logo);
        var brandText = new StackPanel { Spacing = 1 };
        brandText.Children.Add(_brandName);
        brandText.Children.Add(_brandCaption);
        brand.Children.Add(brandText); header.Children.Add(brand);
        var stats = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        stats.Children.Add(_date); stats.Children.Add(_population); _headerStats = stats;
        Grid.SetColumn(stats, 1); header.Children.Add(stats);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, VerticalAlignment = VerticalAlignment.Center };
        actions.Children.Add(Button("新世界", ShowNewWorld, "创建一片新的大陆"));
        actions.Children.Add(Button("存档", ShowStorage, "保存、导出或导入世界"));
        actions.Children.Add(Button("概览", () => { if (_mobilePanel && _inspectorMode == "overview") CloseInspector(); else OpenInspector("overview"); }));
        actions.Children.Add(Named(Button("规则", ShowRules), "header-rules"));
        Grid.SetColumn(actions, 2); header.Children.Add(actions);

        _body.ColumnDefinitions = new ColumnDefinitions("0,*,0");
        var mapLayer = new Grid { ClipToBounds = true, Background = Brush.Parse("#122D3D") };
        mapLayer.Children.Add(_map);
        var camera = new StackPanel { Spacing = 3, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(6) };
        camera.Children.Add(IconButton("plus", () => _map.ZoomIn(), "放大地图", "map-zoom-in"));
        camera.Children.Add(IconButton("minus", () => _map.ZoomOut(), "缩小地图", "map-zoom-out"));
        camera.Children.Add(Named(Button("全图", () => _map.FitWorld(), "显示整个世界", 40), "map-fit"));
        mapLayer.Children.Add(camera);
        var bottom = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(6, 4) };
        var toolsPanel = new StackPanel { Spacing = 4 };
        var categories = new UniformGrid { Columns = 4, Rows = 1 };
        foreach (var (label, category) in new[] { ("山海", "terrain"), ("众生", "life"), ("天灾", "disaster"), ("建设", "build") })
        {
            var categoryButton = Named(Button(label, () => SetCategory(category)), "tool-category-" + category);
            categoryButton.Tag = category; categoryButton.Padding = new Thickness(3, 7); categoryButton.Margin = new Thickness(2, 0);
            _categoryButtons.Add(categoryButton); categories.Children.Add(categoryButton);
        }
        Named(_brushPicker, "brush-size");
        _brushPicker.SelectionChanged += (_, _) =>
        {
            var index = Math.Max(0, _brushPicker.SelectedIndex);
            if (_category == "life") _map.SpawnCount = new[] { 1, 12, 36 }[index];
            else if (_category == "disaster") _map.DisasterRadius = new[] { 2, 5, 10 }[index];
            else _map.BrushRadius = new[] { 2, 5, 10 }[index];
        };
        Named(_buildMode, "build-mode"); _buildMode.SelectionChanged += (_, _) => _map.GiftBuildings = _buildMode.SelectedIndex == 0;
        toolsPanel.Children.Add(categories);
        for (var i = 0; i < 8; i++)
        {
            var slot = i;
            var swatch = new Border { Width = 22, Height = 15, CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Center };
            var label = Text("—", 10); label.HorizontalAlignment = HorizontalAlignment.Center;
            var content = new StackPanel { Spacing = 3 }; content.Children.Add(swatch); content.Children.Add(label);
            var button = Named(new Button { Content = content, HorizontalAlignment = HorizontalAlignment.Stretch, Height = 44, Padding = new Thickness(3, 3), Margin = new Thickness(2), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), HorizontalContentAlignment = HorizontalAlignment.Center }, $"tool-slot-{i}");
            button.Click += (_, _) => { if (_slotTools[slot] is { } tool) SelectTool(tool); };
            _toolSlots[i] = button; _toolSwatches[i] = swatch; _toolLabels[i] = label; _toolChoices.Children.Add(button);
        }
        toolsPanel.Children.Add(_toolChoices);
        var pagination = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Height = 32, HorizontalAlignment = HorizontalAlignment.Center };
        _toolPrevious = Named(Button("上一页", () => { _toolPage--; SetCategory(_category); }), "tool-page-prev");
        _toolNext = Named(Button("下一页", () => { _toolPage++; SetCategory(_category); }), "tool-page-next");
        pagination.Children.Add(_toolPrevious); pagination.Children.Add(_toolPageLabel); pagination.Children.Add(_toolNext); toolsPanel.Children.Add(pagination);
        var settings = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 5, Height = 36 };
        Named(_toolContext, "tool-context"); _toolContext.SelectionChanged += (_, _) => OnToolContextChanged();
        var hint = new StackPanel { Spacing = 2 }; hint.Children.Add(_toolTitle); hint.Children.Add(_toolHint); settings.Children.Add(hint);
        Grid.SetColumn(_toolContext, 1); settings.Children.Add(_toolContext); Grid.SetColumn(_brushPicker, 2); settings.Children.Add(_brushPicker); Grid.SetColumn(_buildMode, 2); settings.Children.Add(_buildMode); toolsPanel.Children.Add(settings);
        var toolBox = _toolBar;
        toolBox.Child = toolsPanel; toolBox.Background = Brush.Parse("#F0172632"); toolBox.BorderBrush = Line; toolBox.BorderThickness = new Thickness(1);
        toolBox.CornerRadius = new CornerRadius(12); toolBox.Padding = new Thickness(6); toolBox.Width = 650;
        bottom.Children.Add(toolBox);
        var placement = new StackPanel { Spacing = 4 };
        _placementText.TextWrapping = TextWrapping.Wrap; placement.Children.Add(_placementText);
        var placementActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        placementActions.Children.Add(Named(Button("确认放置", () => _map.ConfirmPlacement()), "placement-confirm"));
        placementActions.Children.Add(Named(Button("取消", () => _map.CancelPlacement()), "placement-cancel"));
        placement.Children.Add(placementActions);
        _placementBar.Child = placement; _placementBar.Background = Panel; _placementBar.Padding = new Thickness(8); _placementBar.IsVisible = false;
        _placementBar.VerticalAlignment = VerticalAlignment.Top; _placementBar.HorizontalAlignment = HorizontalAlignment.Left;
        _placementBar.Margin = new Thickness(8); _placementBar.MaxWidth = 240;
        _placementBar.CornerRadius = new CornerRadius(8); mapLayer.Children.Add(_placementBar);
        _map.PlacementChanged += message => { _placementText.Text = message; _placementBar.IsVisible = _map.HasPendingPlacement; placementActions.IsVisible = _map.HasPendingPlacement; };
        var eventButton = Named(new Button { Content = _eventText, Padding = new Thickness(8, 3), MinHeight = 28,
            HorizontalAlignment = HorizontalAlignment.Stretch, Background = Panel }, "event-spotlight");
        _eventText.TextTrimming = TextTrimming.CharacterEllipsis;
        eventButton.Click += (_, _) => { if (_engine.State.Events.FirstOrDefault(e => e.Id == _focusedEventId) is { } entry) FocusEvent(entry); else OpenInspector("overview"); };
        bottom.Children.Add(BuildSelectionBar());
        bottom.Children.Add(eventButton);
        var timeControls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
        timeControls.Children.Add(Named(Button("工具", ToggleTools, "展开或收起地图工具", 48), "tools-toggle"));
        timeControls.Children.Add(Named(Button("漫游", SuspendTool, "停用当前工具并移动地图", 48), "tool-suspend"));
        _play = Named(Button("暂停", TogglePause, "空格：暂停或继续", 64), "time-toggle"); _play.Background = Mint; _play.Foreground = Ink; _play.Width = 64;
        timeControls.Children.Add(_play);
        foreach (var speed in new[] { 1, 2, 5 })
        {
            var b = Named(Button($"{speed}倍", () => { _speed = speed; _map.SimulationTickDurationSeconds = .2 / speed; UpdateSpeedButtons(); }, minWidth: 42), $"time-speed-{speed}"); b.Width = 42; b.Padding = new Thickness(3, 7);
            _speeds.Add((speed, b)); timeControls.Children.Add(b);
        }
        _timeStatus.Child = _simulationStatus; _timeStatus.Width = 170; _timeStatus.Padding = new Thickness(10, 8); _timeStatus.Background = Brush.Parse("#E3172632"); _timeStatus.CornerRadius = new CornerRadius(7); _timeStatus.VerticalAlignment = VerticalAlignment.Center;
        timeControls.Children.Add(_timeStatus);
        bottom.Children.Add(timeControls); mapLayer.Children.Add(bottom);
        Grid.SetColumn(mapLayer, 1); _body.Children.Add(mapLayer);

        Named(_inspector, "inspector-panel");
        Named(_inspectorScroll, "inspector-scroll");
        _inspectorScroll.HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
        var inspectorLayout = new DockPanel(); DockPanel.SetDock(_inspectorNavigation, Dock.Top);
        inspectorLayout.Children.Add(_inspectorNavigation); inspectorLayout.Children.Add(_inspectorScroll);
        _inspector.Child = inspectorLayout; _inspector.Background = Panel; _inspector.BorderBrush = Line; _inspector.BorderThickness = new Thickness(1, 0, 0, 0);
        Grid.SetColumn(_inspector, 2); _body.Children.Add(_inspector);
        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(8, 0) };
        _status.VerticalAlignment = VerticalAlignment.Center; _status.TextTrimming = TextTrimming.CharacterEllipsis; footer.Children.Add(_status);
        _version.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(_version, 1); footer.Children.Add(_version);
        var shell = new Grid { RowDefinitions = new RowDefinitions("48,*,22") };
        shell.Children.Add(new Border { Child = header, BorderBrush = Line, BorderThickness = new Thickness(0, 0, 0, 1) });
        Grid.SetRow(_body, 1); shell.Children.Add(_body); Grid.SetRow(footer, 2); shell.Children.Add(footer);
        _shell = shell; shell.IsEnabled = false;
        var root = new Grid(); root.Children.Add(shell); root.Children.Add(_modal); Content = root;
        SizeChanged += (_, _) => ApplyLayout();
        _map.SizeChanged += (_, _) => UpdateToolBarSize();
        KeyDown += OnKeyDown;
        _timer.Tick += OnTick;
        AttachedToVisualTree += async (_, _) => { await (_initialization ??= InitializeAsync()); _previousTime = _clock.Elapsed.TotalSeconds; _timer.Start(); };
        DetachedFromVisualTree += (_, _) => _timer.Stop();
        SetCategory("terrain"); _toolsOpen = false; ApplyLayout(); UpdateSpeedButtons(); RefreshUi(true);
    }

    private async Task InitializeAsync()
    {
        try
        {
            if (App.Storage is not null && await App.Storage.LoadAsync() is { } json)
            { _engine = WorldEngine.ImportJson(json); _map.Engine = _engine; SetStatus("已恢复本机世界，每 30 秒自动保存"); }
            else SetStatus("新世界已诞生，可选择工具创造或观察文明演化");
        }
        catch (Exception ex) { _allowAutosave = false; SetStatus($"本机存档未载入，已暂停自动保存：{FriendlyError(ex)}"); }
        _ready = true; if (_shell is not null) _shell.IsEnabled = true;
        _lastSave = _clock.Elapsed.TotalSeconds; _map.RefreshWorld(true); RefreshUi(true);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed.TotalSeconds;
        var elapsed = Math.Clamp(now - _previousTime, 0, .25); _previousTime = now;
        if (!_ready) return;
        var hidden = App.Storage?.IsBackground == true;
        _map.IsSimulationPaused = hidden || _paused || _modal.IsVisible;
        _map.SimulationTickDurationSeconds = .2 / _speed;
        if (hidden)
        {
            _accumulator = 0;
            if (!_wasBackground) { _wasBackground = true; _ = SaveAsync(false); }
            return;
        }
        _wasBackground = false;
        if (!_paused && !_modal.IsVisible)
        {
            _accumulator = Math.Min(.8, _accumulator + elapsed * _speed);
            var work = Stopwatch.StartNew(); var count = 0;
            while (_accumulator >= .2 && count < 4)
            {
                _engine.Step(); _accumulator -= .2; count++;
                if (work.Elapsed.TotalMilliseconds > 12) break;
            }
            if (count > 0)
            { _map.SimulationTickFraction = Math.Clamp(_accumulator / .2, 0, .999999); _map.RefreshWorld(); }
            _simulationStatus.Text = _accumulator > .4 ? "设备限速，世界继续演化" : "世界正在演化";
        }
        if (now - _lastUi > .7) { _lastUi = now; RefreshUi(); }
        if (now - _lastSave > 30 && !_saving && !_modal.IsVisible) { _lastSave = now; _ = SaveAsync(false); }
    }

    private void TogglePause()
    {
        _paused = !_paused;
        if (!_paused) _checkpoint = null;
        _map.IsSimulationPaused = _paused || _modal.IsVisible; RefreshUi(true);
    }
    private void BeginEdit()
    {
        _paused = true; _map.IsSimulationPaused = true;
        _checkpoint ??= _engine.ExportJson();
        RefreshUi();
    }
    private void RestoreCheckpoint()
    {
        if (_checkpoint is null) { SetStatus("暂无可撤销的编辑。开始绘制时会保存恢复点，继续模拟后清除。"); return; }
        CancelMapPick();
        _engine = WorldEngine.ImportJson(_checkpoint); _checkpoint = null; _paused = true;
        ClearMapSelection(); _selectedTile = null; _selectedNationId = 0; _selectedResidentId = 0; _inspectorMode = "overview"; InvalidateInspector();
        _map.Engine = _engine; _map.IsSimulationPaused = true; _map.RefreshWorld(); UpdateToolContext(); RefreshUi(true); SetStatus("已恢复到本轮编辑之前");
    }
    private void UpdateSpeedButtons()
    {
        foreach (var (speed, button) in _speeds) { button.Background = speed == _speed ? Brush.Parse("#355347") : Panel; button.Foreground = speed == _speed ? Mint : Brushes.White; }
    }

    private void ApplyLayout()
    {
        _isCompact = Bounds.Width < 980;
        _brandName.Text = Bounds.Width < 600 ? "众生纪" : "WORLDBOX";
        _brandName.FontSize = Bounds.Width < 600 ? 11 : 15;
        _brandCaption.IsVisible = false;
        _version.IsVisible = Bounds.Width >= 600;
        _headerStats.IsVisible = Bounds.Width >= 760;
        _rail.IsVisible = false;
        _body.ColumnDefinitions = new ColumnDefinitions(_mobilePanel && !_isCompact ? "0,*,320" : "0,*,0");
        _inspector.IsVisible = _mobilePanel;
        Grid.SetColumn(_inspector, _isCompact ? 1 : 2);
        _inspector.Width = _isCompact ? Math.Max(280, Bounds.Width - 12) : double.NaN;
        _inspector.MaxHeight = _isCompact ? Math.Max(140, Math.Min(430, _body.Bounds.Height * (_expandedInspector ? .84 : .44))) : double.PositiveInfinity;
        _inspector.VerticalAlignment = _isCompact ? VerticalAlignment.Bottom : VerticalAlignment.Stretch;
        _inspector.Margin = _isCompact ? new Thickness(6, 6, 6, 80) : new Thickness(0);
        _inspector.HorizontalAlignment = HorizontalAlignment.Right;
        _inspector.ZIndex = 20;
        _toolBar.IsVisible = _toolsOpen;
        _toolHint.IsVisible = Bounds.Width > 600;
        _timeStatus.IsVisible = Bounds.Width > 760;
        UpdateToolBarSize(); UpdateModalBounds(); RefreshSelectionSummary();
    }

    private void UpdateToolBarSize()
    {
        var compact = Bounds.Width < 700;
        _toolChoices.Columns = compact ? 4 : 8; _toolChoices.Rows = compact ? 2 : 1;
        _toolBar.Width = Math.Max(280, Math.Min(650, _map.Bounds.Width - 12));
        _selectionBar.Width = Math.Max(260, Math.Min(480, _map.Bounds.Width - 12));
        _placementBar.MaxWidth = Math.Max(120, Math.Min(240, _map.Bounds.Width - 60));
        _eventText.MaxWidth = Math.Max(240, Math.Min(620, _map.Bounds.Width - 32));
        _toolContext.Width = compact ? 108 : 132;
        _toolTitle.FontSize = compact ? 11 : 13;
    }

    private WorldState? _spotlightState;
    private (int LastId, int Count, int Watches) _spotlightRevision;
    private EventGroup? _spotlightGroup;

    private void RefreshUi(bool force = false)
    {
        var state = _engine.State;
        _date.Text = $"第 {state.Year} 年 {state.Day} 日";
        _population.Text = $"居民 {state.Population:N0}    国家 {state.Nations.Count}";
        _worldSubtitle.Text = $"地图：{state.Width} × {state.Height}\n种子：{state.Seed}\n文明演化";
        _play.Content = _paused ? "继续" : "暂停";
        if (_paused) _simulationStatus.Text = "时间已暂停";
        var watchHash = 17; foreach (var watch in _watched) watchHash = unchecked(watchHash * 31 + watch.GetHashCode());
        var revision = (state.Events.LastOrDefault()?.Id ?? 0, state.Events.Count, watchHash);
        if (!ReferenceEquals(_spotlightState, state) || _spotlightRevision != revision)
        {
            _spotlightState = state; _spotlightRevision = revision;
            var candidates = state.Events.Where(e => e.Importance >= EventImportance.Notable && e.Kind is not (WorldEventKind.Editor or WorldEventKind.Policy or WorldEventKind.Magic or WorldEventKind.Culture));
            _spotlightGroup = WorldStories.Group(candidates).OrderByDescending(g => _watched.Count > 0 && g.Entries.Any(IsWatched))
                .ThenByDescending(g => g.Latest.Tick).ThenByDescending(g => g.Latest.Id).FirstOrDefault();
        }
        var spotlight = _spotlightGroup;
        _focusedEventId = spotlight?.Latest.Id ?? 0;
        var spotlightText = spotlight is null ? "选择国家、聚落或居民，关注它的故事" : $"{DateLabel(spotlight.Latest.Tick)}\n{spotlight.Latest.Message}" + (spotlight.Count > 1 ? $"（同类 {spotlight.Count} 次）" : "");
        spotlightText = DisplayFormat.Text(spotlightText);
        if (_eventText.Text != spotlightText) _eventText.Text = spotlightText;
        if (_mobilePanel || force) RefreshInspector(force);
        RefreshSelectionSummary();
    }

    private void ShowStorage()
    {
        var panel = ModalPanel("保存你的世界", "存档保存在这台设备的浏览器中。导出文件可以备份或带到另一台设备。");
        panel.Children.Add(Button("保存到本机", async () => { CloseModal(); await SaveAsync(true); }));
        panel.Children.Add(Button("读取本机存档", async () =>
        {
            var generation = _modalGeneration;
            try
            {
                if (App.Storage is null) return;
                string? json;
                await _saveGate.WaitAsync();
                try { json = await App.Storage.LoadAsync(); } finally { _saveGate.Release(); }
                if (generation != _modalGeneration || !_modal.IsVisible) return;
                if (json is null) { SetStatus("还没有本机存档"); return; }
                ReplaceWorld(WorldEngine.ImportJson(json)); CloseModal(); SetStatus("已读取本机存档");
            }
            catch (Exception ex) { SetStatus($"读取失败：{FriendlyError(ex)}"); }
        }));
        panel.Children.Add(Button("导出世界文件", async () =>
        {
            try { if (App.Storage is null) return; await App.Storage.ExportAsync(_engine.ExportJson(), $"worldbox-{_engine.State.Seed}-year{_engine.State.Year}.json"); CloseModal(); SetStatus("已导出世界文件"); }
            catch (OperationCanceledException) { SetStatus("已取消导出"); }
            catch (Exception ex) { SetStatus($"导出失败：{FriendlyError(ex)}"); }
        }));
        panel.Children.Add(Button("导入世界文件", async () =>
        {
            var generation = _modalGeneration;
            try { if (App.Storage is null) return; var json = await App.Storage.ImportAsync(); if (json is null || generation != _modalGeneration || !_modal.IsVisible) return; var candidate = WorldEngine.ImportJson(json); ReplaceWorld(candidate); CloseModal(); SetStatus("导入成功，时间已暂停"); }
            catch (Exception ex) { SetStatus($"导入失败，当前世界未改变：{FriendlyError(ex)}"); }
        }));
        panel.Children.Add(Button("撤销本轮编辑", () => { RestoreCheckpoint(); CloseModal(); }));
        OpenModal(panel);
    }
    private async Task SaveAsync(bool manual)
    {
        if (App.Storage is null || !_ready || !manual && (_saving || !_allowAutosave)) return;
        await _saveGate.WaitAsync();
        _saving = true;
        try
        {
            var year = _engine.State.Year;
            await App.Storage.SaveAsync(_engine.ExportJson());
            if (manual) { _allowAutosave = true; SetStatus("世界已保存到本机"); }
            else SetStatus($"已自动保存（第 {year} 年，{DateTime.Now:HH:mm}）");
        }
        catch (Exception ex) { SetStatus($"保存失败：{FriendlyError(ex)}。请尝试导出文件。"); }
        finally { _saving = false; _saveGate.Release(); }
    }

    private void ShowNewWorld()
    {
        var panel = ModalPanel("让一个新世界诞生", "相同的种子生成相同的山海。当前世界会暂存为本次会话的恢复点。");
        panel.Children.Add(Text("世界种子", 12, Muted));
        var seed = Named(new NumericUpDown { Minimum = int.MinValue, Maximum = int.MaxValue, Increment = 1, Value = Random.Shared.Next(10000, 99999), FormatString = "0" }, "world-seed"); panel.Children.Add(seed);
        panel.Children.Add(Text("世界大小", 12, Muted));
        var size = Named(new ComboBox { ItemsSource = new[] { "小型世界（128 × 128）", "中型世界（256 × 256）" }, SelectedIndex = 1, HorizontalAlignment = HorizontalAlignment.Stretch }, "world-size"); panel.Children.Add(size);
        var life = Named(new CheckBox { Content = Text("播下四个种族，立即开始观察", 12), IsChecked = true }, "world-initial-life"); panel.Children.Add(life);
        var create = Button("创造世界", () =>
        {
            if (seed.Value is not { } seedNumber || seedNumber != decimal.Truncate(seedNumber)) { SetStatus("种子应为一个有效整数"); return; }
            var seedValue = Integer(seed);
            var dimension = size.SelectedIndex == 0 ? 128 : 256;
            ReplaceWorld(WorldEngine.Create(seedValue, dimension, dimension, life.IsChecked == true));
            _inspectorMode = "overview"; CloseModal(); SetStatus("新世界已诞生，点击继续让时间开始流动");
        }); create.Background = Mint; create.Foreground = Ink; panel.Children.Add(create); OpenModal(panel);
    }
    private void ReplaceWorld(WorldEngine engine)
    {
        CancelMapPick();
        ClearMapSelection(); _checkpoint = _engine.ExportJson(); _engine = engine; _paused = true; _accumulator = 0; _selectedTile = null; _selectedNationId = 0;
        _allowAutosave = true;
        _inspectorMode = "overview"; _selectedResidentId = 0; _navigation.Clear(); _watched.Clear(); _historyWatchedOnly = false; _eventDetailId = 0; InvalidateInspector();
        _map.Engine = engine; _map.IsSimulationPaused = true; _map.RefreshWorld(true); UpdateToolContext(); RefreshUi(true);
    }

    private void ShowNationEditor(int nationId)
    {
        var nation = _engine.State.Nations.FirstOrDefault(n => n.Id == nationId); if (nation is null) return;
        _paused = true; RefreshUi();
        var panel = ModalPanel("改写文明的方向", "编辑国家名称、库存与外交关系。变更将在暂停的世界中生效。");
        var name = Named(new TextBox { Text = nation.Name, MaxLength = 40 }, "nation-name"); panel.Children.Add(name);
        panel.Children.Add(Text("旗色", 12, Muted));
        var color = nation.ColorArgb;
        var palette = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var swatches = new List<(uint Color, Button Button)>();
        foreach (var argb in new uint[] { 0xFFE7AD62, 0xFF63CCA7, 0xFF8C9DEB, 0xFFE27A7C, 0xFFDFC16E, 0xFFB593DB, 0xFF74BBDC, 0xFFD294C8 })
        {
            var swatch = new Button { Width = 34, Height = 34, MinHeight = 34, Padding = new Thickness(0), Background = new SolidColorBrush(Color.FromUInt32(argb)), BorderThickness = new Thickness(2), BorderBrush = argb == color ? Brushes.White : Brushes.Transparent };
            swatch.Click += (_, _) => { color = argb; foreach (var entry in swatches) entry.Button.BorderBrush = entry.Color == color ? Brushes.White : Brushes.Transparent; };
            swatches.Add((argb, swatch)); palette.Children.Add(swatch);
        }
        panel.Children.Add(palette);
        panel.Children.Add(Text("古代发展水平（影响生产效率）", 12, Muted));
        var technology = Named(new ComboBox { ItemsSource = new[] { "1  部落", "2  定居", "3  农业", "4  冶炼", "5  城邦" }, SelectedIndex = Math.Clamp(nation.Technology - 1, 0, 4), HorizontalAlignment = HorizontalAlignment.Stretch }, "nation-technology"); panel.Children.Add(technology);
        panel.Children.Add(Text("自主发展方向", 12, Muted));
        var focus = Named(new ComboBox { ItemsSource = Enum.GetValues<DevelopmentFocus>().Select(WorldEngine.DevelopmentFocusName).ToArray(),
            SelectedIndex = (int)nation.DevelopmentFocus, HorizontalAlignment = HorizontalAlignment.Stretch }, "nation-development-focus"); panel.Children.Add(focus);
        panel.Children.Add(Paragraph("科技优先工业与能源；法术传承依靠施法者与训练，不要求魔晶设施。魔法工艺与兼修路线才会自主建设魔晶生产链。"));
        var fields = new List<NumericUpDown>();
        foreach (var kind in AdvancementRules.Resources)
        { var value = nation.Resources.Get(kind); var input = Field(panel, ResourceStock.Name(kind), value, "nation-" + kind.ToString().ToLowerInvariant(), Math.Max(1_000_000, value)); fields.Add(input); }
        var others = _engine.State.Nations.Where(n => n.Id != nationId).ToList();
        var other = Named(new ComboBox { ItemsSource = others.Select(n => n.Name).ToArray(), SelectedIndex = others.Count > 0 ? 0 : -1, HorizontalAlignment = HorizontalAlignment.Stretch }, "nation-diplomacy-target");
        var diplomacy = Named(new ComboBox { ItemsSource = new[] { "保持现有关系", "和平", "结盟", "宣战" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch }, "nation-diplomacy");
        if (others.Count > 0) { panel.Children.Add(Text("与另一个国家的关系", 12, Muted)); panel.Children.Add(other); panel.Children.Add(diplomacy); }
        panel.Children.Add(Button("应用变更", () =>
        {
            var values = new double?[AdvancementRules.Resources.Count];
            if (string.IsNullOrWhiteSpace(name.Text) || name.Text.Any(char.IsControl)) { SetStatus("请输入不含控制字符的国家名称"); return; }
            for (var i = 0; i < fields.Count; i++)
            {
                if (fields[i].Value is null) { SetStatus("请填写资源数量"); return; }
                var value = Number(fields[i]);
                if (value == (double)fields[i].Tag!) continue;
                if (value is < 0 or > 1_000_000) { SetStatus("修改后的资源须在 0 到 1,000,000 之间"); return; }
                values[i] = value;
            }
            BeginEdit(); _engine.RenameNation(nationId, name.Text.Trim()); _engine.SetNationResources(nationId, values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7], values[8], values[9], values[10], values[11]);
            _engine.SetDevelopmentFocus(nationId, (DevelopmentFocus)focus.SelectedIndex);
            _engine.SetNationColor(nationId, color); _engine.SetNationTechnology(nationId, technology.SelectedIndex + 1);
            if (diplomacy.SelectedIndex > 0 && other.SelectedIndex >= 0) _engine.SetDiplomacy(nationId, others[other.SelectedIndex].Id, diplomacy.SelectedIndex switch { 2 => DiplomaticStatus.Allied, 3 => DiplomaticStatus.War, _ => DiplomaticStatus.Neutral });
            CloseModal(); _map.RefreshWorld(); RefreshUi(true); SetStatus("国家已更新");
        }));
        panel.Children.Add(new Border { Height = 1, Background = Line });
        panel.Children.Add(Button("绘制这个国家的领土", () =>
        {
            CloseModal(); BeginEdit(); _map.SelectedNationId = nationId; _map.ActiveTool = "territory";
            SetCategory("terrain"); _map.SelectedNationId = nationId; _map.ActiveTool = "territory";
            _toolTitle.Text = "划定疆域"; _toolHint.Text = "绘制陆地归属，覆盖聚落会一并转移";
            _mobilePanel = false; ApplyLayout(); SetStatus("领土编辑中，覆盖聚落会转移其居民与库存；支持撤销");
        }));
        panel.Children.Add(Text("向首都添加居民（每次 12 人）", 12, Muted));
        var people = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var race in Enum.GetValues<RaceKind>()) people.Children.Add(Button(RaceName(race), () =>
        {
            var capital = _engine.State.Settlements.First(s => s.Id == nation.CapitalId);
            BeginEdit(); _engine.SpawnResidents(capital.X, capital.Y, race, 12); CloseModal(); _map.RefreshWorld(); RefreshUi(true); SetStatus("新居民已加入这个国家");
        }));
        panel.Children.Add(people);
        var towns = _engine.State.Settlements.Where(s => s.NationId == nationId).ToList();
        if (towns.Count >= 2)
        {
            panel.Children.Add(Text("选择一处聚落独立建国", 12, Muted));
            var townPicker = new ComboBox { ItemsSource = towns.Select(t => t.Name).ToArray(), SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch }; panel.Children.Add(townPicker);
            panel.Children.Add(Button("让聚落独立", () =>
            {
                try
                {
                    BeginEdit(); var town = towns[townPicker.SelectedIndex]; var newName = town.Name.Length > 36 ? town.Name[..36] : town.Name;
                    _selectedNationId = _engine.SplitSettlement(town.Id, newName + "国"); _inspectorMode = "nation";
                    CloseModal(); _map.RefreshWorld(); RefreshUi(true); SetStatus("一个新的国家诞生了");
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { SetStatus(FriendlyError(ex)); }
            }));
        }
        else panel.Children.Add(Paragraph("拥有两处及以上聚落后，可以拆分国家。"));
        OpenModal(panel);
    }

    private StackPanel ModalPanel(string title, string description)
    {
        var panel = new StackPanel { Spacing = 5, MaxWidth = 460 };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") }; header.Children.Add(Text(title, 17, null, true));
        var close = IconButton("close", CloseModal, "关闭窗口", "modal-close"); Grid.SetColumn(close, 1); header.Children.Add(close); panel.Children.Add(header);
        panel.Children.Add(Paragraph(description)); return panel;
    }
    private void OpenModal(Control content)
    {
        CancelMapPick();
        _modalGeneration++;
        var hasPrimary = false;
        _modalFeedback = Paragraph(""); _modalFeedback.Foreground = Brush.Parse("#E7BD87");
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 6 };
        if (content is StackPanel panel && panel.Children.Count > 0)
        {
            var header = panel.Children[0]; panel.Children.RemoveAt(0); layout.Children.Add(header);
            var primary = panel.Children.OfType<Button>().LastOrDefault(button => (AutomationProperties.GetAutomationId(button) ?? "").EndsWith("-apply", StringComparison.Ordinal));
            if (primary is not null)
            {
                hasPrimary = true; panel.Children.Remove(primary);
                var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
                footer.Children.Add(primary);
                var cancel = Named(Button("取消", CloseModal), "modal-cancel"); Grid.SetColumn(cancel, 1); footer.Children.Add(cancel);
                var footerArea = new StackPanel { Spacing = 6 }; footerArea.Children.Add(_modalFeedback); footerArea.Children.Add(footer);
                Grid.SetRow(footerArea, 2); layout.Children.Add(footerArea);
            }
        }
        if (!hasPrimary) { Grid.SetRow(_modalFeedback, 2); layout.Children.Add(_modalFeedback); }
        var scroll = Named(new ScrollViewer { Content = content, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }, "modal-scroll");
        Grid.SetRow(scroll, 1); layout.Children.Add(scroll);
        _modal.Child = new Border { Background = Panel, CornerRadius = new CornerRadius(8), BorderBrush = Line, BorderThickness = new Thickness(1), Padding = new Thickness(12), Margin = new Thickness(14), Width = Math.Min(520, Math.Max(280, Bounds.Width - 28)), Height = hasPrimary ? Math.Min(760, Math.Max(220, Bounds.Height - 28)) : double.NaN, MaxHeight = Math.Max(220, Bounds.Height - 28), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Child = layout };
        _modalHasPrimary = hasPrimary; UpdateModalBounds();
        _modal.IsVisible = true; _map.IsSimulationPaused = true;
    }
    private void UpdateModalBounds()
    {
        if (_modal.Child is not Border dialog) return;
        var available = Math.Max(180, Bounds.Height - (_isCompact ? 64 : 28));
        dialog.Width = Math.Min(520, Math.Max(260, Bounds.Width - 28));
        dialog.MaxHeight = available;
        dialog.Height = _modalHasPrimary ? Math.Min(_isCompact ? 560 : 700, available * .9) : double.NaN;
        dialog.Padding = new Thickness(_isCompact ? 8 : 12);
    }
    private void CloseModal() { CancelMapPick(); _modalGeneration++; _modal.IsVisible = false; _modal.Child = null; _map.IsSimulationPaused = _paused; Focus(); }
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (_mapPick is not null) FinishMapPick();
            else if (_map.HasPendingPlacement) _map.CancelPlacement();
            else if (_modal.IsVisible) CloseModal();
            else if (_mobilePanel) CloseInspector();
            else SuspendTool();
            e.Handled = true;
        }
        if (e.Key == Key.Space && !_modal.IsVisible && !IsEditingText()) { TogglePause(); e.Handled = true; }
    }
    private void SetStatus(string text) { _status.Text = text; if (_modal.IsVisible && _modalFeedback is not null) _modalFeedback.Text = text; }
    private static string FriendlyError(Exception ex) => ex.Message.Length > 160 ? ex.Message[..160] : ex.Message;
    private static TextBlock Text(string text, double size = 13, IBrush? color = null, bool bold = false) => new() { Text = DisplayFormat.Text(text), FontSize = size, Foreground = color ?? Brush.Parse("#E9EFEB"), FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal, VerticalAlignment = VerticalAlignment.Center };
    private static TextBlock Paragraph(string text) => new() { Text = DisplayFormat.Text(text), FontSize = 12, Foreground = Muted, TextWrapping = TextWrapping.Wrap, LineHeight = 16 };
    private static Button Button(string label, Action action, string? tooltip = null, double minWidth = 0)
    {
        var button = new Button { Content = label, MinWidth = minWidth, FontSize = 12, Padding = new Thickness(8, 4), HorizontalAlignment = HorizontalAlignment.Left };
        button.Click += (_, _) => action(); if (tooltip is not null) ToolTip.SetTip(button, tooltip);
        var id = label switch
        {
            "新世界" => "header-new-world", "存档" => "header-storage", "概览" => "header-overview", "+" => "map-zoom-in", "-" => "map-zoom-out", "全图" => "map-fit",
            "保存到本机" => "storage-save", "读取本机存档" => "storage-load", "导出世界文件" => "storage-export", "导入世界文件" => "storage-import", "撤销本轮编辑" => "storage-undo", "撤销" => "world-undo",
            "创造世界" => "world-create-apply", "应用变更" => "nation-apply", "编辑这个国家" => "nation-edit", _ => null
        };
        if (id is not null) Named(button, id); return button;
    }
    private static T Named<T>(T control, string id) where T : Control
    { control.Name = id.Replace('-', '_'); AutomationProperties.SetAutomationId(control, id); return control; }
    private static Border Card(Control child) => new() { Child = child, Background = Ink, CornerRadius = new CornerRadius(9), Padding = new Thickness(8) };
    private static Border StatCard(string label, string value, string hint)
    {
        var stack = new StackPanel { Spacing = 6 }; stack.Children.Add(Text(label, 10, Muted)); stack.Children.Add(Text(value, 28, Mint, true)); stack.Children.Add(Text(hint, 9, Muted)); return Card(stack);
    }
    private static string RaceName(RaceKind race) => race switch { RaceKind.Human => "人类", RaceKind.Elf => "精灵", RaceKind.Dwarf => "矮人", _ => "兽人" };
    private static string TerrainName(TerrainType terrain) => terrain switch { TerrainType.DeepWater => "深海", TerrainType.Water => "浅海", TerrainType.Sand => "沙地", TerrainType.Grass => "草地", TerrainType.Forest => "森林", TerrainType.Mountain => "山脉", TerrainType.Snow => "雪原", TerrainType.Hills => "丘陵", TerrainType.Wetland => "湿地", TerrainType.Desert => "荒漠", TerrainType.River => "河流", _ => "苔原" };
    private static string ProfessionName(Profession job) => job switch { Profession.Child => "孩童", Profession.Farmer => "农民", Profession.Lumberjack => "伐木工", Profession.Miner => "矿工", Profession.Soldier => "战士", Profession.Builder => "建造者", Profession.Trader => "商人", Profession.Messenger => "信使", Profession.Representative => "代表", Profession.Scholar => "学者", _ => "法师" };
    private static string ActivityName(ResidentActivity activity) => activity switch { ResidentActivity.Wandering => "探索土地", ResidentActivity.Working => "正在工作", ResidentActivity.Hungry => "寻找食物", ResidentActivity.Marching => "正在行军", ResidentActivity.Sick => "正在养病", ResidentActivity.Eating => "正在进食", ResidentActivity.Resting => "正在休息", ResidentActivity.Talking => "交换消息", ResidentActivity.Delivering => "执行运输", ResidentActivity.Studying => "正在学习", ResidentActivity.Casting => "正在施法", _ => "躲避危险" };

}
