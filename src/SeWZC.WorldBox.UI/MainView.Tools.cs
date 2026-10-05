using Avalonia.Controls;
using Avalonia.Media;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    private readonly Button? _toolNext;
    private readonly TextBlock _toolPageLabel = Text("", 11);
    private readonly Button? _toolPrevious;

    private int _toolPage;

    private void SetCategory(string category)
    {
        if (_mapPick is not null) return;
        if (category is "rules" or "inspect") return;
        CancelPendingEdit();
        _navigation.Clear();
        _inspectorNavigationGeneration++;
        if (_category != category) _toolPage = 0;
        _category = category;
        _toolsOpen = true;
        _mobilePanel = false;
        _map.ActiveTool = "pan";
        _map.CancelPlacement();
        UpdateToolContext();
        var allItems = ToolChoices(category);
        var pages = Math.Max(1, (allItems.Length + _toolSlots.Length - 1) / _toolSlots.Length);
        _toolPage = Math.Clamp(_toolPage, 0, pages - 1);
        _toolPageLabel.Text = $"第 {_toolPage + 1} / {pages} 页";
        if (_toolPrevious is not null) _toolPrevious.IsEnabled = _toolPage > 0;
        if (_toolNext is not null) _toolNext.IsEnabled = _toolPage + 1 < pages;
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
            if (choice is not null) _tools.Add((choice.Key, _toolSlots[i]));
        }

        foreach (var button in _categoryButtons)
            button.Background = Equals(button.Tag, category) ? Brush.Parse("#355347") : Panel;
        (_toolTitle.Text, _toolHint.Text) = category switch
        {
            "terrain" => ("塑造山海", "绘制地形时自动暂停"),
            "life" => ("播下文明", "选择人数，在陆地投放居民"),
            "disaster" => ("改变命运", "点击世界，降下灾害"),
            "build" => ("建设与交通", "选择归属聚落与建造方式"),
            "rules" => ("世界与文明", "查看实际规则及发展方向"),
            _ => ("见证众生", "点选查看，拖动平移，滚轮缩放"),
        };
        _brushPicker.ItemsSource = category == "life" ? new[] { "1 位居民", "12 位居民", "36 位居民" }
            : category == "disaster" ? new[] { "范围 2 格", "范围 5 格", "范围 10 格" } : new[] { "小笔刷", "中笔刷", "大笔刷" };
        _brushPicker.SelectedIndex = category == "life" ? 1 : 0;
        _brushPicker.IsVisible = category is "terrain" or "life" or "disaster";
        _buildMode.IsVisible = category == "build";
        _bridgeSettings.IsVisible = category == "build";
        _toolContext.IsVisible = category == "build";
        foreach (var (_, button) in _tools)
        {
            button.BorderBrush = Brushes.Transparent;
            button.Background = Ink;
        }

        ApplyLayout();
    }

    private ToolChoice[] ToolChoices(string category)
    {
        return category switch
        {
            "terrain" =>
            [
                new ToolChoice("Grass", "草地", "#8CAC69"), new ToolChoice("Forest", "森林", "#427D61"),
                new ToolChoice("Sand", "沙地", "#E6D09A"),
                new ToolChoice("Mountain", "山脉", "#9DABB0"), new ToolChoice("Water", "浅海", "#4A9CBA"),
                new ToolChoice("DeepWater", "深海", "#28556F"),
                new ToolChoice("Snow", "雪原", "#D4E8E7"), new ToolChoice("Hills", "丘陵", "#92905E"),
                new ToolChoice("Wetland", "湿地", "#58887D"),
                new ToolChoice("Desert", "荒漠", "#CEAE75"), new ToolChoice("River", "河流", "#428E9C"),
                new ToolChoice("Tundra", "苔原", "#99A88C"),
                new ToolChoice("Lake", "湖泊", "#559BA8"), new ToolChoice("DryFertile", "旱原", "#A3A66B"),
                new ToolChoice("Stream", "小溪", "#64A8B2"), new ToolChoice("LargeRiver", "江", "#397D99"),
                new ToolChoice("Meadow", "草甸", "#A8BE75"),
                new ToolChoice("Woodland", "疏林", "#73966B"), new ToolChoice("Rainforest", "雨林", "#35694F"),
                new ToolChoice("Savanna", "稀树草原", "#B4AB6B"),
                new ToolChoice("Scrub", "灌丛", "#9B9D72"), new ToolChoice("Floodplain", "河漫滩", "#85AF81"),
                new ToolChoice("AlpineMeadow", "高山草甸", "#91A992"),
            ],
            "life" =>
            [
                new ToolChoice("Human", "人类", "#DEBC85"), new ToolChoice("Elf", "精灵", "#90C599"),
                new ToolChoice("Dwarf", "矮人", "#BE9785"),
                new ToolChoice("Orc", "兽人", "#A9B768"),
            ],
            "disaster" =>
            [
                new ToolChoice("Fire", "火灾", "#F0A065"), new ToolChoice("Drought", "干旱", "#D8C180"),
                new ToolChoice("Plague", "疫病", "#B194C7"),
                new ToolChoice("Meteor", "陨石", "#EC8758"),
            ],
            "build" => BuildToolChoices(),
            _ => [],
        };
    }

    private void UpdateToolContext()
    {
        _updatingToolContext = true;
        if (_category == "build")
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
        if (_updatingToolContext || _mapPick is not null) return;
        if (_category == "build" && _toolContext.SelectedIndex >= 0 &&
            _toolContext.SelectedIndex < _constructionTowns.Length)
            _map.SelectedSettlementId = _constructionTowns[_toolContext.SelectedIndex];
    }

    private ToolChoice[] BuildToolChoices()
    {
        return new ToolChoice[]
        {
            new("build:Farm", "农场", "#ADBB75"), new("build:Workshop", "工坊", "#CEB294"),
            new("build:Academy", "学舍", "#91B0C8"), new("build:Waystation", "驿站", "#CEAB76"),
            new("build:Bridge", "桥梁", "#99AAC8"), new("build:MountainPass", "山路", "#B598D1"),
            new("build:Dock", "码头", "#91C7B1"), new("road:Road", "道路", "#B0A28B"), new("road:Rail", "铁路", "#ADC1D3"),
        }.Concat(Enum.GetValues<BuildingKind>()
            .Where(k => k is not (BuildingKind.TownCenter or BuildingKind.Farm or BuildingKind.Workshop
                or BuildingKind.Academy or BuildingKind.Waystation or BuildingKind.Bridge or BuildingKind.MountainPass
                or BuildingKind.Dock)).Select(k => new ToolChoice("build:" + k, WorldEngine.BuildingName(k),
                AdvancementRules.For(k)?.Magic == true ? "#B598D1" : "#91B0C8"))).ToArray();
    }

    private void SelectTool(string tool)
    {
        if (_mapPick is not null) return;
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
            ? ToolChoices(_category).First(t => t.Key == tool).Label + "（已启用）"
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
        if (_mapPick is not null) return;
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
        if (_mapPick is not null) return;
        CancelPendingEdit();
        if (endNavigation)
        {
            _navigation.Clear();
            _inspectorNavigationGeneration++;
        }

        _map.CancelPlacement();
        _map.ActiveTool = "pan";
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
        if (_mapPick is not null) return;
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

    private sealed record ToolChoice(string Key, string Label, string Color);
}
