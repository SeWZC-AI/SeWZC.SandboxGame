using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI.Controls;

public sealed class ResearchGraphControl : UserControl
{
    private readonly Canvas _surface = new();
    private readonly ScrollViewer _scroll;
    private readonly Connections _connections;
    private readonly Dictionary<ResearchKind, Button> _nodes;
    private readonly List<TextBlock> _laneLabels = [];
    private Point? _press;
    private Vector _pressOffset;
    private bool _dragging;
    public ResearchTreeLayout Layout { get; private set; }
    public double Zoom { get; private set; } = 1;
    public Vector Offset => _scroll.Offset;
    public ResearchKind Selected { get; set; }
    public bool ShowFullPath { get; set; }
    public Func<ResearchKind, bool> IsCompleted { get; set; } = _ => false;

    public ResearchGraphControl(Dictionary<ResearchKind, Button> nodes)
    {
        _nodes = nodes;
        Layout = new(ResearchRules.All.Where(d => ResearchRules.Route(false).Contains(d.Kind)));
        _connections = new Connections(this) { IsHitTestVisible = false };
        _surface.Children.Add(_connections);
        foreach (var node in nodes.Values) _surface.Children.Add(node);
        _scroll = new ScrollViewer { Content = _surface, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Content = _scroll;
        Background = Brush.Parse("#0D1822");
        ClipToBounds = true;
        AddHandler(PointerPressedEvent, BeginDrag, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, MoveDrag, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, EndDrag, RoutingStrategies.Tunnel);
        AddHandler(PointerCaptureLostEvent, (_, e) =>
        {
            // Capturing a dragged node releases its button first. That bubbled event
            // must not cancel the graph's new capture and truncate the gesture.
            if (e.Source != this) return;
            _press = null; _dragging = false;
        });
        ApplyGeometry();
    }

    public void ShowRoute(IEnumerable<ResearchDefinition> definitions)
    {
        Layout = new(definitions);
        ApplyGeometry(); _scroll.Offset = default;
    }

    public void SetZoom(double zoom)
    {
        var center = (_scroll.Offset + new Vector(_scroll.Viewport.Width / 2, _scroll.Viewport.Height / 2)) / Zoom;
        Zoom = Math.Clamp(zoom, .2, 1.5);
        ApplyGeometry();
        _scroll.Offset = center * Zoom - new Vector(_scroll.Viewport.Width / 2, _scroll.Viewport.Height / 2);
    }

    public void Fit()
    {
        if (_scroll.Viewport.Width <= 0 || _scroll.Viewport.Height <= 0) return;
        SetZoom(Math.Min(_scroll.Viewport.Width / Layout.Size.Width, _scroll.Viewport.Height / Layout.Size.Height));
        _scroll.Offset = default;
    }

    public void Focus(ResearchKind kind)
    {
        if (!Layout.Nodes.TryGetValue(kind, out var bounds)) return;
        _scroll.Offset = new Vector(bounds.Center.X * Zoom - _scroll.Viewport.Width / 2,
            bounds.Center.Y * Zoom - _scroll.Viewport.Height / 2);
    }

    public void RefreshConnections() => _connections.InvalidateVisual();

    private void ApplyGeometry()
    {
        _surface.Width = _connections.Width = Layout.Size.Width * Zoom;
        _surface.Height = _connections.Height = Layout.Size.Height * Zoom;
        foreach (var label in _laneLabels) _surface.Children.Remove(label);
        _laneLabels.Clear();
        foreach (var lane in Layout.Lanes)
        {
            var label = new TextBlock { Text = lane.Name, FontSize = 14 * Zoom, Foreground = Brush.Parse("#8EB6C6") };
            Canvas.SetLeft(label, (lane.Left + 10) * Zoom); Canvas.SetTop(label, 12 * Zoom);
            _laneLabels.Add(label); _surface.Children.Add(label);
        }
        foreach (var (kind, node) in _nodes)
        {
            node.IsVisible = Layout.Nodes.TryGetValue(kind, out var rect);
            if (!node.IsVisible) continue;
            Canvas.SetLeft(node, rect.X * Zoom); Canvas.SetTop(node, rect.Y * Zoom);
            node.Width = rect.Width * Zoom; node.Height = rect.Height * Zoom;
            node.Padding = new Thickness(8 * Zoom, 5 * Zoom);
            if (node.Content is StackPanel content)
            {
                content.Spacing = 2 * Zoom;
                for (var i = 0; i < content.Children.Count; i++)
                    if (content.Children[i] is TextBlock text) text.FontSize = (i == 0 ? 13 : 11) * Zoom;
            }
        }
        RefreshConnections();
    }

    private void BeginDrag(object? sender, PointerPressedEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Touch && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (e.Source is Control c && (c is ScrollBar || c.GetVisualAncestors().Any(a => a is ScrollBar))) return;
        _press = e.GetPosition(this); _pressOffset = _scroll.Offset; _dragging = false;
    }

    private void MoveDrag(object? sender, PointerEventArgs e)
    {
        if (_press is not { } start) return;
        var current = e.GetPosition(this);
        var delta = new Vector(current.X - start.X, current.Y - start.Y);
        if (!_dragging && delta.Length < 7) return;
        _dragging = true; e.Pointer.Capture(this);
        _scroll.Offset = _pressOffset - delta;
        e.Handled = true;
    }

    private void EndDrag(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragging) { e.Handled = true; e.Pointer.Capture(null); }
        _press = null; _dragging = false;
    }

