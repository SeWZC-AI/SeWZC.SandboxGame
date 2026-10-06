using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using SeWZC.WorldBox.UI.Platform;

namespace SeWZC.WorldBox.UI;

/// <summary>加载共享应用样式，并为桌面或浏览器创建游戏主界面。</summary>
public class App : Application
{
    /// <summary>平台入口提供的世界存储适配器。</summary>
    public static IWorldStorage? Storage { get; set; }

    /// <summary>加载共享应用的 XAML 样式和资源。</summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>按平台生命周期创建桌面窗口或浏览器单视图。</summary>
    public override void OnFrameworkInitializationCompleted()
    {
        var view = new MainView();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Window
            {
                Title = "SeWZC.WorldBox：众生与山海", Width = 1440, Height = 920,
                MinWidth = 390, MinHeight = 640, Content = view,
                Background = Brush.Parse("#101C27"),
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime single)
            single.MainView = view;

        base.OnFrameworkInitializationCompleted();
    }
}
