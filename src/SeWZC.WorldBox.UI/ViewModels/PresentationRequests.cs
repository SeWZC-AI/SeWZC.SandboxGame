using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;
using SeWZC.WorldBox.UI.ViewModels.Details;
namespace SeWZC.WorldBox.UI.ViewModels;
/// <summary>展示模型发出的导航、地图和窗口请求。</summary>
internal sealed class PresentationRequests
{
    internal required IMapPresentation Map { get; init; }
    internal required MainViewModel State { get; init; }
    internal required Services.WorldSaveCoordinator Storage { get; init; }
    internal required Func<bool> CanSubmitEdit { get; init; }
    internal required Func<bool> ModalVisible { get; init; }
    internal required Func<bool> PickingLocation { get; init; }
    internal required Func<int> ModalGeneration { get; init; }
    internal required Func<Action, bool, Task<bool>> SubmitEdit { get; init; }
    internal required Action<Action, string> RunEdit { get; init; }
    internal required Action<StackNode> InspectorContentChanged { get; init; }
    internal required Action<StackNode> OpenModal { get; init; }
    internal required Action RefreshPerformanceStats { get; init; }
    internal required Action CloseModal { get; init; }
    internal required Action CloseInspector { get; init; }
    internal required Action ApplyLayout { get; init; }
    internal required Action RememberLocation { get; init; }
    internal required Action<bool> RefreshUi { get; init; }
    internal required Action<string> SetStatus { get; init; }
    internal required Action<string, bool> OpenInspector { get; init; }
    internal required Action<int> OpenResident { get; init; }
    internal required Action<int> OpenNation { get; init; }
    internal required Action<int> TerritoryEdit { get; init; }
    internal required Action<int, string> OpenSettlement { get; init; }
    internal required Action<int, int, bool> OpenTile { get; init; }
    internal required Action<int, int> FocusMapTile { get; init; }
    internal required Func<int, bool> FocusMapResident { get; init; }
    internal required Action<Building> OpenBuilding { get; init; }
    internal required Action<WorldEvent> FocusEvent { get; init; }
    internal required Action<ToolCategory> SetCategory { get; init; }
    internal required Action<NumberNode, NumberNode, Action?> StartMapPick { get; init; }
    internal required Action<WorldEngine> ReplaceWorld { get; init; }
    internal InspectorViewModel Inspector { get; set; } = null!;
    internal WorldEditorViewModel Editors { get; set; } = null!;
}
