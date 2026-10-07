using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI.Controls;

/// <summary>可交互的研究树视图。</summary>
public sealed class ResearchGraphControl : UserControl
{
    private readonly Connections _connections;
    private readonly List<TextBlock> _laneLabels = [];
    private readonly Dictionary<Advancement, Button> _nodes;
    private readonly ScrollViewer _scroll;
    private readonly Canvas _surface = new();
    private bool _dragging;
    private Point? _press;
    private Vector _pressOffset;

    /// <summary>创建研究树视图，并将提供的节点按钮加入画布。</summary>
    /// <param name="nodes">以研究类别索引的节点按钮，供视图布局和显示。</param>
    public ResearchGraphControl(Dictionary<Advancement, Button> nodes)
    {
        _nodes = nodes;
        Layout = new ResearchTreeLayout(ResearchRules.Route(false));
        _connections = new Connections(this) { IsHitTestVisible = false };
        _surface.Children.Add(_connections);
        foreach (var node in nodes.Values) _surface.Children.Add(node);
        _scroll = new ScrollViewer
        {
            Content = _surface,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        Content = _scroll;
        Background = Brush.Parse("#0D1822");
        ClipToBounds = true;
        AddHandler(PointerPressedEvent, BeginDrag, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, MoveDrag, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, EndDrag, RoutingStrategies.Tunnel);
        AddHandler(PointerCaptureLostEvent, (_, e) =>
        {
            // 节点开始拖动会先释放按钮捕获；忽略冒泡的旧通知，避免取消研究树刚取得的捕获。
            if (e.Source != this) return;
            _press = null;
            _dragging = false;
        });
        ApplyGeometry();
    }

    /// <summary>当前路线的节点、分支和连接线布局。</summary>
    public ResearchTreeLayout Layout { get; private set; }

    /// <summary>当前缩放倍率。</summary>
    public double Zoom { get; private set; } = 1;

    /// <summary>当前视口的滚动偏移，以控件布局单位计。</summary>
    public Vector Offset => _scroll.Offset;

    /// <summary>当前选中并高亮前置路径的研究。</summary>
    public Advancement Selected { get; set; } = Advancement.Agriculture;

    /// <summary>是否高亮所有递归前置，关闭时只高亮直接前置。</summary>
    public bool ShowFullPath { get; set; }

    /// <summary>查询各研究是否已完成的回调，用于连接线着色。</summary>
    public Func<Advancement, bool> IsCompleted { get; set; } = _ => false;

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
        _scroll.Offset = viewport.Offset;
        // 缩放会在下一次布局才更新滚动范围，须在布局后再次恢复偏移。
        Dispatcher.UIThread.Post(() =>
        {
            if ((isCurrent?.Invoke() ?? true) && TopLevel.GetTopLevel(this) is not null)
                _scroll.Offset = viewport.Offset;
        }, DispatcherPriority.Loaded);
    }

    /// <summary>重新布局指定研究路线，并将滚动位置移回起点。</summary>
    /// <param name="definitions">本次显示路线的非空研究定义集合。</param>
    public void ShowRoute(IEnumerable<Advancement> definitions)
    {
        Layout = new ResearchTreeLayout(definitions);
        ApplyGeometry();
        _scroll.Offset = default;
    }

    /// <summary>限制缩放倍率并保持原视口中心对应的研究位置。</summary>
    /// <param name="zoom">期望缩放倍率，限制在 0.12 至 1.5。</param>
    public void SetZoom(double zoom)
    {
        var center = (_scroll.Offset + new Vector(_scroll.Viewport.Width / 2, _scroll.Viewport.Height / 2)) / Zoom;
        Zoom = Math.Clamp(zoom, .12, 1.5);
        ApplyGeometry();
        _scroll.Offset = center * Zoom - new Vector(_scroll.Viewport.Width / 2, _scroll.Viewport.Height / 2);
    }

    /// <summary>按视口尺寸缩放以容纳当前研究树，并移回画布起点。</summary>
    public void Fit()
    {
        if (_scroll.Viewport.Width <= 0 || _scroll.Viewport.Height <= 0) return;
        SetZoom(Math.Min(_scroll.Viewport.Width / Layout.Size.Width, _scroll.Viewport.Height / Layout.Size.Height));
        _scroll.Offset = default;
    }

    /// <summary>将当前路线中的指定研究节点移到视口中心。</summary>
    /// <param name="kind">希望居中显示的研究节点。</param>
    public void Focus(Advancement kind)
    {
        if (!Layout.Nodes.TryGetValue(kind, out var bounds)) return;
        _scroll.Offset = new Vector(bounds.Center.X * Zoom - _scroll.Viewport.Width / 2,
            bounds.Center.Y * Zoom - _scroll.Viewport.Height / 2);
    }

    /// <summary>请求重绘研究连接线，以反映选择和完成状态。</summary>
    public void RefreshConnections()
    {
        _connections.InvalidateVisual();
    }

