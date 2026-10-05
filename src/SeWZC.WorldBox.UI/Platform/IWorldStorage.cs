namespace SeWZC.WorldBox.UI.Platform;

/// <summary>序列化世界的平台存储及文件交换接口；世界数据由引擎校验。</summary>
public interface IWorldStorage
{
    /// <summary>平台是否隐藏或失去活动状态，此时应临时停止模拟。</summary>
    bool IsBackground { get; }
    /// <summary>用传入的世界 JSON 替换平台本地自动存档。</summary>
    Task SaveAsync(string json);
    /// <summary>用按顺序排列的 JSON 文本块替换本地自动存档；默认实现先拼接各块。</summary>
    Task SaveChunksAsync(string[] chunks) => SaveAsync(string.Concat(chunks));
    /// <summary>读取本地自动存档；不存在时返回 <c>null</c>。</summary>
    Task<string?> LoadAsync();
    /// <summary>将传入的 JSON 以建议文件名提供给用户保存。</summary>
    Task ExportAsync(string json, string fileName);
    /// <summary>将用户选择的文件读取为 JSON 文本；取消选择时返回 <c>null</c>。</summary>
    Task<string?> ImportAsync();
}
