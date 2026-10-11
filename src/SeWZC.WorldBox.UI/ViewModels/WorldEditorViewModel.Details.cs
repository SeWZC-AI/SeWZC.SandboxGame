namespace SeWZC.WorldBox.UI.ViewModels;

internal sealed partial class WorldEditorViewModel
{
    internal void ShowTileEditor(int x, int y)
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
