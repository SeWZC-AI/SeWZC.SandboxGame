using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    private readonly List<Action> _inspectorUpdates = [];
    private int _eventDetailId;
    private int _historyImportance, _historyNationId;
    private WorldEventKind? _historyKind;
    private string _historySearch = "";
    private bool _includeDeceased;
    private string? _inspectorKey;
    private bool _refreshingInspector;
    private int _residentPage;
    private string _residentSearch = "";

    private void InvalidateInspector()
    {
        _inspectorKey = null;
    }

    private void RefreshInspector(bool force = false)
    {
        if (_refreshingInspector) return;
        _refreshingInspector = true;
        try
        {
            // 聚落可能在模拟中消亡，须先解析选择再计算视图键，避免旧选择保留失效回调。
            if (_inspectorMode is "settlement" or "infrastructure" or "research" or "communication" &&
                !_engine.State.Settlements.Any(town => town.Id == _inspectorSettlementId))
                _inspectorSettlementId = _engine.State.Settlements.OrderBy(town => town.Id).FirstOrDefault()?.Id ?? 0;
            if (!_mobilePanel) return;
            var key = InspectorKey();
            if (_inspectorKey != key)
            {
                _inspectorKey = key;
                _inspectorUpdates.Clear();
                var content = new StackPanel { Margin = new Thickness(6), Spacing = 5 };
                var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto") };
                header.Children.Add(Named(Text(_inspectorMode switch
                {
                    "watched" => "我的关注",
                    "story" => "人物故事",
                    "event" => "事件与后果",
                    "resident" => "居民档案",
                    "residents" => "居民列表",
                    "nation" => "国家与文明",
                    "nations" => "国家列表",
                    "building" => "建筑详情",
                    "structures" => "建筑与道路",
                    "history" => "世界编年史",
                    "tile" => "地块详情",
                    "rules" => "世界规则",
                    "guide" => "玩法说明",
                    "settlements" => "聚落列表",
                    "settlement" => "聚落概况",
                    "infrastructure" => "建设与运输",
                    "research" => "科技与魔法研究",
                    "communication" => "消息与通信",
                    _ => "世界概览",
                }, 17, null, true), "inspector-title"));
                var back = IconButton("back", GoBack, "返回上一处", "inspector-back");
                Grid.SetColumn(back, 1);
                header.Children.Add(back);
                var expand = Named(Button(_expandedInspector ? "收起" : "展开", () =>
                {
                    _expandedInspector = !_expandedInspector;
                    ApplyLayout();
                    RefreshInspector();
                }), "inspector-expand");
                expand.Content = LiveText(() => _expandedInspector ? "收起" : "展开", 11);
                expand.IsVisible = _isCompact;
                Grid.SetColumn(expand, 2);
                header.Children.Add(expand);
                _inspectorUpdates.Add(() => expand.IsVisible = _isCompact);
                var close = IconButton("close", CloseInspector, "关闭详情，返回地图", "inspector-close");
                Grid.SetColumn(close, 3);
                header.Children.Add(close);
                _inspectorNavigation.Children.Clear();
                _inspectorNavigation.Children.Add(header);
                var navigation = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*"), ColumnSpacing = 3 };
                var entries = new[]
                {
                    ("世界", "overview"), ("居民", "residents"), ("国家", "nations"), ("聚落", "settlements"),
                    ("日志", "history"),
                };
                for (var i = 0; i < entries.Length; i++)
                {
                    var entry = entries[i];
                    var button = Named(Button(entry.Item1, () => OpenInspector(entry.Item2)),
                        "inspector-" + entry.Item2);
                    button.Padding = new Thickness(4, 3);
                    button.FontSize = 11;
                    button.HorizontalAlignment = HorizontalAlignment.Stretch;
                    Grid.SetColumn(button, i);
                    navigation.Children.Add(button);
                    button.MinWidth = 0;
                    button.Background = _inspectorMode == entry.Item2 ? Line : Ink;
                }

                if (_inspectorMode is "overview" or "residents" or "nations" or "settlements" or "history"
                    or "structures" or "watched")
                    _inspectorNavigation.Children.Add(navigation);
                if (_inspectorMode is "settlement" or "infrastructure" or "research" or "communication")
                    BuildSettlementNavigation(_inspectorNavigation);
                switch (_inspectorMode)
                {
                    case "guide": BuildGameGuide(content); break;
                    case "building": BuildBuildingInspector(content); break;
                    case "structures": BuildStructuresInspector(content); break;
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
                    case "settlements": BuildSettlementList(content); break;
                    case "settlement": BuildSettlementOverview(content); break;
                    case "infrastructure": BuildInfrastructureInspector(content, false); break;
                    case "research": BuildSettlementResearch(content); break;
                    case "communication": BuildInfrastructureInspector(content, true); break;
                    default: BuildOverview(content); break;
                }

                _inspectorScroll.Content = content;
                _inspectorScroll.Offset = default;
            }

            // 定时刷新须保留现有输入控件，避免抢走焦点或覆盖编辑内容。
            foreach (var update in _inspectorUpdates.ToArray()) update();
        }
        finally
        {
            _refreshingInspector = false;
        }
    }

    private TextBlock LiveText(Func<string> value, double size = 12, IBrush? color = null)
    {
        var text = Text(value(), size, color);
        text.TextWrapping = TextWrapping.Wrap;
        text.IsVisible = !string.IsNullOrWhiteSpace(text.Text);
        _inspectorUpdates.Add(() =>
        {
            if (DetailsVisible(text))
            {
                var next = DisplayFormat.Text(value());
                if (text.Text != next) text.Text = next;
                text.IsVisible = !string.IsNullOrWhiteSpace(next);
            }
        });
        return text;
    }

    private void LiveRows<T>(StackPanel parent, Func<IEnumerable<T>> items, Func<T, string> key, Func<T, string> label,
        Action<T>? action = null)
    {
        var list = new StackPanel { Spacing = 3 };
        parent.Children.Add(list);
        var rows = new Dictionary<string, (Control Row, TextBlock Text)>();

        void Update()
        {
            if (!DetailsVisible(list)) return;
            var wanted = items().DistinctBy(key).ToList();
            var keys = new HashSet<string>();
            foreach (var item in wanted)
            {
                var id = key(item);
                if (!keys.Add(id)) continue;
                if (!rows.TryGetValue(id, out var row))
                {
                    var text = Paragraph(label(item));
                    Control control;
                    if (action is null) control = Card(text);
                    else
                    {
                        var button = new Button
                        {
                            Content = text,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            HorizontalContentAlignment = HorizontalAlignment.Stretch,
                            Padding = new Thickness(6, 3),
                            Background = Ink,
                            CornerRadius = new CornerRadius(7),
                        };
                        if (item is Resident resident) Named(button, $"resident-row-{resident.Id}");
                        if (item is Building building) Named(button, $"building-row-{building.Id}");
                        if (item is int tileIndex && _inspectorMode == "structures" && _listRoads)
                            Named(button, $"road-row-{tileIndex}");
                        if (item is Nation nation) Named(button, $"nation-row-{nation.Id}");
                        if (item is Settlement settlement) Named(button, $"settlement-row-{settlement.Id}");
                        if (item is WorldEvent worldEvent) Named(button, $"history-row-{worldEvent.Id}");
                        if (item is EventGroup group) Named(button, $"history-row-{group.Latest.Id}");
                        button.Tag = item;
                        button.Click += (_, _) =>
                        {
                            if (button.Tag is T selected) action(selected);
                        };
                        control = button;
                    }

                    row = (control, text);
                    rows[id] = row;
                    list.Children.Add(control);
                }

                var nextLabel = DisplayFormat.Text(label(item));
                if (row.Text.Text != nextLabel) row.Text.Text = nextLabel;
                if (row.Row is Button b)
                {
                    b.Tag = item;
                    if (item is EventGroup group)
                        AutomationProperties.SetAutomationId(b, $"history-row-{group.Latest.Id}");
                }
            }

            foreach (var obsolete in rows.Keys.Where(id => !keys.Contains(id)).ToArray())
            {
                list.Children.Remove(rows[obsolete].Row);
                rows.Remove(obsolete);
            }

            var index = 0;
            foreach (var item in wanted)
            {
                if (!rows.TryGetValue(key(item), out var row)) continue;
                if (index >= list.Children.Count || !ReferenceEquals(list.Children[index], row.Row))
                {
                    list.Children.Remove(row.Row);
                    list.Children.Insert(index, row.Row);
                }

                index++;
            }
        }

        _inspectorUpdates.Add(Update);
        Update();
    }

    private string NationName(int id)
    {
        return _engine.State.Nations.FirstOrDefault(n => n.Id == id)?.Name ?? (id == 0 ? "世界" : $"国家 #{id}（已消亡）");
    }

    private string ResidentName(int id)
    {
        return _engine.GetResident(id)?.Name ?? (id == 0 ? "未指定" : $"居民 #{id}");
    }

    private string TownName(int id)
    {
        return _engine.State.Settlements.FirstOrDefault(t => t.Id == id)?.Name ?? (id == 0 ? "无" : $"聚落 #{id}");
    }

    private static string DateLabel(long tick)
    {
        return tick < 0 ? "尚无记录" : $"第 {1 + tick / 120} 年 {1 + tick % 120} 日";
    }

    private static string StockLabel(ResourceStock stock)
    {
        return ResourceStock.Format(stock) is { Length: > 0 } text ? text : "暂无库存";
    }

    private void BuildOverview(StackPanel panel)
    {
        panel.Children.Add(LiveText(() => $"{_engine.State.Population:N0} 位居民\n{_engine.State.Nations.Count} 个国家", 19,
            Mint));
        panel.Children.Add(LiveText(() =>
            $"{DateLabel(_engine.State.Tick)}\n{_engine.State.Settlements.Count} 处聚落\n种子 {_engine.State.Seed}"));
        panel.Children.Add(Named(Button("我的关注", () => OpenInspector("watched")), "overview-watched"));
        panel.Children.Add(Named(Button("聚落列表", () => OpenInspector("settlements")), "overview-settlements"));
        panel.Children.Add(Named(Button("科技与魔法研究", () => OpenInspector("research")), "overview-research"));
        panel.Children.Add(Named(Button("消息与通信", () => OpenInspector("communication")), "overview-communication"));
        var borders = Named(new CheckBox { Content = "显示国界", IsChecked = _map.ShowBorders }, "map-borders");
        borders.IsCheckedChanged += (_, _) =>
        {
            _map.ShowBorders = borders.IsChecked == true;
            _map.RefreshWorld();
        };
        panel.Children.Add(borders);
        BuildMapHighlights(panel);
        panel.Children.Add(Named(Button("玩法说明", () => OpenInspector("guide")), "overview-guide"));
        panel.Children.Add(Named(Button("建筑与道路列表", () => OpenInspector("structures")), "overview-structures"));
        panel.Children.Add(Text("矿藏显示", 13, Mint));
        var resources =
            Named(
                new ComboBox
                {
                    ItemsSource = new[] { "已发现或已有聚落掌握开采技术", "全部矿藏（含未发现）", "关闭矿藏显示" },
                    SelectedIndex = (int)_resourceVisibility,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                }, "map-resources");
        resources.SelectionChanged += (_, _) =>
        {
            _resourceVisibility = (ResourceVisibility)Math.Max(0, resources.SelectedIndex);
            _map.ResourceVisibility = _resourceVisibility;
            _map.RefreshWorld();
        };
        panel.Children.Add(resources);
        foreach (var kind in new[] { ResourceKind.Coal, ResourceKind.Oil, ResourceKind.RareEarth })
        {
            var show = Named(
                new CheckBox
                {
                    Content = "显示" + ResourceStock.Name(kind), IsChecked = _map.VisibleResources.Contains(kind),
                },
                "map-resource-" + kind.ToString().ToLowerInvariant());
            show.IsCheckedChanged += (_, _) =>
            {
                if (show.IsChecked == true) _map.VisibleResources.Add(kind);
                else _map.VisibleResources.Remove(kind);
                _map.RefreshWorld();
            };
            panel.Children.Add(show);
        }

        var wildlife = Named(new CheckBox { Content = "近景显示野生动物图标", IsChecked = _map.ShowWildlife }, "map-wildlife");
        wildlife.IsCheckedChanged += (_, _) =>
        {
            _map.ShowWildlife = wildlife.IsChecked == true;
            _map.RefreshWorld();
        };
        panel.Children.Add(wildlife);
        var plants = Named(new CheckBox { Content = "显示植物资源", IsChecked = _map.ShowPlants }, "map-plants");
        plants.IsCheckedChanged += (_, _) =>
        {
            _map.ShowPlants = plants.IsChecked == true;
            _map.RefreshWorld();
        };
        panel.Children.Add(plants);
        var names = Named(new CheckBox { Content = "近景显示建筑名称", IsChecked = _map.ShowBuildingNames },
            "map-building-names");
        names.IsCheckedChanged += (_, _) =>
        {
            _map.ShowBuildingNames = names.IsChecked == true;
            _map.InvalidateVisual();
        };
        panel.Children.Add(names);
        var legend = FoldSection(panel, "图例与资源说明", "map-legend");
        legend.Children.Add(
            Paragraph("矿藏图标：煤堆、油井、紫色矿晶。显示设置只改变你看到的内容，不会让居民获得开采知识。\n放大到 3 倍可见动植物；图标越大，动物越多或植被越密。同格可有多种动物，植物对应当地可采资源。"));
        legend.Children.Add(Paragraph("工作标记在 5 倍近景显示。图标表示实际任务，图标下的短线表示正在移动；查看角色可见具体设施、材料与后续步骤。"));
        var tasks = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var icon in Enum.GetValues<ResidentTaskIcon>())
        {
            var item = new StackPanel { Margin = new Thickness(5), Spacing = 2 };
            item.Children.Add(new Image { Source = _map.ActivityPreview(icon), Width = 28, Height = 28 });
            item.Children.Add(Text(WorldEngine.TaskIconName(icon), 11));
            tasks.Children.Add(item);
        }

        legend.Children.Add(tasks);
        var races = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var race in Enum.GetValues<RaceKind>())
        {
            var item = new StackPanel { Margin = new Thickness(4, 0), Spacing = 2 };
            item.Children.Add(new Image
            {
                Source = _map.ResidentPreview(race, Profession.Lumberjack), Width = 32, Height = 40,
            });
            item.Children.Add(Text(RaceName(race), 11));
            races.Children.Add(item);
        }

        legend.Children.Add(races);
        legend.Children.Add(
            Paragraph("斧头：伐木工   矿镐与头灯：矿工   草帽与锄头：农民\n铁盔与盾：战士   尖帽与法杖：法师   书本：学者\n邮包：信使   背包与货袋：商人   金色绶带：代表"));
        var animals = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var kind in Enum.GetValues<WildlifeKind>().Where(k => k != WildlifeKind.None))
        {
            var item = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 3, Margin = new Thickness(3, 2),
            };
            item.Children.Add(new Image { Source = _map.AnimalPreview(kind), Width = 20, Height = 20 });
            item.Children.Add(Text(WorldEngine.WildlifeName(kind), 11));
            animals.Children.Add(item);
        }

        legend.Children.Add(animals);
        var vegetation = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var kind in Enum.GetValues<PlantKind>())
        {
            var item = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 3, Margin = new Thickness(3, 2),
            };
            item.Children.Add(new Image { Source = _map.PlantPreview(kind), Width = 20, Height = 20 });
            item.Children.Add(Text(PlantResources.Name(kind), 11));
            vegetation.Children.Add(item);
        }

        legend.Children.Add(vegetation);
        panel.Children.Add(Text("文明国家", 12, Mint));
        LiveRows(panel, () => _engine.State.Nations.OrderBy(n => n.Id).Take(16), n => n.Id.ToString(),
            n => $"{n.Name}   人口 {n.Population}   领土 {n.Territory} 格", n => OpenNation(n.Id));
        panel.Children.Add(Button("世界规则", () => OpenInspector("rules")));
        panel.Children.Add(Named(Button("建设与运输", () => OpenInspector("infrastructure")), "overview-infrastructure"));
        panel.Children.Add(Text("近期重要事件", 12, Mint));
        LiveRows(panel,
            () => WorldStories.Group(_engine.State.Events.Where(e =>
                e.Importance >= EventImportance.Notable && e.Kind != WorldEventKind.Editor &&
                e.Kind != WorldEventKind.Policy)).Take(5), GroupKey, GroupLabel, g => FocusEvent(g.Latest));
        panel.Children.Add(Button("展开编年史", () => OpenInspector("history")));
    }

    private void BuildNationList(StackPanel panel)
    {
        panel.Children.Add(LiveText(() => $"{_engine.State.Nations.Count} 个国家\n点击查看其文化、政策、研究与库存"));
        LiveRows(panel, () => _engine.State.Nations.OrderBy(n => n.Id), n => n.Id.ToString(),
            n => $"{n.Name}   人口 {n.Population}   领土 {n.Territory} 格\n{n.Decision}", n => OpenNation(n.Id));
    }

    private void BuildResidentList(StackPanel panel)
    {
        var search = Named(new TextBox { Text = _residentSearch, PlaceholderText = "搜索姓名 / 编号 / 国家" },
            "resident-search");
        BindSearch(search, value =>
        {
            _residentSearch = value;
            _residentPage = 0;
        });
        panel.Children.Add(search);
        var deceased = Named(new CheckBox { Content = "包含亡者", IsChecked = _includeDeceased }, "residents-deceased");
        deceased.IsCheckedChanged += (_, _) =>
        {
            _includeDeceased = deceased.IsChecked == true;
            _residentPage = 0;
            RefreshInspector();
        };
        panel.Children.Add(deceased);

        IEnumerable<Resident> Matches()
        {
            return (_includeDeceased
                    ? _engine.State.Residents.Concat(_engine.State.ArchivedResidents)
                    : _engine.State.Residents)
                .Where(r => string.IsNullOrWhiteSpace(_residentSearch) || DisplayFormat.Text(r.Name)
                                                                           .Contains(
                                                                               DisplayFormat.Text(_residentSearch),
                                                                               StringComparison.OrdinalIgnoreCase)
                                                                       || r.Id.ToString() == _residentSearch ||
                                                                       NationName(r.NationId).Contains(_residentSearch,
                                                                           StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.Id);
        }

        Resident[] filtered = [];
        _inspectorUpdates.Add(() =>
        {
            filtered = Matches().ToArray();
            _residentPage = Math.Clamp(_residentPage, 0, Math.Max(0, (filtered.Length - 1) / 20));
        });
        filtered = Matches().ToArray();
        var pages = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        pages.Children.Add(Named(Button("上一页", () =>
        {
            _residentPage = Math.Max(0, _residentPage - 1);
            RefreshInspector();
        }), "residents-prev"));
        pages.Children.Add(Named(Button("下一页", () =>
        {
            _residentPage = Math.Min(Math.Max(0, (filtered.Length - 1) / 20), _residentPage + 1);
            RefreshInspector();
        }), "residents-next"));
        pages.Children.Add(LiveText(() =>
            $"{filtered.Length} 人   第 {_residentPage + 1}/{Math.Max(1, (filtered.Length + 19) / 20)} 页"));
        panel.Children.Add(pages);
        LiveRows(panel, () => filtered.Skip(_residentPage * 20).Take(20), r => r.Id.ToString(),
            r =>
                $"{r.Name}   {ProfessionName(r.Profession)}   {r.Age:0} 岁\n{(r.Health <= 0 ? WorldEngine.DeathCauseName(r.DeathCause) : ResidentTask(r))}",
            r => OpenResident(r.Id));
    }

    private void BuildTileInspector(StackPanel panel)
    {
        if (_selectedTile is not { } point || point.X < 0 || point.Y < 0 || point.X >= _engine.State.Width ||
            point.Y >= _engine.State.Height)
        {
            panel.Children.Add(Paragraph("请先在地图上选择一处位置。"));
            return;
        }

        Tile Tile()
        {
            return _engine.State.Tiles[point.Y * _engine.State.Width + point.X];
        }

        panel.Children.Add(LiveText(() => TerrainName(Tile().Terrain), 16, Mint));
        panel.Children.Add(Named(
            LiveText(() => _engine.GetTileProductionSummary(point.X, point.Y, _resourceVisibility)), "tile-water"));
        panel.Children.Add(Named(Button("安排居民改造此地", () => ShowLandProject(point.X, point.Y)), "tile-improve"));
        panel.Children.Add(Named(Button("编辑此地资源与道路", () => ShowTileEditor(point.X, point.Y)), "tile-edit"));
        LiveRows(panel, () => _engine.State.Society.Buildings.Where(b => b.X == point.X && b.Y == point.Y),
            b => b.Id.ToString(),
            b => BuildingLabel(b) + "\n" + BuildingTask(b), OpenBuilding);
        var wildlife = FoldSection(panel, "生态与栖息地", "tile-wildlife");
        wildlife.Children.Add(LiveText(() => _engine.GetTileEcologySummary(point.X, point.Y)));
        wildlife.Children.Add(LiveText(() => string.Join("\n", Enum.GetValues<RaceKind>().Select(r =>
            $"{RaceName(r)}：{(RaceTerrainRules.For(r, Tile().Terrain).Habitable ? "宜居" : "不宜居")}"))));
        var effects = FoldSection(panel, "地块加成与减益", "tile-effects");
        effects.Children.Add(LiveText(() => EffectLabel(_engine.GetTileEffects(point.X, point.Y))));
        var local = FoldSection(panel, "归属与周围环境", "tile-context");
        var geography = FoldSection(local, "地形生成信息", "tile-geography");
        geography.Children.Add(LiveText(() => $"海拔 {Tile().Elevation} / 255"));
        local.Children.Add(LiveText(() =>
            $"{NationName(Tile().NationId)}   {TownName(Tile().ClaimedSettlementId)}\n{(Tile().RoadLevel > 0 ? $"道路 {Tile().RoadLevel} 级\n" : "")}步行：{(double.IsFinite(_engine.GetTerrainMoveCost(point.X, point.Y)) ? $"耗时系数 {_engine.GetTerrainMoveCost(point.X, point.Y):0.##}" : "无法通行，需桥梁、山路或载具")}"));
        if (_engine.State.Nations.Any(nation => nation.Id == Tile().NationId))
            local.Children.Add(Named(Button("查看归属国家", () => OpenNation(Tile().NationId)), "tile-nation"));
        if (_engine.State.Settlements.Any(town => town.Id == Tile().ClaimedSettlementId))
        {
            local.Children.Add(Named(Button("查看归属聚落", () => OpenSettlement(Tile().ClaimedSettlementId)),
                "tile-settlement"));
        }

        LiveRows(local,
            () => _engine.State.Conflicts.Where(c =>
                c.SettlementId == Tile().SettlementId || Math.Abs(c.X - point.X) + Math.Abs(c.Y - point.Y) <= 3),
            c => c.Id.ToString(),
            c =>
                $"{(c.Stage == ConflictStage.Dispute ? "资源争执" : c.Stage == ConflictStage.Confrontation ? "持续对峙" : c.Stage == ConflictStage.Violence ? "局部斗殴" : "已平息")}   {c.Participants.Count} 人   紧张 {c.Tension:0}%");
        var nearby = FoldSection(panel, "附近居民", "tile-residents");
        LiveRows(nearby,
            () => _engine.State.Residents.Where(r => Math.Abs(r.X - point.X) <= 6 && Math.Abs(r.Y - point.Y) <= 6)
                .OrderBy(r => r.Id).Take(16), r => r.Id.ToString(),
            r => $"{r.Name}   {ProfessionName(r.Profession)}\n{ResidentTask(r)}", r => OpenResident(r.Id));
        panel.Children.Add(Button("查看建筑与道路", () => OpenInspector("structures")));
    }

    private string EventLabel(WorldEvent item)
    {
        return
            $"{ImportanceName(item.Importance)}   {DateLabel(item.Tick)}   {NationName(item.NationId)}\n{item.Message}";
    }

    private void FocusEvent(WorldEvent item)
    {
        if (_mapPick is not null) return;
        if (_mobilePanel && _inspectorMode == "event" && _eventDetailId == item.Id) return;
        RememberLocation();
        _eventDetailId = item.Id;
        OpenInspector("event", false);
    }

    private void BuildEventInspector(StackPanel panel)
    {
        var item = _engine.State.Events.FirstOrDefault(e => e.Id == _eventDetailId);
        if (item is null)
        {
            panel.Children.Add(Paragraph("这条事件已不在保留的历史中。"));
            return;
        }

        panel.Children.Add(Paragraph(EventLabel(item)));
        foreach (var causeId in WorldStories.Causes(item))
        {
            var cause = _engine.State.Events.FirstOrDefault(e => e.Id == causeId);
            if (cause is not null)
            {
                panel.Children.Add(Named(Button("前因：" + cause.Message, () => FocusEvent(cause)),
                    $"event-cause-{cause.Id}"));
            }
            else panel.Children.Add(Paragraph($"前因 #{causeId} 已超出历史保留范围。"));
        }

        if (item.EvidenceFactId > 0)
            panel.Children.Add(Paragraph(EvidenceLabel(item.EvidenceFactId) + "\n关联不意味着其他居民已经获知。"));
        LiveRows(panel, () => _engine.State.Events.Where(e => WorldStories.Causes(e).Contains(item.Id)),
            e => e.Id.ToString(), e => "已发生的后续：" + EventLabel(e), FocusEvent);
        var group = WorldStories.Group(_engine.State.Events)
            .FirstOrDefault(g => g.Count > 1 && g.Entries.Any(e => e.Id == item.Id));
        if (group is not null)
        {
            panel.Children.Add(Text($"同类记录 {group.Count} 次\n各次事件保留独立结果", 13, Mint));
            foreach (var member in group.Entries)
                panel.Children.Add(Named(Button(EventLabel(member), () => FocusEvent(member)),
                    $"event-member-{member.Id}"));
        }

        if (item.SettlementId > 0 && _engine.State.Settlements.Any(t => t.Id == item.SettlementId))
        {
            panel.Children.Add(Button("查看 " + TownName(item.SettlementId),
                () => OpenWatched(new ObservedObject(ObservedObjectKind.Settlement, item.SettlementId))));
        }

        if (item.NationId > 0 && _engine.State.Nations.Any(n => n.Id == item.NationId))
            panel.Children.Add(Button("查看 " + NationName(item.NationId), () => OpenNation(item.NationId)));
        if (item.ResidentId > 0)
            panel.Children.Add(Button("查看 " + ResidentName(item.ResidentId), () => OpenResident(item.ResidentId)));
        panel.Children.Add(Named(Button("定位事件", () =>
        {
            if (item.X >= 0) _map.FocusTile(item.X, item.Y);
            else if (item.ResidentId > 0) _map.FocusResident(item.ResidentId);
            CloseInspector();
        }), "event-locate"));
    }

    private void BuildHistoryInspector(StackPanel panel)
    {
        var watched = Named(new CheckBox { Content = Text("仅看关注对象", 12), IsChecked = _historyWatchedOnly },
            "history-watched");
        watched.IsCheckedChanged += (_, _) =>
        {
            _historyWatchedOnly = watched.IsChecked == true;
            RefreshInspector();
        };
        panel.Children.Add(watched);
        var importance =
            Named(
                new ComboBox
                {
                    ItemsSource = new[] { "重大事件（默认）", "普通与重要日常", "全部事件" },
                    SelectedIndex = _historyImportance,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                }, "history-importance");
        importance.SelectionChanged += (_, _) =>
        {
            _historyImportance = importance.SelectedIndex;
            RefreshInspector();
        };
        panel.Children.Add(importance);
        var countries = new[] { (Id: 0, Name: "所有国家") }.Concat(_engine.State.Nations.Select(n => (n.Id, n.Name)))
            .Concat(_engine.State.Events.SelectMany(e => new[] { e.NationId, e.SecondNationId })
                .Where(id => id > 0 && !_engine.State.Nations.Any(n => n.Id == id)).Distinct()
                .Select(id => (Id: id, Name: NationName(id)))).ToList();
        var country =
            Named(
                new ComboBox
                {
                    ItemsSource = countries.Select(n => n.Name).ToArray(),
                    SelectedIndex = Math.Max(0, countries.FindIndex(n => n.Id == _historyNationId)),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                }, "history-nation");
        country.SelectionChanged += (_, _) =>
        {
            if (country.SelectedIndex >= 0) _historyNationId = countries[country.SelectedIndex].Id;
            RefreshInspector();
        };
        panel.Children.Add(country);
        var kinds = Enum.GetValues<WorldEventKind>();
        var kind = Named(
            new ComboBox
            {
                ItemsSource = new[] { "所有类型" }.Concat(kinds.Select(EventKindName)).ToArray(),
                SelectedIndex = _historyKind.HasValue ? Array.IndexOf(kinds, _historyKind.Value) + 1 : 0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            }, "history-kind");
        kind.SelectionChanged += (_, _) =>
        {
            _historyKind = kind.SelectedIndex > 0 ? kinds[kind.SelectedIndex - 1] : null;
            RefreshInspector();
        };
        panel.Children.Add(kind);
        var search = Named(new TextBox { Text = _historySearch, PlaceholderText = "搜索事件内容" }, "history-search");
        BindSearch(search, value => _historySearch = value);
        panel.Children.Add(search);

        IEnumerable<WorldEvent> Filter()
        {
            return _engine.State.Events.Where(e =>
                (!_historyWatchedOnly || IsWatched(e)) &&
                (_historyImportance == 2 || (_historyImportance == 0
                    ? e.Importance >= EventImportance.Major
                    : e.Importance < EventImportance.Major)) &&
                (_historyNationId == 0 || e.NationId == _historyNationId || e.SecondNationId == _historyNationId) &&
                (!_historyKind.HasValue || e.Kind == _historyKind) &&
                e.Message.Contains(_historySearch, StringComparison.OrdinalIgnoreCase)).Reverse();
        }

        panel.Children.Add(LiveText(() => $"符合筛选：{Filter().Count()} 条\n点击事件查看经过和前因"));
        LiveRows(panel, () => WorldStories.Group(Filter()).Take(100), GroupKey, GroupLabel, g => FocusEvent(g.Latest));
    }

    private static string ImportanceName(EventImportance value)
    {
        return value switch
        {
            EventImportance.Routine => "普通",
            EventImportance.Notable => "重要日常",
            EventImportance.Major => "重大",
            _ => "历史转折",
        };
    }

    private static string GoalName(AgentGoalKind value)
    {
        return value switch
        {
            AgentGoalKind.Idle => "重新选择任务",
            AgentGoalKind.Eat => "寻找食物",
            AgentGoalKind.Gather => "采集资源",
            AgentGoalKind.Work => "生产劳动",
            AgentGoalKind.Rest => "休息恢复",
            AgentGoalKind.Flee => "逃离危险",
            AgentGoalKind.Socialize => "交流消息",
            AgentGoalKind.DeliverMessage => "传递消息",
            AgentGoalKind.Trade => "运输货物",
            AgentGoalKind.Petition => "表达诉求",
            AgentGoalKind.Study => "学习研究",
            AgentGoalKind.TrainMagic => "魔法训练",
            AgentGoalKind.March => "执行军令",
            AgentGoalKind.Migrate => "迁往新家园",
            AgentGoalKind.Explore => "实地探索",
            AgentGoalKind.ClaimLand => "占领地块",
            AgentGoalKind.FetchWater => "打水或寻找水源",
            AgentGoalKind.Hunt => "狩猎",
            AgentGoalKind.Fish => "捕鱼",
            AgentGoalKind.ExtinguishFire => "用水扑救火灾",
            _ => "返回家园",
        };
    }

    private static string EventKindName(WorldEventKind value)
    {
        return value switch
        {
            WorldEventKind.Founding => "建国定居",
            WorldEventKind.Growth => "发展人口",
            WorldEventKind.Trade => "贸易运输",
            WorldEventKind.Diplomacy => "外交",
            WorldEventKind.War => "战争",
            WorldEventKind.Disaster => "灾害",
            WorldEventKind.Death => "死亡",
            WorldEventKind.Editor => "玩家编辑",
            WorldEventKind.Personal => "个人经历",
            WorldEventKind.Communication => "消息通信",
            WorldEventKind.Culture => "文化",
            WorldEventKind.Policy => "制度政策",
            WorldEventKind.Research => "研究",
            WorldEventKind.Construction => "建设",
            _ => "魔法",
        };
    }
}
