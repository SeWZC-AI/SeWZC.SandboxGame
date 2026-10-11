using static SeWZC.WorldBox.UI.ViewModels.WorldPresentationModel;
using System.Diagnostics;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    private async Task InitializeAsync()
    {
        try
        {
            if (App.Storage is not null && await _storage.LoadAsync(() => true) is { } json)
            {
                _engine = WorldEngine.ImportJson(json);
                _map.Engine = _engine;
                _simulation.ClearActivity();
                _storage.MarkLoaded(_engine);
                SetStatus("已恢复本机世界，每 30 秒自动保存");
            }
            else
                SetStatus("新世界已诞生，可选择工具创造或观察文明演化");
        }
        catch (Exception ex)
        {
            _allowAutosave = false;
            SetStatus($"本机存档未载入，已暂停自动保存：{FriendlyError(ex)}");
        }

        _ready = true;
        _storage.Ready = true;
        if (_shell is not null)
            _shell.IsEnabled = true;
        _lastSave = _clock.Elapsed.TotalSeconds;
        _map.RefreshWorld(true);
        RefreshUi(true);
    }

    /// <summary>推进到期的模拟日，刷新界面并调度保存。</summary>
    private void OnTick(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed.TotalSeconds;
        var elapsed = Math.Clamp(now - _previousTime, 0, .25);
        _previousTime = now;
        if (!_ready)
            return;
        var hidden = App.Storage?.IsBackground == true;
        var unlimited = _speed == UnlimitedSpeed;
        _map.IsSimulationPaused = WorldTimeStopped;
        _map.SimulationTickDurationSeconds = unlimited ? .016 : SimulationTickSeconds / _speed;
        if (hidden)
        {
            _simulation.ClearPendingTime();
            if (!_wasBackground)
            {
                _wasBackground = true;
                _ = SaveAsync(false);
            }

            ScheduleNextTick();
            return;
        }

        _wasBackground = false;
        if (!WorldTimeStopped)
        {
            var count = _simulation.Advance(_engine, _speed, elapsed);
            if (count > 0)
            {
                _map.SimulationTickFraction = _simulation.TickFraction;
                var mapStarted = Stopwatch.GetTimestamp();
                _map.RefreshWorld(deferAnimation: _speed == 5 || unlimited);
                _simulation.LastMapRefreshMilliseconds = Stopwatch.GetElapsedTime(mapStarted).TotalMilliseconds;
            }

            RefreshTimeState();
        }

        if (now - _lastPerformanceUpdate >= .25)
        {
            _lastPerformanceUpdate = now;
            RefreshPerformanceStats();
        }

        if (now - _lastUi > .7)
        {
            _lastUi = now;
            RefreshUi();
        }

        if (now - _lastSave > 30 && !_saving && !_modal.IsVisible && _mapPick is null && !EditCaptureActive)
        {
            _lastSave = now;
            _ = SaveAsync(false);
        }

        ScheduleNextTick();
    }

    private void ScheduleNextTick()
    {
        var idle = !_ready || WorldTimeStopped || _wasBackground;
        // 计时器在回调结束后才等待，须把回调耗时计入下日截止时间，避免重步骤额外叠加固定等待。
        var workMilliseconds = (_clock.Elapsed.TotalSeconds - _previousTime) * 1000;
        _timer.Interval = TimeSpan.FromMilliseconds(_simulation.NextDelay(_speed, idle, workMilliseconds));
    }

    private void TogglePause()
    {
        if (EditCaptureActive || _modal.IsVisible || _mapPick is not null)
            return;
        RefreshTimeState();
        _viewModel.PauseCommand.Execute(null);
    }

    private void RestoreCheckpoint()
    {
        if (_mapPick is not null || EditCaptureActive)
            return;
        if (_checkpoint is null)
        {
            SetStatus("暂无可撤销的编辑。开始绘制时会保存恢复点，继续模拟后清除。");
            return;
        }

        _saveCapture?.Cancel();
        CloseModal();
        _engine = WorldEngine.FromSnapshot(_checkpoint.Value);
        _viewModel.ResetWorldPresentation();
        _returnViewport = null;
        _simulation.ClearActivity();
        _checkpoint = null;
        _paused = true;
        ClearMapSelection();
        ResetInfrastructureFilters();
        _selectedTile = null;
        _selectedNationId = 0;
        _selectedResidentId = 0;
        _inspectorMode = "overview";
        _navigation.Clear();
        _inspectorNavigationGeneration++;
        InvalidateInspector();
        _map.Engine = _engine;
        _map.IsSimulationPaused = true;
        _map.RefreshWorld();
        UpdateToolContext();
        RefreshUi(true);
        SetStatus("已恢复到本轮编辑之前");
    }

    private void UpdateUndoButtons()
    {
        var available = _checkpoint is not null && !EditCaptureActive && _mapPick is null;
        _viewModel.CanUndo = available;
    }

    private void UpdateSpeedButtons()
    {
        ScheduleNextTick();
    }

    private void RefreshPerformanceStats()
    {
        if (!_viewModel.ShowPerformance)
            return;
        var timestamp = Stopwatch.GetTimestamp();
        var text =
            $"FPS {_map.GetRecentFrameCount(timestamp)}\n{_simulation.DaysPerSecond(timestamp):0.##} 日/秒";
        _viewModel.PerformanceText = text;
    }

    private void RefreshUi(bool force = false)
    {
        RefreshPerformanceStats();
        _viewModel.RefreshWorld();
        RefreshTimeState();
        UpdateUndoButtons();
        var spotlight = _inspectorModel.SelectSpotlight();
        _focusedEventId = spotlight?.Latest.Id ?? 0;
        _viewModel.ShowEvent(spotlight);
        if (_mobilePanel || force)
            RefreshInspector(force);
        RefreshSelectionSummary();
    }

    private void ReplaceWorld(WorldEngine engine)
    {
        _saveCapture?.Cancel();
        CloseModal();
        ClearMapSelection();
        ResetInfrastructureFilters();
        _checkpoint ??= _engine.State;
        _engine = engine;
        _viewModel.ResetWorldPresentation();
        _returnViewport = null;
        _paused = true;
        _selectedTile = null;
        _selectedNationId = 0;
        _simulation.Reset();
        _allowAutosave = true;
        _inspectorMode = "overview";
        _selectedResidentId = 0;
        _navigation.Clear();
        _inspectorNavigationGeneration++;
        _watched.Clear();
        _historyWatchedOnly = false;
        _eventDetailId = 0;
        InvalidateInspector();
        _map.Engine = engine;
        _map.IsSimulationPaused = true;
        _map.RefreshWorld(true);
        UpdateToolContext();
        RefreshUi(true);
    }
}
