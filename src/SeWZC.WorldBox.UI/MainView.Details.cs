using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using SeWZC.WorldBox.UI.ViewModels.Details;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    private readonly Dictionary<string, bool> _expandedDetails = [];

    private StackNode FoldSection(StackNode parent, string title, string id, bool expanded = false)
    {
        var content = new StackNode { Spacing = 3, Margin = new Thickness(0) };
        var fold = Named(new ExpanderNode
        {
            Header = Text(title, 12, Mint),
            Content = content,
            Margin = new Thickness(0),
            IsExpanded = _expandedDetails.GetValueOrDefault(id, expanded),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        }, id);
        fold.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ExpanderNode.IsExpanded))
            {
                _expandedDetails[id] = fold.IsExpanded;
                RefreshInspector();
            }
        };
        parent.Children.Add(fold);
        return content;
    }

    private void ShowTileEditor(int x, int y)
    {
        var tile = _engine.State.Tiles[y * _engine.State.Width + x];
        var panel = ModalPanel("编辑地格", "直接调整当地资源、肥沃度与道路；地形种类可用地图笔刷改变。本轮编辑可撤销。");
        var resource = Field(panel, "可采集资源 0–1,000,000", tile.ResourceAmount, "tile-resources", 1_000_000);
        var fertility = Field(panel, "肥沃度 0–100", tile.Fertility, "tile-fertility", 100);
        var road = Field(panel, "道路等级 0–3（0 为移除）", tile.RoadLevel, "tile-road", 3);
        fertility.Increment = 1;
        road.Increment = 1;
        panel.Children.Add(Named(Button("应用地格编辑", () => RunEdit(() =>
        {
            _engine.EditTile(x, y, Number(resource), Integer(fertility), Integer(road));
            CloseModal();
        }, "地格已更新，可撤销")), "tile-apply"));
        OpenModal(panel);
    }
}
