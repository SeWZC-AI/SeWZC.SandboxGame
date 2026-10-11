using static SeWZC.WorldBox.UI.ViewModels.WorldPresentationModel;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    private WorldEngine _engine { get => _viewModel.Engine; set => _viewModel.Engine = value; }
    private bool _paused { get => _viewModel.IsPaused; set => _viewModel.IsPaused = value; }
    private int _speed => _viewModel.Speed;
    private bool _mobilePanel { get => _viewModel.InspectorVisible; set => _viewModel.InspectorVisible = value; }
    private bool _toolsOpen { get => _viewModel.ToolsVisible; set => _viewModel.ToolsVisible = value; }
    private string _inspectorMode { get => _viewModel.InspectorMode; set => _viewModel.InspectorMode = value; }
    private string? _mapSelectionKind { get => _viewModel.SelectionKind; set => _viewModel.SelectionKind = value; }
    private int _selectedResidentId { get => _viewModel.SelectedResidentId; set => _viewModel.SelectedResidentId = value; }
    private int _selectedNationId { get => _viewModel.SelectedNationId; set => _viewModel.SelectedNationId = value; }
    private int _selectedBuildingId { get => _viewModel.SelectedBuildingId; set => _viewModel.SelectedBuildingId = value; }
    private (int X, int Y)? _selectedTile { get => _viewModel.SelectedTile; set => _viewModel.SelectedTile = value; }

    private void RefreshTimeState()
    {
        _viewModel.RefreshTime(_modal.IsVisible, _mapPick is not null, EditCaptureActive,
            _saveCapture is not null, App.Storage?.IsBackground == true, _simulation.IsCatchingUp);
    }

    private bool IsEditingText()
    {
        return TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox
               || (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Control control &&
                   control.GetVisualAncestors().Any(c => c is TextBox));
    }

}
