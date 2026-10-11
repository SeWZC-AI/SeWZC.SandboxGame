using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Platform;
using SeWZC.WorldBox.UI.Services;

namespace SeWZC.WorldBox.UI.Tests;

/// <summary>存档服务的串行读写、失效窗口和保存修订行为。</summary>
public sealed class WorldSaveCoordinatorTests
{
    /// <summary>没有推进世界时间的编辑也必须重新保存。</summary>
    [Fact]
    public async Task Editing_same_tick_requires_a_new_save()
    {
        var engine = WorldEngine.Create(42, 32, 32, false);
        var storage = new MemoryStorage();
        var coordinator = new WorldSaveCoordinator(() => engine, () => storage, () => 50) { Ready = true };
        await coordinator.SaveAsync(false);
        await coordinator.SaveAsync(false);
        Assert.Equal(1, storage.Saves);

        coordinator.EditRevision++;
        engine.PaintTerrain(10, 10, TerrainType.Mountain, 0);
        await coordinator.SaveAsync(false);

        Assert.Equal(2, storage.Saves);
        Assert.Equal(engine.ExportJson(), WorldEngine.ImportJson(storage.Json!).ExportJson());
        Assert.False(coordinator.WorldNeedsSave());
        Assert.Null(coordinator.Capture);
    }

    /// <summary>导出排在保存之后，原窗口关闭时不得导出后来选择的世界。</summary>
    [Fact]
    public async Task Queued_export_is_discarded_when_original_window_closes()
    {
        var engine = WorldEngine.Create(42, 32, 32, false);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var storage = new MemoryStorage { SaveStarted = started, SaveGate = gate.Task };
        var coordinator = new WorldSaveCoordinator(() => engine, () => storage, () => 50) { Ready = true };
        var save = coordinator.SaveAsync(true);
        await started.Task;
        var open = true;
        var exported = false;
        var export = coordinator.ExportWorldAsync(() => open, () => exported = true);
        open = false;
        engine = WorldEngine.Create(43, 32, 32, false);
        gate.SetResult();
        await Task.WhenAll(save, export);

        Assert.Equal(0, storage.Exports);
        Assert.False(exported);
        Assert.True(coordinator.WorldNeedsSave());
        Assert.False(coordinator.IsSaving);
    }

    /// <summary>加载失败后暂停自动保存，成功的手动保存恢复自动保存。</summary>
    [Fact]
    public async Task Manual_save_resumes_suspended_autosave()
    {
        var engine = WorldEngine.Create(42, 32, 32, false);
        var storage = new MemoryStorage();
        var coordinator = new WorldSaveCoordinator(() => engine, () => storage, () => 50)
        { Ready = true, AllowAutosave = false };
        await coordinator.SaveAsync(false);
        Assert.Equal(0, storage.Saves);
        await coordinator.SaveAsync(true);
        Assert.Equal(1, storage.Saves);
        Assert.True(coordinator.AllowAutosave);
    }

    private sealed class MemoryStorage : IWorldStorage
    {
        public bool IsBackground => false;
        internal string? Json { get; private set; }
        internal int Saves { get; private set; }
        internal int Exports { get; private set; }
        internal TaskCompletionSource? SaveStarted { get; init; }
        internal Task? SaveGate { get; init; }
        public async Task SaveChunksAsync(string[] chunks)
        {
            Saves++;
            SaveStarted?.SetResult();
            if (SaveGate is not null) await SaveGate;
            Json = string.Concat(chunks);
        }
        public Task<string?> LoadAsync() => Task.FromResult(Json);
        public Task<string?> ImportAsync() => Task.FromResult(Json);
        public Task ExportAsync(string json, string fileName)
        {
            Exports++;
            return Task.CompletedTask;
        }
    }
}
