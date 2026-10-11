using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;
using SeWZC.WorldBox.UI.ViewModels.Details;

namespace SeWZC.WorldBox.UI.ViewModels;

internal sealed partial class InspectorViewModel
{
    internal int _inspectorSettlementId;

    internal ButtonNode RelatedObjectLink(string label, Func<int> target, Func<int, string> name,
        Func<int, bool> exists, Action<int> open, string id)
    {
        var button = Named(Button(label, () =>
        {
            var current = target();
            if (exists(current))
                open(current);
        }), id);

        void Update()
        {
            var current = target();
            var text = label + " " + name(current);
            if (!Equals(button.Content, text))
                button.Content = text;
            button.IsEnabled = exists(current);
            button.ToolTip = button.IsEnabled ? label : "对象尚未指定或已不存在";
        }

        _inspectorUpdates.Add(Update);
        Update();
        return button;
    }

    internal void BuildNationInspector(StackNode panel)
    {
        var nation = _engine.State.Nations.FirstOrDefault(n => n.Id == _selectedNationId);
        if (nation is null)
        {
            panel.Children.Add(Paragraph("这个国家已不在当前世界中。其历史仍可在编年史查询。"));
            return;
        }

        _inspectorUpdates.Add(() => nation = _engine.State.Nations.FirstOrDefault(n => n.Id == nation.Id) ?? nation);
        panel.Children.Add(LiveText(() => nation.Name, 18, Mint));
        panel.Children.Add(LiveText(() =>
            $"实际国家状态\n人口 {nation.Population}\n领土 {nation.Territory}\n{StockLabel(nation.Resources)}\n国家文化：{CultureName(nation.CultureId)}\n首都：{TownName(nation.CapitalId)}\n代表：{ResidentName(nation.RepresentativeId)}"));
        panel.Children.Add(WatchControl(ObservedObjectKind.Nation, nation.Id, "nation-follow"));
        BuildMilitarySummary(panel, nation);
        panel.Children.Add(Text("外交关系\n依据已收到的信息", 13, Mint));
        LiveRows(panel,
            () => _engine.State.Diplomacies.Where(d => d.FirstNationId == nation.Id || d.SecondNationId == nation.Id),
            d => $"{d.FirstNationId}:{d.SecondNationId}",
            d =>
                $"{NationName(d.FirstNationId == nation.Id ? d.SecondNationId : d.FirstNationId)}\n{(d.Status == DiplomaticStatus.War ? "战争" : d.Status == DiplomaticStatus.Allied ? "联盟" : "和平")}\n关系 {d.Opinion}\n{d.Reason}",
            d => OpenNation(d.FirstNationId == nation.Id ? d.SecondNationId : d.FirstNationId));
        panel.Children.Add(RelatedObjectLink("查看首都", () => nation.CapitalId, TownName,
            id => _engine.State.Settlements.Any(t => t.Id == id), id => OpenSettlement(id), "nation-capital"));
        panel.Children.Add(RelatedObjectLink("查看代表", () => nation.RepresentativeId, ResidentName,
            id => _engine.GetResident(id) is not null, OpenResident, "nation-representative"));
        panel.Children.Add(Button("编辑这个国家", () => _requests.Editors.ShowNationEditor(nation.Id)));
        panel.Children.Add(Named(Button("文化、制度与政策", () => _requests.Editors.ShowGovernanceEditor(nation.Id)),
            "nation-governance"));
        panel.Children.Add(Named(Button("编辑国家文化价值", () => _requests.Editors.ShowCultureEditor(nation.CultureId)),
            "nation-culture-edit"));
        panel.Children.Add(Text("国家决策及其已知议题", 12, Mint));
        panel.Children.Add(LiveText(() =>
        {
            var institution = _engine.State.Society.Institutions.FirstOrDefault(i => i.NationId == nation.Id);
            return
                $"{(institution is null ? "尚未建立制度" : InstitutionName(institution.Kind))}\n{institution?.LastDecision}\n政策：{(institution?.PlayerPolicy is { } policy ? WorldEngine.PolicyName(policy) + "（玩家指定）" : "居民与代表协商自治")}\n{nation.Decision}";
        }));
        panel.Children.Add(Paragraph("决策取决于代表与信使实际送达的议题。下方聚落库存是上帝视角事实，不代表首都已知这些变化。"));
        LiveRows(panel, () => _engine.State.Settlements.Where(t => t.NationId == nation.Id).OrderBy(t => t.Id),
            t => t.Id.ToString(), t =>
            {
                var development = _engine.GetDevelopment(t.Id);
                return
                    $"{t.Name}   人口 {t.Population}\n阶段：{development.Stage}\n目标：{development.Goal}\n进度：{development.Progress:P0}\n{development.Blocker}\n{_engine.GetDevelopmentEstimate(t.Id).Explanation}\n库存：{StockLabel(t.Resources)}";
            }, t => OpenSettlement(t.Id));
        panel.Children.Add(Text("居民构成与文化传播", 12, Mint));
        panel.Children.Add(LiveText(() => string.Join("\n",
            _engine.State.Residents.Where(r => r.NationId == nation.Id).GroupBy(r => r.CultureId)
                .Select(g => $"{CultureName(g.Key)}：{g.Count()} 人"))));
        panel.Children.Add(Button("前往首都", () =>
        {
            var town = _engine.State.Settlements.FirstOrDefault(t => t.Id == nation.CapitalId);
            if (town is not null)
                FocusMapTile(town.X, town.Y);
            CloseInspector();
        }));
    }

