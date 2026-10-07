using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    private int _inspectorSettlementId;

    private string CultureName(int id)
    {
        return _engine.State.Society.Cultures.FirstOrDefault(c => c.Id == id)?.Name ?? $"文化 #{id}";
    }

    private static string InstitutionName(InstitutionKind value)
    {
        return value switch
        {
            InstitutionKind.Council => "居民议事会",
            InstitutionKind.Monarchy => "君主制",
            _ => "行会议会",
        };
    }

    private static string SpellName(SpellKind value)
    {
        return WorldEngine.SpellName(value);
    }

    private Button RelatedObjectLink(string label, Func<int> target, Func<int, string> name,
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
            ToolTip.SetTip(button, button.IsEnabled ? label : "对象尚未指定或已不存在");
        }

        _inspectorUpdates.Add(Update);
        Update();
        return button;
    }

    private void BuildNationInspector(StackPanel panel)
    {
        var nation = _engine.State.Nations.FirstOrDefault(n => n.Id == _selectedNationId);
        if (nation is null)
        {
            panel.Children.Add(Paragraph("这个国家已不在当前世界中。其历史仍可在编年史查询。"));
            return;
        }

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
        panel.Children.Add(Button("编辑这个国家", () => ShowNationEditor(nation.Id)));
        panel.Children.Add(Named(Button("文化、制度与政策", () => ShowGovernanceEditor(nation.Id)), "nation-governance"));
        panel.Children.Add(Named(Button("编辑国家文化价值", () => ShowCultureEditor(nation.CultureId)), "nation-culture-edit"));
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
                _map.FocusTile(town.X, town.Y);
            CloseInspector();
        }));
    }

    private void ShowGovernanceEditor(int nationId)
    {
        var nation = _engine.State.Nations.FirstOrDefault(n => n.Id == nationId);
        if (nation is null)
            return;
        var institution = _engine.State.Society.Institutions.First(i => i.NationId == nationId);
        var panel = ModalPanel("文化、制度与政策", "制度改变居民与代表意见的权重。玩家指定政策会持续覆盖自治；恢复自治后由实际收到的报告继续决策。国家文化不会瞬间改写每位居民的文化。");
        var cultures = _engine.State.Society.Cultures.ToArray();
        panel.Children.Add(Text("国家文化", 12, Muted));
        var culture =
            Named(
                new ComboBox
                {
                    ItemsSource = cultures.Select(c => c.Name).ToArray(),
                    SelectedIndex = Array.FindIndex(cultures, c => c.Id == nation.CultureId),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                }, "nation-culture");
        panel.Children.Add(culture);
        var government = EnumField(panel, "政治制度", institution.Kind, InstitutionName, "nation-institution");
        var policy = EnumField(panel, "政策方向", institution.PlayerPolicy ?? PolicyKind.Balanced, WorldEngine.PolicyName,
            "nation-policy");
        var autonomous =
            Named(new CheckBox { Content = Text("由居民与代表自治决定政策", 12), IsChecked = institution.PlayerPolicy is null },
                "nation-policy-autonomy");
        panel.Children.Add(autonomous);
        policy.IsEnabled = autonomous.IsChecked != true;
        autonomous.IsCheckedChanged += (_, _) => policy.IsEnabled = autonomous.IsChecked != true;
        panel.Children.Add(Named(Button("应用制度与政策", () =>
        {
            if (culture.SelectedIndex < 0)
            {
                SetStatus("请选择有效文化。");
                return;
            }

            RunEdit(() =>
            {
                _engine.SetNationCulture(nationId, cultures[culture.SelectedIndex].Id);
                _engine.SetInstitution(nationId, (InstitutionKind)government.SelectedItem!);
                if (autonomous.IsChecked == true)
                    _engine.SetPolicyAutonomy(nationId);
                else
                    _engine.SetPolicy(nationId, (PolicyKind)policy.SelectedItem!);
                CloseModal();
            }, "文化、制度与政策已更新");
        }), "nation-governance-apply"));
        OpenModal(panel);
    }

    private void ShowCultureEditor(int cultureId)
    {
        var culture = _engine.State.Society.Cultures.FirstOrDefault(c => c.Id == cultureId);
        if (culture is null)
            return;
        var panel = ModalPanel("编辑文化价值", "文化独立于种族和国家。数值影响合作、创新与自然偏好，居民通过接触积累文化影响；修改会影响拥有该文化的居民未来行为。");
        var name = Field(panel, "名称", culture.Name, "culture-name");
        var cooperation = Field(panel, "合作倾向 0–1", culture.Cooperation, "culture-cooperation");
        var innovation = Field(panel, "创新倾向 0–1", culture.Innovation, "culture-innovation");
        var nature = Field(panel, "自然亲和 0–1", culture.NatureAffinity, "culture-nature");
        panel.Children.Add(Named(Button("应用文化价值", () =>
        {
            try
            {
                var a = Number(cooperation);
                var b = Number(innovation);
                var c = Number(nature);
                if (a is < 0 or > 1 || b is < 0 or > 1 || c is < 0 or > 1 || string.IsNullOrWhiteSpace(name.Text) ||
                    name.Text.Length > 60 ||
                    name.Text.Any(char.IsControl))
                    throw new ArgumentException("请输入名称及 0–1 的文化数值。");
                RunEdit(() =>
                {
                    _engine.RenameCulture(cultureId, name.Text);
                    _engine.SetCultureValues(cultureId, a, b, c);
                    CloseModal();
                }, "文化价值已更新");
            }
            catch (ArgumentException ex)
            {
                SetStatus(FriendlyError(ex));
            }
        }), "culture-apply"));
        OpenModal(panel);
    }

    private void BuildSettlementNavigation(StackPanel panel)
    {
        panel.Children.Add(Named(LiveText(() => "当前聚落：" + TownName(_inspectorSettlementId), 11, Muted),
            "settlement-context"));
        var tabs = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*"), ColumnSpacing = 4 };
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
            Grid.SetColumn(button, i);
            tabs.Children.Add(button);
        }

        panel.Children.Add(tabs);
    }

    private Settlement? BuildSettlementPicker(StackPanel panel)
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
                new ComboBox
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

    private void BuildSettlementList(StackPanel panel)
    {
        panel.Children.Add(LiveText(() => $"{_engine.State.Settlements.Count} 处聚落\n选择聚落后可查看概况、建设、研究与通信。"));
        LiveRows(panel, () => _engine.State.Settlements.OrderBy(t => t.NationId).ThenBy(t => t.Id),
            t => t.Id.ToString(),
            t =>
                $"{t.Name}   {NationName(t.NationId)}\n人口 {t.Population}   城镇等级 {WorldEngine.SettlementTierName(t.Tier)}\n{_engine.GetDevelopment(t.Id).Stage}",
            t => OpenSettlement(t.Id));
    }

    private void BuildSettlementOverview(StackPanel panel)
    {
        if (BuildSettlementPicker(panel) is not { } town)
            return;
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
            _map.FocusTile(town.X, town.Y);
            CloseInspector();
        }), "settlement-locate"));
    }

    private void BuildSettlementResearch(StackPanel panel)
    {
        if (BuildSettlementPicker(panel) is not { } town)
            return;
        BuildResearchTree(panel, town);
    }

    private void BuildInfrastructureInspector(StackPanel panel, bool communications)
    {
        if (BuildSettlementPicker(panel) is not { } town)
            return;
        if (!communications)
        {
            var actions = new WrapPanel { Orientation = Orientation.Horizontal };
            actions.Children.Add(Named(Button("定位聚落并开始建设", () =>
            {
                _map.SelectedSettlementId = town.Id;
                SetCategory(ToolCategory.Build);
                _map.FocusTile(town.X, town.Y);
            }), "infrastructure-build"));
            actions.Children.Add(Named(Button("查看设施成本与建造", () => ShowBuildingEditor(town.Id)), "building-open"));
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
                ? $"{town.Name} ↔ {t.Name}\n信号连通\n预计 {ticks} 日"
                : $"{town.Name} ↔ {t.Name}\n信号未连通\n依赖居民实际携带消息",
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
                $"{FactKindName(r.Topic)}\n主题 #{r.SubjectId}\n数值 {r.Value:F2}\n原始提出者：{ResidentName(r.OriginResidentId)}（{ProfessionName(r.ReportedProfession)}）\n递送代表：{ResidentName(r.RepresentativeId)}\n观察：{DateLabel(r.ObservedTick)}\n递送：{DateLabel(r.ReceivedTick)}\n可信度 {r.Confidence:P0}\n消息年龄 {Math.Max(0, _engine.State.Tick - r.ObservedTick)} 日");
    }

    private void ShowBuildingEditor(int townId)
    {
        ShowBuildingSelectionEditor(townId, BuildingKind.Farm);
    }

    private void ShowBuildingSelectionEditor(int townId, BuildingKind initialKind)
    {
        var town = _engine.State.Settlements.FirstOrDefault(t => t.Id == townId);
        if (town is null)
            return;
        var panel = ModalPanel("建造设施", "选择设施和目标地块。安排施工会扣除当地材料，等待居民到场完成。");
        var type = EnumField(panel, "设施类型", initialKind, WorldEngine.BuildingName, "building-kind");
        var bridgeOptions = Named(new StackPanel { Spacing = 10 }, "building-bridge-options");
        panel.Children.Add(bridgeOptions);
        var direction = EnumField(bridgeOptions, "桥梁方向", BridgeDirection.Horizontal, WorldEngine.BridgeDirectionName,
            "building-bridge-direction");
        var level = ObjectField(bridgeOptions, "桥梁等级",
            new[] { (1, "1 级：离岸 2 格"), (2, "2 级：离岸 4 格"), (3, "3 级：离岸 6 格") }, 1, "building-bridge-level");
        var cost = Named(Paragraph(""), "building-requirements");
        panel.Children.Add(cost);

        void RefreshCost()
        {
            bridgeOptions.IsVisible = type.SelectedItem is BuildingKind.Bridge;
            if (type.SelectedItem is BuildingKind kind)
            {
                cost.Text = DisplayFormat.Text(
                    "施工材料：" + StockLabel(WorldEngine.FacilityCost(kind,
                                kind == BuildingKind.Bridge ? Integer(level) : 1))
                            + (WorldEngine.IsWaterfrontBuilding(kind) ? "须建在紧邻自然陆岸的水域。" : "")
                            + "\n" + (ProductionRules.For(kind) is { } a
                                ? "运营需要：" + a.Research.Name + "及其前置\n"
                                : "")
                            + (kind is BuildingKind.Waystation or BuildingKind.MountainPass or BuildingKind.Bridge
                                    or BuildingKind.Dock ? "建设知识：驿路运输。\n"
                                : kind == BuildingKind.ArcaneSanctum ? "建设知识：奥术基础，且须开启魔法发展。\n" : "")
                            + (WorldEngine.BuildingRace(kind) is { } race
                                ? $"种族条件：本聚落须有成年{RaceName(race)}，由同族成年人运营。\n"
                                : "")
                            + WorldEngine.BuildingDescription(kind));
            }
        }

        type.SelectionChanged += (_, _) => RefreshCost();
        level.SelectionChanged += (_, _) => RefreshCost();
        RefreshCost();
        panel.Children.Add(Paragraph($"{town.Name}库存：{StockLabel(town.Resources)}"));
        var x = Field(panel, "目标 X", _selectedTile?.X ?? town.X + 1, "building-x");
        var y = Field(panel, "目标 Y", _selectedTile?.Y ?? town.Y, "building-y");
        AddMapPicker(panel, x, y);
        var gift = Named(new CheckBox { Content = Text("直接赐予完工（不扣施工材料，仍需运营条件）", 12), IsChecked = false },
            "building-gift");
        panel.Children.Add(gift);
        var placement = Named(Paragraph(""), "building-placement-status");
        panel.Children.Add(placement);

        void RefreshPlacement()
        {
            try
            {
                var kind = (BuildingKind)type.SelectedItem!;
                var error = _engine.FacilityPlacementError(townId, kind, Integer(x), Integer(y), gift.IsChecked == true,
                    kind == BuildingKind.Bridge ? (BridgeDirection?)direction.SelectedItem : null,
                    kind == BuildingKind.Bridge ? Integer(level) : 1);
                placement.Text = DisplayFormat.Text(error is not null ? "暂不能建造：" + error
                    : gift.IsChecked == true ? "可直接赐予完工；运营仍需满足设施条件" : "可安排施工；提交后扣除材料，等待居民到场");
            }
            catch (ArgumentException)
            {
                placement.Text = "请填写有效的整数坐标与桥梁等级。";
            }
        }

        type.SelectionChanged += (_, _) => RefreshPlacement();
        direction.SelectionChanged += (_, _) => RefreshPlacement();
        level.SelectionChanged += (_, _) => RefreshPlacement();
        x.ValueChanged += (_, _) => RefreshPlacement();
        y.ValueChanged += (_, _) => RefreshPlacement();
        gift.IsCheckedChanged += (_, _) => RefreshPlacement();
        RefreshPlacement();
        panel.Children.Add(Named(Button("建造设施", () =>
        {
            try
            {
                var xx = Integer(x);
                var yy = Integer(y);
                RunEdit(() =>
                {
                    if (gift.IsChecked == true)
                    {
                        _engine.GrantFacility(townId, (BuildingKind)type.SelectedItem!, xx, yy,
                            (BuildingKind)type.SelectedItem! == BuildingKind.Bridge
                                ? (BridgeDirection?)direction.SelectedItem
                                : null, (BuildingKind)type.SelectedItem! == BuildingKind.Bridge ? Integer(level) : 1);
                    }
                    else
                    {
                        _engine.BuildFacility(townId, (BuildingKind)type.SelectedItem!, xx, yy,
                            (BuildingKind)type.SelectedItem! == BuildingKind.Bridge
                                ? (BridgeDirection?)direction.SelectedItem
                                : null, (BuildingKind)type.SelectedItem! == BuildingKind.Bridge ? Integer(level) : 1);
                    }

                    CloseModal();
                }, gift.IsChecked == true ? "设施已赐予；效果按各建筑的生效条件提供" : "设施已立项，继续模拟后居民会施工");
            }
            catch (ArgumentException ex)
            {
                SetStatus(FriendlyError(ex));
            }
        }), "building-apply"));
        OpenModal(panel);
    }

    private void ShowSpellSelectionEditor(SpellKind initialSpell, int? townId = null)
    {
        var selectedPerson = _inspectorMode == "resident" && townId is null
            ? _engine.GetResident(_selectedResidentId)
            : null;
        var localTown = townId ?? selectedPerson?.SettlementId ?? _inspectorSettlementId;
        var casters = _engine.State.Residents
            .Where(r => r.Health > 0 && r.Age >= 14 && (localTown <= 0 || r.SettlementId == localTown))
            .OrderBy(r => _engine.SpellUnlockError(r.Id, initialSpell) is not null)
            .ThenBy(r => r.MagicTalent < 25 || r.MagicTraining < 8).ThenByDescending(r => r.MagicTraining)
            .ThenBy(r => r.Id).ToArray();
        if (casters.Length == 0)
        {
            OpenModal(ModalPanel("施放魔法", "当前聚落没有存活的成年居民可供施法。可在居民档案查看天赋、训练与魔力。"));
            return;
        }

        var panel = ModalPanel("施放魔法", "选择当地施法者和目标。需要成年、天赋至少 25、训练至少 8；目标须在施法者 4 格内，具体法术还需满足当地研究、魔力和目标条件。");
        var caster = Named(new ComboBox
        {
            ItemsSource = casters.Select(r =>
                    $"{r.Name}   {TownName(r.SettlementId)}\n天赋 {r.MagicTalent:F0}   训练 {r.MagicTraining:F0}   魔力 {r.Mana:F0}")
                .ToArray(),
            SelectedIndex = selectedPerson is null
                ? 0
                : Math.Max(0, Array.FindIndex(casters, r => r.Id == selectedPerson.Id)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        }, "spell-caster");
        panel.Children.Add(caster);
        var spell = EnumField(panel, "法术", initialSpell, SpellName, "spell-kind");
        var initial = casters[caster.SelectedIndex];
        var x = Field(panel, "目标 X", _selectedTile?.X ?? initial.X, "spell-x");
        var y = Field(panel, "目标 Y", _selectedTile?.Y ?? initial.Y, "spell-y");
        var targetChosen = _selectedTile.HasValue;
        var movingTarget = false;
        AddMapPicker(panel, x, y, () => targetChosen = true);
        var requirements = Named(Paragraph(""), "spell-requirements");
        panel.Children.Add(requirements);

        string? Blocker()
        {
            var person = casters[caster.SelectedIndex];
            if (person.MagicTalent < 25 || person.MagicTraining < 8)
                return "需要魔法天赋至少 25、训练至少 8";
            if (_engine.SpellUnlockError(person.Id, (SpellKind)spell.SelectedItem!) is { } knowledge)
                return knowledge;
            if (x.Value is not { } xx || y.Value is not { } yy || xx != decimal.Truncate(xx) ||
                yy != decimal.Truncate(yy))
                return "请选择有效的整数目标坐标";
            if (Math.Abs(person.X - Integer(x)) + Math.Abs(person.Y - Integer(y)) > 4)
                return "目标须位于施法者 4 格以内";
            return null;
        }

        var apply = Named(Button("施放法术", () =>
        {
            try
            {
                var xx = Integer(x);
                var yy = Integer(y);
                RunEdit(() =>
                {
                    _engine.CastSpell(casters[caster.SelectedIndex].Id, (SpellKind)spell.SelectedItem!, xx, yy);
                    CloseModal();
                }, "法术已生效，消耗已从施法者魔力扣除");
            }
            catch (ArgumentException ex)
            {
                SetStatus(FriendlyError(ex));
            }
        }), "spell-apply");

        void RefreshRequirements()
        {
            var blocker = Blocker();
            requirements.Text = DisplayFormat.Text(
                $"基础魔力：{WorldEngine.SpellManaCost((SpellKind)spell.SelectedItem!):0}\n"
                + (blocker is null ? "训练、当地知识与目标距离满足；施放时检查魔力和实际目标" : "暂不能施放：" + blocker));
            apply.IsEnabled = blocker is null;
        }

        void TargetChanged()
        {
            if (!movingTarget)
                targetChosen = true;
            RefreshRequirements();
        }

        x.ValueChanged += (_, _) => TargetChanged();
        y.ValueChanged += (_, _) => TargetChanged();
        caster.SelectionChanged += (_, _) =>
        {
            if (!targetChosen)
            {
                movingTarget = true;
                x.Value = casters[caster.SelectedIndex].X;
                y.Value = casters[caster.SelectedIndex].Y;
                movingTarget = false;
            }

            RefreshRequirements();
        };
        spell.SelectionChanged += (_, _) => RefreshRequirements();
        RefreshRequirements();
        panel.Children.Add(apply);
        OpenModal(panel);
    }
}
