using Avalonia;
using SeWZC.WorldBox.UI.ViewModels.Details;

namespace SeWZC.WorldBox.UI.ViewModels;

/// <summary>操作窗口的内容、反馈与提交区域。</summary>
internal sealed class ModalViewModel(
    VisualNode? header,
    VisualNode content,
    ButtonNode? primary,
    TextNode feedback,
    Action close) : ViewModelBase
{
    private Thickness _padding = new(12);
    private bool _submitting;
    private double _width = 520, _height = double.NaN, _maxHeight = 700;

    public bool IsSubmitting
    {
        get => _submitting;
        internal set => Set(ref _submitting, value);
    }

    public double Width
    {
        get => _width;
        internal set => Set(ref _width, value);
    }

    public double Height
    {
        get => _height;
        internal set => Set(ref _height, value);
    }

    public double MaxHeight
    {
        get => _maxHeight;
        internal set => Set(ref _maxHeight, value);
    }

    public Thickness Padding
    {
        get => _padding;
        internal set => Set(ref _padding, value);
    }

    public VisualNode? Header { get; } = header;
    public VisualNode Content { get; } = content;
    public ButtonNode? Primary { get; } = primary;
    public TextNode Feedback { get; } = feedback;
    public bool HasPrimary => Primary is not null;
    public UiCommand CloseCommand { get; } = new(_ => close());
}
