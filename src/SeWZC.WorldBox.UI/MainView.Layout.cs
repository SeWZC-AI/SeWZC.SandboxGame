using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    private void BuildMainLayout()
    {
        var mapLayer = new Grid { ClipToBounds = true, Background = Brush.Parse("#122D3D") };
        mapLayer.Children.Add(_map);
        _hud.DataContext = _viewModel;
        _performancePanel.Child = _performanceText;
        _performancePanel.Width = 76;
        _performancePanel.IsHitTestVisible = false;
        _hud.FindControl<ContentControl>("PerformanceHost")!.Content = _performancePanel;
        _cameraBar.Children.Add(IconButton("plus", () => _map.ZoomIn(), "放大地图", "map-zoom-in"));
        _cameraBar.Children.Add(IconButton("minus", () => _map.ZoomOut(), "缩小地图", "map-zoom-out"));
        _cameraBar.Children.Add(Named(Button("全图", () => _map.FitWorld(), "显示整个世界", 40), "map-fit"));
        mapLayer.Children.Add(_cameraBar);
        var toolsPanel = new StackPanel { Spacing = 4 };
        var categories = new UniformGrid { Columns = 4, Rows = 1 };
        foreach (var (label, category) in new[]
                 {
                     ("山海", ToolCategory.Terrain), ("众生", ToolCategory.Life), ("天灾", ToolCategory.Disaster),
                     ("建设", ToolCategory.Build),
                 })
        {
            var categoryButton = Named(Button(label, () => SetCategory(category)), "tool-category-" + category.Id);
            categoryButton.Tag = category;
            categoryButton.Padding = new Thickness(3, 3);
            categoryButton.Margin = new Thickness(2, 0);
            _categoryButtons.Add(categoryButton);
            categories.Children.Add(categoryButton);
        }

        Named(_brushPicker, "brush-size");
        _brushPicker.SelectionChanged += (_, _) =>
        {
            var index = Math.Max(0, _brushPicker.SelectedIndex);
            _category.SetBrush(_map, index);
        };
        Named(_buildMode, "build-mode");
        _buildMode.SelectionChanged += (_, _) => _map.GiftBuildings = _buildMode.SelectedIndex == 0;
        toolsPanel.Children.Add(categories);
        for (var i = 0; i < 8; i++)
        {
            var slot = i;
            var swatch = new Border
            {
                Width = 22,
                Height = 15,
                CornerRadius = new CornerRadius(3),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            var label = Text("—", 10);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            var content = new StackPanel { Spacing = 3 };
            content.Children.Add(swatch);
            content.Children.Add(label);
            var button =
                Named(
                    new Button
                    {
                        Content = content,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        Height = 44,
                        Padding = new Thickness(3, 3),
                        Margin = new Thickness(2),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(6),
                        HorizontalContentAlignment = HorizontalAlignment.Center,
                    }, $"tool-slot-{i}");
            button.Click += (_, _) =>
            {
                if (_slotTools[slot] is { } tool)
                    SelectTool(tool);
            };
            _toolSlots[i] = button;
            _toolSwatches[i] = swatch;
            _toolLabels[i] = label;
            _toolChoices.Children.Add(button);
        }

        toolsPanel.Children.Add(_toolChoices);
        var pagination = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _toolPrevious = Named(Button("上一页", () =>
        {
            _toolPage--;
            SetCategory(_category);
        }), "tool-page-prev");
        _toolNext = Named(Button("下一页", () =>
        {
            _toolPage++;
            SetCategory(_category);
        }), "tool-page-next");
        pagination.Children.Add(_toolPrevious);
        pagination.Children.Add(_toolPageLabel);
        pagination.Children.Add(_toolNext);
        toolsPanel.Children.Add(pagination);
        var settings = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 5, Height = 36,
        };
        Named(_toolContext, "tool-context");
        _toolContext.SelectionChanged += (_, _) => OnToolContextChanged();
        var hint = new StackPanel { Spacing = 2 };
        hint.Children.Add(_toolTitle);
        hint.Children.Add(_toolHint);
        settings.Children.Add(hint);
        Grid.SetColumn(_toolContext, 1);
        settings.Children.Add(_toolContext);
        Grid.SetColumn(_brushPicker, 2);
        settings.Children.Add(_brushPicker);
        Grid.SetColumn(_buildMode, 2);
        settings.Children.Add(_buildMode);
        toolsPanel.Children.Add(settings);
        var bridgeDirection = Named(new ComboBox { ItemsSource = new[] { "左右", "上下" }, SelectedIndex = 0, Width = 105 },
            "bridge-direction");
        bridgeDirection.Margin = new Thickness(0, 0, 8, 4);
        var bridgeLevel =
            Named(
                new ComboBox
                {
                    ItemsSource = new[] { "1 级：离岸 2 格", "2 级：离岸 4 格", "3 级：离岸 6 格" },
                    SelectedIndex = 0,
                    Width = 165,
                }, "bridge-level");
        bridgeLevel.Margin = new Thickness(0, 0, 0, 4);
        bridgeDirection.SelectionChanged += (_, _) =>
        {
            _map.ConstructionBridgeDirection = (BridgeDirection)Math.Max(0, bridgeDirection.SelectedIndex);
            _map.CancelPlacement();
        };
        bridgeLevel.SelectionChanged += (_, _) =>
        {
            _map.ConstructionBridgeLevel = Math.Max(1, bridgeLevel.SelectedIndex + 1);
            _map.CancelPlacement();
        };
        var bridgeLabel = Text("桥梁", 12);
        bridgeLabel.VerticalAlignment = VerticalAlignment.Center;
        bridgeLabel.Margin = new Thickness(0, 0, 8, 4);
        _bridgeSettings.Children.Add(bridgeLabel);
        _bridgeSettings.Children.Add(bridgeDirection);
        _bridgeSettings.Children.Add(bridgeLevel);
        toolsPanel.Children.Add(_bridgeSettings);
        var toolBox = _toolBar;
        toolBox.Child = toolsPanel;
        toolBox.Background = Brush.Parse("#F0172632");
        toolBox.BorderBrush = Line;
        toolBox.BorderThickness = new Thickness(1);
        toolBox.CornerRadius = new CornerRadius(4);
        toolBox.Padding = new Thickness(6);
        _toolScroll.Content = toolBox;
        _hud.FindControl<ContentControl>("ToolHost")!.Content = _toolScroll;
        var placement = new StackPanel { Spacing = 4 };
        _placementText.TextWrapping = TextWrapping.Wrap;
        placement.Children.Add(_placementText);
        var placementActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        placementActions.Children.Add(Named(Button("确认放置", () => _map.ConfirmPlacement()), "placement-confirm"));
        placementActions.Children.Add(Named(Button("取消", () => _map.CancelPlacement()), "placement-cancel"));
        placement.Children.Add(placementActions);
        _placementBar.Child = placement;
        _placementBar.Background = Panel;
        _placementBar.Padding = new Thickness(8);
        _placementBar.IsVisible = false;
        _placementBar.VerticalAlignment = VerticalAlignment.Top;
        _placementBar.HorizontalAlignment = HorizontalAlignment.Left;
        _placementBar.Margin = new Thickness(10, 112, 10, 10);
        _placementBar.MaxWidth = 240;
        _placementBar.CornerRadius = new CornerRadius(8);
        _placementBar.ZIndex = 30;
        mapLayer.Children.Add(_placementBar);
        var mapPickPrompt = new StackPanel { Spacing = 4 };
        mapPickPrompt.Children.Add(Text("点选目标地格", 12, Mint));
        mapPickPrompt.Children.Add(Paragraph("时间暂时停止，选点后返回原表单。"));
        mapPickPrompt.Children.Add(Named(Button("返回表单", () => FinishMapPick(), "保留原坐标并返回编辑窗口"), "map-pick-return"));
        _mapPickBar.Child = mapPickPrompt;
        _mapPickBar.Background = Panel;
        _mapPickBar.Padding = new Thickness(8);
        _mapPickBar.VerticalAlignment = VerticalAlignment.Top;
        _mapPickBar.HorizontalAlignment = HorizontalAlignment.Left;
        _mapPickBar.Margin = new Thickness(10, 112, 10, 10);
        _mapPickBar.MaxWidth = 240;
        _mapPickBar.CornerRadius = new CornerRadius(8);
        _mapPickBar.ZIndex = 30;
        Named(_mapPickBar, "map-pick-prompt");
        mapLayer.Children.Add(_mapPickBar);
        _map.PlacementChanged += message =>
        {
            _placementText.Text = message;
            _viewModel.PlacementPending = _map.HasPendingPlacement;
            _placementBar.IsVisible = _map.HasPendingPlacement;
            placementActions.IsVisible = _map.HasPendingPlacement;
        };
        mapLayer.Children.Add(_hud);
        _body.Children.Add(mapLayer);
        Named(_inspector, "inspector-panel");
        Named(_inspectorScroll, "inspector-scroll");
        _inspectorScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        var inspectorLayout = new DockPanel();
        DockPanel.SetDock(_inspectorNavigation, Dock.Top);
        inspectorLayout.Children.Add(_inspectorNavigation);
        inspectorLayout.Children.Add(_inspectorScroll);
        _inspector.Child = inspectorLayout;
        _inspector.Background = Panel;
        _inspector.BorderBrush = Line;
        _inspector.BorderThickness = new Thickness(1);
        _inspector.CornerRadius = new CornerRadius(4);
        _body.Children.Add(_inspector);
        _shell.Children.Add(_body);
        _shell.IsEnabled = false;
        var root = new Grid();
        root.Children.Add(_shell);
        root.Children.Add(_modal);
        Content = root;
    }

    private void HandleViewRequest(string request)
    {
        switch (request)
        {
            case "menu": ShowWorldMenu(); break;
            case "tools": ToggleTools(); break;
            case "pan": SuspendTool(); break;
            case "selection": ViewMapSelection(); break;
            case "clear": ClearMapSelection(); break;
            case "locate": LocateMapSelection(); break;
            case "return-observation": ReturnToObservation(); break;
            case "undo": RestoreCheckpoint(); break;
            case "event":
                if (_engine.State.Events.FirstOrDefault(entry => entry.Id == _focusedEventId) is { } entry)
                    FocusEvent(entry);
                break;
            default: OpenInspector(request); break;
        }
    }

    private void ShowWorldMenu()
    {
        var panel = new StackPanel { Spacing = 6, MinWidth = 180 };
        var flyout = new Flyout { Content = panel };
        foreach (var (label, mode) in new[]
                 { ("世界概览", "overview"), ("居民", "residents"), ("国家", "nations"), ("聚落", "settlements"), ("玩法说明", "guide") })
            panel.Children.Add(Named(Button(label, () =>
            {
                flyout.Hide();
                OpenInspector(mode);
            }), "menu-" + mode));
        panel.Children.Add(new Separator());
        panel.Children.Add(Button("存档", () => { flyout.Hide(); ShowStorage(); }));
        panel.Children.Add(Named(Button("规则", () => { flyout.Hide(); ShowRules(); }), "header-rules"));
        panel.Children.Add(Button("新世界", () => { flyout.Hide(); ShowNewWorld(); }));
        flyout.ShowAt(_hud.FindControl<Button>("WorldMenu")!);
    }

    private void LocateMapSelection()
    {
        if (_mapSelectionKind == "resident")
            FocusMapResident(_selectedResidentId);
        else if (_selectedTile is { } tile)
            FocusMapTile(tile.X, tile.Y);
    }
}
