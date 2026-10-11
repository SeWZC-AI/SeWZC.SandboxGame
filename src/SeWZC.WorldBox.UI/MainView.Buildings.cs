using static SeWZC.WorldBox.UI.ViewModels.WorldPresentationModel;
using Avalonia.Controls;
using Avalonia.Layout;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.ViewModels.Details;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{

    private void OpenBuilding(Building building)
    {
        if (_mapPick is not null)
            return;
        if (_mobilePanel && _inspectorMode == "building" && _selectedBuildingId == building.Id)
            return;
        RememberLocation();
        _selectedBuildingId = building.Id;
        _selectedTile = (building.X, building.Y);
        _mapSelectionKind = "building";
        OpenInspector("building", false);
    }
}
