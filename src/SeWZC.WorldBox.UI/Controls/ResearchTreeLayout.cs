using System.Collections.ObjectModel;
using Avalonia;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI.Controls;

/// <summary>研究树在画布中的几何布局。</summary>
public sealed class ResearchTreeLayout
{
    /// <summary>未缩放的研究节点宽度和高度，以控件布局单位计。</summary>
    public const double NodeWidth = 140, NodeHeight = 70;

    private static readonly IReadOnlyDictionary<Advancement, (int Column, int Row)> Positions =
        new Dictionary<Advancement, (int, int)>
        {
            [Advancement.Agriculture] = (0, 0),
            [Advancement.Logistics] = (2, 0),
            [Advancement.Irrigation] = (0, 1),
            [Advancement.Medicine] = (1, 1),
            [Advancement.Forestry] = (2, 1),
            [Advancement.Education] = (3, 1),
            [Advancement.CivilEngineering] = (0, 2),
            [Advancement.Pharmacology] = (1, 2),
            [Advancement.Cartography] = (3, 2),
            [Advancement.FireEngineering] = (0, 3),
            [Advancement.Sanitation] = (1, 3),
            [Advancement.ScientificMethod] = (4, 1),
            [Advancement.Industry] = (6, 1),
            [Advancement.MechanicalEngineering] = (4, 2),
            [Advancement.EfficientSmelting] = (6, 2),
            [Advancement.Electrification] = (7, 2),
            [Advancement.Toolmaking] = (4, 3),
            [Advancement.EnergyRecycling] = (7, 3),
            [Advancement.SignalNetwork] = (8, 3),
            [Advancement.Automation] = (9, 3),
            [Advancement.RailTransport] = (10, 3),
            [Advancement.Aviation] = (8, 4),
            [Advancement.AdvancedComputing] = (9, 4),
            [Advancement.Observation] = (10, 4),
            [Advancement.Ballistics] = (11, 3),
            [Advancement.ProtectiveEquipment] = (12, 4),
            [Advancement.ArcaneArts] = (5, 0),
            [Advancement.ManaAttunement] = (4, 1),
            [Advancement.Crystalcraft] = (6, 1),
            [Advancement.Restoration] = (4, 2),
            [Advancement.ArcaneScholarship] = (6, 2),
            [Advancement.RunicEngineering] = (8, 2),
            [Advancement.Alchemy] = (6, 3),
            [Advancement.NatureBinding] = (4, 3),
            [Advancement.Leylines] = (8, 3),
            [Advancement.AetherMastery] = (9, 3),
            [Advancement.Elementalism] = (10, 2),
            [Advancement.Warding] = (11, 3),
            [Advancement.BattleMagic] = (10, 4),
            [Advancement.SpatialMagic] = (8, 4),
        };

    /// <summary>为非空的研究定义集合计算稳定分支布局及集合内的前置连线。</summary>
    /// <param name="definitions">需要布局的非空研究定义集合，前置连线仅连接集合内节点。</param>
    public ResearchTreeLayout(IEnumerable<Advancement> definitions)
    {
        var items = definitions.ToArray();
        var included = items.ToHashSet();
        if (items.Length == 0) throw new ArgumentException("Choose at least one research branch.", nameof(definitions));
        var columns = items.Select(research => Positions[research].Column).Distinct().Order().ToArray();
        var rows = items.Select(research => Positions[research].Row).Distinct().Order().ToArray();
        var nodes = items.ToDictionary(research => research, research =>
        {
            var (column, row) = Positions[research];
            return new Rect(32 + Array.IndexOf(columns, column) * 168, 52 + Array.IndexOf(rows, row) * 120, NodeWidth,
                NodeHeight);
        });

        Lanes = Array.AsReadOnly(items.GroupBy(research => research.Branch.Lane).Select(g => new ResearchTreeLane(g.Key,
                g.Min(research => nodes[research].Left) - 14,
                g.Max(research => nodes[research].Right) - g.Min(research => nodes[research].Left) + 28))
            .ToArray());
        var edges = new List<ResearchTreeEdge>();
        foreach (var research in items)
            for (var index = 0; index < research.Prerequisites.Length; index++)
            {
                var prerequisite = research.Prerequisites[index];
                if (!included.Contains(prerequisite)) continue;
                var source = nodes[prerequisite];
                var target = nodes[research];
                var from = new Point(source.Center.X, source.Bottom);
                var to = new Point(target.Center.X + (index - (research.Prerequisites.Length - 1) / 2d) * 12, target.Top);
                Point[] points;
                if (target.Top - source.Top > 120)
                {
                    // 跨层连线绕到目标分支旁的空隙，避免穿过中间节点。
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

                edges.Add(new ResearchTreeEdge(prerequisite, research, points));
            }

        Nodes = new ReadOnlyDictionary<Advancement, Rect>(nodes);
        Edges = edges.AsReadOnly();
        Size = new Size(nodes.Values.Max(r => r.Right) + 32, nodes.Values.Max(r => r.Bottom) + 24);
    }

    /// <summary>各研究节点在未缩放画布中的矩形。</summary>
    public IReadOnlyDictionary<Advancement, Rect> Nodes { get; }

    /// <summary>当前布局内研究节点之间的前置连接折线。</summary>
    public IReadOnlyList<ResearchTreeEdge> Edges { get; }

    /// <summary>当前布局的分支标题及横向背景范围。</summary>
    public IReadOnlyList<ResearchTreeLane> Lanes { get; }

    /// <summary>容纳节点和留白所需的未缩放画布尺寸。</summary>
    public Size Size { get; }
}
