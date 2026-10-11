using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SeWZC.WorldBox.UI.Views;

/// <summary>地图放置与表单选点提示。</summary>
public sealed partial class MapInteractionView : UserControl
{
    /// <summary>加载区域布局与绑定。</summary>
    public MapInteractionView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
