using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SeWZC.WorldBox.UI.Views;

/// <summary>桌面平台的游戏窗口。</summary>
public sealed partial class GameWindow : Window
{
    /// <summary>加载桌面窗口及共享主视图。</summary>
    public GameWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
