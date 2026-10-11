using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SeWZC.WorldBox.UI.Views;

/// <summary>地图工具分类、分页与参数。</summary>
public sealed partial class ToolPaletteView : UserControl
{
    /// <summary>加载区域布局与绑定。</summary>
    public ToolPaletteView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
