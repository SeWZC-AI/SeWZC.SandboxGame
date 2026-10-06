using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using SeWZC.WorldBox.UI;

namespace SeWZC.WorldBox.Browser;

/// <summary>向浏览器自动化检查提供只读的界面状态入口。</summary>
[SupportedOSPlatform("browser")]
public static partial class BrowserTestBridge
{
    internal static bool Enabled { get; set; }

    /// <summary>在启用浏览器测试入口后读取主界面的只读 JSON 状态快照。</summary>
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
