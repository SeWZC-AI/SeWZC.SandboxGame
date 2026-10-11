using Avalonia.Controls;
using Avalonia.Markup.Xaml;
namespace SeWZC.WorldBox.UI.Views;
/// <summary>操作窗口的遮罩与内容。</summary>
public sealed partial class ModalHost : UserControl
{
    /// <summary>加载区域布局与绑定。</summary>
    public ModalHost() => AvaloniaXamlLoader.Load(this);
}
