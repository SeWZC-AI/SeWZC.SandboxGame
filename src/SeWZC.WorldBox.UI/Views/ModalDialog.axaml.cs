using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SeWZC.WorldBox.UI.Views;

/// <summary>XAML 定义的操作窗口布局。</summary>
public sealed partial class ModalDialog : Border
{
    /// <summary>加载固定标题、滚动内容和提交区域。</summary>
    public ModalDialog()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
