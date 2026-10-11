using Avalonia;
using Avalonia.Media;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;
using SeWZC.WorldBox.UI.ViewModels.Details;

namespace SeWZC.WorldBox.UI.ViewModels;

/// <summary>研究树的节点、缩放与视口展示状态。</summary>
internal sealed class ResearchGraphViewModel : VisualNode
{
    private readonly List<TextNode> _laneLabels = [];
    private Dictionary<Advancement, ButtonNode> _nodes = [];
    private Vector _offset;
    private double _zoom = 1;

    internal ResearchGraphViewModel()
    {
        Layout = new ResearchTreeLayout(ResearchRules.Route(false));
        ApplyGeometry();
    }

    public CanvasNode Surface { get; } = new();

    public Size Viewport { get; set; }

    /// <summary>当前路线的节点、分支和连接线布局。</summary>
    public ResearchTreeLayout Layout { get; private set; }

    /// <summary>当前缩放倍率。</summary>
    public double Zoom
    {
        get => _zoom;
        private set => Set(ref _zoom, value);
    }

    /// <summary>当前视口的滚动偏移，以控件布局单位计。</summary>
    public Vector Offset
    {
        get => _offset;
        set => Set(ref _offset, value);
    }

    /// <summary>当前选中并高亮前置路径的研究。</summary>
    public Advancement Selected { get; set; } = Advancement.Agriculture;

    /// <summary>是否高亮所有递归前置，关闭时只高亮直接前置。</summary>
    public bool ShowFullPath { get; set; }

    /// <summary>查询各研究是否已完成的回调，用于连接线着色。</summary>
    public Func<Advancement, bool> IsCompleted { get; set; } = _ => false;

    internal event Action? ConnectionsChanged;
    internal event Action<Vector, Func<bool>?>? ViewportRestoreRequested;

    internal void SetNodes(Dictionary<Advancement, ButtonNode> nodes)
    {
        _nodes = nodes;
        foreach (var node in nodes.Values)
            Surface.Children.Add(node);
        ApplyGeometry();
    }

    /// <summary>捕获当前缩放、滚动位置和前置路径显示设置。</summary>
    public ViewportState CaptureViewport()
    {
        return new ViewportState(Zoom, Offset, ShowFullPath);
    }

    /// <summary>恢复视口设置，并在布局完成后再次校正滚动位置。</summary>
    /// <param name="viewport">此前捕获的视口设置。</param>
    /// <param name="isCurrent">延迟校正前检查界面会话仍有效的回调，空值表示不额外检查。</param>
    public void RestoreViewport(ViewportState viewport, Func<bool>? isCurrent = null)
    {
        Zoom = Math.Clamp(viewport.Zoom, .12, 1.5);
        ShowFullPath = viewport.ShowFullPath;
        ApplyGeometry();
        Offset = viewport.Offset;
        ViewportRestoreRequested?.Invoke(viewport.Offset, isCurrent);
    }

    /// <summary>重新布局指定研究路线，并将滚动位置移回起点。</summary>
    /// <param name="definitions">本次显示路线的非空研究定义集合。</param>
    public void ShowRoute(IEnumerable<Advancement> definitions)
    {
        Layout = new ResearchTreeLayout(definitions);
        ApplyGeometry();
        Offset = default;
    }

    /// <summary>限制缩放倍率并保持原视口中心对应的研究位置。</summary>
    /// <param name="zoom">期望缩放倍率，限制在 0.12 至 1.5。</param>
    public void SetZoom(double zoom)
    {
        var center = (Offset + new Vector(Viewport.Width / 2, Viewport.Height / 2)) / Zoom;
        Zoom = Math.Clamp(zoom, .12, 1.5);
        ApplyGeometry();
        Offset = center * Zoom - new Vector(Viewport.Width / 2, Viewport.Height / 2);
    }

    /// <summary>按视口尺寸缩放以容纳当前研究树，并移回画布起点。</summary>
    public void Fit()
    {
        if (Viewport.Width <= 0 || Viewport.Height <= 0)
            return;
        SetZoom(Math.Min(Viewport.Width / Layout.Size.Width, Viewport.Height / Layout.Size.Height));
        Offset = default;
    }

    /// <summary>将当前路线中的指定研究节点移到视口中心。</summary>
    /// <param name="kind">希望居中显示的研究节点。</param>
    public void Focus(Advancement kind)
    {
        if (!Layout.Nodes.TryGetValue(kind, out var bounds))
            return;
        Offset = new Vector(bounds.Center.X * Zoom - Viewport.Width / 2,
            bounds.Center.Y * Zoom - Viewport.Height / 2);
    }

    /// <summary>请求重绘研究连接线，以反映选择和完成状态。</summary>
    public void RefreshConnections()
    {
        ConnectionsChanged?.Invoke();
    }

    private void ApplyGeometry()
    {
        Surface.Width = Layout.Size.Width * Zoom;
        Surface.Height = Layout.Size.Height * Zoom;
        foreach (var label in _laneLabels)
            Surface.Children.Remove(label);
        _laneLabels.Clear();
        foreach (var lane in Layout.Lanes)
        {
            var label = new TextNode { Text = lane.Name, FontSize = 14 * Zoom, Foreground = Brush.Parse("#8EB6C6") };
            label.Left = (lane.Left + 10) * Zoom;
            label.Top = 12 * Zoom;
            _laneLabels.Add(label);
            Surface.Children.Add(label);
        }

        foreach (var (kind, node) in _nodes)
        {
            node.IsVisible = Layout.Nodes.TryGetValue(kind, out var rect);
            if (!node.IsVisible)
                continue;
            node.Left = rect.X * Zoom;
            node.Top = rect.Y * Zoom;
            node.Width = rect.Width * Zoom;
            node.Height = rect.Height * Zoom;
            node.Padding = new Thickness(8 * Zoom, 5 * Zoom);
            if (node.Content is StackNode content)
            {
                content.Spacing = 2 * Zoom;
                for (var i = 0; i < content.Children.Count; i++)
                    if (content.Children[i] is TextNode text)
                        text.FontSize = (i == 0 ? 13 : 11) * Zoom;
            }
        }

        RefreshConnections();
    }

    /// <summary>研究树视口的显示状态。</summary>
    /// <param name="Zoom">当前缩放倍率。</param>
    /// <param name="Offset">视口滚动偏移，以控件布局单位计。</param>
    /// <param name="ShowFullPath">是否显示选中研究的全部递归前置路径。</param>
    public readonly record struct ViewportState(double Zoom, Vector Offset, bool ShowFullPath);
}
