using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using SeWZC.WorldBox.UI.Platform;

namespace SeWZC.WorldBox.UI;

public class App : Application
{
    public static IWorldStorage? Storage { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

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
