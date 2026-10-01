using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;

namespace SeWZC.WorldBox.UI;

public sealed class MainView : UserControl
{
    private static readonly IBrush Ink = Brush.Parse("#111E29");
    private static readonly IBrush Panel = Brush.Parse("#172632");
    private static readonly IBrush Line = Brush.Parse("#2A3C46");
    private static readonly IBrush Muted = Brush.Parse("#8FA8AE");
    private static readonly IBrush Mint = Brush.Parse("#B8E9BC");
    private WorldEngine _engine = WorldEngine.Create(73921, 256, 256, true);
    private readonly WorldMapControl _map = new();
    private readonly Grid _body = new();
    private readonly Border _rail = new();
    private readonly Border _inspector = new();
    private readonly ScrollViewer _inspectorScroll = new();
    private readonly StackPanel _toolChoices = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
    private readonly TextBlock _toolTitle = Text("塑造山海", 13, Mint);
    private readonly TextBlock _toolHint = Text("选择一种地形，在世界上绘制", 11, Muted);
    private readonly TextBlock _worldTitle = Text("晨曦群岛", 22);
    private readonly TextBlock _worldSubtitle = Text("", 11, Muted);
    private readonly TextBlock _date = Text("", 13);
    private readonly TextBlock _population = Text("", 13, Mint);
    private readonly TextBlock _status = Text("正在唤醒世界…", 11, Muted);
    private readonly TextBlock _version = Text("古代纪元 · v0.1 原型", 10, Muted);
    private readonly TextBlock _simulationStatus = Text("● 世界正在演化", 11, Mint);
    private readonly Button _play;
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
    private int _speed = 1, _selectedNationId;
    private int _modalGeneration;
    private string _category = "terrain", _inspectorMode = "overview";
    private (int X, int Y)? _selectedTile;
    private string? _checkpoint;
    private bool _isCompact;
    private readonly Control _headerStats;
    private readonly TextBlock _brandName = Text("SeWZC. WORLDBOX", 15, null, true);
    private readonly TextBlock _brandCaption = Text("众生与山海  /  ANCIENT WORLDS", 9, Muted);

