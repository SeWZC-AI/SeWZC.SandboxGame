using Avalonia;
using SeWZC.WorldBox.UI;

namespace SeWZC.WorldBox.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        App.Storage = new DesktopWorldStorage();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
