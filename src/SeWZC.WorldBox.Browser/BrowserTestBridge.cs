using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using SeWZC.WorldBox.UI;

namespace SeWZC.WorldBox.Browser;

[SupportedOSPlatform("browser")]
public static partial class BrowserTestBridge
{
    internal static bool Enabled { get; set; }

    [JSExport]
    public static string ReadSnapshot()
    {
        if (!Enabled) throw new InvalidOperationException("UI test inspection is disabled.");
        var lifetime = Application.Current?.ApplicationLifetime as ISingleViewApplicationLifetime;
        if (lifetime?.MainView is not MainView view)
            throw new InvalidOperationException("The application view is not ready.");
        return view.GetAutomationSnapshotJson();
    }
}