    public MainView()
    {
        Background = Ink;
        Focusable = true;
        _map.Engine = _engine;
        _map.WorldEditing += (_, _) => BeginEdit();
        _map.WorldEdited += (_, _) => { RefreshUi(true); SetStatus("世界已更新 · 暂停中，可撤销本轮编辑"); };
        _map.TileSelected += (x, y) => { _selectedTile = (x, y); _inspectorMode = "tile"; _mobilePanel = true; ApplyLayout(); RefreshInspector(true); };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(18, 0) };
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center };
        var logo = new Grid { Width = 28, Height = 28, ColumnDefinitions = new ColumnDefinitions("*,*"), RowDefinitions = new RowDefinitions("*,*") };
        var colors = new[] { "#B8E9BC", "#E4C889", "#558F88", "#88BBA0" };
        for (var i = 0; i < 4; i++) { var square = new Border { Background = Brush.Parse(colors[i]), Margin = new Thickness(1) }; Grid.SetColumn(square, i % 2); Grid.SetRow(square, i / 2); logo.Children.Add(square); }
        brand.Children.Add(logo);
        var brandText = new StackPanel { Spacing = 1 };
        brandText.Children.Add(_brandName);
        brandText.Children.Add(_brandCaption);
        brand.Children.Add(brandText); header.Children.Add(brand);
        var stats = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 22, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        stats.Children.Add(_date); stats.Children.Add(_population); _headerStats = stats;
        Grid.SetColumn(stats, 1); header.Children.Add(stats);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, VerticalAlignment = VerticalAlignment.Center };
        actions.Children.Add(Button("新世界", ShowNewWorld, "创建一片新的大陆"));
        actions.Children.Add(Button("存档", ShowStorage, "保存、导出或导入世界"));
        actions.Children.Add(Button("概览", () => { _mobilePanel = !_mobilePanel; _inspectorMode = "overview"; ApplyLayout(); RefreshInspector(true); }));
        Grid.SetColumn(actions, 2); header.Children.Add(actions);

        _body.ColumnDefinitions = new ColumnDefinitions("76,*,284");
        var railItems = new StackPanel { Spacing = 14, Margin = new Thickness(9, 22, 9, 10) };
        railItems.Children.Add(Text("造 物", 10, Muted));
        foreach (var (label, category) in new[] { ("山海", "terrain"), ("众生", "life"), ("天灾", "disaster"), ("观察", "inspect") })
            railItems.Children.Add(Button(label, () => SetCategory(category), minWidth: 54));
        railItems.Children.Add(new Border { Height = 1, Background = Line, Margin = new Thickness(4, 3) });
        railItems.Children.Add(Button("国界", () => { _map.ShowBorders = !_map.ShowBorders; _map.RefreshWorld(); SetStatus(_map.ShowBorders ? "已显示国界" : "已隐藏国界"); }));
        railItems.Children.Add(Button("历史", () => { _inspectorMode = "history"; _mobilePanel = true; ApplyLayout(); RefreshInspector(true); }));
        railItems.Children.Add(Button("撤销", RestoreCheckpoint, "恢复本轮暂停编辑前的世界"));
        _rail.Child = railItems; _rail.Background = Ink; _rail.BorderBrush = Line; _rail.BorderThickness = new Thickness(0, 0, 1, 0);
        _body.Children.Add(_rail);

        var mapLayer = new Grid { ClipToBounds = true, Background = Brush.Parse("#122D3D") };
        mapLayer.Children.Add(_map);
        var titleStack = new StackPanel { Spacing = 5 };
        titleStack.Children.Add(Text("你的世界  /  WORLD 01", 10, Mint)); titleStack.Children.Add(_worldTitle); titleStack.Children.Add(_worldSubtitle);
        var worldCard = new Border { Child = titleStack, Padding = new Thickness(17, 13), Background = Brush.Parse("#E3162834"), CornerRadius = new CornerRadius(10), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(18), IsHitTestVisible = false };
        mapLayer.Children.Add(worldCard);
        var camera = new StackPanel { Spacing = 5, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(18) };
        camera.Children.Add(Button("+", () => _map.ZoomIn(), "放大地图", 38));
        camera.Children.Add(Button("-", () => _map.ZoomOut(), "缩小地图", 38));
        camera.Children.Add(Button("全图", () => _map.FitWorld(), "显示整个世界", 38));
        mapLayer.Children.Add(camera);

        var bottom = new StackPanel { Spacing = 10, VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(12, 10) };
        var toolsPanel = new StackPanel { Spacing = 10 };
        var categories = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var (label, category) in new[] { ("山海", "terrain"), ("众生", "life"), ("天灾", "disaster"), ("观察", "inspect") })
            categories.Children.Add(Button(label, () => SetCategory(category)));
        var brush = new ComboBox { Width = 80, MinHeight = 36, ItemsSource = new[] { "小笔刷", "中笔刷", "大笔刷" }, SelectedIndex = 0, FontSize = 11 };
        brush.SelectionChanged += (_, _) => _map.BrushRadius = brush.SelectedIndex switch { 1 => 5, 2 => 10, _ => 2 };
        categories.Children.Add(brush);
        toolsPanel.Children.Add(categories);
        toolsPanel.Children.Add(new ScrollViewer { Content = _toolChoices, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        var hint = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 }; hint.Children.Add(_toolTitle); hint.Children.Add(_toolHint); toolsPanel.Children.Add(hint);
        var toolBox = new Border { Child = toolsPanel, Background = Brush.Parse("#F0172632"), BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(12), MaxWidth = 650 };
        bottom.Children.Add(toolBox);
        var timeControls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, HorizontalAlignment = HorizontalAlignment.Center };
        _play = Button("暂停", TogglePause, "空格：暂停或继续", 88); _play.Background = Mint; _play.Foreground = Ink;
        timeControls.Children.Add(_play);
        foreach (var speed in new[] { 1, 2, 5 })
        {
            var b = Button($"{speed}×", () => { _speed = speed; UpdateSpeedButtons(); }, minWidth: 42);
            _speeds.Add((speed, b)); timeControls.Children.Add(b);
        }
        timeControls.Children.Add(new Border { Child = _simulationStatus, Padding = new Thickness(12, 8), Background = Brush.Parse("#E3172632"), CornerRadius = new CornerRadius(7), VerticalAlignment = VerticalAlignment.Center });
        bottom.Children.Add(timeControls); mapLayer.Children.Add(bottom);
        Grid.SetColumn(mapLayer, 1); _body.Children.Add(mapLayer);

        _inspectorScroll.HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
        _inspector.Child = _inspectorScroll; _inspector.Background = Panel; _inspector.BorderBrush = Line; _inspector.BorderThickness = new Thickness(1, 0, 0, 0);
        Grid.SetColumn(_inspector, 2); _body.Children.Add(_inspector);
        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(16, 0) };
        _status.VerticalAlignment = VerticalAlignment.Center; _status.TextTrimming = TextTrimming.CharacterEllipsis; footer.Children.Add(_status);
        _version.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(_version, 1); footer.Children.Add(_version);
        var shell = new Grid { RowDefinitions = new RowDefinitions("68,*,30") };
        shell.Children.Add(new Border { Child = header, BorderBrush = Line, BorderThickness = new Thickness(0, 0, 0, 1) });
        Grid.SetRow(_body, 1); shell.Children.Add(_body); Grid.SetRow(footer, 2); shell.Children.Add(footer);
        _shell = shell; shell.IsEnabled = false;
        var root = new Grid(); root.Children.Add(shell); root.Children.Add(_modal); Content = root;
        SizeChanged += (_, _) => { toolBox.MaxWidth = Math.Max(300, Math.Min(650, Bounds.Width - (_isCompact ? 24 : 410))); ApplyLayout(); };
        KeyDown += OnKeyDown;
        _timer.Tick += OnTick;
        AttachedToVisualTree += async (_, _) => { await (_initialization ??= InitializeAsync()); _previousTime = _clock.Elapsed.TotalSeconds; _timer.Start(); };
        DetachedFromVisualTree += (_, _) => _timer.Stop();
        SetCategory("inspect"); UpdateSpeedButtons(); RefreshUi(true);
    }

    private async Task InitializeAsync()
    {
        try
        {
            if (App.Storage is not null && await App.Storage.LoadAsync() is { } json)
            { _engine = WorldEngine.ImportJson(json); _map.Engine = _engine; SetStatus("已恢复本机世界 · 每 30 秒自动保存"); }
            else SetStatus("新世界已诞生 · 选择工具开始创造，或观察文明演化");
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
            if (count > 0) _map.RefreshWorld();
            _simulationStatus.Text = _accumulator > .4 ? "● 设备限速 · 世界继续" : "● 世界正在演化";
        }
        else _accumulator = 0;
        if (now - _lastUi > .7) { _lastUi = now; RefreshUi(); }
        if (now - _lastSave > 30 && !_saving && !_modal.IsVisible) { _lastSave = now; _ = SaveAsync(false); }
    }

    private void TogglePause()
    {
        _paused = !_paused;
        if (!_paused) _checkpoint = null;
        _accumulator = 0; RefreshUi(true);
    }
    private void BeginEdit()
    {
        _paused = true;
        _checkpoint ??= _engine.ExportJson();
        RefreshUi();
    }
    private void RestoreCheckpoint()
    {
        if (_checkpoint is null) { SetStatus("暂无可撤销的编辑。开始绘制时会保存恢复点，继续模拟后清除。"); return; }
        _engine = WorldEngine.ImportJson(_checkpoint); _checkpoint = null; _paused = true;
        _selectedTile = null; _selectedNationId = 0; _inspectorMode = "overview";
        _map.Engine = _engine; _map.RefreshWorld(); RefreshUi(true); SetStatus("已恢复到本轮编辑之前");
    }
    private void UpdateSpeedButtons()
    {
        foreach (var (speed, button) in _speeds) { button.Background = speed == _speed ? Brush.Parse("#355347") : Panel; button.Foreground = speed == _speed ? Mint : Brushes.White; }
    }

    private void SetCategory(string category)
    {
        _category = category; _toolChoices.Children.Clear(); _tools.Clear();
        var items = category switch
        {
            "terrain" => new[] { ("Grass", "草地", "#8CAC69"), ("Forest", "森林", "#427D61"), ("Sand", "沙地", "#E6D09A"), ("Mountain", "山脉", "#9DABB0"), ("Water", "浅海", "#4A9CBA"), ("DeepWater", "深海", "#28556F"), ("Snow", "雪原", "#D4E8E7") },
            "life" => new[] { ("Human", "人类", "#DEBC85"), ("Elf", "精灵", "#90C599"), ("Dwarf", "矮人", "#BE9785"), ("Orc", "兽人", "#A9B768") },
            "disaster" => new[] { ("Fire", "火灾", "#F0A065"), ("Drought", "干旱", "#D8C180"), ("Plague", "疫病", "#B194C7") },
            _ => new[] { ("inspect", "检查", "#B8E9BC"), ("pan", "漫游", "#7DACBA") }
        };
        foreach (var (tool, label, color) in items)
        {
            var content = new StackPanel { Spacing = 5, HorizontalAlignment = HorizontalAlignment.Center };
            content.Children.Add(new Border { Width = 23, Height = 19, Background = Brush.Parse(color), CornerRadius = new CornerRadius(category == "life" ? 7 : 3), HorizontalAlignment = HorizontalAlignment.Center });
            content.Children.Add(Text(label, 11));
            var button = new Button { Content = content, MinWidth = 55, Padding = new Thickness(9, 8), Background = Ink, CornerRadius = new CornerRadius(7), BorderThickness = new Thickness(1), BorderBrush = Brushes.Transparent };
            button.Click += (_, _) => SelectTool(tool); _toolChoices.Children.Add(button); _tools.Add((tool, button));
        }
        (_toolTitle.Text, _toolHint.Text) = category switch
        {
            "terrain" => ("塑造山海", "绘制地形 · 编辑时自动暂停"),
            "life" => ("播下文明", "点击陆地，投放 12 位居民"),
            "disaster" => ("改变命运", "点击世界，降下灾害"),
            _ => ("见证众生", "点击检查 · 拖动平移 · 滚轮缩放")
        };
        SelectTool(items[0].Item1);
    }
    private void SelectTool(string tool)
    {
        _map.ActiveTool = tool;
        foreach (var (key, button) in _tools) { button.BorderBrush = key == tool ? Mint : Brushes.Transparent; button.Background = key == tool ? Brush.Parse("#2C423F") : Ink; }
    }

    private void ApplyLayout()
    {
        _isCompact = Bounds.Width < 980;
        _brandName.Text = Bounds.Width < 600 ? "WORLDBOX" : "SeWZC. WORLDBOX";
        _brandCaption.IsVisible = Bounds.Width >= 600;
        _version.IsVisible = Bounds.Width >= 600;
        _headerStats.IsVisible = Bounds.Width >= 760;
        _rail.IsVisible = !_isCompact;
        _body.ColumnDefinitions = new ColumnDefinitions(_isCompact ? "0,*,0" : "76,*,284");
        _inspector.IsVisible = !_isCompact || _mobilePanel;
        Grid.SetColumn(_inspector, _isCompact ? 1 : 2);
        _inspector.Width = _isCompact ? Math.Min(300, Math.Max(260, Bounds.Width - 50)) : double.NaN;
        _inspector.HorizontalAlignment = _isCompact ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;
        _inspector.ZIndex = _isCompact ? 20 : 0;
        _toolHint.IsVisible = Bounds.Width > 600;
        _simulationStatus.IsVisible = Bounds.Width > 600;
    }

    private void RefreshUi(bool force = false)
    {
        var state = _engine.State;
        _date.Text = $"第 {state.Year} 年 · {state.Day} 日";
        _population.Text = $"{state.Population:N0} 位居民  /  {state.Nations.Count} 个国家";
        _worldSubtitle.Text = $"{state.Width} × {state.Height}  ·  种子 {state.Seed}  ·  古代纪元";
        _play.Content = _paused ? "继续" : "暂停";
        if (_paused) _simulationStatus.Text = "● 时间已暂停";
        RefreshInspector(force);
    }

    private void RefreshInspector(bool force = false)
    {
        if (!force && _inspector.IsPointerOver) return;
        var state = _engine.State;
        var content = new StackPanel { Margin = new Thickness(18, 20), Spacing = 16 };
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        head.Children.Add(Text(_inspectorMode switch { "history" => "世界编年史", "tile" => "此处的故事", "nation" => "文明档案", _ => "世界概览" }, 17, null, true));
        var close = Button(_isCompact ? "×" : "返回", () => { _mobilePanel = false; _inspectorMode = "overview"; ApplyLayout(); RefreshInspector(true); }, minWidth: 28);
        close.Padding = new Thickness(6, 2); Grid.SetColumn(close, 1); head.Children.Add(close); content.Children.Add(head);

        if (_inspectorMode == "tile" && _selectedTile is { } point && point.X >= 0 && point.Y >= 0 && point.X < state.Width && point.Y < state.Height)
        {
            var tile = state.Tiles[point.Y * state.Width + point.X];
            content.Children.Add(Text($"{TerrainName(tile.Terrain)}  ·  {point.X}, {point.Y}", 15, Mint));
            content.Children.Add(Paragraph($"肥沃度 {tile.Fertility}  /  {(tile.IsWalkable ? "可以通行" : "阻挡陆路通行")}"));
            if (tile.FireTicks > 0) content.Children.Add(Paragraph("火焰正在蔓延，居民和资源受到威胁。"));
            if (tile.DroughtTicks > 0) content.Children.Add(Paragraph("干旱正在降低这片土地的产出。"));
            var nation = state.Nations.FirstOrDefault(n => n.Id == tile.NationId);
            if (nation is not null) content.Children.Add(NationCard(nation));
            var nearby = state.Residents.Where(r => Math.Abs(r.X - point.X) <= 3 && Math.Abs(r.Y - point.Y) <= 3).Take(6).ToList();
            content.Children.Add(Text("附近的居民", 12, Muted));
            foreach (var resident in nearby)
            {
                var info = new StackPanel { Spacing = 4 }; info.Children.Add(Text(resident.Name, 13));
                info.Children.Add(Paragraph($"{RaceName(resident.Race)} · {resident.Age:F0} 岁 · {ProfessionName(resident.Profession)}\n{ActivityName(resident.Activity)} · 生命 {resident.Health:F0}\n特质：{resident.Trait}"));
                content.Children.Add(Card(info));
            }
            if (nearby.Count == 0) content.Children.Add(Paragraph("这里还没有居民。试着在陆地上播下文明。"));
        }
        else if (_inspectorMode == "nation" && state.Nations.FirstOrDefault(n => n.Id == _selectedNationId) is { } nation)
        {
            content.Children.Add(NationCard(nation));
            content.Children.Add(Text("国家正在思考", 12, Muted)); content.Children.Add(Paragraph(nation.Decision));
            content.Children.Add(Paragraph($"粮食  {nation.Resources.Food:F0}\n木材  {nation.Resources.Wood:F0}\n石材  {nation.Resources.Stone:F0}\n矿产  {nation.Resources.Ore:F0}\n发展阶段  {nation.Technology}"));
            content.Children.Add(Text("居民构成", 12, Muted));
            foreach (var group in state.Residents.Where(r => r.NationId == nation.Id).GroupBy(r => r.Race)) content.Children.Add(Text($"{RaceName(group.Key)}  {group.Count()} 人", 12));
            content.Children.Add(Button("编辑这个国家", () => ShowNationEditor(nation.Id)));
            var capital = state.Settlements.FirstOrDefault(s => s.Id == nation.CapitalId);
            if (capital is not null) content.Children.Add(Button("前往首都", () => { _map.FocusTile(capital.X, capital.Y); _mobilePanel = false; ApplyLayout(); }));
            var army = state.Armies.FirstOrDefault(a => a.NationId == nation.Id);
            if (army is not null) content.Children.Add(Paragraph($"军队：{army.Soldiers} 人 · {army.Status}\n补给 {army.Supplies:F0} · 士气 {army.Morale:F0}"));
        }
        else if (_inspectorMode != "history")
        {
            var overview = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 10 };
            overview.Children.Add(StatCard("世界人口", state.Population.ToString("N0"), "生命正在延续"));
            var nations = StatCard("文明国家", state.Nations.Count.ToString(), $"{state.Settlements.Count} 处聚落"); Grid.SetColumn(nations, 1); overview.Children.Add(nations); content.Children.Add(overview);
            content.Children.Add(Paragraph($"第 {state.Year} 年，第 {state.Day} 日\n土地孕育生命，生命书写历史。"));
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") }; row.Children.Add(Text("大地上的文明", 12, Muted));
            var borderToggle = Button("国界", () => { _map.ShowBorders = !_map.ShowBorders; _map.RefreshWorld(); }, minWidth: 40); borderToggle.Padding = new Thickness(8, 3); Grid.SetColumn(borderToggle, 1); row.Children.Add(borderToggle); content.Children.Add(row);
            foreach (var item in state.Nations.OrderByDescending(n => n.Population).Take(16)) content.Children.Add(NationCard(item));
            if (state.Nations.Count == 0) content.Children.Add(Paragraph("等待第一座聚落诞生。投放同伴，让他们在适宜的土地上定居。"));
            var natural = new CheckBox { Content = Text("允许自然灾害", 12), IsChecked = state.NaturalDisasters };
            natural.IsCheckedChanged += (_, _) => { BeginEdit(); _engine.State.NaturalDisasters = natural.IsChecked == true; SetStatus("自然灾害规则已更新"); };
            content.Children.Add(natural);
        }

        if (_inspectorMode is "overview" or "history")
        {
            content.Children.Add(new Border { Height = 1, Background = Line });
            content.Children.Add(Text("世界的回声", 12, Muted));
            foreach (var item in state.Events.AsEnumerable().Reverse().Take(_inspectorMode == "history" ? 40 : 5))
            {
                var evt = new StackPanel { Spacing = 4 }; evt.Children.Add(Text($"第 {1 + item.Tick / 120} 年 · {1 + item.Tick % 120} 日", 10, Mint)); evt.Children.Add(Paragraph(item.Message));
                var b = new Button { Content = evt, HorizontalContentAlignment = HorizontalAlignment.Stretch, Background = Brushes.Transparent, Padding = new Thickness(0, 4) };
                b.Click += (_, _) => { if (item.X >= 0) _map.FocusTile(item.X, item.Y); }; content.Children.Add(b);
            }
            if (_inspectorMode == "overview") content.Children.Add(Button("展开编年史", () => { _inspectorMode = "history"; RefreshInspector(true); }));
        }
        var offset = _inspectorScroll.Offset; _inspectorScroll.Content = content; _inspectorScroll.Offset = offset;
    }

    private Control NationCard(Nation nation)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("8,*,Auto"), ColumnSpacing = 10 };
        row.Children.Add(new Border { Background = new SolidColorBrush(Color.FromUInt32(nation.ColorArgb)), CornerRadius = new CornerRadius(3), Width = 5 });
        var name = new StackPanel { Spacing = 4 }; name.Children.Add(Text(nation.Name, 13, null, true)); name.Children.Add(Text($"{nation.Population} 人 · {nation.Territory} 领土", 10, Muted)); Grid.SetColumn(name, 1); row.Children.Add(name);
        var mark = Text("›", 22, Muted); mark.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(mark, 2); row.Children.Add(mark);
        var button = new Button { Content = row, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(12), Background = Ink, CornerRadius = new CornerRadius(8) };
        button.Click += (_, _) => { _selectedNationId = nation.Id; _inspectorMode = "nation"; RefreshInspector(true); }; return button;
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
            try { if (App.Storage is null) return; var json = await App.Storage.ImportAsync(); if (json is null || generation != _modalGeneration || !_modal.IsVisible) return; var candidate = WorldEngine.ImportJson(json); ReplaceWorld(candidate); CloseModal(); SetStatus("导入成功 · 时间已暂停"); }
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
            else SetStatus($"已自动保存 · 第 {year} 年 · {DateTime.Now:HH:mm}");
        }
        catch (Exception ex) { SetStatus($"保存失败：{FriendlyError(ex)}。请尝试导出文件。"); }
        finally { _saving = false; _saveGate.Release(); }
    }

    private void ShowNewWorld()
    {
        var panel = ModalPanel("让一个新世界诞生", "相同的种子生成相同的山海。当前世界会暂存为本次会话的恢复点。");
        panel.Children.Add(Text("世界种子", 12, Muted));
        var seed = new TextBox { Text = Random.Shared.Next(10000, 99999).ToString(), PlaceholderText = "输入整数种子" }; panel.Children.Add(seed);
        panel.Children.Add(Text("世界大小", 12, Muted));
        var size = new ComboBox { ItemsSource = new[] { "128 × 128 · 小型世界", "256 × 256 · 中型世界" }, SelectedIndex = 1, HorizontalAlignment = HorizontalAlignment.Stretch }; panel.Children.Add(size);
        var life = new CheckBox { Content = Text("播下四个种族，立即开始观察", 12), IsChecked = true }; panel.Children.Add(life);
        var create = Button("创造世界", () =>
        {
            if (!int.TryParse(seed.Text, out var seedValue)) { SetStatus("种子应为一个有效整数"); return; }
            var dimension = size.SelectedIndex == 0 ? 128 : 256;
            ReplaceWorld(WorldEngine.Create(seedValue, dimension, dimension, life.IsChecked == true));
            _inspectorMode = "overview"; CloseModal(); SetStatus("新世界已诞生 · 点击继续，让时间开始流动");
        }); create.Background = Mint; create.Foreground = Ink; panel.Children.Add(create); OpenModal(panel);
    }
    private void ReplaceWorld(WorldEngine engine)
    {
        _checkpoint = _engine.ExportJson(); _engine = engine; _paused = true; _accumulator = 0; _selectedTile = null; _selectedNationId = 0;
        _allowAutosave = true;
        _inspectorMode = "overview";
        _map.Engine = engine; _map.RefreshWorld(true); RefreshUi(true);
    }

    private void ShowNationEditor(int nationId)
    {
        var nation = _engine.State.Nations.FirstOrDefault(n => n.Id == nationId); if (nation is null) return;
        _paused = true; RefreshUi();
        var panel = ModalPanel("改写文明的方向", "编辑国家名称、库存与外交关系。变更将在暂停的世界中生效。");
        var name = new TextBox { Text = nation.Name, MaxLength = 40 }; panel.Children.Add(name);
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
        panel.Children.Add(Text("古代发展水平 · 影响生产效率", 12, Muted));
        var technology = new ComboBox { ItemsSource = new[] { "1 · 部落", "2 · 定居", "3 · 农业", "4 · 冶炼", "5 · 城邦" }, SelectedIndex = Math.Clamp(nation.Technology - 1, 0, 4), HorizontalAlignment = HorizontalAlignment.Stretch }; panel.Children.Add(technology);
        var fields = new List<TextBox>();
        foreach (var (label, value) in new[] { ("粮食", nation.Resources.Food), ("木材", nation.Resources.Wood), ("石材", nation.Resources.Stone), ("矿产", nation.Resources.Ore) })
        { var row = new Grid { ColumnDefinitions = new ColumnDefinitions("70,*") }; row.Children.Add(Text(label, 12, Muted)); var input = new TextBox { Text = Math.Round(value).ToString(System.Globalization.CultureInfo.InvariantCulture) }; Grid.SetColumn(input, 1); row.Children.Add(input); fields.Add(input); panel.Children.Add(row); }
        var others = _engine.State.Nations.Where(n => n.Id != nationId).ToList();
        var other = new ComboBox { ItemsSource = others.Select(n => n.Name).ToArray(), SelectedIndex = others.Count > 0 ? 0 : -1, HorizontalAlignment = HorizontalAlignment.Stretch };
        var diplomacy = new ComboBox { ItemsSource = new[] { "保持现有关系", "和平", "结盟", "宣战" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        if (others.Count > 0) { panel.Children.Add(Text("与另一个国家的关系", 12, Muted)); panel.Children.Add(other); panel.Children.Add(diplomacy); }
        panel.Children.Add(Button("应用变更", () =>
        {
            var values = new double[4];
            if (string.IsNullOrWhiteSpace(name.Text) || name.Text.Any(char.IsControl)) { SetStatus("请输入不含控制字符的国家名称"); return; }
            for (var i = 0; i < fields.Count; i++) if (!double.TryParse(fields[i].Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out values[i]) || !double.IsFinite(values[i]) || values[i] < 0 || values[i] > 1000000) { SetStatus("资源必须是 0 到 1,000,000 之间的数值"); return; }
            BeginEdit(); _engine.RenameNation(nationId, name.Text.Trim()); _engine.SetNationResources(nationId, values[0], values[1], values[2], values[3]);
            _engine.SetNationColor(nationId, color); _engine.SetNationTechnology(nationId, technology.SelectedIndex + 1);
            if (diplomacy.SelectedIndex > 0 && other.SelectedIndex >= 0) _engine.SetDiplomacy(nationId, others[other.SelectedIndex].Id, diplomacy.SelectedIndex switch { 2 => DiplomaticStatus.Allied, 3 => DiplomaticStatus.War, _ => DiplomaticStatus.Neutral });
            CloseModal(); _map.RefreshWorld(); RefreshUi(true); SetStatus("国家已更新");
        }));
        panel.Children.Add(new Border { Height = 1, Background = Line });
        panel.Children.Add(Button("绘制这个国家的领土", () =>
        {
            CloseModal(); BeginEdit(); _map.SelectedNationId = nationId; _map.ActiveTool = "territory";
            _toolChoices.Children.Clear(); _tools.Clear(); _toolChoices.Children.Add(Text(nation.Name + " · 领土笔刷", 15, Mint));
            _toolTitle.Text = "划定疆域"; _toolHint.Text = "绘制陆地归属，覆盖聚落会一并转移";
            _mobilePanel = false; ApplyLayout(); SetStatus("领土编辑中 · 覆盖聚落会转移其居民与库存，支持撤销");
        }));
        panel.Children.Add(Text("向首都添加居民 · 每次 12 人", 12, Muted));
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
        var panel = new StackPanel { Spacing = 14, MaxWidth = 420 };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") }; header.Children.Add(Text(title, 21, null, true));
        var close = Button("×", CloseModal, minWidth: 30); Grid.SetColumn(close, 1); header.Children.Add(close); panel.Children.Add(header);
        panel.Children.Add(Paragraph(description)); return panel;
    }
    private void OpenModal(Control content)
    {
        _modalGeneration++;
        _modal.Child = new Border { Background = Panel, CornerRadius = new CornerRadius(16), BorderBrush = Line, BorderThickness = new Thickness(1), Padding = new Thickness(24), Margin = new Thickness(18), MaxWidth = 480, MaxHeight = Math.Max(450, Bounds.Height - 40), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Child = new ScrollViewer { Content = content } };
        _modal.IsVisible = true;
    }
    private void CloseModal() { _modalGeneration++; _modal.IsVisible = false; _modal.Child = null; Focus(); }
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { CloseModal(); _mobilePanel = false; ApplyLayout(); e.Handled = true; }
        if (e.Key == Key.Space && !_modal.IsVisible && e.Source is not TextBox) { TogglePause(); e.Handled = true; }
    }
    private void SetStatus(string text) => _status.Text = text;
    private static string FriendlyError(Exception ex) => ex.Message.Length > 160 ? ex.Message[..160] : ex.Message;
    private static TextBlock Text(string text, double size = 13, IBrush? color = null, bool bold = false) => new() { Text = text, FontSize = size, Foreground = color ?? Brush.Parse("#E9EFEB"), FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal, VerticalAlignment = VerticalAlignment.Center };
    private static TextBlock Paragraph(string text) => new() { Text = text, FontSize = 12, Foreground = Muted, TextWrapping = TextWrapping.Wrap, LineHeight = 21 };
    private static Button Button(string label, Action action, string? tooltip = null, double minWidth = 0)
    {
        var button = new Button { Content = label, MinWidth = minWidth, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
        button.Click += (_, _) => action(); if (tooltip is not null) ToolTip.SetTip(button, tooltip); return button;
    }
    private static Border Card(Control child) => new() { Child = child, Background = Ink, CornerRadius = new CornerRadius(9), Padding = new Thickness(12) };
    private static Border StatCard(string label, string value, string hint)
    {
        var stack = new StackPanel { Spacing = 6 }; stack.Children.Add(Text(label, 10, Muted)); stack.Children.Add(Text(value, 28, Mint, true)); stack.Children.Add(Text(hint, 9, Muted)); return Card(stack);
    }
    private static string RaceName(RaceKind race) => race switch { RaceKind.Human => "人类", RaceKind.Elf => "精灵", RaceKind.Dwarf => "矮人", _ => "兽人" };
    private static string TerrainName(TerrainType terrain) => terrain switch { TerrainType.DeepWater => "深海", TerrainType.Water => "浅海", TerrainType.Sand => "沙地", TerrainType.Grass => "草地", TerrainType.Forest => "森林", TerrainType.Mountain => "山脉", _ => "雪原" };
    private static string ProfessionName(Profession job) => job switch { Profession.Child => "孩童", Profession.Farmer => "农民", Profession.Lumberjack => "伐木工", Profession.Miner => "矿工", Profession.Soldier => "战士", _ => "建造者" };
    private static string ActivityName(ResidentActivity activity) => activity switch { ResidentActivity.Wandering => "探索土地", ResidentActivity.Working => "正在工作", ResidentActivity.Hungry => "寻找食物", ResidentActivity.Marching => "正在行军", _ => "正在养病" };
}
