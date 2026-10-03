using Avalonia.Controls;
using Avalonia.Layout;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    private ResourceVisibility _resourceVisibility = ResourceVisibility.Researched;
    private int _structurePage;
    private bool _listRoads;
    private string _structureSearch = "";

    private string BuildingLabel(Building building)
    {
        var name = WorldEngine.BuildingName(building.Kind);
        var duplicates = _engine.State.Society.Buildings.Count(b => b.SettlementId == building.SettlementId && b.Kind == building.Kind) > 1;
        return duplicates ? $"{name}（{building.X}, {building.Y}）" : name;
    }

    private void OpenBuilding(Building building)
    {
        RememberLocation(); _selectedBuildingId = building.Id; _selectedTile = (building.X, building.Y);
        _mapSelectionKind = "building"; OpenInspector("building", false);
    }

    private void BuildBuildingInspector(StackPanel panel)
    {
        var id = _selectedBuildingId;
        Building? Current() => _engine.State.Society.Buildings.FirstOrDefault(b => b.Id == id);
        if (Current() is not { } building) { panel.Children.Add(Paragraph("建筑已毁坏或被移除，可在建筑与道路列表查看其他设施。")); return; }
        panel.Children.Add(LiveText(() => Current() is { } b ? BuildingLabel(b) : "建筑已不存在", 18, Mint));
        panel.Children.Add(Paragraph(WorldEngine.BuildingDescription(building.Kind)));
        panel.Children.Add(LiveText(() => Current() is not { } b ? "建筑已不存在" :
            $"归属聚落：{TownName(b.SettlementId)}\n位置：{b.X}, {b.Y}\n生命：{b.Health:0.0} / 100\n状态：{(b.Health <= 0 ? "已损毁，等待重建" : !b.Enabled ? "已停用" : b.IsCompleted ? "已建成" : "居民施工中")}\n施工：{b.ConstructionProgress:0.0} / {b.ConstructionRequired:0}\n最近工作：{DateLabel(b.LastWorkedTick)}\n累计加工：{b.ProductionBatches} 批\n{_engine.GetProductionStatus(b.Id)}"));
        panel.Children.Add(Named(Button("定位建筑", () => { if (Current() is { } b) _map.FocusTile(b.X, b.Y); CloseInspector(); }), "building-locate"));
        panel.Children.Add(Named(Button("修复建筑", () => RunEdit(() => _engine.RestoreBuilding(id), "建筑已修复")), "building-repair"));
        if (!building.IsCompleted) panel.Children.Add(Named(Button("赐予完工", () => RunEdit(() => _engine.RestoreBuilding(id, true), "已赐予完工；运营仍需实际条件")), "building-finish"));
        if (building.Kind != BuildingKind.TownCenter)
        {
            var toggle = Named(Button(building.Enabled ? "停用建筑" : "恢复运营", () =>
            {
                if (Current() is { } b) RunEdit(() => _engine.SetBuildingEnabled(id, !b.Enabled), "建筑运营状态已更新");
            }), "building-toggle");
            _inspectorUpdates.Add(() => toggle.Content = Current()?.Enabled == true ? "停用建筑" : "恢复运营"); panel.Children.Add(toggle);
        }
        panel.Children.Add(Text("实际到场工作人员", 13, Mint));
        LiveRows(panel, () => Current() is { } b ? _engine.State.Residents.Where(r => b.Workers.Contains(r.Id) && _engine.State.Tick - b.LastWorkedTick <= 1) : [],
            r => r.Id.ToString(), r => r.Name + "\n" + _engine.GetResidentActionSummary(r.Id), r => OpenResident(r.Id));
        panel.Children.Add(Button("查看归属聚落", () => { _inspectorSettlementId = building.SettlementId; OpenInspector("infrastructure"); }));
        panel.Children.Add(Named(Button("查看所在土地", () => { _selectedTile = (building.X, building.Y); OpenInspector("tile"); }), "building-ground"));
    }

    private void BuildStructuresInspector(StackPanel panel)
    {
        var mode = Named(new ComboBox { ItemsSource = new[] { "建筑", "道路地块" }, SelectedIndex = _listRoads ? 1 : 0,
            HorizontalAlignment = HorizontalAlignment.Stretch }, "structures-kind");
        mode.SelectionChanged += (_, _) => { _listRoads = mode.SelectedIndex == 1; _structurePage = 0; InvalidateInspector(); RefreshInspector(); }; panel.Children.Add(mode);
        var search = Named(new TextBox { Text = _structureSearch, PlaceholderText = "按建筑、聚落或国家名称搜索" }, "structures-search");
        search.TextChanged += (_, _) => { _structureSearch = search.Text ?? ""; _structurePage = 0; RefreshInspector(); }; panel.Children.Add(search);
        bool Matches(string text) => text.Contains(_structureSearch, StringComparison.OrdinalIgnoreCase);
        IEnumerable<Building> Buildings() => _engine.State.Society.Buildings.OrderBy(b => b.Id).Where(b => Matches(BuildingLabel(b) + TownName(b.SettlementId)));
        IEnumerable<int> Roads() => Enumerable.Range(0, _engine.State.Tiles.Length).Where(i => _engine.State.Tiles[i].RoadLevel > 0 && Matches(NationName(_engine.State.Tiles[i].NationId)));
        int Count() => _listRoads ? Roads().Count() : Buildings().Count();
        panel.Children.Add(LiveText(() => $"共 {Count()} 处{(_listRoads ? "道路地块" : "建筑")}\n第 {_structurePage + 1} / {Math.Max(1, (Count() + 19) / 20)} 页"));
        var pages = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        pages.Children.Add(Named(Button("上一页", () => { _structurePage = Math.Max(0, _structurePage - 1); RefreshInspector(); }), "structures-prev"));
        pages.Children.Add(Named(Button("下一页", () => { _structurePage = Math.Min(Math.Max(0, (Count() - 1) / 20), _structurePage + 1); RefreshInspector(); }), "structures-next")); panel.Children.Add(pages);
        _inspectorUpdates.Add(() => _structurePage = Math.Clamp(_structurePage, 0, Math.Max(0, (Count() - 1) / 20)));
        if (_listRoads)
            LiveRows(panel, () => Roads().Skip(_structurePage * 20).Take(20), i => i.ToString(),
                i => $"道路 {_engine.State.Tiles[i].RoadLevel} 级\n位置 {i % _engine.State.Width}, {i / _engine.State.Width}\n归属：{NationName(_engine.State.Tiles[i].NationId)}\n步行耗时系数 {_engine.GetTerrainMoveCost(i % _engine.State.Width, i / _engine.State.Width):0.##}",
                i => { _selectedTile = (i % _engine.State.Width, i / _engine.State.Width); _map.FocusTile(_selectedTile.Value.X, _selectedTile.Value.Y); OpenInspector("tile"); });
        else LiveRows(panel, () => Buildings().Skip(_structurePage * 20).Take(20), b => b.Id.ToString(),
            b => $"{BuildingLabel(b)}\n聚落：{TownName(b.SettlementId)}\n{(b.IsCompleted ? "已建成" : "施工或重建中")}\n生命 {b.Health:0} / 100", OpenBuilding);
        panel.Children.Add(Button("地图突出显示建筑与道路", () => { _map.Overlay = 4; _map.RefreshWorld(); CloseInspector(); }));
    }

    private void QuickResidentGoal(int id, AgentGoalKind? kind)
    {
        if (_engine.GetResident(id) is not { Health: > 0 } person) return;
        var mind = CloneMind(id);
        var town = _engine.State.Settlements.FirstOrDefault(t => t.Id == person.SettlementId);
        if (kind.HasValue && person.ArmyId != 0) { SetStatus("军队成员由军令统一调动，请先调整军队归属。"); return; }
        if (kind.HasValue && town is not null) mind.Goal = new AgentGoal { Kind = kind.Value, TargetX = town.X, TargetY = town.Y,
            TargetSettlementId = town.Id, StartedTick = _engine.State.Tick, ReviewTick = _engine.State.Tick + 48,
            PlayerDirected = true, Reason = kind == AgentGoalKind.Rest ? "玩家安排返回家园休息" : "玩家安排返回家园交付物资" };
        else { mind.Goal.PlayerDirected = false; mind.NextThinkTick = _engine.State.Tick; }
        RunEdit(() => _engine.EditResident(id, new ResidentEdit { Agent = mind }), kind.HasValue ? "已安排任务，继续模拟后执行" : "已恢复自主行动");
    }
}
