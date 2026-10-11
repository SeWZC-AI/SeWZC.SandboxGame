using Avalonia.Controls;
using Avalonia.Markup.Xaml;
namespace SeWZC.WorldBox.UI.Views;
/// <summary>详情导航与滚动内容。</summary>
public sealed partial class InspectorPanel : UserControl
{
    /// <summary>加载区域布局与绑定。</summary>
    public InspectorPanel() => AvaloniaXamlLoader.Load(this);
}