    private void ApplyGeometry()
    {
        _surface.Width = _connections.Width = Layout.Size.Width * Zoom;
        _surface.Height = _connections.Height = Layout.Size.Height * Zoom;
        foreach (var label in _laneLabels) _surface.Children.Remove(label);
        _laneLabels.Clear();
        foreach (var lane in Layout.Lanes)
        {
            var label = new TextBlock { Text = lane.Name, FontSize = 14 * Zoom, Foreground = Brush.Parse("#8EB6C6") };
            Canvas.SetLeft(label, (lane.Left + 10) * Zoom);
            Canvas.SetTop(label, 12 * Zoom);
            _laneLabels.Add(label);
            _surface.Children.Add(label);
        }

        foreach (var (kind, node) in _nodes)
        {
            node.IsVisible = Layout.Nodes.TryGetValue(kind, out var rect);
            if (!node.IsVisible) continue;
            Canvas.SetLeft(node, rect.X * Zoom);
            Canvas.SetTop(node, rect.Y * Zoom);
            node.Width = rect.Width * Zoom;
            node.Height = rect.Height * Zoom;
            node.Padding = new Thickness(8 * Zoom, 5 * Zoom);
            if (node.Content is StackPanel content)
            {
                content.Spacing = 2 * Zoom;
                for (var i = 0; i < content.Children.Count; i++)
                    if (content.Children[i] is TextBlock text)
                        text.FontSize = (i == 0 ? 13 : 11) * Zoom;
            }
        }

        RefreshConnections();
    }

    private void BeginDrag(object? sender, PointerPressedEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Touch && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (e.Source is Control c && (c is ScrollBar || c.GetVisualAncestors().Any(a => a is ScrollBar))) return;
        _press = e.GetPosition(this);
        _pressOffset = _scroll.Offset;
        _dragging = false;
    }

    private void MoveDrag(object? sender, PointerEventArgs e)
    {
        if (_press is not { } start) return;
        var current = e.GetPosition(this);
        var delta = new Vector(current.X - start.X, current.Y - start.Y);
        if (!_dragging && delta.Length < 7) return;
        // 已处理事件仍会进入手势识别；研究树取得拖动后须阻止嵌套滚动视图再次捕获。
        e.PreventGestureRecognition();
        _dragging = true;
        e.Pointer.Capture(this);
        _scroll.Offset = _pressOffset - delta;
        e.Handled = true;
    }

    private void EndDrag(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragging)
        {
            e.Handled = true;
            e.Pointer.Capture(null);
        }

        _press = null;
        _dragging = false;
    }

    /// <summary>研究树视口的显示状态。</summary>
    /// <param name="Zoom">当前缩放倍率。</param>
    /// <param name="Offset">视口滚动偏移，以控件布局单位计。</param>
    /// <param name="ShowFullPath">是否显示选中研究的全部递归前置路径。</param>
    public readonly record struct ViewportState(double Zoom, Vector Offset, bool ShowFullPath);

    private sealed class Connections(ResearchGraphControl owner) : Control
    {
        private static readonly IBrush Locked = Brush.Parse("#496075"),
            Done = Brush.Parse("#65B995"),
            Path = Brush.Parse("#F1CD83"),
            Magic = Brush.Parse("#A996D8");

        public override void Render(DrawingContext context)
        {
            var ancestors = new HashSet<Advancement>();

            void Visit(Advancement kind)
            {
                if (!ancestors.Add(kind)) return;
                foreach (var p in kind.Prerequisites) Visit(p);
            }

            if (owner.ShowFullPath) Visit(owner.Selected);
            else
            {
                ancestors.Add(owner.Selected);
                foreach (var p in owner.Selected.Prerequisites) ancestors.Add(p);
            }

            using var scale = context.PushTransform(Matrix.CreateScale(owner.Zoom, owner.Zoom));
            var laneBottom = owner.Layout.Nodes.Max(n => n.Value.Bottom) + 16;
            foreach (var lane in owner.Layout.Lanes)
                context.DrawRectangle(Brush.Parse("#10212D"), null,
                    new Rect(lane.Left, 38, lane.Width, laneBottom - 38), 8, 8);
            // 选中前置路径最后绘制，使交叉处仍能清晰追踪。
            foreach (var edge in
                     owner.Layout.Edges.OrderBy(e => ancestors.Contains(e.To) && ancestors.Contains(e.From)))
            {
                var selected = owner.ShowFullPath
                    ? ancestors.Contains(edge.To) && ancestors.Contains(edge.From)
                    : edge.To == owner.Selected;
                var brush = selected ? Path
                    : owner.IsCompleted(edge.From) && owner.IsCompleted(edge.To) ? Done
                    : edge.To.Magic ? Magic : Locked;
                var pen = new Pen(brush, selected ? 2.4 : 1.6);
                var geometry = new StreamGeometry();
                using (var path = geometry.Open())
                {
                    path.BeginFigure(edge.Points[0], false);
                    if (edge.Points.Count == 4)
                        path.CubicBezierTo(edge.Points[1], edge.Points[2], edge.Points[3]);
                    else
                    {
                        foreach (var point in edge.Points.Skip(1))
                            path.LineTo(point);
                    }

                    path.EndFigure(false);
                }

                context.DrawGeometry(null, pen, geometry);
                var tip = edge.Points[^1];
                context.DrawLine(pen, tip, tip + new Vector(-4, -6));
                context.DrawLine(pen, tip, tip + new Vector(4, -6));
            }
        }
    }
}
