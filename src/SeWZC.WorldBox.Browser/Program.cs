using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Browser;
using SeWZC.WorldBox.UI;

namespace SeWZC.WorldBox.Browser;

[SupportedOSPlatform("browser")]
internal static class Program
{
    private static Task Main(string[] args)
    {
        App.Storage = new BrowserWorldStorage();
        return BuildAvaloniaApp().StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>().WithInterFont();
    }
}
