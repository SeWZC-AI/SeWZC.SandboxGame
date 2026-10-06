using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    private bool _civilizationDetails;
    private string _researchBranch = "";
    private bool _researchExpanded;
    private string _researchRoute = "technology";
    private ResearchKind _selectedResearch = ResearchKind.Agriculture;

    private void BuildResearchTree(StackPanel panel, Settlement town)
    {
        panel.Children.Add(LiveText(
            () => _researchRoute == "magic" ? "魔法研究树" : _researchRoute == "common" ? "两条路线的共同基础" : "科技研究树", 17, Mint));
        panel.Children.Add(Named(Button("展开 / 收起科技树视野", () =>
        {
            _researchExpanded = !_researchExpanded;
            _expandedInspector = _researchExpanded;
            ApplyLayout();
            RefreshInspector();
        }), "research-expand"));
        panel.Children.Add(Paragraph("从上向下发展，每条支线占据独立位置。箭头表示真实前置。可按分支查看并显示前置路径；文明发展成果在研究树外单独判定。"));
        panel.Children.Add(Named(LiveText(() =>
        {
            var r = _engine.State.Society.Research.First(x => x.SettlementId == town.Id);
            return $"已掌握 {r.Completed.Count} / {ResearchRules.All.Count}\n" + (r.ActiveProject is { } active
                ? $"正在研究：{WorldEngine.ResearchName(active)}\n进度 {r.Progress:0.#} / {r.RequiredProgress:0}\n{_engine.GetCompletionEstimate(r.Observation, r.Progress, r.RequiredProgress).Explanation}"
                : "学舍可立项新研究");
        }), "research-summary"));

        string? Blocker(ResearchKind kind)
        {
            var r = _engine.State.Society.Research.First(x => x.SettlementId == town.Id);
            if (r.Completed.Contains(kind)) return "当地已经掌握";
            if (r.ActiveProject is { } active) return active == kind ? "当地正在研究" : "等待当前研究完成";
            return _engine.ResearchPrerequisiteError(town.Id, kind)
                   ?? (!_engine.State.Society.Buildings.Any(b =>
                       b.SettlementId == town.Id && b.Kind == BuildingKind.Academy && b.IsCompleted)
                       ? "需要已建成的学舍"
                       : null)
                   ?? WorldEngine.MissingResources(town.Resources, WorldEngine.GetResearchCost(kind));
        }

        var nodes = new Dictionary<ResearchKind, Button>();
        foreach (var definition in ResearchRules.All)
        {
            var content = new StackPanel { Spacing = 2 };
            content.Children.Add(Text(definition.Name, 13, null, true));
            content.Children.Add(Text(definition.Branch, 11, Muted));
            content.Children.Add(Named(LiveText(() =>
            {
                var r = _engine.State.Society.Research.First(x => x.SettlementId == town.Id);
                return r.Completed.Contains(definition.Kind)
                    ? "已掌握"
                    : r.ActiveProject == definition.Kind
                        ? $"研究中 {r.Progress / r.RequiredProgress:P0}"
                        : _engine.ResearchPrerequisiteError(town.Id, definition.Kind) is not null
                            ? "前置未解锁"
                            : Blocker(definition.Kind) is null
                                ? "可研究"
                                : "待投入";
            }, 11, Mint), "research-state-" + definition.Kind));
            var node = Named(new Button
            {
                Content = content,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1.5),
                Margin = new Thickness(0),
                Padding = new Thickness(8, 5),
            }, "research-node-" + definition.Kind);
            ToolTip.SetTip(node, definition.Name + "\n" + definition.Effect);
            node.Click += (_, _) =>
            {
                _selectedResearch = definition.Kind;
                RefreshInspector();
            };
            _inspectorUpdates.Add(() =>
            {
                node.BorderBrush = _selectedResearch == definition.Kind ? Brush.Parse("#F1CD83")
                    : _engine.HasResearch(town.Id, definition.Kind) ? Brush.Parse("#65B995")
                    : Blocker(definition.Kind) is null ? Brush.Parse("#80CED1")
                    : definition.Magic ? Brush.Parse("#A996D8") : Line;
                node.Background = _engine.HasResearch(town.Id, definition.Kind) ? Brush.Parse("#19352C") : Ink;
            });
            nodes.Add(definition.Kind, node);
        }

        var graph = Named(
            new ResearchGraphControl(nodes)
            {
                Height = _isCompact ? 380 : 560,
                IsCompleted = kind => _engine.HasResearch(town.Id, kind),
                Selected = _selectedResearch,
            }, "research-graph");

        IEnumerable<ResearchDefinition> Route()
        {
            var route = _researchRoute == "common"
                ? ResearchRules.All.Where(d => d.Shared)
                : ResearchRules.All.Where(d => ResearchRules.Route(_researchRoute == "magic").Contains(d.Kind));
            if (_researchBranch.Length == 0) return route;
            var wanted = new HashSet<ResearchKind>();

            void Add(ResearchKind kind)
            {
                if (!wanted.Add(kind)) return;
                foreach (var p in ResearchRules.For(kind).Prerequisites) Add(p);
            }

            foreach (var d in route.Where(d => d.Branch == _researchBranch)) Add(d.Kind);
            return route.Where(d => wanted.Contains(d.Kind));
        }

        graph.ShowRoute(Route());
        var tabs = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 4 };
        var routes = new[] { ("科技路线", "technology"), ("魔法路线", "magic"), ("共同基础", "common") };
        for (var i = 0; i < routes.Length; i++)
        {
            var (label, route) = routes[i];
            var tab = Named(Button(label, () =>
            {
                _researchRoute = route;
                _researchBranch = "";
                graph.ShowRoute(Route());
                if (!graph.Layout.Nodes.ContainsKey(_selectedResearch))
                    _selectedResearch = route == "magic" ? ResearchKind.ArcaneArts : ResearchKind.Agriculture;
                graph.Focus(_selectedResearch);
                RefreshInspector();
            }), "research-route-" + route);
            tab.HorizontalAlignment = HorizontalAlignment.Stretch;
            tab.Padding = new Thickness(4, 5);
            _inspectorUpdates.Add(() => tab.BorderBrush = _researchRoute == route ? Mint : Line);
            Grid.SetColumn(tab, i);
            tabs.Children.Add(tab);
        }

        panel.Children.Add(tabs);
        var branches = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var branch in new[] { "" }.Concat(ResearchRules.All.Select(d => d.Branch).Distinct()))
        {
            var button = Named(Button(branch.Length == 0 ? "全部分支" : branch, () =>
            {
                _researchBranch = branch;
                graph.ShowRoute(Route());
                if (!graph.Layout.Nodes.ContainsKey(_selectedResearch))
                    _selectedResearch = graph.Layout.Nodes.Keys.First();
                graph.Fit();
                RefreshInspector();
            }), "research-branch-" + (branch.Length == 0 ? "all" : branch));
            branches.Children.Add(button);
            _inspectorUpdates.Add(() =>
            {
                button.IsVisible = branch.Length == 0 || ResearchRules.All.Any(d =>
                    d.Branch == branch && (_researchRoute == "common"
                        ? d.Shared
                        : ResearchRules.Route(_researchRoute == "magic").Contains(d.Kind)));
                button.BorderBrush = _researchBranch == branch ? Mint : Line;
            });
        }

        panel.Children.Add(branches);
        var outcome = new StackPanel { Spacing = 5 };
        outcome.Children.Add(Named(LiveText(() =>
        {
            var result = _engine.GetCivilizationProgress(town.Id, _researchRoute == "magic");
            return "文明发展成果：" + result.Name + (result.Achieved ? "  已达到" : "  发展中")
                   + $"\n研究 {result.KnownResearch}/{result.TotalResearch}   配套设施 {result.ReadyFacilities}/{result.TotalFacilities}   待首次加工 {result.UnprovenProduction.Count}";
        }, 13, Mint), "civilization-progress"));
        var outcomeDetails = Named(LiveText(() =>
        {
            var result = _engine.GetCivilizationProgress(town.Id, _researchRoute == "magic");
            return "文明阶段由实际成果判定，无须另行研究或花费材料。\n未掌握：" + (result.MissingResearch.Count == 0
                                                        ? "无"
                                                        : string.Join("、", result.MissingResearch))
                                                    + "\n未就绪设施：" + (result.MissingFacilities.Count == 0
                                                        ? "无"
                                                        : string.Join("、", result.MissingFacilities))
                                                    + "\n待首批实物加工：" + (result.UnprovenProduction.Count == 0
                                                        ? "无"
                                                        : string.Join("、", result.UnprovenProduction));
        }, 12, Muted), "civilization-requirements");
        outcome.Children.Add(outcomeDetails);
        _inspectorUpdates.Add(() => outcomeDetails.IsVisible = _civilizationDetails);
        panel.Children.Add(Card(outcome));
        var tools = new WrapPanel { Orientation = Orientation.Horizontal };
        tools.Children.Add(Named(Button("缩小", () =>
        {
            graph.SetZoom(graph.Zoom - .15);
            RefreshInspector();
        }), "research-zoom-out"));
        tools.Children.Add(Named(Button("放大", () =>
        {
            graph.SetZoom(graph.Zoom + .15);
            RefreshInspector();
        }), "research-zoom-in"));
        tools.Children.Add(Named(LiveText(() => $"{graph.Zoom:P0}", 11, Muted), "research-zoom"));
        tools.Children.Add(Named(Button("全树", () =>
        {
            graph.Fit();
            RefreshInspector();
        }), "research-fit"));
        tools.Children.Add(Named(Button("定位所选", () => graph.Focus(_selectedResearch)), "research-focus"));
        var pathButton = Named(Button("完整路径", () =>
        {
            graph.ShowFullPath = !graph.ShowFullPath;
            RefreshInspector();
        }), "research-full-path");
        tools.Children.Add(pathButton);
        _inspectorUpdates.Add(() =>
        {
            pathButton.Content = graph.ShowFullPath ? "只看直接前置" : "完整路径";
            pathButton.BorderBrush = graph.ShowFullPath ? Mint : Line;
        });
        tools.Children.Add(Named(Button("发展成果", () =>
        {
            _civilizationDetails = !_civilizationDetails;
            RefreshInspector();
        }), "research-development"));
        panel.Children.Add(tools);
        panel.Children.Add(Named(LiveText(() => "所选：" + WorldEngine.ResearchName(_selectedResearch) + "\n直接前置："
                                                + (ResearchRules.For(_selectedResearch).Prerequisites.Length == 0
                                                    ? "根部研究，无前置"
                                                    : string.Join("、",
                                                        ResearchRules.For(_selectedResearch).Prerequisites
                                                            .Select(WorldEngine.ResearchName))), 12, Muted),
            "research-path-caption"));
        panel.Children.Add(new Border
        {
            Child = graph,
            BorderBrush = Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            ClipToBounds = true,
        });
        _inspectorUpdates.Add(() =>
        {
            graph.Selected = _selectedResearch;
            graph.Height = _isCompact ? 380 : _researchExpanded ? 720 : 560;
            graph.RefreshConnections();
        });
        var detail = new StackPanel { Spacing = 6 };
        detail.Children.Add(Named(LiveText(() => WorldEngine.ResearchName(_selectedResearch), 15, Mint),
            "research-selected"));
        detail.Children.Add(Named(LiveText(() =>
        {
            var definition = ResearchRules.For(_selectedResearch);
            return WorldEngine.ResearchDescription(_selectedResearch)
                   + "\n前置知识：" + (definition.Prerequisites.Length == 0
                       ? "根部研究，无前置知识"
                       : string.Join("、", definition.Prerequisites.Select(WorldEngine.ResearchName)))
                   + "\n投入材料：" + StockLabel(WorldEngine.GetResearchCost(_selectedResearch))
                   + "\n" + (Blocker(_selectedResearch) ?? "前置知识与魔法规则已满足，研究材料充足");
        }), "research-requirements"));
        var unlocks = Named(new StackPanel { Spacing = 5 }, "research-unlocks");
        detail.Children.Add(unlocks);
        ResearchKind? shown = null;
        bool? shownKnown = null;
        _inspectorUpdates.Add(() =>
        {
            var known = _engine.HasResearch(town.Id, _selectedResearch);
            if (shown == _selectedResearch && shownKnown == known) return;
            shown = _selectedResearch;
            shownKnown = known;
            unlocks.Children.Clear();
            var definition = ResearchRules.For(_selectedResearch);
            foreach (var kind in definition.UnlockedBuildings)
            {
                var button =
                    Named(
                        Button("建造 " + WorldEngine.BuildingName(kind),
                            () => ShowBuildingSelectionEditor(town.Id, kind)), "research-build-" + kind);
                button.IsEnabled = known;
                unlocks.Children.Add(button);
            }

            foreach (var job in definition.UnlockedProfessions)
            {
                unlocks.Children.Add(Paragraph(WorldEngine.ProfessionName(job) + "：" +
                                               WorldEngine.ProfessionDescription(job)));
                var button =
                    Named(Button("分配 " + WorldEngine.ProfessionName(job), () => ShowResearchJobEditor(town.Id, job)),
                        "research-job-" + job);
                button.IsEnabled = known;
                unlocks.Children.Add(button);
            }

            foreach (var spell in definition.UnlockedSpells)
            {
                var button =
                    Named(
                        Button("施放 " + WorldEngine.SpellName(spell) + $"  魔力 {WorldEngine.SpellManaCost(spell):0}",
                            () => ShowSpellSelectionEditor(spell, town.Id)), "research-spell-" + spell);
                button.IsEnabled = known;
                unlocks.Children.Add(button);
            }

            if (definition.Action == "铺设铁路")
            {
                var button = Named(Button("铺设铁路", () => ShowRailEditor(town.Id)), "research-action-rail");
                button.IsEnabled = known;
                unlocks.Children.Add(button);
            }

            if (definition.Action == "使用折跃门")
            {
                var button = Named(Button("使用折跃门", ShowWaygateEditor), "research-action-waygate");
                button.IsEnabled = known;
                unlocks.Children.Add(button);
            }
        });
        var start = Named(Button("投入研究", () =>
        {
            var research = _selectedResearch;
            RunEdit(() => _engine.StartResearch(town.Id, research), "研究已立项，居民将到学舍推进");
        }), "research-start");
        detail.Children.Add(start);
        panel.Children.Add(Card(detail));
        _inspectorUpdates.Add(() => start.IsEnabled = Blocker(_selectedResearch) is null);
    }
}
