using static SeWZC.WorldBox.UI.ViewModels.WorldPresentationModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Interactivity;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    private void InitializeViewControls()
    {
        foreach (var name in new[] { "TerrainCategory", "LifeCategory", "DisasterCategory", "BuildCategory" })
        {
            var button = this.FindControl<Button>(name)!;
            var category = (ToolCategory)button.Tag!;
            AutomationProperties.SetAutomationId(button, "tool-category-" + category.Id);
            button.Click += (_, _) => SetCategory(category);
            _categoryButtons.Add(button);
        }
        for (var i = 0; i < _toolSlots.Length; i++)
        {
            var slot = i;
            _toolSlots[i] = this.FindControl<Button>($"ToolSlot{i}")!;
            _toolSwatches[i] = this.FindControl<Border>($"ToolSwatch{i}")!;
            _toolLabels[i] = this.FindControl<TextBlock>($"ToolLabel{i}")!;
            _toolSlots[i].Click += (_, _) =>
            {
                if (_slotTools[slot] is { } tool)
                    SelectTool(tool);
            };
        }
        _brushPicker.SelectionChanged += (_, _) => _category.SetBrush(_map, Math.Max(0, _brushPicker.SelectedIndex));
        _buildMode.SelectionChanged += (_, _) => _map.GiftBuildings = _buildMode.SelectedIndex == 0;
        _map.GiftBuildings = _buildMode.SelectedIndex == 0;
        _toolContext.SelectionChanged += (_, _) => OnToolContextChanged();
        var direction = this.FindControl<ComboBox>("BridgeDirectionPicker")!;
        var level = this.FindControl<ComboBox>("BridgeLevelPicker")!;
        direction.SelectionChanged += (_, _) =>
        {
            _map.ConstructionBridgeDirection = (BridgeDirection)Math.Max(0, direction.SelectedIndex);
            _map.CancelPlacement();
        };
        level.SelectionChanged += (_, _) =>
        {
            _map.ConstructionBridgeLevel = Math.Max(1, level.SelectedIndex + 1);
            _map.CancelPlacement();
        };
        _map.PlacementChanged += message =>
        {
            _placementText.Text = message;
            _viewModel.PlacementPending = _map.HasPendingPlacement;
            _placementBar.IsVisible = _map.HasPendingPlacement;
        };
    }

    private void OnZoomIn(object? sender, RoutedEventArgs args) => _map.ZoomIn();
    private void OnZoomOut(object? sender, RoutedEventArgs args) => _map.ZoomOut();
    private void OnFitWorld(object? sender, RoutedEventArgs args) => _map.FitWorld();
    private void OnConfirmPlacement(object? sender, RoutedEventArgs args) => _map.ConfirmPlacement();
    private void OnCancelPlacement(object? sender, RoutedEventArgs args) => _map.CancelPlacement();
    private void OnReturnToForm(object? sender, RoutedEventArgs args) => FinishMapPick();
    private void OnPreviousToolPage(object? sender, RoutedEventArgs args) { _toolPage--; SetCategory(_category); }
    private void OnNextToolPage(object? sender, RoutedEventArgs args) { _toolPage++; SetCategory(_category); }
    private void OnInspectorBack(object? sender, RoutedEventArgs args) => GoBack();
    private void OnInspectorClose(object? sender, RoutedEventArgs args) => CloseInspector();
    private void OnInspectorExpand(object? sender, RoutedEventArgs args)
    {
        _expandedInspector = !_expandedInspector;
        ApplyLayout();
        RefreshInspector();
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

    private void ShowWorldMenu() => _worldMenuFlyout.ShowAt(_hud.FindControl<Button>("WorldMenu")!);

    private void OnWorldMenuClick(object? sender, RoutedEventArgs args)
    {
        _worldMenuFlyout.Hide();
        switch (((Button)sender!).Tag)
        {
            case "storage": ShowStorage(); break;
            case "rules": ShowRules(); break;
            case "new-world": ShowNewWorld(); break;
            case string mode: OpenInspector(mode); break;
        }
    }

    private void LocateMapSelection()
    {
        if (_mapSelectionKind == "resident")
            FocusMapResident(_selectedResidentId);
        else if (_selectedTile is { } tile)
            FocusMapTile(tile.X, tile.Y);
    }
    private void ApplyLayout()
    {
        _isCompact = Bounds.Width < 980;
        _viewModel.IsCompactHud = _isCompact;
        _viewModel.IsShortHud = Bounds.Height < 540;
        var stackedDock = Views.GameHud.HasStackedDock(Bounds.Width);
        _map.ScaleBottomInset = stackedDock ? 138 : 88;
        _hud.Resize(Bounds.Width, _mobilePanel || _toolsOpen);
        _cameraBar.Orientation = _mobilePanel || _toolsOpen ? Orientation.Horizontal : Orientation.Vertical;
        _inspector.IsVisible = _mobilePanel;
        _inspector.Width = _isCompact ? Math.Max(280, Bounds.Width - 20)
            : _inspectorMode == "research" && _researchExpanded ? Math.Max(360, Bounds.Width - 20) : 360;
        var inspectorTop = _isCompact && _viewModel.CanReturnObservation ? 130 : 90;
        var inspectorBottom = stackedDock ? 126 : 90;
        var inspectorSpace = Math.Max(0, Bounds.Height - inspectorTop - inspectorBottom);
        _inspector.MaxHeight = _isCompact && !_expandedInspector
            ? Math.Min(inspectorSpace, Math.Clamp(inspectorSpace * .7, 140, 430)) : inspectorSpace;
        _inspector.Height = _expandedInspector ? inspectorSpace : double.NaN;
        _inspector.VerticalAlignment = _isCompact ? VerticalAlignment.Bottom : VerticalAlignment.Top;
        _inspector.HorizontalAlignment = HorizontalAlignment.Right;
        _inspector.Margin = new Thickness(10, inspectorTop, 10, inspectorBottom);
        _inspector.ZIndex = 20;
        _mapPickBar.IsVisible = _mapPick is not null;
        _toolHint.IsVisible = Bounds.Width > 600;
        UpdateToolBarSize();
        UpdateModalBounds();
        RefreshSelectionSummary();
    }

    private void UpdateToolBarSize()
    {
        var compact = Bounds.Width < 700;
        _toolChoices.Columns = compact ? 4 : 8;
        _toolChoices.Rows = compact ? 2 : 1;
        _toolScroll.Width = Math.Max(280, Math.Min(650, _map.Bounds.Width - 20));
        var hudSpace = Views.GameHud.HasStackedDock(Bounds.Width) ? 240 : 180;
        if (_viewModel.CanReturnObservation)
            hudSpace += 40;
        _toolScroll.MaxHeight = Math.Max(80, Bounds.Height - hudSpace);
        _placementBar.MaxWidth = Math.Max(120, Math.Min(280, _map.Bounds.Width - 60));
        _toolContext.Width = compact ? 108 : 132;
        _toolTitle.FontSize = compact ? 12 : 13;
    }
}
