using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SeWZC.WorldBox.UI.Views;

/// <summary>地图缩放与全图操作。</summary>
public sealed partial class CameraControls : UserControl
{
    /// <summary>加载区域布局与绑定。</summary>
    public CameraControls()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
