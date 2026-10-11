using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.ViewModels;

namespace SeWZC.WorldBox.UI.Services;

/// <summary>管理世界编辑恢复点与提交，拒绝已经关闭或切换的会话。</summary>
internal sealed class WorldEditCoordinator
{
    private readonly MainViewModel _state;
    private readonly WorldSaveCoordinator _storage;
    private readonly Func<EditSession> _currentSession;
    private EditSubmission? _submission;
    private CancellationTokenSource? _mapCapture;
    private int _captureCount;
    private Task<WorldState>? _prepareTask;

    /// <summary>连接当前世界、保存捕获和界面会话检查。</summary>
    /// <param name="state">主界面的引擎引用与暂停偏好。</param>
    /// <param name="storage">编辑前取消捕获，提交后推迟自动保存。</param>
    /// <param name="currentSession">读取当前窗口及导航会话，不捕获旧会话。</param>
    internal WorldEditCoordinator(MainViewModel state, WorldSaveCoordinator storage, Func<EditSession> currentSession)
    {
        _state = state;
        _storage = storage;
        _currentSession = currentSession;
    }

    internal event Action? StateChanged;
    internal event Action? Completed;
    internal event Action<string>? Feedback;
    internal WorldState? Checkpoint { get; set; }
    internal bool CaptureActive => _captureCount > 0 || _prepareTask is not null;
    internal bool CanSubmit => !_currentSession().PickingLocation && _submission is null;

    internal void CancelPending()
    {
        _mapCapture?.Cancel();
        if (_submission is not { Committing: false } request)
            return;
        _submission = null;
        request.Cancellation.Cancel();
    }

    private bool SubmissionCurrent(EditSubmission request)
    {
        var session = _currentSession();
        return !request.Cancellation.IsCancellationRequested && ReferenceEquals(request.Source, _state.Engine) &&
               request.Session.Generation == session.Generation &&
               (request.Session.Modal ? session.Modal : !session.Modal && request.Session.Navigation == session.Navigation);
    }

    private Task<WorldState> ReadCheckpointAsync(WorldEngine source, CancellationToken cancellationToken, Func<bool> current)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!ReferenceEquals(source, _state.Engine) || !current())
            throw new OperationCanceledException(cancellationToken);
        return Task.FromResult(source.State);
    }

    private async Task<WorldState> PrepareCheckpointAsync(WorldEngine source, CancellationToken cancellationToken,
        Func<bool> current)
    {
        _captureCount++;
        StateChanged?.Invoke();
        Task<WorldState>? capture = null;
        try
        {
            capture = ReadCheckpointAsync(source, cancellationToken, current);
            _prepareTask = capture;
            StateChanged?.Invoke();
            return await capture;
        }
        finally
        {
            _captureCount--;
            if (ReferenceEquals(_prepareTask, capture))
                _prepareTask = null;
            StateChanged?.Invoke();
        }
    }

    /// <summary>地图笔刷准备恢复点，工具和世界的有效性由地图适配层检查。</summary>
    internal async Task PrepareMapEditAsync(Func<bool> current)
    {
        var source = _state.Engine;
        using var cancellation = new CancellationTokenSource();
        _mapCapture?.Cancel();
        _mapCapture = cancellation;
        bool Current() => !cancellation.IsCancellationRequested && ReferenceEquals(source, _state.Engine) && current();
        try
        {
            if (!Current())
                throw new OperationCanceledException(cancellation.Token);
            _storage.CancelCapture();
            if (Checkpoint is null)
            {
                var checkpoint = await PrepareCheckpointAsync(source, cancellation.Token, Current);
                if (!Current())
                    throw new OperationCanceledException(cancellation.Token);
                Checkpoint ??= checkpoint;
            }
            if (!Current())
                throw new OperationCanceledException(cancellation.Token);
            _state.IsPaused = true;
            StateChanged?.Invoke();
        }
        finally
        {
            if (ReferenceEquals(_mapCapture, cancellation))
                _mapCapture = null;
            Completed?.Invoke();
        }
    }

    /// <summary>提交同步世界修改；准备时锁定原输入，结束后恢复，部分失败保留撤销点。</summary>
    internal async Task<bool> SubmitAsync(Action command, bool replaceCheckpoint = false, Func<bool, Action>? lockInputs = null)
    {
        if (!CanSubmit)
            return false;
        var request = new EditSubmission(_state.Engine, _currentSession());
        _submission = request;
        var previousCheckpoint = Checkpoint;
        var previousPaused = _state.IsPaused;
        Action? restoreInputs = null;
        try
        {
            restoreInputs = lockInputs?.Invoke(request.Session.Modal);
            _storage.CancelCapture();
            var checkpoint = replaceCheckpoint || previousCheckpoint is null
                ? await PrepareCheckpointAsync(request.Source, request.Cancellation.Token, () => SubmissionCurrent(request))
                : previousCheckpoint;
            if (!SubmissionCurrent(request))
                return false;
            // 会话仍有效且即将同步提交时才发布恢复点。
            Checkpoint = checkpoint;
            _state.IsPaused = true;
            StateChanged?.Invoke();
            request.Committing = true;
            _storage.EditRevision++;
            command();
            return true;
        }
        catch (Exception ex)
        {
            var partialChange = false;
            if (ReferenceEquals(request.Source, _state.Engine) && request.Committing)
            {
                if (request.Source.State == Checkpoint)
                {
                    Checkpoint = previousCheckpoint;
                    _state.IsPaused = previousPaused;
                }
                else
                {
                    _state.IsPaused = true;
                    partialChange = true;
                }
            }
            if (ex is not OperationCanceledException && SubmissionCurrent(request))
                Feedback?.Invoke((partialChange ? "变更未完成，可撤销：" : "未应用变更：") + FriendlyError(ex));
            return false;
        }
        finally
        {
            if (request.Committing)
                _storage.DeferAutosaveAfterEdit();
            if (ReferenceEquals(_submission, request))
                _submission = null;
            restoreInputs?.Invoke();
            request.Cancellation.Dispose();
            Completed?.Invoke();
        }
    }

    private static string FriendlyError(Exception exception) => exception.Message.Length > 160 ? exception.Message[..160] : exception.Message;

    private sealed class EditSubmission(WorldEngine source, EditSession session)
    {
        internal WorldEngine Source { get; } = source;
        internal EditSession Session { get; } = session;
        internal CancellationTokenSource Cancellation { get; } = new();
        internal bool Committing { get; set; }
    }
}
