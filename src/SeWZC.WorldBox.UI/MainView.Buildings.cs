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
    private int _structuresTownId;
    private BuildingKind? _structuresBuildingKind;
    private bool _openedStructureHighlights;

    private void ResetInfrastructureFilters()
    {
        _structuresTownId = 0; _structuresBuildingKind = null; _structurePage = 0; _structureSearch = "";
        _map.InfrastructureTownId = 0; _map.InfrastructureKind = null;
    }

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
        if (Current() is not { } building) { panel.Children.Add(Paragraph("建筑已被移除，可在建筑与道路列表查看其他设施。")); return; }
        panel.Children.Add(LiveText(() => Current() is { } b ? WorldEngine.BuildingName(b.Kind) : "建筑已不存在", 18, Mint));
        panel.Children.Add(Named(LiveText(() => Current() is { } b ? $"建筑生命 {b.Health:0.#} / 100" : "建筑已不存在"), "building-health"));
        panel.Children.Add(Named(LiveText(() => Current() is { } b ? $"等级 {b.Level} / 3" +
            (b.IsUpgrading ? $"\n{(b.PendingDirection.HasValue ? "改向" : "升级")}施工 {b.UpgradeProgress:0.0} / {b.UpgradeRequired:0}" : "") : ""), "building-level"));
        panel.Children.Add(Named(LiveText(() => _engine.GetBuildingDetailStatus(id)), "building-status"));
        if (building.Kind == BuildingKind.TownCenter)
        {
            panel.Children.Add(Named(LiveText(() => _engine.GetSettlementSummary(building.SettlementId)), "center-town-summary"));
            panel.Children.Add(Named(Button("查看城镇信息", () => { _inspectorSettlementId = building.SettlementId; OpenInspector("infrastructure"); }), "center-town-info"));
        }
        panel.Children.Add(Named(LiveText(() => EffectLabel(_engine.GetBuildingEffects(id).Where(e => e.Name is not ("建筑耐火" or "下一级")).ToArray())), "building-effects"));
        panel.Children.Add(Named(LiveText(() => string.Join("\n", _engine.GetBuildingEffects(id).Where(e => e.Name == "下一级"))), "building-next-level"));
        var condition = FoldSection(panel, "耐火与工作记录", "building-condition");
        condition.Children.Add(LiveText(() => Current() is not { } b ? "建筑已不存在" :
            $"聚落：{TownName(b.SettlementId)}" + (b.LastWorkedTick >= 0 ? $"\n最近工作：{DateLabel(b.LastWorkedTick)}" : "")));
        condition.Children.Add(LiveText(() => string.Join("\n", _engine.GetBuildingEffects(id).Where(e => e.Name == "建筑耐火"))));
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(actions);
        actions.Children.Add(Named(Button("定位建筑", () => { if (Current() is { } b) _map.FocusTile(b.X, b.Y); CloseInspector(); }), "building-locate"));
        actions.Children.Add(Named(Button("修复建筑", () => RunEdit(() => _engine.RestoreBuilding(id), "建筑已修复")), "building-repair"));
        actions.Children.Add(Named(Button("升级建筑", () => ShowBuildingUpgrade(id, false)), "building-upgrade"));
        if (building.Kind == BuildingKind.Bridge)
            actions.Children.Add(Named(Button("改造桥梁方向", () => ShowBuildingUpgrade(id, true)), "building-reorient"));
        if (!building.IsCompleted) actions.Children.Add(Named(Button("赐予完工", () => RunEdit(() => _engine.RestoreBuilding(id, true), "已赐予完工；运营仍需实际条件")), "building-finish"));
        if (building.Kind != BuildingKind.TownCenter)
        {
            var toggle = Named(Button(building.Enabled ? "停用建筑" : "恢复运营", () =>
            {
                if (Current() is { } b) RunEdit(() => _engine.SetBuildingEnabled(id, !b.Enabled), "建筑运营状态已更新");
            }), "building-toggle");
            _inspectorUpdates.Add(() => toggle.Content = Current()?.Enabled == true ? "停用建筑" : "恢复运营"); actions.Children.Add(toggle);
        }
        IEnumerable<Resident> OnSiteWorkers() => Current() is { } b ? _engine.State.Residents.Where(r => b.Workers.Contains(r.Id)
            && _engine.State.Tick - b.LastWorkedTick <= 1 && r.SettlementId == b.SettlementId
            && r.Health > 0 && Math.Abs(r.X - b.X) + Math.Abs(r.Y - b.Y) <= 1) : [];
        condition.Children.Add(LiveText(() => OnSiteWorkers().Any() ? "到场工作人员" : "", 13, Mint));
        LiveRows(condition, OnSiteWorkers, r => r.Id.ToString(),
            r => r.Name + "   " + ProfessionName(r.Profession) + "\n" + ResidentTask(r), r => OpenResident(r.Id));
        if (building.Kind != BuildingKind.TownCenter)
            panel.Children.Add(Named(Button("查看归属城镇信息", () => { _inspectorSettlementId = building.SettlementId; OpenInspector("infrastructure"); }), "building-town-info"));
        panel.Children.Add(Named(Button("查看所在土地", () => { _selectedTile = (building.X, building.Y); OpenInspector("tile"); }), "building-ground"));
    }

    private static string EffectLabel(IReadOnlyList<EffectInfo> effects) => effects.Count == 0 ? "当前无额外加成或减益" : string.Join("\n\n", effects);

    private void ShowBuildingUpgrade(int id, bool reorient)
    {
        var building = _engine.State.Society.Buildings.FirstOrDefault(b => b.Id == id);
        if (building is null) return;
        if (!reorient && building.Level >= 3)
        {
            var completed = ModalPanel("升级建筑", $"{BuildingLabel(building)}已达到最高等级（3 级）。");
            completed.Children.Add(Named(Button("关闭", CloseModal), "upgrade-close"));
            OpenModal(completed); return;
        }
        var direction = reorient ? (BridgeDirection?)(building.Direction == BridgeDirection.Horizontal ? BridgeDirection.Vertical : BridgeDirection.Horizontal) : null;
        var panel = ModalPanel(reorient ? "改造桥梁方向" : "升级建筑", reorient
            ? $"{WorldEngine.BridgeDirectionName(building.Direction)} → {WorldEngine.BridgeDirectionName(direction!.Value)}。施工期间仍沿原方向通行，完工后改向。"
            : $"{BuildingLabel(building)}：{building.Level} 级 → {building.Level + 1} 级。"
                + (building.Kind == BuildingKind.Bridge ? "施工期间保留原通道，完工后提高离岸上限。" : "升级期间暂停运营，居民到场施工后生效。"));
        panel.Children.Add(Paragraph("施工材料：" + StockLabel(WorldEngine.GetUpgradeCost(building, reorient))));
        panel.Children.Add(Named(LiveText(() => "居民施工：" + (_engine.BuildingUpgradeError(id, direction: direction) ?? "材料与条件满足，投入后等待居民到场施工")), "upgrade-paid-status"));
        panel.Children.Add(Named(LiveText(() => "直接赐予：" + (_engine.BuildingUpgradeError(id, true, direction) ?? "可立即完成，不扣施工材料")), "upgrade-gift-status"));
        panel.Children.Add(Named(Button("安排居民施工", () => RunEdit(() => { _engine.UpgradeBuilding(id, direction: direction); CloseModal(); }, "项目已开始，等待居民到场施工")), "upgrade-apply"));
        panel.Children.Add(Named(Button("直接赐予完成", () => RunEdit(() => { _engine.UpgradeBuilding(id, true, direction); CloseModal(); }, "建筑改造已完成")), "upgrade-gift"));
        OpenModal(panel);
    }

    private void BuildStructuresInspector(StackPanel panel)
    {
        if (!_engine.State.Settlements.Any(t => t.Id == _structuresTownId)) _structuresTownId = 0;
        _map.InfrastructureTownId = _structuresTownId; _map.InfrastructureKind = _structuresBuildingKind;
        if (!_openedStructureHighlights) { _map.Overlay = 4; _openedStructureHighlights = true; }
        _map.RefreshWorld();
        panel.Children.Add(Paragraph("可启用建设图层。颜色表示设施用途，黄色地块表示道路；仅显示可步行连通的道路连接。"));
        panel.Children.Add(Paragraph("绿：农业   蓝：交通   紫：知识与通信\n橙：材料与工业   青：公共设施   白：中心\n金黄：施工或升级   红：严重受损   灰：停用"));
        BuildMapHighlights(panel);
        panel.Children.Add(Named(Button("收起面板查看地图", CloseInspector), "structures-map"));
        var towns = _engine.State.Settlements.OrderBy(t => t.Id).ToArray();
        var townFilter = Named(new ComboBox { ItemsSource = new[] { "全部城镇" }.Concat(towns.Select(t => t.Name)).ToArray(),
            SelectedIndex = Math.Max(0, Array.FindIndex(towns, t => t.Id == _structuresTownId) + 1), HorizontalAlignment = HorizontalAlignment.Stretch }, "structures-town");
        townFilter.SelectionChanged += (_, _) => { _structuresTownId = townFilter.SelectedIndex > 0 ? towns[townFilter.SelectedIndex - 1].Id : 0;
            _map.InfrastructureTownId = _structuresTownId; _structurePage = 0; InvalidateInspector(); RefreshInspector(); _map.RefreshWorld(); }; panel.Children.Add(townFilter);
        var kinds = Enum.GetValues<BuildingKind>();
        var kindFilter = Named(new ComboBox { ItemsSource = new[] { "全部建筑种类" }.Concat(kinds.Select(WorldEngine.BuildingName)).ToArray(),
            SelectedIndex = _structuresBuildingKind is { } selectedKind ? Array.IndexOf(kinds, selectedKind) + 1 : 0, HorizontalAlignment = HorizontalAlignment.Stretch }, "structures-building-kind");
        kindFilter.SelectionChanged += (_, _) => { _structuresBuildingKind = kindFilter.SelectedIndex > 0 ? kinds[kindFilter.SelectedIndex - 1] : null;
            _map.InfrastructureKind = _structuresBuildingKind; _structurePage = 0; InvalidateInspector(); RefreshInspector(); _map.RefreshWorld(); }; panel.Children.Add(kindFilter);
        var showBuildings = Named(new CheckBox { Content = "地图显示建筑", IsChecked = _map.HighlightBuildings }, "structures-show-buildings");
        showBuildings.IsCheckedChanged += (_, _) => { _map.HighlightBuildings = showBuildings.IsChecked == true; _map.RefreshWorld(); }; panel.Children.Add(showBuildings);
        var showRoads = Named(new CheckBox { Content = "地图显示道路", IsChecked = _map.HighlightRoads }, "structures-show-roads");
        showRoads.IsCheckedChanged += (_, _) => { _map.HighlightRoads = showRoads.IsChecked == true; _map.RefreshWorld(); }; panel.Children.Add(showRoads);
        var mode = Named(new ComboBox { ItemsSource = new[] { "建筑", "道路地块" }, SelectedIndex = _listRoads ? 1 : 0,
            HorizontalAlignment = HorizontalAlignment.Stretch }, "structures-kind");
        mode.SelectionChanged += (_, _) => { _listRoads = mode.SelectedIndex == 1; _structurePage = 0; InvalidateInspector(); RefreshInspector(); }; panel.Children.Add(mode);
        var search = Named(new TextBox { Text = _structureSearch, PlaceholderText = "按建筑、聚落或国家名称搜索" }, "structures-search");
        BindSearch(search, value => { _structureSearch = value; _structurePage = 0; }); panel.Children.Add(search);
        bool Matches(string text) => text.Contains(_structureSearch, StringComparison.OrdinalIgnoreCase);
        Building[] buildingRows = []; int[] roadRows = [];
        long sampledTick = -1; int sampledEventId = -1; string? sampledSearch = null;
        void SampleRows()
        {
            var latestEventId = _engine.State.Events.LastOrDefault()?.Id ?? 0;
            if (sampledTick == _engine.State.Tick && sampledEventId == latestEventId && sampledSearch == _structureSearch) return;
            sampledTick = _engine.State.Tick; sampledEventId = latestEventId; sampledSearch = _structureSearch;
            if (_listRoads) roadRows = Enumerable.Range(0, _engine.State.Tiles.Length)
                .Where(i => _engine.State.Tiles[i].RoadLevel > 0 && (_structuresTownId == 0 || _engine.State.Tiles[i].ClaimedSettlementId == _structuresTownId)
                    && Matches(NationName(_engine.State.Tiles[i].NationId) + TownName(_engine.State.Tiles[i].ClaimedSettlementId))).ToArray();
            else buildingRows = _engine.State.Society.Buildings.OrderBy(b => b.SettlementId).ThenBy(b => b.Kind).ThenBy(b => b.Id)
                .Where(b => (_structuresTownId == 0 || b.SettlementId == _structuresTownId) && (!_structuresBuildingKind.HasValue || b.Kind == _structuresBuildingKind)
                    && Matches(BuildingLabel(b) + TownName(b.SettlementId) + NationName(_engine.State.Settlements.First(t => t.Id == b.SettlementId).NationId))).ToArray();
        }
        _inspectorUpdates.Add(SampleRows); SampleRows();
        IEnumerable<Building> Buildings() => buildingRows;
        IEnumerable<int> Roads() => roadRows;
        int Count() => _listRoads ? Roads().Count() : Buildings().Count();
        panel.Children.Add(LiveText(() => $"共 {Count()} 处{(_listRoads ? "道路地块" : "建筑")}   第 {_structurePage + 1} / {Math.Max(1, (Count() + 19) / 20)} 页"));
        var pages = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        pages.Children.Add(Named(Button("上一页", () => { _structurePage = Math.Max(0, _structurePage - 1); RefreshInspector(); }), "structures-prev"));
        pages.Children.Add(Named(Button("下一页", () => { _structurePage = Math.Min(Math.Max(0, (Count() - 1) / 20), _structurePage + 1); RefreshInspector(); }), "structures-next")); panel.Children.Add(pages);
        _inspectorUpdates.Add(() => _structurePage = Math.Clamp(_structurePage, 0, Math.Max(0, (Count() - 1) / 20)));
        if (_listRoads)
            LiveRows(panel, () => Roads().Skip(_structurePage * 20).Take(20), i => i.ToString(),
                i => $"{(_engine.State.Tiles[i].Improvement == LandImprovement.Bridge ? "桥梁 " + WorldEngine.BridgeDirectionName(_engine.State.Tiles[i].BridgeDirection) : "道路")} {_engine.State.Tiles[i].RoadLevel} 级\n位置 {i % _engine.State.Width}, {i / _engine.State.Width}\n归属：{TownName(_engine.State.Tiles[i].ClaimedSettlementId)}\n步行耗时系数 {_engine.GetTerrainMoveCost(i % _engine.State.Width, i / _engine.State.Width):0.##}",
                i => { _selectedTile = (i % _engine.State.Width, i / _engine.State.Width); _map.FocusTile(_selectedTile.Value.X, _selectedTile.Value.Y); OpenInspector("tile"); });
        else LiveRows(panel, () => Buildings().Skip(_structurePage * 20).Take(20), b => b.Id.ToString(),
            b => $"{BuildingLabel(b)}   {TownName(b.SettlementId)}\n{BuildingTask(b)}", OpenBuilding);
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
