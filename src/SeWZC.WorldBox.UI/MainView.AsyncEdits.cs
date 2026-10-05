using System.Diagnostics;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    private int _editCaptureCount;

    private EditSubmission? _editSubmission;
    private CancellationTokenSource? _mapEditCapture;
    private bool EditCaptureActive => _editCaptureCount > 0 || _prepareEditTask is not null;

    private bool WorldTimeStopped => _paused || _modal.IsVisible || _mapPick is not null || EditCaptureActive ||
                                     _saveCapture is not null || App.Storage?.IsBackground == true;

    private bool CanSubmitEdit()
    {
        return _mapPick is null && _editSubmission is null;
    }

    private bool SubmissionCurrent(EditSubmission request)
    {
        return !request.Cancellation.IsCancellationRequested &&
               ReferenceEquals(request.Source, _engine) && request.Generation == _modalGeneration &&
               (request.Modal
                   ? _modal.IsVisible
                   : !_modal.IsVisible && request.Navigation == _inspectorNavigationGeneration);
    }

    private void CancelPendingEdit()
    {
        _mapEditCapture?.Cancel();
        if (_editSubmission is not { Committing: false } request) return;
        _editSubmission = null;
        request.Cancellation.Cancel();
    }

    private void LockSubmissionInputs(EditSubmission request)
    {
        if (!request.Modal) return;
        foreach (var control in _modal.GetLogicalDescendants().OfType<Control>().ToArray())
        {
            if (control is not (TextBox or NumericUpDown or ComboBox or CheckBox or Avalonia.Controls.Button)) continue;
            if (AutomationProperties.GetAutomationId(control) is "modal-close" or "modal-cancel") continue;
            request.Inputs.Add((control, control.IsEnabled));
            control.IsEnabled = false;
        }
    }

    private async Task<string> ReadEditCheckpointAsync(WorldEngine source, CancellationToken cancellationToken,
        Func<bool>? current = null)
    {
        await _saveGate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ReferenceEquals(source, _engine) || current?.Invoke() == false)
                throw new OperationCanceledException(cancellationToken);
            SetStatus("正在准备编辑恢复点，地图可继续平移；可以取消提交");
            _lastSaveYield = Stopwatch.GetTimestamp();
            return await source.ExportJsonAsync(YieldDuringSave, cancellationToken);
        }
        finally
        {
            _saveGate.Release();
        }
    }

    /// <summary>捕获指定世界的可取消撤销快照，期间临时停止模拟。</summary>
    private async Task<string> PrepareCheckpointAsync(WorldEngine source, CancellationToken cancellationToken,
        Func<bool>? current = null)
    {
        _editCaptureCount++;
        _map.IsSimulationPaused = true;
        var capture = ReadEditCheckpointAsync(source, cancellationToken, current);
        _prepareEditTask = capture;
        UpdateUndoButtons();
        try
        {
            return await capture;
        }
        finally
        {
            _editCaptureCount--;
            if (ReferenceEquals(_prepareEditTask, capture)) _prepareEditTask = null;
            _previousTime = _clock.Elapsed.TotalSeconds;
            _map.IsSimulationPaused = WorldTimeStopped;
            UpdateUndoButtons();
        }
    }

    // A map stroke owns its tool/engine checks in WorldMapControl. Modal commands use SubmitEditAsync.
    private async Task PrepareEditAsync()
    {
        var source = _engine;
        var tool = _map.ActiveTool;
        var generation = _modalGeneration;
        using var cancellation = new CancellationTokenSource();
        _mapEditCapture?.Cancel();
        _mapEditCapture = cancellation;

        bool Current()
        {
            return !cancellation.IsCancellationRequested && ReferenceEquals(source, _engine) &&
                   tool == _map.ActiveTool && generation == _modalGeneration && !_modal.IsVisible && _mapPick is null &&
                   !_map.PickingLocation;
        }

        try
        {
            if (!Current()) throw new OperationCanceledException(cancellation.Token);
            _saveCapture?.Cancel();
            if (_checkpoint is null)
            {
                var checkpoint = await PrepareCheckpointAsync(source, cancellation.Token, Current);
                if (!Current()) throw new OperationCanceledException(cancellation.Token);
                _checkpoint ??= checkpoint;
            }

            if (!Current()) throw new OperationCanceledException(cancellation.Token);
            _paused = true;
            _map.IsSimulationPaused = true;
            UpdateUndoButtons();
        }
        finally
        {
            if (ReferenceEquals(_mapEditCapture, cancellation)) _mapEditCapture = null;
            _map.IsSimulationPaused = WorldTimeStopped;
        }
    }

    /// <summary>准备撤销恢复点，仅在原世界及界面会话仍有效时执行编辑。</summary>
    /// <param name="command">捕获完成且会话校验通过后执行的同步世界修改。</param>
    /// <param name="replaceCheckpoint">本轮编辑已有恢复点时，是否仍重新捕获。</param>
    /// <returns>命令是否执行成功；部分执行后失败仍保留撤销恢复点。</returns>
    private async Task<bool> SubmitEditAsync(Action command, bool replaceCheckpoint = false)
    {
        if (!CanSubmitEdit()) return false;
        var request = new EditSubmission(_engine, _modalGeneration, _modal.IsVisible, _inspectorNavigationGeneration);
        _editSubmission = request;
        var previousCheckpoint = _checkpoint;
        var previousPaused = _paused;
        try
        {
            LockSubmissionInputs(request);
            _saveCapture?.Cancel();
            var checkpoint = replaceCheckpoint || previousCheckpoint is null
                ? await PrepareCheckpointAsync(request.Source, request.Cancellation.Token,
                    () => SubmissionCurrent(request))
                : previousCheckpoint;
            if (!SubmissionCurrent(request)) return false;
            // Publish only while the original request is still valid, immediately before its synchronous commit.
            _checkpoint = checkpoint;
            _paused = true;
            _map.IsSimulationPaused = true;
            request.Committing = true;
            _worldEditRevision++;
            command();
            return true;
        }
        catch (Exception ex)
        {
            var partialChange = false;
            if (ReferenceEquals(request.Source, _engine) && request.Committing)
            {
                // A multi-command edit or its refresh can fail after changing the world. Keep its undo point.
                var unchanged = false;
                try
                {
                    unchanged = request.Source.ExportJson() == _checkpoint;
                }
                catch (Exception)
                {
                    /* A state that cannot be exported still needs the valid pre-edit undo point. */
                }

                if (unchanged)
                {
                    _checkpoint = previousCheckpoint;
                    _paused = previousPaused;
                }
                else
                {
                    _paused = true;
                    partialChange = true;
                }
            }

            if (ex is not OperationCanceledException && SubmissionCurrent(request))
                SetStatus((partialChange ? "变更未完成，可撤销：" : "未应用变更：") + FriendlyError(ex));
            return false;
        }
        finally
        {
            if (request.Committing) DeferAutosaveAfterEdit();
            if (ReferenceEquals(_editSubmission, request)) _editSubmission = null;
            foreach (var (control, enabled) in request.Inputs) control.IsEnabled = enabled;
            request.Cancellation.Dispose();
            _previousTime = _clock.Elapsed.TotalSeconds;
            _map.IsSimulationPaused = WorldTimeStopped;
            RefreshUi(true);
        }
    }

    /// <summary>将待提交的命令绑定到原世界及界面会话，并记录恢复输入控件所需的状态。</summary>
    private sealed class EditSubmission(WorldEngine source, int generation, bool modal, int navigation)
    {
        public WorldEngine Source { get; } = source;
        public int Generation { get; } = generation;
        public bool Modal { get; } = modal;
        public int Navigation { get; } = navigation;
        public CancellationTokenSource Cancellation { get; } = new();
        public bool Committing { get; set; }
        public List<(Control Control, bool Enabled)> Inputs { get; } = [];
    }
}