    private sealed class Connections(ResearchGraphControl owner) : Control
    {
        private static readonly IBrush Locked = Brush.Parse("#496075"), Done = Brush.Parse("#65B995"),
            Path = Brush.Parse("#F1CD83"), Magic = Brush.Parse("#A996D8");
        public override void Render(DrawingContext context)
        {
            var ancestors = new HashSet<ResearchKind>();
            void Visit(ResearchKind kind)
            {
                if (!ancestors.Add(kind)) return;
                foreach (var p in ResearchRules.For(kind).Prerequisites) Visit(p);
            }
            if (owner.ShowFullPath) Visit(owner.Selected);
            else
            {
                ancestors.Add(owner.Selected);
                foreach (var p in ResearchRules.For(owner.Selected).Prerequisites) ancestors.Add(p);
            }
            using var scale = context.PushTransform(Matrix.CreateScale(owner.Zoom, owner.Zoom));
            var laneBottom = owner.Layout.Nodes.Where(n => n.Key is not ResearchKind.TechnologicalEmpire and not ResearchKind.MagicalEmpire).Max(n => n.Value.Bottom) + 16;
            foreach (var lane in owner.Layout.Lanes)
                context.DrawRectangle(Brush.Parse("#10212D"), null, new Rect(lane.Left, 38, lane.Width, laneBottom - 38), 8, 8);
            // Draw the selected prerequisite path last so crossings remain easy to follow.
            foreach (var edge in owner.Layout.Edges.OrderBy(e => ancestors.Contains(e.To) && ancestors.Contains(e.From)))
            {
                var selected = owner.ShowFullPath ? ancestors.Contains(edge.To) && ancestors.Contains(edge.From) : edge.To == owner.Selected;
                var brush = selected ? Path : owner.IsCompleted(edge.From) && owner.IsCompleted(edge.To) ? Done
                    : ResearchRules.For(edge.To).Magic ? Magic : Locked;
                var pen = new Pen(brush, selected ? 2.4 : edge.EmpireMerge ? 1 : 1.6,
                    edge.EmpireMerge ? DashStyle.Dash : null);
                var geometry = new StreamGeometry();
                using (var path = geometry.Open())
                {
                    path.BeginFigure(edge.Points[0], false);
                    if (!edge.EmpireMerge && edge.Points.Length == 4)
                        path.CubicBezierTo(edge.Points[1], edge.Points[2], edge.Points[3]);
                    else foreach (var point in edge.Points.Skip(1)) path.LineTo(point);
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
