using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SeWZC.WorldBox.UI.ViewModels;

namespace SeWZC.WorldBox.UI.Controls;

/// <summary>研究树的 AXAML 视图及指针拖动适配。</summary>
public sealed partial class ResearchGraphControl : UserControl
{
    private readonly ResearchConnectionsControl _connections;
    private readonly ScrollViewer _scroll;
    private bool _dragging;
    private ResearchGraphViewModel? _model;
    private Point? _press;
    private Vector _pressOffset;

    /// <summary>加载研究树视图并接入拖动与滚动事件。</summary>
    public ResearchGraphControl()
    {
        AvaloniaXamlLoader.Load(this);
        _scroll = this.FindControl<ScrollViewer>("GraphScroll")!;
        _connections = this.FindControl<ResearchConnectionsControl>("Connections")!;
        DataContextChanged += (_, _) => ConnectModel();
        AttachedToVisualTree += (_, _) => ConnectModel();
        DetachedFromVisualTree += (_, _) => DisconnectModel();
        _scroll.PropertyChanged += (_, args) =>
        {
            if (_model is not null && args.Property == ScrollViewer.ViewportProperty)
                _model.Viewport = _scroll.Viewport;
        };
        AddHandler(PointerPressedEvent, BeginDrag, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, MoveDrag, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, EndDrag, RoutingStrategies.Tunnel);
        AddHandler(PointerCaptureLostEvent, (_, e) =>
        {
            if (e.Source != this)
                return;
            _press = null;
            _dragging = false;
        });
    }

    private void ConnectModel()
    {
        DisconnectModel();
        _model = DataContext as ResearchGraphViewModel;
        if (_model is null)
            return;
        _model.Viewport = _scroll.Viewport;
        _model.ConnectionsChanged += _connections.InvalidateVisual;
        _model.ViewportRestoreRequested += RestoreOffset;
    }

    private void DisconnectModel()
    {
        if (_model is null)
            return;
        _model.ConnectionsChanged -= _connections.InvalidateVisual;
        _model.ViewportRestoreRequested -= RestoreOffset;
        _model = null;
    }

    private void RestoreOffset(Vector offset, Func<bool>? current)
    {
        var model = _model;
        Dispatcher.UIThread.Post(() =>
        {
            if (ReferenceEquals(_model, model) && current?.Invoke() != false && TopLevel.GetTopLevel(this) is not null)
                _scroll.Offset = offset;
        }, DispatcherPriority.Loaded);
    }

    private void BeginDrag(object? sender, PointerPressedEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Touch && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        if (e.Source is Control c && (c is ScrollBar || c.GetVisualAncestors().Any(a => a is ScrollBar)))
            return;
        _press = e.GetPosition(this);
        _pressOffset = _scroll.Offset;
        _dragging = false;
    }

    private void MoveDrag(object? sender, PointerEventArgs e)
    {
        if (_press is not { } start)
            return;
        var current = e.GetPosition(this);
        var delta = new Vector(current.X - start.X, current.Y - start.Y);
        if (!_dragging && delta.Length < 7)
            return;
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
}
