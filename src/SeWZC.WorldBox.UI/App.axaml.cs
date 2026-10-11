using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SeWZC.WorldBox.UI.Platform;

namespace SeWZC.WorldBox.UI;

/// <summary>桌面与浏览器共用的 Avalonia 应用入口。</summary>
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
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new Views.GameWindow();
        else if (ApplicationLifetime is ISingleViewApplicationLifetime single)
            single.MainView = new MainView();

        base.OnFrameworkInitializationCompleted();
    }
}
