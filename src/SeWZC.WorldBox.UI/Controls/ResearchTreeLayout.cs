using Avalonia;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI.Controls;

public sealed record ResearchTreeEdge(ResearchKind From, ResearchKind To, Point[] Points, bool EmpireMerge);
public sealed record ResearchTreeLane(string Name, double Left, double Width);

// Stable branch columns make each route readable. Rules remain the authority for edges.
public sealed class ResearchTreeLayout
{
    public const double NodeWidth = 140, NodeHeight = 70;
    public IReadOnlyDictionary<ResearchKind, Rect> Nodes { get; }
    public IReadOnlyList<ResearchTreeEdge> Edges { get; }
    public IReadOnlyList<ResearchTreeLane> Lanes { get; }
    public Size Size { get; }

    private static readonly IReadOnlyDictionary<ResearchKind, (double Column, int Row)> Positions =
        new Dictionary<ResearchKind, (double, int)>
        {
            [ResearchKind.Agriculture] = (.5, 0), [ResearchKind.Logistics] = (1.5, 0),
            [ResearchKind.Irrigation] = (0, 1), [ResearchKind.Forestry] = (1, 1), [ResearchKind.Medicine] = (2, 1),
            [ResearchKind.ScientificMethod] = (3, 1), [ResearchKind.Industry] = (4, 1),
            [ResearchKind.EfficientSmelting] = (4, 2), [ResearchKind.Electrification] = (5, 2),
            [ResearchKind.EnergyRecycling] = (5, 3), [ResearchKind.SignalNetwork] = (6, 3), [ResearchKind.Automation] = (7, 3),
            [ResearchKind.Aviation] = (6, 4), [ResearchKind.AdvancedComputing] = (7, 4),
            [ResearchKind.TechnologicalEmpire] = (3.5, 5),
            [ResearchKind.ArcaneArts] = (4.5, 0), [ResearchKind.ManaAttunement] = (3, 1), [ResearchKind.Crystalcraft] = (5, 1),
            [ResearchKind.Restoration] = (3, 2), [ResearchKind.ArcaneScholarship] = (4, 2), [ResearchKind.RunicEngineering] = (5, 2),
            [ResearchKind.Leylines] = (5, 3), [ResearchKind.AetherMastery] = (6, 3), [ResearchKind.MagicalEmpire] = (3, 4)
        };

    public ResearchTreeLayout(IEnumerable<ResearchDefinition> definitions)
    {
        var items = definitions.ToArray();
        var included = items.Select(d => d.Kind).ToHashSet();
        if (included.Contains(ResearchKind.TechnologicalEmpire) && included.Contains(ResearchKind.MagicalEmpire))
            throw new ArgumentException("Choose one empire route so its branches remain distinct.", nameof(definitions));
        var nodes = items.ToDictionary(d => d.Kind, d =>
        {
            var (column, row) = Positions[d.Kind];
            return new Rect(32 + column * 168, 52 + row * 120, NodeWidth, NodeHeight);
        });
        var magic = items.Any(d => d.Magic);
        var technology = items.Any(d => d.Branch == "工业与能源");
        Lanes = technology ? [new("民生支线", 18, 490), new("研究方法", 522, 154), new("工业与能源", 690, 322), new("运输与制造", 1026, 322)]
            : magic ? [new("民生支线", 18, 490), new("奥术与修复", 522, 322), new("符文与以太", 858, 322)]
            : [new("共同基础", 18, 490)];
        var edges = new List<ResearchTreeEdge>();
        foreach (var d in items)
        for (var index = 0; index < d.Prerequisites.Length; index++)
        {
            var p = d.Prerequisites[index];
            if (!included.Contains(p)) continue;
            var source = nodes[p]; var target = nodes[d.Kind];
            var from = new Point(source.Center.X, source.Bottom);
            var empire = d.Kind is ResearchKind.TechnologicalEmpire or ResearchKind.MagicalEmpire;
            var to = new Point(target.Center.X + (empire ? 0 : (index - (d.Prerequisites.Length - 1) / 2d) * 12), target.Top);
            Point[] points;
            if (empire)
            {
                // Every terminal prerequisite owns its column down to the merge.
                // No perimeter loops: the horizontal merge sits beneath every branch.
                var merge = target.Top - 18;
                points = [from, new(from.X, merge), new(to.X, merge), to];
            }
            else if (target.Top - source.Top > 120)
            {
                // A skipped level uses the gap beside the target's branch.
                var track = target.Right + 10 + index * 6;
                points = [from, new(from.X, from.Y + 18), new(track, from.Y + 18),
                    new(track, to.Y - 18), new(to.X, to.Y - 18), to];
            }
            else
            {
                var middle = (from.Y + to.Y) / 2 + index * 6;
                points = [from, new(from.X, middle), new(to.X, middle), to];
            }
            edges.Add(new(p, d.Kind, points, empire));
        }
        Nodes = nodes; Edges = edges;
        Size = new Size(nodes.Values.Max(r => r.Right) + 32, nodes.Values.Max(r => r.Bottom) + 24);
    }
}
