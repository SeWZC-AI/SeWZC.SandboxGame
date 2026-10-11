using static SeWZC.WorldBox.UI.ViewModels.WorldPresentationModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.ViewModels.Details;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{

    private void FocusEvent(WorldEvent item)
    {
        if (_mapPick is not null)
            return;
        if (_mobilePanel && _inspectorMode == "event" && _eventDetailId == item.Id)
            return;
        RememberLocation();
        _eventDetailId = item.Id;
        OpenInspector("event", false);
    }
}
