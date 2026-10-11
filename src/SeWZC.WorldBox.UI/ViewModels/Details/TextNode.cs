using Avalonia.Media;

namespace SeWZC.WorldBox.UI.ViewModels.Details;

/// <summary>详情和表单的Text展示状态。</summary>
internal sealed class TextNode : VisualNode
{
    private double _lineHeight = double.NaN;
    private string? _text = "";
    private TextWrapping _textWrapping = TextWrapping.NoWrap;

    public string? Text
    {
        get => _text;
        set => Set(ref _text, value);
    }

    public TextWrapping TextWrapping
    {
        get => _textWrapping;
        set => Set(ref _textWrapping, value);
    }

    public double LineHeight
    {
        get => _lineHeight;
        set => Set(ref _lineHeight, value);
    }
}
