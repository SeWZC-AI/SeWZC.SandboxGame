using Avalonia;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI.Controls;

public sealed record ResearchTreeEdge(ResearchKind From, ResearchKind To, Point[] Points);

// Presentation only: every connector comes from the authoritative research catalogue.
public sealed class ResearchTreeLayout
{
    public const double NodeWidth = 140, NodeHeight = 70;
    public IReadOnlyDictionary<ResearchKind, Rect> Nodes { get; }
    public IReadOnlyList<ResearchTreeEdge> Edges { get; }
    public Size Size { get; }

    public ResearchTreeLayout(IEnumerable<ResearchDefinition> definitions)
    {
        var items = definitions.ToArray();
        var included = items.Select(d => d.Kind).ToHashSet();
        var depths = new Dictionary<ResearchKind, int>();
        foreach (var d in ResearchRules.All)
            depths[d.Kind] = d.Prerequisites.Length == 0 ? 0 : d.Prerequisites.Max(p => depths[p]) + 1;
        var rows = items.GroupBy(d => depths[d.Kind]).OrderBy(g => g.Key).ToArray();
        var longEdges = items.Sum(d => d.Prerequisites.Count(p => included.Contains(p) && depths[d.Kind] - depths[p] > 1));
        var margin = 36 + longEdges * 9;
        var width = rows.Max(g => g.Count()) * 168 - 28 + margin * 2;
        var nodes = new Dictionary<ResearchKind, Rect>();
        foreach (var row in rows)
        {
            var ordered = row.ToArray();
            var left = (width - (ordered.Length * 168 - 28)) / 2;
            for (var i = 0; i < ordered.Length; i++)
                nodes[ordered[i].Kind] = new Rect(left + i * 168, 30 + row.Key * 140, NodeWidth, NodeHeight);
        }
        var edges = new List<ResearchTreeEdge>();
        var leftTrack = 0; var rightTrack = 0;
        foreach (var d in items)
        foreach (var p in d.Prerequisites.Where(included.Contains))
        {
            var source = nodes[p]; var target = nodes[d.Kind];
            var from = new Point(source.Center.X, source.Bottom);
            var to = new Point(target.Center.X, target.Top);
            Point[] points;
            if (depths[d.Kind] - depths[p] == 1)
            {
                var middle = (from.Y + to.Y) / 2;
                points = [from, new(from.X, middle), new(to.X, middle), to];
            }
            else
            {
                // Long dependencies travel outside the node columns, never through another node.
                var useLeft = from.X + to.X < width;
                var track = useLeft ? ++leftTrack : ++rightTrack;
                var x = useLeft ? margin - 18 - track * 9 : width - margin + 18 + track * 9;
                var departure = from.Y + 14 + track % 5 * 7;
                var arrival = to.Y - 14 - track % 5 * 7;
                points = [from, new(from.X, departure), new(x, departure), new(x, arrival), new(to.X, arrival), to];
            }
            edges.Add(new(p, d.Kind, points));
        }
        Nodes = nodes; Edges = edges;
        Size = new Size(width, nodes.Values.Max(r => r.Bottom) + 30);
    }
}
