using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using SeWZC.WorldBox.UI.Platform;

namespace SeWZC.WorldBox.Browser;

[SupportedOSPlatform("browser")]
internal sealed partial class BrowserWorldStorage : IWorldStorage
{
    public bool IsBackground => GetIsBackground();

    public Task SaveAsync(string json) => Save(json);
    public Task<string?> LoadAsync() => Load();
    public Task ExportAsync(string json, string fileName) => ExportFile(json, fileName);
    public Task<string?> ImportAsync() => ImportFile();

    [JSImport("isBackground", "worldbox")]
    private static partial bool GetIsBackground();

    [JSImport("save", "worldbox")]
    [return: JSMarshalAs<JSType.Promise<JSType.Void>>]
    private static partial Task Save(string json);

    [JSImport("load", "worldbox")]
    [return: JSMarshalAs<JSType.Promise<JSType.String>>]
    private static partial Task<string?> Load();

    [JSImport("exportFile", "worldbox")]
    [return: JSMarshalAs<JSType.Promise<JSType.Void>>]
    private static partial Task ExportFile(string json, string fileName);

    [JSImport("importFile", "worldbox")]
    [return: JSMarshalAs<JSType.Promise<JSType.String>>]
    private static partial Task<string?> ImportFile();
}
