using static SeWZC.WorldBox.UI.ViewModels.WorldPresentationModel;
using System.Globalization;
using Avalonia.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;
using SeWZC.WorldBox.UI.ViewModels.Details;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    private Action<int, int>? _mapPick;
    private WorldEngine? _mapPickEngine;
    private int _mapPickGeneration;
    private bool _mapPickInspectorVisible, _mapPickToolsOpen;
    private Avalonia.Controls.Control? _mapPickModal;
    private WorldMapControl.MapSelectionState _mapPickSelection;
    private MapTool _mapPickTool = MapTool.Pan;

    private void StartMapPick(NumberNode x, NumberNode y, Action? onLocationPicked)
    {
        if (!_modal.IsVisible || _modal.Child is null)
            return;
        _mapPickGeneration = _modalGeneration;
        _mapPickEngine = _engine;
        _mapPickModal = _modal.Child;
        _mapPickInspectorVisible = _mobilePanel;
        _mapPickToolsOpen = _toolsOpen;
        _mapPickTool = _map.ActiveTool;
        _mapPickSelection = _map.CaptureMapSelection();
        _mapPick = (xx, yy) =>
        {
            x.Value = xx;
            y.Value = yy;
            onLocationPicked?.Invoke();
        };
        _map.PickingLocation = true;
        _map.ActiveTool = MapTool.Inspect;
        _modal.IsVisible = false;
        _mobilePanel = false;
        _toolsOpen = false;
        _map.IsSimulationPaused = true;
        ApplyLayout();
        RefreshUi();
        SetStatus("点选目标地格，按 Escape 返回表单；时间暂时停止");
    }

    private void FinishMapPick(int? x = null, int? y = null)
    {
        if (_mapPick is null)
            return;
        var assignLocation = _mapPick;
        var current = _mapPickGeneration == _modalGeneration && ReferenceEquals(_mapPickEngine, _engine) &&
                      ReferenceEquals(_mapPickModal, _modal.Child);
        CancelMapPick(current);
        if (!current)
            return;
        if (x.HasValue && y.HasValue)
            assignLocation(x.Value, y.Value);
        _modal.IsVisible = true;
        _map.IsSimulationPaused = true;
        RefreshUi();
        SetStatus(x.HasValue && y.HasValue ? "已选定位置，可继续编辑表单" : "已返回表单，位置未改变");
    }

    private void CancelMapPick(bool restorePresentation = false)
    {
        if (_mapPick is null)
            return;
        _mapPick = null;
        _mapPickEngine = null;
        _mapPickModal = null;
        _map.PickingLocation = false;
        _map.ActiveTool = restorePresentation ? _mapPickTool : MapTool.Pan;
        if (restorePresentation)
        {
            _map.RestoreMapSelection(_mapPickSelection);
            _mobilePanel = _mapPickInspectorVisible;
            _toolsOpen = _mapPickToolsOpen;
            ApplyLayout();
        }
    }
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (_mapPick is not null)
                FinishMapPick();
            else if (_map.HasPendingPlacement)
                _map.CancelPlacement();
            else if (_modal.IsVisible)
                CloseModal();
            else if (_mobilePanel)
                CloseInspector();
            else
                SuspendTool();
            e.Handled = true;
        }

        if (e.Key == Key.Space && !_modal.IsVisible && _mapPick is null && !IsEditingText())
        {
            TogglePause();
            e.Handled = true;
        }
    }
}
