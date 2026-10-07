using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;

namespace SeWZC.WorldBox.UI.Tests;

/// <summary>研究布局的输入范围和前置连接检查。</summary>
public sealed class ResearchTreeLayoutTests
{
    /// <summary>空集合没有可布局的节点。</summary>
    [Fact]
    public void Constructor_rejects_an_empty_selection()
    {
        Assert.Throws<ArgumentException>(() => new ResearchTreeLayout([]));
    }

    /// <summary>输入中的实际前置关系产生一条连接。</summary>
    [Fact]
    public void Edges_connect_an_included_prerequisite_to_its_project()
    {
        var layout = new ResearchTreeLayout([Advancement.Agriculture, Advancement.Irrigation]);

        var edge = Assert.Single(layout.Edges);
        Assert.Same(Advancement.Agriculture, edge.From);
        Assert.Same(Advancement.Irrigation, edge.To);
    }

    /// <summary>集合外前置不会生成虚构节点或悬空连线。</summary>
    [Fact]
    public void Edges_exclude_prerequisites_outside_the_selection()
    {
        var layout = new ResearchTreeLayout([Advancement.Irrigation]);

        Assert.Single(layout.Nodes);
        Assert.Empty(layout.Edges);
    }

    /// <summary>缩小选择后布局压缩空列。</summary>
    [Fact]
    public void Single_node_selection_does_not_keep_hidden_columns()
    {
        var layout = new ResearchTreeLayout([Advancement.SpatialMagic]);

        var node = layout.Nodes[Advancement.SpatialMagic];
        Assert.Equal(32, node.Left);
        Assert.Equal(52, node.Top);
        Assert.True(layout.Size.Width > node.Right);
        Assert.True(layout.Size.Height > node.Bottom);
    }
}
