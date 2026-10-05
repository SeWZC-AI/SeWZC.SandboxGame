using Avalonia;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI.Controls;

public sealed record ResearchTreeEdge(ResearchKind From, ResearchKind To, Point[] Points);

public sealed record ResearchTreeLane(string Name, double Left, double Width);

// Stable branch columns make each route readable. Rules remain the authority for edges.
public sealed class ResearchTreeLayout
{
    public const double NodeWidth = 140, NodeHeight = 70;

    private static readonly IReadOnlyDictionary<ResearchKind, (int Column, int Row)> Positions =
        new Dictionary<ResearchKind, (int, int)>
        {
            [ResearchKind.Agriculture] = (0, 0), [ResearchKind.Logistics] = (2, 0),
            [ResearchKind.Irrigation] = (0, 1), [ResearchKind.Medicine] = (1, 1), [ResearchKind.Forestry] = (2, 1),
            [ResearchKind.Education] = (3, 1),
            [ResearchKind.CivilEngineering] = (0, 2), [ResearchKind.Pharmacology] = (1, 2),
            [ResearchKind.Cartography] = (3, 2),
            [ResearchKind.FireEngineering] = (0, 3), [ResearchKind.Sanitation] = (1, 3),
            [ResearchKind.ScientificMethod] = (4, 1), [ResearchKind.Industry] = (6, 1),
            [ResearchKind.MechanicalEngineering] = (4, 2), [ResearchKind.EfficientSmelting] = (6, 2),
            [ResearchKind.Electrification] = (7, 2),
            [ResearchKind.Toolmaking] = (4, 3), [ResearchKind.EnergyRecycling] = (7, 3),
            [ResearchKind.SignalNetwork] = (8, 3), [ResearchKind.Automation] = (9, 3),
            [ResearchKind.RailTransport] = (10, 3),
            [ResearchKind.Aviation] = (8, 4), [ResearchKind.AdvancedComputing] = (9, 4),
            [ResearchKind.Observation] = (10, 4),
            [ResearchKind.Ballistics] = (11, 3), [ResearchKind.ProtectiveEquipment] = (12, 4),
            [ResearchKind.ArcaneArts] = (5, 0), [ResearchKind.ManaAttunement] = (4, 1),
            [ResearchKind.Crystalcraft] = (6, 1),
            [ResearchKind.Restoration] = (4, 2), [ResearchKind.ArcaneScholarship] = (6, 2),
            [ResearchKind.RunicEngineering] = (8, 2),
            [ResearchKind.Alchemy] = (6, 3), [ResearchKind.NatureBinding] = (4, 3), [ResearchKind.Leylines] = (8, 3),
            [ResearchKind.AetherMastery] = (9, 3),
            [ResearchKind.Elementalism] = (10, 2), [ResearchKind.Warding] = (11, 3),
            [ResearchKind.BattleMagic] = (10, 4), [ResearchKind.SpatialMagic] = (8, 4),
        };

    public ResearchTreeLayout(IEnumerable<ResearchDefinition> definitions)
    {
        var items = definitions.ToArray();
        var included = items.Select(d => d.Kind).ToHashSet();
        if (items.Length == 0) throw new ArgumentException("Choose at least one research branch.", nameof(definitions));
        var columns = items.Select(d => Positions[d.Kind].Column).Distinct().Order().ToArray();
        var rows = items.Select(d => Positions[d.Kind].Row).Distinct().Order().ToArray();
        var nodes = items.ToDictionary(d => d.Kind, d =>
        {
            var (column, row) = Positions[d.Kind];
            return new Rect(32 + Array.IndexOf(columns, column) * 168, 52 + Array.IndexOf(rows, row) * 120, NodeWidth,
                NodeHeight);
        });

        string Lane(ResearchDefinition d)
        {
            return d.Shared ? "共同基础" : d.Magic ? d.Branch : d.Branch == "知识与通信" ? "运输与计算" : d.Branch;
        }

        Lanes = items.GroupBy(Lane).Select(g => new ResearchTreeLane(g.Key,
                g.Min(d => nodes[d.Kind].Left) - 14,
                g.Max(d => nodes[d.Kind].Right) - g.Min(d => nodes[d.Kind].Left) + 28))
            .ToArray();
        var edges = new List<ResearchTreeEdge>();
        foreach (var d in items)
            for (var index = 0; index < d.Prerequisites.Length; index++)
            {
                var p = d.Prerequisites[index];
                if (!included.Contains(p)) continue;
                var source = nodes[p];
                var target = nodes[d.Kind];
                var from = new Point(source.Center.X, source.Bottom);
                var to = new Point(target.Center.X + (index - (d.Prerequisites.Length - 1) / 2d) * 12, target.Top);
                Point[] points;
                if (target.Top - source.Top > 120)
                {
                    // A skipped level uses the gap beside the target's branch.
                    var track = target.Right + 10 + index * 6;
                    points =
                    [
                        from, new Point(from.X, from.Y + 18), new Point(track, from.Y + 18),
                        new Point(track, to.Y - 18), new Point(to.X, to.Y - 18), to,
                    ];
                }
                else
                {
                    var middle = (from.Y + to.Y) / 2 + index * 6;
                    points = [from, new Point(from.X, middle), new Point(to.X, middle), to];
                }

                edges.Add(new ResearchTreeEdge(p, d.Kind, points));
            }

        Nodes = nodes;
        Edges = edges;
        Size = new Size(nodes.Values.Max(r => r.Right) + 32, nodes.Values.Max(r => r.Bottom) + 24);
    }

    public IReadOnlyDictionary<ResearchKind, Rect> Nodes { get; }
    public IReadOnlyList<ResearchTreeEdge> Edges { get; }
    public IReadOnlyList<ResearchTreeLane> Lanes { get; }
    public Size Size { get; }
}
