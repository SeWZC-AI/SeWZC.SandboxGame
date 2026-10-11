
namespace SeWZC.WorldBox.UI.ViewModels.Details;

/// <summary>开关选项的内容与选择。</summary>
internal sealed class CheckNode : VisualNode
{
    private object? _content;
    public object? Content { get => _content; set { Adopt(value); Set(ref _content, value); } }
    private bool? _isChecked;
    public bool? IsChecked { get => _isChecked; set { if (Set(ref _isChecked, value)) IsCheckedChanged?.Invoke(this, EventArgs.Empty); } }
    public event EventHandler? IsCheckedChanged;
}
