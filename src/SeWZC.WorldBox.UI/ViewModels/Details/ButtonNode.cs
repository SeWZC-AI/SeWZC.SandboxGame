using Avalonia;
using Avalonia.Media;

namespace SeWZC.WorldBox.UI.ViewModels.Details;

/// <summary>操作按钮的内容与命令。</summary>
internal sealed class ButtonNode : VisualNode
{
    private object? _content;
    public object? Content { get => _content; set { Adopt(value); Set(ref _content, value); } }
    public event EventHandler? Click;
    public UiCommand Command { get; }
    public ButtonNode() : this(null)
    {
    }
    internal ButtonNode(UiCommand? command)
    {
        CornerRadius = new CornerRadius(4);
        Background = Brush.Parse("#213240");
        Foreground = Brush.Parse("#DDE8E1");
        Command = command ?? new UiCommand(_ => Click?.Invoke(this, EventArgs.Empty));
    }
}
