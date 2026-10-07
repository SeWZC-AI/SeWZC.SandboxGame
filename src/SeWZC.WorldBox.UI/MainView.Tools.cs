using Avalonia.Controls;
using Avalonia.Media;
using SeWZC.WorldBox.UI.Controls;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    private readonly Button? _toolNext;
    private readonly TextBlock _toolPageLabel = Text("", 11);
    private readonly Button? _toolPrevious;

    private int _toolPage;

    private void SetCategory(ToolCategory category)
    {
        if (_mapPick is not null)
            return;
        CancelPendingEdit();
        _navigation.Clear();
        _inspectorNavigationGeneration++;
        if (_category != category)
            _toolPage = 0;
        _category = category;
        _toolsOpen = true;
        _mobilePanel = false;
        _map.ActiveTool = MapTool.Pan;
        _map.CancelPlacement();
        UpdateToolContext();
        var allItems = category.Choices;
        var pages = Math.Max(1, (allItems.Count + _toolSlots.Length - 1) / _toolSlots.Length);
        _toolPage = Math.Clamp(_toolPage, 0, pages - 1);
        _toolPageLabel.Text = $"第 {_toolPage + 1} / {pages} 页";
        if (_toolPrevious is not null)
            _toolPrevious.IsEnabled = _toolPage > 0;
        if (_toolNext is not null)
            _toolNext.IsEnabled = _toolPage + 1 < pages;
        var items = allItems.Skip(_toolPage * _toolSlots.Length).Take(_toolSlots.Length).ToArray();
        _tools.Clear();
        for (var i = 0; i < _toolSlots.Length; i++)
        {
            var choice = i < items.Length ? items[i] : null;
            _slotTools[i] = choice?.Key;
            _toolSlots[i].IsEnabled = choice is not null;
            _toolSlots[i].Opacity = 1;
            _toolSlots[i].IsVisible = true;
            _toolLabels[i].Text = choice?.Label ?? "—";
            _toolSwatches[i].Background = Brush.Parse(choice?.Color ?? "#2A3C46");
            ToolTip.SetTip(_toolSlots[i], choice?.Label ?? "此分组没有更多工具");
            if (choice is not null)
                _tools.Add((choice.Key, _toolSlots[i]));
        }

        foreach (var button in _categoryButtons)
            button.Background = Equals(button.Tag, category) ? Brush.Parse("#355347") : Panel;
        _toolTitle.Text = category.Title;
        _toolHint.Text = category.Hint;
        _brushPicker.ItemsSource = category.BrushLabels;
        _brushPicker.SelectedIndex = category.DefaultBrushIndex;
        _brushPicker.IsVisible = category.HasBrush;
        _buildMode.IsVisible = category.IsConstruction;
        _bridgeSettings.IsVisible = category.IsConstruction;
        _toolContext.IsVisible = category.IsConstruction;
        foreach (var (_, button) in _tools)
        {
            button.BorderBrush = Brushes.Transparent;
            button.Background = Ink;
        }

        ApplyLayout();
    }

    private void UpdateToolContext()
    {
        _updatingToolContext = true;
        if (_category.IsConstruction)
        {
            var towns = _engine.State.Settlements.OrderBy(t => t.Id).ToArray();
            _constructionTowns = towns.Select(t => t.Id).ToArray();
            _toolContext.ItemsSource = towns.Select(t => t.Name).ToArray();
            var selected = Array.IndexOf(_constructionTowns, _map.SelectedSettlementId);
            _toolContext.SelectedIndex = selected >= 0 ? selected : towns.Length > 0 ? 0 : -1;
            if (_toolContext.SelectedIndex >= 0)
                _map.SelectedSettlementId = _constructionTowns[_toolContext.SelectedIndex];
            _toolContext.IsEnabled = towns.Length > 0;
            _toolContext.Opacity = 1;
        }
        else
        {
            _toolContext.ItemsSource = new[] { "工具分组" };
            _toolContext.SelectedIndex = 0;
            _toolContext.IsEnabled = false;
            _toolContext.Opacity = .32;
        }

        _updatingToolContext = false;
    }

    private void OnToolContextChanged()
    {
        if (_updatingToolContext || _mapPick is not null)
            return;
        if (_category.IsConstruction && _toolContext.SelectedIndex >= 0 &&
            _toolContext.SelectedIndex < _constructionTowns.Length)
            _map.SelectedSettlementId = _constructionTowns[_toolContext.SelectedIndex];
    }

    private void SelectTool(MapTool tool)
    {
        if (_mapPick is not null)
            return;
        CancelPendingEdit();
        _navigation.Clear();
        _inspectorNavigationGeneration++;
        if (_map.ActiveTool == tool)
        {
            SuspendTool();
            return;
        }

        _map.ActiveTool = tool;
        _map.CancelPlacement();
        _mobilePanel = false;
        _toolTitle.Text = _tools.Select(t => t.Tool).Contains(tool)
            ? _category.Choices.First(t => t.Key == tool).Label + "（已启用）"
            : "地图工具已启用";
        foreach (var (key, button) in _tools)
        {
            button.BorderBrush = key == tool ? Mint : Brushes.Transparent;
            button.Background = key == tool ? Brush.Parse("#2C423F") : Ink;
        }

        ApplyLayout();
        SetStatus("工具已启用，再点一次停用；触屏拖动或右键可漫游");
    }

    private void ToggleTools()
    {
        if (_mapPick is not null)
            return;
        CancelPendingEdit();
        _navigation.Clear();
        _inspectorNavigationGeneration++;
        if (_toolsOpen)
        {
            SuspendTool();
            _toolsOpen = false;
        }
        else
        {
            _toolsOpen = true;
            _mobilePanel = false;
        }

        ApplyLayout();
    }

    private void SuspendTool()
    {
        SuspendMapTool(true);
    }

    private void SuspendMapTool(bool endNavigation)
    {
        if (_mapPick is not null)
            return;
        CancelPendingEdit();
        if (endNavigation)
        {
            _navigation.Clear();
            _inspectorNavigationGeneration++;
        }

        _map.CancelPlacement();
        _map.ActiveTool = MapTool.Pan;
        _toolTitle.Text = "漫游（点选查看）";
        foreach (var (_, button) in _tools)
        {
            button.BorderBrush = Brushes.Transparent;
            button.Background = Ink;
        }

        SetStatus("漫游中，可拖动地图或轻点查看对象");
    }

    private void CloseInspector()
    {
        if (_mapPick is not null)
            return;
        CancelPendingEdit();
        _navigation.Clear();
        _inspectorNavigationGeneration++;
        _mobilePanel = false;
        _expandedInspector = false;
        _researchExpanded = false;
        ApplyLayout();
    }

    private void ShowRules()
    {
        var panel = ModalPanel("世界规则", "规则会随世界保存。关闭自主行为停止新的选择，已有项目与成果保留。");
        BuildWorldRules(panel);
        OpenModal(panel);
    }

    private async void RunEdit(Action command, string message)
    {
        await SubmitEditAsync(() =>
        {
            command();
            _map.RefreshWorld();
            RefreshUi(true);
            SetStatus(message);
        });
    }
}
