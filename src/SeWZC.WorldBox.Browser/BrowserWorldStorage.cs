using System.Runtime.InteropServices.JavaScript;
using System.Diagnostics;
using System.Runtime.Versioning;
using SeWZC.WorldBox.UI.Platform;

namespace SeWZC.WorldBox.Browser;

[SupportedOSPlatform("browser")]
internal sealed partial class BrowserWorldStorage : IWorldStorage
{
    public bool IsBackground => GetIsBackground();

    public Task SaveAsync(string json) => Save(json);
    public async Task SaveChunksAsync(string[] chunks)
    {
        var id = BeginSave();
        try
        {
            var started = Stopwatch.GetTimestamp();
            // Bound both WASM string marshaling and worker structured cloning.
            // The captured chunks are immutable; simulation may run during transfer.
            for (var offset = 0; offset < chunks.Length; offset++)
            {
                AppendSave(id, chunks[offset]);
                if (offset + 1 < chunks.Length && Stopwatch.GetElapsedTime(started).TotalMilliseconds >= 4)
                {
                    await YieldSave();
                    started = Stopwatch.GetTimestamp();
                }
            }
            await CommitSave(id);
        }
        finally { DiscardSave(id); }
    }
    public Task<string?> LoadAsync() => Load();
    public Task ExportAsync(string json, string fileName) => ExportFile(json, fileName);
    public Task<string?> ImportAsync() => ImportFile();

    [JSImport("isBackground", "worldbox")]
    private static partial bool GetIsBackground();

    [JSImport("save", "worldbox")]
    [return: JSMarshalAs<JSType.Promise<JSType.Void>>]
    private static partial Task Save(string json);

    [JSImport("beginSave", "worldbox")]
    private static partial int BeginSave();

    [JSImport("yieldSave", "worldbox")]
    [return: JSMarshalAs<JSType.Promise<JSType.Void>>]
    private static partial Task YieldSave();

    [JSImport("appendSave", "worldbox")]
    private static partial void AppendSave(int id, string chunk);

    [JSImport("commitSave", "worldbox")]
    [return: JSMarshalAs<JSType.Promise<JSType.Void>>]
    private static partial Task CommitSave(int id);

    [JSImport("discardSave", "worldbox")]
    private static partial void DiscardSave(int id);

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
