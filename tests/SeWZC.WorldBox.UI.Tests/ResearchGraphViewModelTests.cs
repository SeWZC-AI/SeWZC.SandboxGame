using Avalonia;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.ViewModels;

namespace SeWZC.WorldBox.UI.Tests;

/// <summary>研究图的缩放与导航恢复无需实例化视图。</summary>
public sealed class ResearchGraphViewModelTests
{
    /// <summary>缩放保留观察中心对应的研究位置。</summary>
    [Fact(DisplayName = "缩放保留观察中心对应的研究位置")]
    public void Zoom_keeps_same_logical_center()
    {
        var graph = new ResearchGraphViewModel { Viewport = new Size(300, 200), Offset = new Vector(200, 150) };
        var center = graph.Offset + new Vector(150, 100);
        graph.SetZoom(.5);
        Assert.Equal(center, (graph.Offset + new Vector(150, 100)) / graph.Zoom);
    }

    /// <summary>路线跳转后恢复缩放、偏移和完整前置路径设置。</summary>
    [Fact(DisplayName = "路线跳转后恢复缩放、偏移和完整前置路径设置")]
    public void Restoring_viewport_requests_a_layout_correction()
    {
        var graph = new ResearchGraphViewModel { Viewport = new Size(300, 200) };
        graph.SetZoom(.5);
        graph.Offset = new Vector(80, 40);
        graph.ShowFullPath = true;
        var bookmark = graph.CaptureViewport();
        graph.ShowRoute(ResearchRules.Route(true));
        Assert.Equal(default, graph.Offset);
        Vector? correction = null;
        var current = () => true;
        graph.ViewportRestoreRequested += (offset, check) =>
        {
            correction = offset;
            Assert.Same(current, check);
        };

        graph.RestoreViewport(bookmark, current);

        Assert.Equal(bookmark, graph.CaptureViewport());
        Assert.Equal(bookmark.Offset, correction);
    }
}
