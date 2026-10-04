using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    private bool _researchExpanded;
    private ResearchKind _selectedResearch = ResearchKind.Agriculture;

    private void BuildResearchTree(StackPanel panel, Settlement town)
    {
        panel.Children.Add(Text("文明科技树", 17, Mint, true));
        panel.Children.Add(Named(Button("展开 / 收起科技树视野", () => { _researchExpanded = !_researchExpanded; _expandedInspector = _researchExpanded; ApplyLayout(); RefreshInspector(); }), "research-expand"));
        panel.Children.Add(Paragraph("点击研究卡片查看用途与投入。箭头标明前置知识；每处聚落同时研究一项。"));
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
        var detail = new StackPanel { Spacing = 6 };
        detail.Children.Add(Named(LiveText(() => WorldEngine.ResearchName(_selectedResearch), 15, Mint), "research-selected"));
        detail.Children.Add(Named(LiveText(() => WorldEngine.ResearchDescription(_selectedResearch)
            + "\n投入材料：" + StockLabel(WorldEngine.GetResearchCost(_selectedResearch))
            + "\n" + (Blocker(_selectedResearch) ?? "前置知识与魔法规则已满足，研究材料充足")), "research-requirements"));
        var start = Named(Button("投入研究", () => RunEdit(() => _engine.StartResearch(town.Id, _selectedResearch), "研究已立项，居民将到学舍推进")), "research-start");
        detail.Children.Add(start); panel.Children.Add(Card(detail));
        _inspectorUpdates.Add(() => start.IsEnabled = Blocker(_selectedResearch) is null);
        var branches = ResearchRules.All.GroupBy(r => r.Branch).ToArray();
        foreach (var branch in branches)
        {
            var body = new WrapPanel { Orientation = Orientation.Horizontal };
            var header = LiveText(() => $"{branch.Key}   {branch.Count(r => _engine.HasResearch(town.Id, r.Kind))} / {branch.Count()}", 14, Mint);
            var section = Named(new Expander { Header = header, Content = body, IsExpanded = true, HorizontalAlignment = HorizontalAlignment.Stretch }, "research-branch-" + Array.IndexOf(branches, branch));
            panel.Children.Add(section);
            body.SizeChanged += (_, _) =>
            {
                var columns = Math.Max(1, (int)(body.Bounds.Width / 245));
                foreach (var child in body.Children) child.Width = Math.Max(160, body.Bounds.Width / columns - 6);
            };
            foreach (var definition in branch)
            {
                var content = new StackPanel { Spacing = 3 };
                content.Children.Add(Text(definition.Name, 13, null, true));
                content.Children.Add(Text(definition.Stage, 11, Muted));
                content.Children.Add(Text(definition.Prerequisites.Length == 0 ? "起点" : "← " + string.Join("、", definition.Prerequisites.Select(WorldEngine.ResearchName)), 11, Muted));
                var state = Named(LiveText(() =>
                {
                    var r = _engine.State.Society.Research.First(x => x.SettlementId == town.Id);
                    return r.Completed.Contains(definition.Kind) ? "已掌握" : r.ActiveProject == definition.Kind
                        ? $"研究中 {r.Progress / r.RequiredProgress:P0}" : _engine.ResearchPrerequisiteError(town.Id, definition.Kind) is not null
                            ? "前置未解锁" : Blocker(definition.Kind) is null ? "可研究" : "待投入";
                }, 12, Mint), "research-state-" + definition.Kind);
                content.Children.Add(state);
                var node = Named(new Button { Content = content, HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(10, 7),
                    CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Width = 260, Margin = new Thickness(3) }, "research-node-" + definition.Kind);
                node.Click += (_, _) => { _selectedResearch = definition.Kind; RefreshInspector(); };
                _inspectorUpdates.Add(() =>
                {
                    node.BorderBrush = _selectedResearch == definition.Kind ? Mint : Line;
                    node.Background = _engine.HasResearch(town.Id, definition.Kind) ? Brush.Parse("#1B352D") : Ink;
                });
                body.Children.Add(node);
            }
        }
    }
}
