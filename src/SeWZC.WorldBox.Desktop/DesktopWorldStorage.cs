using System.IO.Compression;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using SeWZC.WorldBox.UI.Platform;

namespace SeWZC.WorldBox.Desktop;

/// <summary>保存压缩的本地自动存档，并通过系统文件选择器导入或导出 UTF-8 世界文件。</summary>
internal sealed class DesktopWorldStorage(string? savePath = null) : IWorldStorage
{
    private const int MaxFileBytes = 64 * 1024 * 1024;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    private static readonly FilePickerFileType WorldFileType = new("WorldBox 世界存档")
    {
        Patterns = ["*.json", "*.worldbox"],
        MimeTypes = ["application/json"],
    };

    private readonly SemaphoreSlim _saveLock = new(1, 1);

    private readonly string _savePath = savePath ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SeWZC", "WorldBox", "autosave.worldbox");

    private static Window? MainWindow =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    public bool IsBackground => MainWindow is { IsActive: false };

    public Task SaveAsync(string json)
    {
        return SaveChunksAsync([json]);
    }

    public async Task SaveChunksAsync(string[] chunks)
    {
        await _saveLock.WaitAsync();
        string? temporaryPath = null;
        try
        {
            temporaryPath = _savePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            // Encoding, compression and file replacement must not block Avalonia.
            await Task.Run(() =>
            {
                long bytes = 0;
                foreach (var chunk in chunks)
                {
                    bytes += Utf8.GetByteCount(chunk);
                    if (bytes > MaxFileBytes) throw new IOException("存档不能超过 64 MiB。");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(_savePath)!);
                using (var file = File.Create(temporaryPath))
                using (var compressed = new GZipStream(file, CompressionLevel.Fastest))
                using (var writer = new StreamWriter(compressed, Utf8))
                {
                    foreach (var chunk in chunks) writer.Write(chunk);
                }

                // Same-directory rename replaces only a complete snapshot.
                File.Move(temporaryPath, _savePath, true);
            });
        }
        finally
        {
            try
            {
                if (temporaryPath is not null && File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
            finally
            {
                _saveLock.Release();
            }
        }
    }

    public async Task<string?> LoadAsync()
    {
        await _saveLock.WaitAsync();
        try
        {
            if (!File.Exists(_savePath)) return null;
            await using var stream = File.OpenRead(_savePath);
            await using var compressed = new GZipStream(stream, CompressionMode.Decompress);
            return await ReadUtf8Async(compressed);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    public async Task ExportAsync(string json, string fileName)
    {
        CheckSize(json);
        var provider = GetStorageProvider();
        if (!provider.CanSave) throw new IOException("此平台暂不支持文件导出。");
        var file = await provider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出 WorldBox 世界",
            SuggestedFileName = fileName,
            DefaultExtension = "json",
            FileTypeChoices = [WorldFileType],
            ShowOverwritePrompt = true,
        });
        if (file is null) throw new OperationCanceledException("已取消导出。");
        using (file)
        await using (var stream = await file.OpenWriteAsync())
        {
            if (stream.CanSeek) stream.SetLength(0);
            await using var writer = new StreamWriter(stream, Utf8);
            await writer.WriteAsync(json);
        }
    }

    public async Task<string?> ImportAsync()
    {
        var provider = GetStorageProvider();
        if (!provider.CanOpen) throw new IOException("此平台暂不支持文件导入。");
        var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "导入 WorldBox 世界",
            AllowMultiple = false,
            FileTypeFilter = [WorldFileType],
        });
        if (files.Count == 0) return null;
        using var file = files[0];
        await using var stream = await file.OpenReadAsync();
        return await ReadUtf8Async(stream);
    }

    private static IStorageProvider GetStorageProvider()
    {
        return MainWindow?.StorageProvider
               ?? throw new InvalidOperationException("主窗口尚未准备好。");
    }

    private static void CheckSize(string json)
    {
        if (Utf8.GetByteCount(json) > MaxFileBytes) throw new IOException("存档不能超过 64 MiB。");
    }

    private static async Task<string> ReadUtf8Async(Stream stream)
    {
        if (stream.CanSeek && stream.Length > MaxFileBytes) throw new IOException("存档不能超过 64 MiB。");
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int count;
        while ((count = await stream.ReadAsync(chunk)) != 0)
        {
            if (buffer.Length + count > MaxFileBytes) throw new IOException("存档不能超过 64 MiB。");
            buffer.Write(chunk, 0, count);
        }

        var text = Utf8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
        return text.StartsWith('\uFEFF') ? text[1..] : text;
    }
}
