using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    private bool _researchExpanded;
    private string _researchRoute = "all";
    private ResearchKind _selectedResearch = ResearchKind.Agriculture;

    private void BuildResearchTree(StackPanel panel, Settlement town)
    {
        panel.Children.Add(Text("文明科技树", 17, Mint, true));
        panel.Children.Add(Named(Button("展开 / 收起科技树视野", () => { _researchExpanded = !_researchExpanded; _expandedInspector = _researchExpanded; ApplyLayout(); RefreshInspector(); }), "research-expand"));
        panel.Children.Add(Paragraph("从根部向下发展。连线连接前置知识与后续研究；点击节点查看用途与投入，拖动空白处移动视野。"));
        panel.Children.Add(Named(LiveText(() =>
        {
            var r = _engine.State.Society.Research.First(x => x.SettlementId == town.Id);
            return $"已掌握 {r.Completed.Count} / {ResearchRules.All.Count}\n" + (r.ActiveProject is { } active
                ? $"正在研究：{WorldEngine.ResearchName(active)}\n进度 {r.Progress:0.#} / {r.RequiredProgress:0}\n{_engine.GetCompletionEstimate(r.Observation, r.Progress, r.RequiredProgress).Explanation}" : "学舍可立项新研究");
        }), "research-summary"));
        string? Blocker(ResearchKind kind)
        {
            var r = _engine.State.Society.Research.First(x => x.SettlementId == town.Id);
            if (r.Completed.Contains(kind)) return "当地已经掌握";
            if (r.ActiveProject is { } active) return active == kind ? "当地正在研究" : "等待当前研究完成";
            return _engine.ResearchPrerequisiteError(town.Id, kind)
                ?? (!_engine.State.Society.Buildings.Any(b => b.SettlementId == town.Id && b.Kind == BuildingKind.Academy && b.IsCompleted) ? "需要已建成的学舍" : null)
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
                return r.Completed.Contains(definition.Kind) ? "已掌握" : r.ActiveProject == definition.Kind
                    ? $"研究中 {r.Progress / r.RequiredProgress:P0}" : _engine.ResearchPrerequisiteError(town.Id, definition.Kind) is not null
                        ? "前置未解锁" : Blocker(definition.Kind) is null ? "可研究" : "待投入";
            }, 11, Mint), "research-state-" + definition.Kind));
            var node = Named(new Button { Content = content, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1.5), Margin = new Thickness(0),
                Padding = new Thickness(8, 5) }, "research-node-" + definition.Kind);
            ToolTip.SetTip(node, definition.Name + "\n" + definition.Effect);
            node.Click += (_, _) => { _selectedResearch = definition.Kind; RefreshInspector(); };
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
        var graph = Named(new ResearchGraphControl(nodes) { Height = _isCompact ? 380 : 560,
            IsCompleted = kind => _engine.HasResearch(town.Id, kind), Selected = _selectedResearch }, "research-graph");
        IEnumerable<ResearchDefinition> Route() => _researchRoute == "all" ? ResearchRules.All
            : ResearchRules.All.Where(d => ResearchRules.Route(_researchRoute == "magic").Contains(d.Kind));
        graph.ShowRoute(Route());
        var tabs = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 4 };
        var routes = new[] { ("全部枝干", "all"), ("科技帝国", "technology"), ("魔法帝国", "magic") };
        for (var i = 0; i < routes.Length; i++)
        {
            var (label, route) = routes[i];
            var tab = Named(Button(label, () => { _researchRoute = route; graph.ShowRoute(Route()); RefreshInspector(); }), "research-route-" + route);
            tab.HorizontalAlignment = HorizontalAlignment.Stretch; tab.Padding = new Thickness(4, 5);
            _inspectorUpdates.Add(() => tab.BorderBrush = _researchRoute == route ? Mint : Line);
            Grid.SetColumn(tab, i); tabs.Children.Add(tab);
        }
        panel.Children.Add(tabs);
        var tools = new WrapPanel { Orientation = Orientation.Horizontal };
        tools.Children.Add(Named(Button("缩小", () => { graph.SetZoom(graph.Zoom - .15); RefreshInspector(); }), "research-zoom-out"));
        tools.Children.Add(Named(Button("放大", () => { graph.SetZoom(graph.Zoom + .15); RefreshInspector(); }), "research-zoom-in"));
        tools.Children.Add(Named(LiveText(() => $"{graph.Zoom:P0}", 11, Muted), "research-zoom"));
        tools.Children.Add(Named(Button("全树", () => { graph.Fit(); RefreshInspector(); }), "research-fit"));
        tools.Children.Add(Named(Button("定位所选", () => graph.Focus(_selectedResearch)), "research-focus"));
        tools.Children.Add(Named(Button("帝国终点", () =>
        {
            _selectedResearch = _researchRoute == "magic" ? ResearchKind.MagicalEmpire : ResearchKind.TechnologicalEmpire;
            graph.Focus(_selectedResearch); RefreshInspector();
        }), "research-jump-end"));
        panel.Children.Add(tools);
        panel.Children.Add(new TextBlock { Text = "绿色 已掌握   青色 可研究   紫色 魔法   金色 所选前置路径", FontSize = 11,
            Foreground = Muted, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new Border { Child = graph, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), ClipToBounds = true });
        _inspectorUpdates.Add(() =>
        {
            graph.Selected = _selectedResearch; graph.Height = _isCompact ? 380 : _researchExpanded ? 720 : 560;
            graph.RefreshConnections();
        });
        var detail = new StackPanel { Spacing = 6 };
        detail.Children.Add(Named(LiveText(() => WorldEngine.ResearchName(_selectedResearch), 15, Mint), "research-selected"));
        detail.Children.Add(Named(LiveText(() =>
        {
            var definition = ResearchRules.For(_selectedResearch);
            return WorldEngine.ResearchDescription(_selectedResearch)
                + "\n前置知识：" + (definition.Prerequisites.Length == 0 ? "根部研究，无前置知识" : string.Join("、", definition.Prerequisites.Select(WorldEngine.ResearchName)))
                + "\n投入材料：" + StockLabel(WorldEngine.GetResearchCost(_selectedResearch))
                + "\n" + (Blocker(_selectedResearch) ?? "前置知识与魔法规则已满足，研究材料充足");
        }), "research-requirements"));
        var start = Named(Button("投入研究", () => RunEdit(() => _engine.StartResearch(town.Id, _selectedResearch), "研究已立项，居民将到学舍推进")), "research-start");
        detail.Children.Add(start); panel.Children.Add(Card(detail));
        _inspectorUpdates.Add(() => start.IsEnabled = Blocker(_selectedResearch) is null);
    }
}
