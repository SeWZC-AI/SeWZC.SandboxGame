namespace SeWZC.WorldBox.UI.ViewModels.Details;

/// <summary>详情卡片的内容与外观。</summary>
internal sealed class BorderNode : VisualNode
{
    private object? _child;

    public object? Child
    {
        get => _child;
        set
        {
            Adopt(value);
            Set(ref _child, value);
        }
    }
}
