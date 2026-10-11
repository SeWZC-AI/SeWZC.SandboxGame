using static SeWZC.WorldBox.UI.ViewModels.WorldPresentationModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.ViewModels.Details;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    private bool _expandedInspector { get => _viewModel.InspectorExpanded; set => _viewModel.InspectorExpanded = value; }

    private void SelectMapObject(string kind, int id = 0, int x = 0, int y = 0)
    {
        if (_mapPick is not null)
            return;
        _mapSelectionKind = kind;
        _selectedResidentId = kind == "resident" ? id : 0;
        _selectedBuildingId = kind == "building" ? id : 0;
        _selectedTile = kind == "resident" ? null : (x, y);
        _toolsOpen = false;
        CloseInspector();
        RefreshSelectionSummary();
    }

    private void RefreshSelectionSummary()
    {
        if (!_viewModel.RefreshSelection() && _mapSelectionKind is not null)
            ClearMapSelection();
    }

    private void ViewMapSelection()
    {
        if (_mapPick is not null)
            return;
        if (_mapSelectionKind == "resident")
            OpenResident(_selectedResidentId);
        else if (_mapSelectionKind == "building")
            OpenInspector("building");
        else if (_selectedTile is not null)
            OpenInspector("tile");
        RefreshSelectionSummary();
    }

    private void ClearMapSelection()
    {
        if (_mapPick is not null)
            return;
        if (!_mobilePanel)
        {
            _navigation.Clear();
            _inspectorNavigationGeneration++;
        }

        _mapSelectionKind = null;
        _selectedBuildingId = 0;
        _viewModel.RefreshSelection();
        if (!_mobilePanel)
        {
            _selectedResidentId = 0;
            _selectedTile = null;
        }

        _map.ClearMapSelection();
    }

}
