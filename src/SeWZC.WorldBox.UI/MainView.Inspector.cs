using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    private readonly List<Action> _inspectorUpdates = [];
    private string? _inspectorKey;
    private bool _refreshingInspector;
    private string _residentSearch = "";
    private int _historyImportance, _historyNationId;
    private WorldEventKind? _historyKind;
    private string _historySearch = "";
    private int _eventDetailId;

    private void InvalidateInspector() => _inspectorKey = null;
    private void RememberLocation()
    {
        if (_mobilePanel) _navigation.Push((_inspectorMode, _selectedNationId, _selectedResidentId, _inspectorSettlementId, _selectedTile, _eventDetailId, _inspectorScroll.Offset));
        if (_navigation.Count > 32) _navigation.Clear();
    }
    private void OpenInspector(string mode, bool remember = true)
    {
        if (mode == "rules") { ShowRules(); return; }
        if (remember) RememberLocation();
        _inspectorMode = mode; _mobilePanel = true; _toolsOpen = false; SuspendTool();
        ApplyLayout(); RefreshInspector(true);
    }
    private void GoBack()
    {
        if (!_navigation.TryPop(out var view)) { CloseInspector(); return; }
        _selectedNationId = view.Nation; _selectedResidentId = view.Resident;
        _inspectorSettlementId = view.Town; _selectedTile = view.Tile; _eventDetailId = view.Event;
        OpenInspector(view.Mode, false);
        Avalonia.Threading.Dispatcher.UIThread.Post(() => _inspectorScroll.Offset = view.Scroll, Avalonia.Threading.DispatcherPriority.Loaded);
    }
    private void OpenResident(int id)
    {
        RememberLocation(); _selectedResidentId = id; _map.SelectResident(id); OpenInspector("resident", false);
    }
    private void OpenNation(int id) { RememberLocation(); _selectedNationId = id; OpenInspector("nation", false); }

    private void RefreshInspector(bool force = false)
    {
        if (_refreshingInspector) return;
        _refreshingInspector = true;
        try
        {
            // A town can disappear during simulation. Resolve the selection before computing
            // the key so only a changed selection rebuilds controls and their live callbacks.
            if (_inspectorMode is "infrastructure" or "communication" &&
                !_engine.State.Settlements.Any(town => town.Id == _inspectorSettlementId))
                _inspectorSettlementId = _engine.State.Settlements.OrderBy(town => town.Id).FirstOrDefault()?.Id ?? 0;
            var key = $"{_inspectorMode}:{_selectedNationId}:{_selectedResidentId}:{_selectedTile}:{_eventDetailId}:{_inspectorSettlementId}";
            if (_inspectorKey != key)
            {
                _inspectorKey = key; _inspectorUpdates.Clear();
                var content = new StackPanel { Margin = new Thickness(8), Spacing = 6 };
                var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
                header.Children.Add(Text(_inspectorMode switch
                {
                    "watched" => "我的关注", "story" => "人物故事", "event" => "事件与后果", "resident" => "居民档案", "residents" => "大地上的居民", "nation" => "国家与文明", "nations" => "文明国家",
                    "history" => "世界编年史", "tile" => "此处的故事", "rules" => "世界规则", "infrastructure" => "建设与运输", "communication" => "消息与通信", _ => "世界概览"
                }, 17, null, true));
                var back = IconButton("back", GoBack, "返回上一处", "inspector-back"); Grid.SetColumn(back, 1); header.Children.Add(back);
                var close = IconButton("close", CloseInspector, "关闭详情，返回地图", "inspector-close");
                Grid.SetColumn(close, 2); header.Children.Add(close); _inspectorNavigation.Children.Clear(); _inspectorNavigation.Children.Add(header);
                var navigation = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*"), ColumnSpacing = 4 };
                var entries = new[] { ("世界", "overview"), ("居民", "residents"), ("国家", "nations"), ("日志", "history") };
                for (var i = 0; i < entries.Length; i++)
                {
                    var entry = entries[i]; var button = Named(Button(entry.Item1, () => OpenInspector(entry.Item2)), "inspector-" + entry.Item2);
                    button.Padding = new Thickness(4, 6); button.FontSize = 11; Grid.SetColumn(button, i); navigation.Children.Add(button);
                }
                _inspectorNavigation.Children.Add(navigation);
                switch (_inspectorMode)
                {
                    case "watched": BuildWatchedInspector(content); break;
                    case "story": BuildStoryInspector(content); break;
                    case "event": BuildEventInspector(content); break;
                    case "resident": BuildResidentInspector(content); break;
                    case "residents": BuildResidentList(content); break;
                    case "nation": BuildNationInspector(content); break;
                    case "nations": BuildNationList(content); break;
                    case "history": BuildHistoryInspector(content); break;
                    case "tile": BuildTileInspector(content); break;
                    case "rules": BuildWorldRules(content); break;
                    case "infrastructure": BuildInfrastructureInspector(content, false); break;
                    case "communication": BuildInfrastructureInspector(content, true); break;
                    default: BuildOverview(content); break;
                }
                _inspectorScroll.Content = content; _inspectorScroll.Offset = default;
            }
            // Existing controls stay attached. Timed updates never replace inputs or steal focus.
            foreach (var update in _inspectorUpdates.ToArray()) update();
        }
        finally { _refreshingInspector = false; }
    }

    private TextBlock LiveText(Func<string> value, double size = 12, IBrush? color = null)
    {
        var text = Text(value(), size, color); text.TextWrapping = TextWrapping.Wrap;
        _inspectorUpdates.Add(() => text.Text = value()); return text;
    }
    private void LiveRows<T>(StackPanel parent, Func<IEnumerable<T>> items, Func<T, string> key, Func<T, string> label, Action<T>? action = null)
    {
        var list = new StackPanel { Spacing = 6 }; parent.Children.Add(list);
        var rows = new Dictionary<string, (Control Row, TextBlock Text)>();
        void Update()
        {
            var wanted = items().DistinctBy(key).ToList(); var keys = new HashSet<string>();
            foreach (var item in wanted)
            {
                var id = key(item); if (!keys.Add(id)) continue;
                if (!rows.TryGetValue(id, out var row))
                {
                    var text = Paragraph(label(item));
                    Control control;
                    if (action is null) control = Card(text);
                    else
                    {
                        var button = new Button { Content = text, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(10), Background = Ink, CornerRadius = new CornerRadius(7) };
                        if (item is Resident resident) Named(button, $"resident-row-{resident.Id}");
                        if (item is Nation nation) Named(button, $"nation-row-{nation.Id}");
                        if (item is WorldEvent worldEvent) Named(button, $"history-row-{worldEvent.Id}");
                        if (item is EventGroup group) Named(button, $"history-row-{group.Latest.Id}");
                        button.Tag = item; button.Click += (_, _) => { if (button.Tag is T selected) action(selected); }; control = button;
                    }
                    row = (control, text); rows[id] = row; list.Children.Add(control);
                }
                row.Text.Text = label(item); if (row.Row is Button b) { b.Tag = item; if (item is EventGroup group) Avalonia.Automation.AutomationProperties.SetAutomationId(b, $"history-row-{group.Latest.Id}"); }
            }
            foreach (var obsolete in rows.Keys.Where(id => !keys.Contains(id)).ToArray())
            { list.Children.Remove(rows[obsolete].Row); rows.Remove(obsolete); }
            var index = 0;
            foreach (var item in wanted)
            {
                if (!rows.TryGetValue(key(item), out var row)) continue;
                var current = list.Children.IndexOf(row.Row);
                if (current != index) { list.Children.Remove(row.Row); list.Children.Insert(index, row.Row); }
                index++;
            }
        }
        _inspectorUpdates.Add(Update); Update();
    }

    private string NationName(int id) => _engine.State.Nations.FirstOrDefault(n => n.Id == id)?.Name ?? (id == 0 ? "世界" : $"国家 #{id}（已消亡）");
    private string ResidentName(int id) => _engine.GetResident(id)?.Name ?? (id == 0 ? "未指定" : $"居民 #{id}");
    private string TownName(int id) => _engine.State.Settlements.FirstOrDefault(t => t.Id == id)?.Name ?? (id == 0 ? "无" : $"聚落 #{id}");
    private static string DateLabel(long tick) => tick < 0 ? "尚无记录" : $"第 {1 + tick / 120} 年 · {1 + tick % 120} 日";
    private static string StockLabel(ResourceStock stock) => $"粮 {stock.Food:F0} · 木 {stock.Wood:F0} · 石 {stock.Stone:F0} · 矿 {stock.Ore:F0}";

    private void BuildOverview(StackPanel panel)
    {
        panel.Children.Add(LiveText(() => $"{_engine.State.Population:N0} 位居民 · {_engine.State.Nations.Count} 个国家", 19, Mint));
        panel.Children.Add(LiveText(() => $"{DateLabel(_engine.State.Tick)}\n{_engine.State.Settlements.Count} 处聚落 · 种子 {_engine.State.Seed}"));
        panel.Children.Add(Paragraph("关注国家、聚落或居民，持续追踪它们的发展与转折。"));
        panel.Children.Add(Named(Button("我的关注", () => OpenInspector("watched")), "overview-watched"));
        var borders = Named(new CheckBox { Content = "显示国界", IsChecked = _map.ShowBorders }, "map-borders");
        borders.IsCheckedChanged += (_, _) => { _map.ShowBorders = borders.IsChecked == true; _map.RefreshWorld(); }; panel.Children.Add(borders);
        var overlay = Named(new ComboBox { ItemsSource = new[] { "地图图层：无", "粮食压力：红色短缺 / 绿色充足", "运输：标记正在实地递送的居民", "通信：运作设施与实际连通聚落" }, SelectedIndex = _map.Overlay, HorizontalAlignment = HorizontalAlignment.Stretch }, "map-overlay");
        overlay.SelectionChanged += (_, _) => { _map.Overlay = Math.Max(0, overlay.SelectedIndex); _map.RefreshWorld(); }; panel.Children.Add(overlay);
        panel.Children.Add(Text("文明国家", 12, Mint));
        LiveRows(panel, () => _engine.State.Nations.OrderBy(n => n.Id).Take(16), n => n.Id.ToString(), n => $"{n.Name}\n{n.Population} 人 · {n.Territory} 格领土", n => OpenNation(n.Id));
        panel.Children.Add(Button("世界规则与魔法", () => OpenInspector("rules")));
        panel.Children.Add(Named(Button("聚落发展与运输", () => OpenInspector("infrastructure")), "overview-infrastructure"));
        panel.Children.Add(Text("近期重要事件", 12, Mint));
        LiveRows(panel, () => WorldStories.Group(_engine.State.Events.Where(e => e.Importance >= EventImportance.Notable && e.Kind != WorldEventKind.Editor && e.Kind != WorldEventKind.Policy)).Take(5), GroupKey, GroupLabel, g => FocusEvent(g.Latest));
        panel.Children.Add(Button("展开编年史", () => OpenInspector("history")));
    }
    private void BuildNationList(StackPanel panel)
    {
        panel.Children.Add(LiveText(() => $"{_engine.State.Nations.Count} 个国家 · 点击查看其文化、政策、研究与库存"));
        LiveRows(panel, () => _engine.State.Nations.OrderBy(n => n.Id), n => n.Id.ToString(), n => $"{n.Name}\n{n.Population} 人 · {n.Territory} 领土\n{n.Decision}", n => OpenNation(n.Id));
    }
    private void BuildResidentList(StackPanel panel)
    {
        var search = Named(new TextBox { Text = _residentSearch, PlaceholderText = "搜索姓名 / 编号 / 国家" }, "resident-search");
        search.TextChanged += (_, _) => { _residentSearch = search.Text ?? ""; RefreshInspector(); }; panel.Children.Add(search);
        panel.Children.Add(Paragraph("名单保留固定编号顺序。也可直接点击地图居民；重叠时连续点击切换。"));
        IEnumerable<Resident> Filter() => _engine.State.Residents.Concat(_engine.State.ArchivedResidents).Where(r => string.IsNullOrWhiteSpace(_residentSearch) || r.Name.Contains(_residentSearch, StringComparison.OrdinalIgnoreCase) || r.Id.ToString() == _residentSearch || NationName(r.NationId).Contains(_residentSearch, StringComparison.OrdinalIgnoreCase)).OrderBy(r => r.Id).Take(60);
        LiveRows(panel, Filter, r => r.Id.ToString(), r => $"{r.Name}  #{r.Id}\n{RaceName(r.Race)} · {ProfessionName(r.Profession)} · {NationName(r.NationId)}\n{(r.Health <= 0 ? "已离世 · 保留生平" : GoalName(r.Agent.Goal.Kind))}", r => OpenResident(r.Id));
        panel.Children.Add(LiveText(() => _engine.State.Residents.Count > 60 ? "最多显示前 60 条，请用搜索缩小范围。" : ""));
    }
    private void BuildTileInspector(StackPanel panel)
    {
        if (_selectedTile is not { } point || point.X < 0 || point.Y < 0 || point.X >= _engine.State.Width || point.Y >= _engine.State.Height) { panel.Children.Add(Paragraph("请先在地图上选择一处位置。")); return; }
        Tile Tile() => _engine.State.Tiles[point.Y * _engine.State.Width + point.X];
        panel.Children.Add(LiveText(() => $"{TerrainName(Tile().Terrain)} · {point.X}, {point.Y}", 16, Mint));
        panel.Children.Add(LiveText(() => $"实际地格状态\n肥沃度 {Tile().Fertility} · 资源 {Tile().ResourceAmount:F0}\n道路 {Tile().RoadLevel} 级 · {(Tile().IsWalkable ? "可通行" : "不可通行")}\n归属：{NationName(Tile().NationId)}\n火灾 {Tile().FireTicks} 日 · 干旱 {Tile().DroughtTicks} 日"));
        panel.Children.Add(Text("附近居民 · 点击打开完整档案", 12, Mint));
        LiveRows(panel, () => _engine.State.Residents.Where(r => Math.Abs(r.X - point.X) <= 6 && Math.Abs(r.Y - point.Y) <= 6).OrderBy(r => r.Id).Take(16), r => r.Id.ToString(), r => $"{r.Name} · {ProfessionName(r.Profession)}\n{GoalName(r.Agent.Goal.Kind)} · {r.Agent.Goal.Reason}", r => OpenResident(r.Id));
        panel.Children.Add(Button("查看建筑与道路", () => OpenInspector("infrastructure")));
    }

    private string EventLabel(WorldEvent item) => $"{ImportanceName(item.Importance)} · {DateLabel(item.Tick)}\n{NationName(item.NationId)}{(item.ResidentId > 0 ? " · " + ResidentName(item.ResidentId) : "")}\n{item.Message}{(item.X >= 0 ? $"\n定位 {item.X}, {item.Y}" : "")}";
    private void FocusEvent(WorldEvent item)
    {
        RememberLocation(); _eventDetailId = item.Id; OpenInspector("event", false);
    }
    private void BuildEventInspector(StackPanel panel)
    {
        var item = _engine.State.Events.FirstOrDefault(e => e.Id == _eventDetailId);
        if (item is null) { panel.Children.Add(Paragraph("这条事件已不在保留的历史中。")); return; }
        panel.Children.Add(Paragraph(EventLabel(item)));
        foreach (var causeId in WorldStories.Causes(item))
        {
            var cause = _engine.State.Events.FirstOrDefault(e => e.Id == causeId);
            if (cause is not null) panel.Children.Add(Named(Button("前因：" + cause.Message, () => FocusEvent(cause)), $"event-cause-{cause.Id}"));
            else panel.Children.Add(Paragraph($"前因 #{causeId} 已超出历史保留范围。"));
        }
        if (item.EvidenceFactId > 0) panel.Children.Add(Paragraph(EvidenceLabel(item.EvidenceFactId) + "\n关联不意味着其他居民已经获知。"));
        LiveRows(panel, () => _engine.State.Events.Where(e => WorldStories.Causes(e).Contains(item.Id)), e => e.Id.ToString(), e => "已发生的后续：" + EventLabel(e), FocusEvent);
        var group = WorldStories.Group(_engine.State.Events).FirstOrDefault(g => g.Count > 1 && g.Entries.Any(e => e.Id == item.Id));
        if (group is not null)
        {
            panel.Children.Add(Text($"同类记录 {group.Count} 次 · 各次事件保留独立结果", 13, Mint));
            foreach (var member in group.Entries)
                panel.Children.Add(Named(Button(EventLabel(member), () => FocusEvent(member)), $"event-member-{member.Id}"));
        }
        if (item.SettlementId > 0 && _engine.State.Settlements.Any(t => t.Id == item.SettlementId))
            panel.Children.Add(Button("查看 " + TownName(item.SettlementId), () => OpenWatched(new(ObservedObjectKind.Settlement, item.SettlementId))));
        if (item.NationId > 0 && _engine.State.Nations.Any(n => n.Id == item.NationId)) panel.Children.Add(Button("查看 " + NationName(item.NationId), () => OpenNation(item.NationId)));
        if (item.ResidentId > 0) panel.Children.Add(Button("查看 " + ResidentName(item.ResidentId), () => OpenResident(item.ResidentId)));
        panel.Children.Add(Named(Button("定位事件", () => { if (item.X >= 0) _map.FocusTile(item.X, item.Y); else if (item.ResidentId > 0) _map.FocusResident(item.ResidentId); CloseInspector(); }), "event-locate"));
    }
    private void BuildHistoryInspector(StackPanel panel)
    {
        var watched = Named(new CheckBox { Content = Text("仅看关注对象", 12), IsChecked = _historyWatchedOnly }, "history-watched");
        watched.IsCheckedChanged += (_, _) => { _historyWatchedOnly = watched.IsChecked == true; RefreshInspector(); }; panel.Children.Add(watched);
        var importance = Named(new ComboBox { ItemsSource = new[] { "重大事件（默认）", "普通与重要日常", "全部事件" }, SelectedIndex = _historyImportance, HorizontalAlignment = HorizontalAlignment.Stretch }, "history-importance");
        importance.SelectionChanged += (_, _) => { _historyImportance = importance.SelectedIndex; RefreshInspector(); }; panel.Children.Add(importance);
        var countries = new[] { (Id: 0, Name: "所有国家") }.Concat(_engine.State.Nations.Select(n => (n.Id, n.Name))).Concat(_engine.State.Events.SelectMany(e => new[] { e.NationId, e.SecondNationId }).Where(id => id > 0 && !_engine.State.Nations.Any(n => n.Id == id)).Distinct().Select(id => (Id: id, Name: NationName(id)))).ToList();
        var country = Named(new ComboBox { ItemsSource = countries.Select(n => n.Name).ToArray(), SelectedIndex = Math.Max(0, countries.FindIndex(n => n.Id == _historyNationId)), HorizontalAlignment = HorizontalAlignment.Stretch }, "history-nation");
        country.SelectionChanged += (_, _) => { if (country.SelectedIndex >= 0) _historyNationId = countries[country.SelectedIndex].Id; RefreshInspector(); }; panel.Children.Add(country);
        var kinds = Enum.GetValues<WorldEventKind>();
        var kind = Named(new ComboBox { ItemsSource = new[] { "所有类型" }.Concat(kinds.Select(EventKindName)).ToArray(), SelectedIndex = _historyKind.HasValue ? Array.IndexOf(kinds, _historyKind.Value) + 1 : 0, HorizontalAlignment = HorizontalAlignment.Stretch }, "history-kind");
        kind.SelectionChanged += (_, _) => { _historyKind = kind.SelectedIndex > 0 ? kinds[kind.SelectedIndex - 1] : null; RefreshInspector(); }; panel.Children.Add(kind);
        var search = Named(new TextBox { Text = _historySearch, PlaceholderText = "搜索事件内容" }, "history-search");
        search.TextChanged += (_, _) => { _historySearch = search.Text ?? ""; RefreshInspector(); }; panel.Children.Add(search);
        IEnumerable<WorldEvent> Filter() => _engine.State.Events.Where(e => (!_historyWatchedOnly || IsWatched(e)) && (_historyImportance == 2 || (_historyImportance == 0 ? e.Importance >= EventImportance.Major : e.Importance < EventImportance.Major)) && (_historyNationId == 0 || (e.NationId == _historyNationId || e.SecondNationId == _historyNationId)) && (!_historyKind.HasValue || e.Kind == _historyKind) && e.Message.Contains(_historySearch, StringComparison.OrdinalIgnoreCase)).Reverse();
        panel.Children.Add(LiveText(() => $"符合筛选：{Filter().Count()} 条 · 点击有坐标的记录定位"));
        LiveRows(panel, () => WorldStories.Group(Filter()).Take(100), GroupKey, GroupLabel, g => FocusEvent(g.Latest));
    }

    private static string ImportanceName(EventImportance value) => value switch { EventImportance.Routine => "普通", EventImportance.Notable => "重要日常", EventImportance.Major => "重大", _ => "历史转折" };
    private static string GoalName(AgentGoalKind value) => value switch
    {
        AgentGoalKind.Idle => "观察与等待", AgentGoalKind.Eat => "寻找食物", AgentGoalKind.Gather => "采集资源", AgentGoalKind.Work => "生产劳动", AgentGoalKind.Rest => "休息恢复", AgentGoalKind.Flee => "逃离危险", AgentGoalKind.Socialize => "交流消息", AgentGoalKind.DeliverMessage => "传递消息", AgentGoalKind.Trade => "运输货物", AgentGoalKind.Petition => "表达诉求", AgentGoalKind.Study => "学习研究", AgentGoalKind.TrainMagic => "魔法训练", AgentGoalKind.March => "执行军令", AgentGoalKind.Migrate => "迁往新家园", _ => "返回家园"
    };
    private static string EventKindName(WorldEventKind value) => value switch
    {
        WorldEventKind.Founding => "建国定居", WorldEventKind.Growth => "发展人口", WorldEventKind.Trade => "贸易运输", WorldEventKind.Diplomacy => "外交", WorldEventKind.War => "战争", WorldEventKind.Disaster => "灾害", WorldEventKind.Death => "死亡", WorldEventKind.Editor => "玩家编辑", WorldEventKind.Personal => "个人经历", WorldEventKind.Communication => "消息通信", WorldEventKind.Culture => "文化", WorldEventKind.Policy => "制度政策", WorldEventKind.Research => "研究", WorldEventKind.Construction => "建设", _ => "魔法"
    };
}
