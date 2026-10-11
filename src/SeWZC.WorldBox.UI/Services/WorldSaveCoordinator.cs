using System.Diagnostics;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Platform;

namespace SeWZC.WorldBox.UI.Services;

/// <summary>串行执行世界存档读写与导出，管理捕获停表及自动保存时机。</summary>
internal sealed class WorldSaveCoordinator
{
    private readonly Func<WorldEngine> _currentEngine;
    private readonly Func<IWorldStorage?> _currentStorage;
    private readonly Func<double> _elapsedSeconds;
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private bool _ready, _allowAutosave = true, _saving;
    private CancellationTokenSource? _saveCapture;
    private WorldEngine? _savedSource;
    private long _savedTick = -1, _worldEditRevision, _savedEditRevision = -1, _lastSaveYield;
    private double _lastSave;
    private WorldEngine _engine => _currentEngine();

    /// <summary>连接当前世界、平台存储与会话内计时。</summary>
    /// <param name="currentEngine">读取当前世界引擎。</param>
    /// <param name="currentStorage">读取当前平台存储。</param>
    /// <param name="elapsedSeconds">会话内单调经过时间，用于自动保存静默期。</param>
    internal WorldSaveCoordinator(Func<WorldEngine> currentEngine, Func<IWorldStorage?> currentStorage,
        Func<double> elapsedSeconds)
    {
        _currentEngine = currentEngine;
        _currentStorage = currentStorage;
        _elapsedSeconds = elapsedSeconds;
    }

    internal event Action? CaptureChanged;
    internal event Action? TimeAnchorChanged;
    internal event Action<string>? Feedback;
    internal bool Ready { get => _ready; set => _ready = value; }
    internal bool AllowAutosave { get => _allowAutosave; set => _allowAutosave = value; }
    internal bool IsSaving => _saving;
    internal CancellationTokenSource? Capture => _saveCapture;
    internal double LastSave { get => _lastSave; set => _lastSave = value; }
    internal long EditRevision { get => _worldEditRevision; set => _worldEditRevision = value; }
    internal bool IsAvailable => _currentStorage() is not null;
    internal bool IsBackground => _currentStorage()?.IsBackground == true;
    internal void CancelCapture() => _saveCapture?.Cancel();
    internal void MarkLoaded(WorldEngine source)
    {
        _savedSource = source;
        _savedTick = source.State.Tick;
        _savedEditRevision = _worldEditRevision;
    }
    internal async Task<string?> LoadAsync(Func<bool> current)
    {
        var storage = _currentStorage();
        if (storage is null) return null;
        await _saveGate.WaitAsync();
        try { return current() ? await storage.LoadAsync() : null; }
        finally { _saveGate.Release(); }
    }
    internal Task<string?> ImportAsync() => _currentStorage()?.ImportAsync() ?? Task.FromResult<string?>(null);
    private void SetStatus(string text) => Feedback?.Invoke(text);
    private static string FriendlyError(Exception exception) => exception.Message.Length > 160 ? exception.Message[..160] : exception.Message;

    internal async Task ExportWorldAsync(Func<bool> originalWindowOpen, Action exported)
    {
        var storage = _currentStorage();
        if (storage is null)
            return;
        var source = _engine;

        bool OriginalWindowOpen() => originalWindowOpen();

        await _saveGate.WaitAsync();
        using var capture = new CancellationTokenSource();
        try
        {
            // 排队请求须绑定原窗口和世界，避免窗口关闭后捕获新选中的世界。
            if (!OriginalWindowOpen())
                return;
            _saving = true;
            _saveCapture = capture;
            _lastSaveYield = Stopwatch.GetTimestamp();
            CaptureChanged?.Invoke();
            var filename = $"worldbox-{source.State.Seed}-year{source.State.Year}.json";
            var json = await source.ExportJsonAsync(YieldDuringSave, capture.Token);
            capture.Token.ThrowIfCancellationRequested();
            FinishSaveCapture(capture);
            await storage.ExportAsync(json, filename);
            if (OriginalWindowOpen())
            {
                exported();
                SetStatus("已导出世界文件");
            }
        }
        catch (OperationCanceledException)
        {
            if (OriginalWindowOpen())
                SetStatus("已取消导出");
        }
        catch (Exception ex)
        {
            if (OriginalWindowOpen())
                SetStatus($"导出失败：{FriendlyError(ex)}");
        }
        finally
        {
            FinishSaveCapture(capture);
            _saving = false;
            _saveGate.Release();
        }
    }

    internal async Task SaveAsync(bool manual)
    {
        var storage = _currentStorage();
        if (storage is null || !_ready || (!manual && (_saving || !_allowAutosave || !WorldNeedsSave())))
            return;
        await _saveGate.WaitAsync();
        using var capture = new CancellationTokenSource();
        try
        {
            if (!manual && !WorldNeedsSave())
                return;
            _saving = true;
            var source = _engine;
            var tick = source.State.Tick;
            var revision = _worldEditRevision;
            var year = source.State.Year;
            _saveCapture = capture;
            _lastSaveYield = Stopwatch.GetTimestamp();
            CaptureChanged?.Invoke();

            var chunks = await source.ExportJsonChunksAsync(YieldDuringSave, capture.Token);
            capture.Token.ThrowIfCancellationRequested();
            FinishSaveCapture(capture);
            await storage.SaveChunksAsync(chunks);
            _savedSource = source;
            _savedTick = tick;
            _savedEditRevision = revision;
            _lastSave = _elapsedSeconds();
            if (manual)
            {
                _allowAutosave = true;
                SetStatus("世界已保存到本机");
            }
            else
                SetStatus($"已自动保存（第 {year} 年，{DateTime.Now:HH:mm}）");
        }
        catch (Exception) when (capture.IsCancellationRequested)
        {
            _lastSave = _elapsedSeconds() - 25;
        }
        catch (Exception ex)
        {
            SetStatus($"保存失败：{FriendlyError(ex)}。请尝试导出文件。");
        }
        finally
        {
            FinishSaveCapture(capture);
            _saving = false;
            _saveGate.Release();
        }
    }

    internal bool WorldNeedsSave()
    {
        return !ReferenceEquals(_savedSource, _engine) || _savedTick != _engine.State.Tick
                                                       || _savedEditRevision != _worldEditRevision;
    }

    // 从编辑结束重新计算保存静默期，避免连续命令立即触发自动保存。
    internal void DeferAutosaveAfterEdit()
    {
        _lastSave = Math.Max(_lastSave, _elapsedSeconds() - 25);
    }

    private async ValueTask YieldDuringSave(CancellationToken cancellationToken)
    {
        if (_currentStorage()?.IsBackground == true || Stopwatch.GetElapsedTime(_lastSaveYield).TotalMilliseconds < 4)
            return;
        await Task.Delay(1, cancellationToken);
        _lastSaveYield = Stopwatch.GetTimestamp();
    }

    private void FinishSaveCapture(CancellationTokenSource capture)
    {
        if (!ReferenceEquals(_saveCapture, capture))
            return;
        // 捕获期间模拟暂停，恢复时不能把捕获耗时计入待推进的模拟时间。
        _saveCapture = null;
        TimeAnchorChanged?.Invoke();
        CaptureChanged?.Invoke();
    }

}
