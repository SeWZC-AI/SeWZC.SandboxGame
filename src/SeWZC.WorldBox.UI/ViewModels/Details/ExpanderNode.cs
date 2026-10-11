namespace SeWZC.WorldBox.UI.ViewModels.Details;

/// <summary>可折叠详情的标题与展开状态。</summary>
internal sealed class ExpanderNode : VisualNode
{
    private object? _content;
    private object? _header;
    private bool _isExpanded;

    public object? Content
    {
        get => _content;
        set
        {
            Adopt(value);
            Set(ref _content, value);
        }
    }

    public object? Header
    {
        get => _header;
        set
        {
            Adopt(value);
            Set(ref _header, value);
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => Set(ref _isExpanded, value);
    }
}
