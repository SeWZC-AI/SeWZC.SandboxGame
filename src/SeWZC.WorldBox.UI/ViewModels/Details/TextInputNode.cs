using Avalonia.Media;

namespace SeWZC.WorldBox.UI.ViewModels.Details;

/// <summary>可编辑文本与输入状态。</summary>
internal sealed class TextInputNode : VisualNode
{
    private bool _acceptsReturn;
    private bool _isReadOnly;
    private int _maxLength;
    private string? _placeholderText;
    private string? _text;
    private TextWrapping _textWrapping = TextWrapping.NoWrap;

    public string? Text
    {
        get => _text;
        set
        {
            if (Set(ref _text, value))
                TextChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public int MaxLength
    {
        get => _maxLength;
        set => Set(ref _maxLength, value);
    }

    public string? PlaceholderText
    {
        get => _placeholderText;
        set => Set(ref _placeholderText, value);
    }

    public bool AcceptsReturn
    {
        get => _acceptsReturn;
        set => Set(ref _acceptsReturn, value);
    }

    public bool IsReadOnly
    {
        get => _isReadOnly;
        set => Set(ref _isReadOnly, value);
    }

    public TextWrapping TextWrapping
    {
        get => _textWrapping;
        set => Set(ref _textWrapping, value);
    }

    public event EventHandler? TextChanged;
}
