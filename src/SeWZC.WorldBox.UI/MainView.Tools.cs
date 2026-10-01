using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    private sealed record ToolChoice(string Key, string Label, string Color);

    private void SetCategory(string category)
    {
        _category = category;
        UpdateToolContext();
        var items = ToolChoices(category);
        _tools.Clear();
        for (var i = 0; i < _toolSlots.Length; i++)
        {
            var choice = i < items.Length ? items[i] : null;
            _slotTools[i] = choice?.Key;
            _toolSlots[i].IsEnabled = choice is not null;
            _toolSlots[i].Opacity = choice is null ? .24 : 1;
            _toolLabels[i].Text = choice?.Label ?? "—";
            _toolSwatches[i].Background = Brush.Parse(choice?.Color ?? "#2A3C46");
            ToolTip.SetTip(_toolSlots[i], choice?.Label ?? "此分组没有更多工具");
            if (choice is not null) _tools.Add((choice.Key, _toolSlots[i]));
        }
        foreach (var button in _categoryButtons) button.Background = Equals(button.Tag, category) ? Brush.Parse("#355347") : Panel;
        (_toolTitle.Text, _toolHint.Text) = category switch
        {
            "terrain" => ("塑造山海", "绘制地形 · 编辑时自动暂停"),
            "life" => ("播下文明", "点击陆地，投放 12 位居民"),
            "disaster" => ("改变命运", "点击世界，降下灾害"),
            "build" => ("建设与交通", "设施需要归属聚落与材料"),
            "rules" => ("世界与文明", "查看实际规则及发展方向"),
            _ => ("见证众生", "点击居民检查 · 拖动平移 · 滚轮缩放")
        };
        if (items.Length > 0) SelectTool(items[0].Key);
    }

    private ToolChoice[] ToolChoices(string category) => category switch
    {
        "terrain" when _terrainPage == 1 => [new("Wetland", "湿地", "#58887D"), new("Desert", "荒漠", "#CEAE75"), new("River", "河流", "#428E9C"), new("Tundra", "苔原", "#99A88C"), new("Forest", "森林", "#427D61"), new("Grass", "草地", "#8CAC69"), new("Snow", "雪原", "#D4E8E7"), new("Hills", "丘陵", "#92905E")],
        "terrain" => [new("Grass", "草地", "#8CAC69"), new("Forest", "森林", "#427D61"), new("Sand", "沙地", "#E6D09A"), new("Mountain", "山脉", "#9DABB0"), new("Water", "浅海", "#4A9CBA"), new("DeepWater", "深海", "#28556F"), new("Snow", "雪原", "#D4E8E7"), new("Hills", "丘陵", "#92905E")],
        "life" => [new("Human", "人类", "#DEBC85"), new("Elf", "精灵", "#90C599"), new("Dwarf", "矮人", "#BE9785"), new("Orc", "兽人", "#A9B768")],
        "disaster" => [new("Fire", "火灾", "#F0A065"), new("Drought", "干旱", "#D8C180"), new("Plague", "疫病", "#B194C7")],
        "build" => BuildToolChoices(),
        "rules" => [new("panel:rules", "世界规则", "#B8E9BC"), new("panel:nations", "文化制度", "#DEC18C"), new("panel:infrastructure", "建设物流", "#91B0C8"), new("panel:communication", "消息网络", "#C3A7DB")],
        _ => [new("inspect", "检查", "#B8E9BC"), new("pan", "漫游", "#7DACBA"), new("panel:residents", "居民", "#DEBC85"), new("panel:nations", "国家", "#91B0C8"), new("panel:history", "编年史", "#DEC18C"), new("toggle:borders", "国界", "#A7C195"), new("panel:infrastructure", "运输", "#91B0C8"), new("panel:communication", "通信", "#C3A7DB")]
    };

    private void UpdateToolContext()
    {
        _updatingToolContext = true;
        if (_category == "terrain")
        {
            _toolContext.ItemsSource = new[] { "基础地形", "生态地形" }; _toolContext.SelectedIndex = _terrainPage; _toolContext.IsEnabled = true; _toolContext.Opacity = 1;
        }
        else if (_category == "build")
        {
            var towns = _engine.State.Settlements.OrderBy(t => t.Id).ToArray(); _constructionTowns = towns.Select(t => t.Id).ToArray();
            _toolContext.ItemsSource = towns.Select(t => t.Name).ToArray();
            var selected = Array.IndexOf(_constructionTowns, _map.SelectedSettlementId); _toolContext.SelectedIndex = selected >= 0 ? selected : towns.Length > 0 ? 0 : -1;
            if (_toolContext.SelectedIndex >= 0) _map.SelectedSettlementId = _constructionTowns[_toolContext.SelectedIndex];
            _toolContext.IsEnabled = towns.Length > 0; _toolContext.Opacity = 1;
        }
        else { _toolContext.ItemsSource = new[] { "工具分组" }; _toolContext.SelectedIndex = 0; _toolContext.IsEnabled = false; _toolContext.Opacity = .32; }
        _updatingToolContext = false;
    }
    private void OnToolContextChanged()
    {
        if (_updatingToolContext) return;
        if (_category == "terrain" && _toolContext.SelectedIndex >= 0) { _terrainPage = _toolContext.SelectedIndex; SetCategory("terrain"); }
        else if (_category == "build" && _toolContext.SelectedIndex >= 0 && _toolContext.SelectedIndex < _constructionTowns.Length) _map.SelectedSettlementId = _constructionTowns[_toolContext.SelectedIndex];
    }
    private ToolChoice[] BuildToolChoices() =>
    [new("build:Farm", "农场", "#ADBB75"), new("build:Workshop", "工坊", "#CEB294"), new("build:Academy", "学院", "#91B0C8"), new("build:Waystation", "驿站", "#CEAB76"), new("build:SignalTower", "信号塔", "#99AAC8"), new("build:ArcaneSanctum", "秘法所", "#B598D1"), new("build:Infirmary", "医馆", "#91C7B1"), new("road:Road", "道路", "#B0A28B")];

    private void SelectTool(string tool)
    {
        if (tool.StartsWith("panel:")) { _map.ActiveTool = "inspect"; OpenInspector(tool[6..]); }
        else if (tool == "toggle:borders") { _map.ShowBorders = !_map.ShowBorders; _map.RefreshWorld(); }
        else
        {
            _map.ActiveTool = tool;
            if (_isCompact) { _mobilePanel = false; ApplyLayout(); }
        }
        foreach (var (key, button) in _tools)
        {
            button.BorderBrush = key == tool ? Mint : Brushes.Transparent;
            button.Background = key == tool ? Brush.Parse("#2C423F") : Ink;
        }
    }

    private void RunEdit(Action command, string message)
    {
        try
        {
            BeginEdit(); command(); _map.RefreshWorld(); RefreshUi(true); SetStatus(message);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        { SetStatus("未应用变更：" + FriendlyError(ex)); }
    }
}