    internal void BuildSettlementNavigation(StackNode panel)
    {
        panel.Children.Add(Named(LiveText(() => "当前聚落：" + TownName(_inspectorSettlementId), 11, Muted),
            "settlement-context"));
        var tabs = new GridNode { ColumnDefinitions = new ColumnDefinitions("*,*,*,*"), ColumnSpacing = 4 };
        var entries = new[]
        {
            ("概况", "settlement"), ("建设", "infrastructure"), ("研究", "research"), ("通信", "communication"),
        };
        for (var i = 0; i < entries.Length; i++)
        {
            var (label, mode) = entries[i];
            var button = Named(Button(label, () => OpenInspector(mode)), "settlement-tab-" + mode);
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.Padding = new Thickness(4, 3);
            button.FontSize = 11;
            button.BorderBrush = _inspectorMode == mode ? Mint : Line;
            GridNode.SetColumn(button, i);
            tabs.Children.Add(button);
        }

        panel.Children.Add(tabs);
    }

    internal Settlement? BuildSettlementPicker(StackNode panel)
    {
        var towns = _engine.State.Settlements.OrderBy(t => t.Id).ToArray();
        if (towns.Length == 0)
        {
            panel.Children.Add(Paragraph("先投放居民形成聚落，才能查看建设、研究和通信。"));
            return null;
        }

        var selected = Math.Max(0, Array.FindIndex(towns, t => t.Id == _inspectorSettlementId));
        var town = towns[selected];
        _inspectorSettlementId = town.Id;
        var picker =
            Named(
                new ChoiceNode
                {
                    ItemsSource = towns.Select(t => $"{t.Name}\n{NationName(t.NationId)}").ToArray(),
                    SelectedIndex = selected,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                }, "infrastructure-town");
        picker.SelectionChanged += (_, _) =>
        {
            if (picker.SelectedIndex >= 0)
                OpenSettlement(towns[picker.SelectedIndex].Id, _inspectorMode);
        };
        panel.Children.Add(picker);
        return town;
    }

    internal void BuildSettlementList(StackNode panel)
    {
        panel.Children.Add(LiveText(() => $"{_engine.State.Settlements.Count} 处聚落\n选择聚落后可查看概况、建设、研究与通信。"));
        LiveRows(panel, () => _engine.State.Settlements.OrderBy(t => t.NationId).ThenBy(t => t.Id),
            t => t.Id.ToString(),
            t =>
                $"{t.Name}   {NationName(t.NationId)}\n人口 {t.Population}   城镇等级 {WorldEngine.SettlementTierName(t.Tier)}\n{_engine.GetDevelopment(t.Id).Stage}",
            t => OpenSettlement(t.Id));
    }

