using Avalonia.Media;

namespace SeWZC.WorldBox.UI.ViewModels.Details;

/// <summary>可编辑文本与输入状态。</summary>
internal sealed class TextInputNode : VisualNode
{
    private string? _text;
    public string? Text { get => _text; set { if (Set(ref _text, value)) TextChanged?.Invoke(this, EventArgs.Empty); } }
    public event EventHandler? TextChanged;
    private int _maxLength = 0;
    public int MaxLength { get => _maxLength; set => Set(ref _maxLength, value); }
    private string? _placeholderText = null;
    public string? PlaceholderText { get => _placeholderText; set => Set(ref _placeholderText, value); }
    private bool _acceptsReturn = false;
    public bool AcceptsReturn { get => _acceptsReturn; set => Set(ref _acceptsReturn, value); }
    private bool _isReadOnly = false;
    public bool IsReadOnly { get => _isReadOnly; set => Set(ref _isReadOnly, value); }
    private TextWrapping _textWrapping = TextWrapping.NoWrap;
    public TextWrapping TextWrapping { get => _textWrapping; set => Set(ref _textWrapping, value); }
}
