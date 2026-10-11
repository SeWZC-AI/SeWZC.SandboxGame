using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    private bool EditCaptureActive => _edits.CaptureActive;
    private bool WorldTimeStopped => _paused || _modal.IsVisible || _mapPick is not null || EditCaptureActive ||
                                     _saveCapture is not null || _storage.IsBackground;
    private bool CanSubmitEdit() => _edits.CanSubmit;
    private void CancelPendingEdit() => _edits.CancelPending();

    private Action LockSubmissionInputs(bool modal)
    {
        var inputs = new List<(Control Control, bool Enabled)>();
        if (modal)
            foreach (var control in _modal.GetLogicalDescendants().OfType<Control>().ToArray())
            {
                if (control is not (TextBox or NumericUpDown or ComboBox or CheckBox or Button))
                    continue;
                if (AutomationProperties.GetAutomationId(control) is "modal-close" or "modal-cancel")
                    continue;
                inputs.Add((control, control.IsEnabled));
                control.SetCurrentValue(IsEnabledProperty, false);
            }
        return () =>
        {
            foreach (var (control, enabled) in inputs)
                control.SetCurrentValue(IsEnabledProperty, enabled);
        };
    }

    private Task PrepareEditAsync()
    {
        var source = _engine;
        var tool = _map.ActiveTool;
        var generation = _modalGeneration;
        return _edits.PrepareMapEditAsync(() => ReferenceEquals(source, _engine) && tool == _map.ActiveTool &&
            generation == _modalGeneration && !_modal.IsVisible && _mapPick is null && !_map.PickingLocation);
    }

    private Task<bool> SubmitEditAsync(Action command, bool replaceCheckpoint = false) =>
        _edits.SubmitAsync(command, replaceCheckpoint, LockSubmissionInputs);
}