    internal void BuildSettlementOverview(StackNode panel)
    {
        if (BuildSettlementPicker(panel) is not { } town)
            return;
        _inspectorUpdates.Add(() => town = _engine.State.Settlements.FirstOrDefault(t => t.Id == town.Id) ?? town);
        panel.Children.Add(WatchControl(ObservedObjectKind.Settlement, town.Id, "settlement-watch"));
        panel.Children.Add(Named(LiveText(() => _engine.GetSettlementSummary(town.Id)), "town-expansion-summary"));
        panel.Children.Add(Named(Button("投入城镇扩充", () => RunEdit(() => _engine.ExpandTown(town.Id), "已投入扩充材料，居民将到中心施工")),
            "town-expand"));
        panel.Children.Add(LiveText(() =>
            $"人口 {town.Population} / 住房容量 {_engine.GetHousingCapacity(town.Id)}\n实际库存：{StockLabel(town.Resources)}"));
        panel.Children.Add(RelatedObjectLink("查看国家", () => town.NationId, NationName,
            id => _engine.State.Nations.Any(n => n.Id == id), OpenNation, "settlement-nation"));
        panel.Children.Add(RelatedObjectLink("查看代表", () => town.RepresentativeId, ResidentName,
            id => _engine.GetResident(id) is not null, OpenResident, "settlement-representative"));
        panel.Children.Add(LiveText(() =>
        {
            var d = _engine.GetDevelopment(town.Id);
            return $"阶段：{d.Stage}\n目标：{d.Goal}\n进度：{d.Progress:P0}\n{d.Blocker}\n动荡 {town.Unrest:0}/100";
        }, 13, Mint));
        panel.Children.Add(Named(LiveText(() => _engine.GetDevelopmentEstimate(town.Id).Explanation),
            "development-estimate"));
        panel.Children.Add(Named(LiveText(() => _engine.GetAdvancementStage(town.Id)), "advancement-stage"));
        panel.Children.Add(Named(
            LiveText(() =>
                $"可占领范围上限：{town.MaxClaimRadius} 格\n" + (town.FoundationPending ? "拓荒队尚未完成到场登记" : "相邻空地须由居民到场驻留登记")),
            "town-claim-limit"));
        var founding = FoldSection(panel, "另建村庄的条件", "town-founding");
        founding.Children.Add(Paragraph(
            $"投入：{StockLabel(WorldEngine.VillageFoundingCost)}\n新村与其他城镇至少相距 {WorldEngine.MinimumSettlementDistance} 格。居民先勘察并带回报告，拓荒队再携物资抵达、驻留建村。"));
        panel.Children.Add(Named(Button("定位聚落", () =>
        {
            FocusMapTile(town.X, town.Y);
            CloseInspector();
        }), "settlement-locate"));
    }

    internal void BuildSettlementResearch(StackNode panel)
    {
        if (BuildSettlementPicker(panel) is not { } town)
            return;
        _inspectorUpdates.Add(() => town = _engine.State.Settlements.FirstOrDefault(t => t.Id == town.Id) ?? town);
        BuildResearchTree(panel, town);
    }

