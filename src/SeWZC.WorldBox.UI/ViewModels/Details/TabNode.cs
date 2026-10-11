namespace SeWZC.WorldBox.UI.ViewModels.Details;

/// <summary>编辑表单的一个分页。</summary>
internal sealed class TabNode : VisualNode
{
    private object? _content;
    private object? _header;

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
}
