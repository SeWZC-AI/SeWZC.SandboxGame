namespace SeWZC.WorldBox.UI.Platform;

public interface IWorldStorage
{
    bool IsBackground { get; }
    Task SaveAsync(string json);
    Task<string?> LoadAsync();
    Task ExportAsync(string json, string fileName);
    Task<string?> ImportAsync();
}