    internal void BuildInfrastructureInspector(StackNode panel, bool communications)
    {
        if (BuildSettlementPicker(panel) is not { } town)
            return;
        _inspectorUpdates.Add(() => town = _engine.State.Settlements.FirstOrDefault(t => t.Id == town.Id) ?? town);
        if (!communications)
        {
            var actions = new WrapNode { Orientation = Orientation.Horizontal };
            actions.Children.Add(Named(Button("定位聚落并开始建设", () =>
            {
                _map.SelectedSettlementId = town.Id;
                SetCategory(ToolCategory.Build);
                FocusMapTile(town.X, town.Y);
            }), "infrastructure-build"));
            actions.Children.Add(Named(Button("查看设施成本与建造", () => _requests.Editors.ShowBuildingEditor(town.Id)),
                "building-open"));
            panel.Children.Add(actions);
            panel.Children.Add(Named(LiveText(() => _engine.GetSettlementSummary(town.Id)), "town-expansion-summary"));
            panel.Children.Add(LiveText(() => "实际库存：" + StockLabel(town.Resources)));
            panel.Children.Add(Text("设施\n施工与工作人员", 12, Mint));
            LiveRows(panel,
                () => _engine.State.Society.Buildings.Where(b => b.SettlementId == town.Id).OrderBy(b => b.Id),
                b => b.Id.ToString(), b => BuildingLabel(b) + "\n" + BuildingTask(b), OpenBuilding);
            panel.Children.Add(Text("进阶生产\n配方与阻碍", 12, Mint));
            LiveRows(panel,
                () => _engine.State.Society.Buildings
                    .Where(b => b.SettlementId == town.Id && ProductionRules.For(b.Kind) is not null)
                    .OrderBy(b => b.Id),
                b => b.Id.ToString(),
                b => WorldEngine.BuildingName(b.Kind) + "\n" + WorldEngine.ProductionRecipe(b.Kind) + "\n" +
                     _engine.GetProductionStatus(b.Id), OpenBuilding);
            panel.Children.Add(Text("实体运输", 12, Mint));
            LiveRows(panel, () => _engine.State.Residents.Where(r =>
                        (r.SettlementId == town.Id || r.Agent.DestinationSettlementId == town.Id)
                        && (r.Agent.Goal.Kind == AgentGoalKind.Trade || r.Profession == Profession.Trader))
                    .OrderBy(r => r.Id).Take(30),
                r => r.Id.ToString(),
                r =>
                    $"{r.Name}\n{WorldEngine.TravelModeName(r.TravelMode)}\n目的地：{TownName(r.Agent.DestinationSettlementId)}\n携带：{StockLabel(r.Inventory)}\n{r.Agent.Goal.Reason}",
                r => OpenResident(r.Id));
            return;
        }

        panel.Children.Add(Text("通信覆盖与连通\n当前实际状态", 12, Mint));
        panel.Children.Add(
            Paragraph("同国在运作的信号塔通过视线连通；1 级接入 12 格、塔间 24 格，每级增加 4／8 格；塔间取较低等级。山脉阻挡。设施需要工作人员、足够健康且未着火。道路与驿站改变实际信使行程。"));
        LiveRows(panel,
            () => _engine.State.Settlements.Where(t => t.NationId == town.NationId && t.Id != town.Id)
                .OrderBy(t => t.Id),
            t => t.Id.ToString(), t => _engine.CanRelayInformation(town.Id, t.Id, out var ticks)
                ? $"{town.Name} 与 {t.Name}\n信号连通\n预计 {ticks / (double)SimulationTime.TicksPerDay:0.##} 日"
                : $"{town.Name} 与 {t.Name}\n信号未连通\n依赖居民实际携带消息",
            t => OpenSettlement(t.Id, "communication"));
        panel.Children.Add(LiveText(() =>
            $"全世界待投递消息 {_engine.State.PendingMessages.Count} 条\n本聚落公开知识 {town.PublicKnowledge.Count} 条\n本聚落已收到报告 {_engine.State.Society.Reports.Count(r => r.RecipientSettlementId == town.Id)} 条"));
        panel.Children.Add(Text("实体信使", 12, Mint));
        LiveRows(panel, () => _engine.State.Residents.Where(r =>
                    (r.SettlementId == town.Id || r.Agent.DestinationSettlementId == town.Id)
                    && (r.Agent.Goal.Kind == AgentGoalKind.DeliverMessage || r.Profession == Profession.Messenger))
                .OrderBy(r => r.Id).Take(30),
            r => r.Id.ToString(),
            r =>
                $"{r.Name}\n目的地：{TownName(r.Agent.DestinationSettlementId)}\n携带消息 {r.Agent.CarriedMessages.Count} 条\n{r.Agent.Goal.Reason}",
            r => OpenResident(r.Id));
        panel.Children.Add(Text("聚落已知消息\n与实际世界可能不同步", 12, Mint));
        LiveRows(panel, () => town.PublicKnowledge.OrderByDescending(f => f.LearnedTick).Take(30), f => f.Id.ToString(),
            FactLabel);
        panel.Children.Add(Text("实际递送的公民报告", 12, Mint));
        LiveRows(panel,
            () => _engine.State.Society.Reports.Where(r => r.RecipientSettlementId == town.Id)
                .OrderByDescending(r => r.ReceivedTick).Take(20),
            r => $"{r.FactId}:{r.OriginResidentId}:{r.RepresentativeId}:{r.ReceivedTick}",
            r =>
                $"{FactKindName(r.Topic)}\n主题 #{r.SubjectId}\n数值 {r.Value:F2}\n原始提出者：{ResidentName(r.OriginResidentId)}（{ProfessionName(r.ReportedProfession)}）\n递送代表：{ResidentName(r.RepresentativeId)}\n观察：{DateLabel(r.ObservedTick)}\n递送：{DateLabel(r.ReceivedTick)}\n可信度 {r.Confidence:P0}\n消息年龄 {Math.Max(0, _engine.State.Tick - r.ObservedTick) / (double)SimulationTime.TicksPerDay:0.##} 日");
    }
}
