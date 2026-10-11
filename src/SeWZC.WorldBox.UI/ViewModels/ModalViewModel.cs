using SeWZC.WorldBox.UI.ViewModels.Details;
namespace SeWZC.WorldBox.UI.ViewModels;
/// <summary>操作窗口的内容、反馈与提交区域。</summary>
internal sealed class ModalViewModel(VisualNode? header, VisualNode content, ButtonNode? primary, TextNode feedback, Action close)
{
    public VisualNode? Header { get; } = header;
    public VisualNode Content { get; } = content;
    public ButtonNode? Primary { get; } = primary;
    public TextNode Feedback { get; } = feedback;
    public bool HasPrimary => Primary is not null;
    public UiCommand CloseCommand { get; } = new(_ => close());
}
