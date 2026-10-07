namespace SeWZC.WorldBox.UI.Platform;

/// <summary>世界存档的跨平台存储接口。</summary>
public interface IWorldStorage
{
    /// <summary>平台是否隐藏或失去活动状态，此时应临时停止模拟。</summary>
    bool IsBackground { get; }

    /// <summary>用按顺序排列的 JSON 文本块替换本地自动存档。</summary>
    /// <param name="chunks">按原始顺序排列的世界 JSON 文本块。</param>
    Task SaveChunksAsync(string[] chunks);

    /// <summary>读取本地自动存档；不存在时返回 <c>null</c>。</summary>
    Task<string?> LoadAsync();

    /// <summary>将传入的 JSON 以建议文件名提供给用户保存。</summary>
    /// <param name="json">要导出的世界 JSON。</param>
    /// <param name="fileName">提供给用户保存时建议使用的文件名。</param>
    Task ExportAsync(string json, string fileName);

    /// <summary>将用户选择的文件读取为 JSON 文本；取消选择时返回 <c>null</c>。</summary>
    Task<string?> ImportAsync();
}
