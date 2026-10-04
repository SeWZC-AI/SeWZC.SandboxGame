using Avalonia.Controls;
using Avalonia.Layout;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    private static readonly string[] MapHighlightLabels = ["关闭高亮标记", "粮食压力", "货物与消息运输", "通信网络", "道路与建筑",
        "城镇占领区域", "饮水与水源", "地形肥力", "火灾与干旱", "捕鱼与舟船", "居民当前任务"];

    private void BuildMapHighlights(StackPanel panel)
    {
        panel.Children.Add(Text("地图高亮标记", 13, Mint));
        var overlay = Named(new ComboBox { ItemsSource = MapHighlightLabels, SelectedIndex = _map.Overlay,
            HorizontalAlignment = HorizontalAlignment.Stretch }, "map-overlay");
        overlay.SelectionChanged += (_, _) => { _map.Overlay = Math.Max(0, overlay.SelectedIndex); _map.RefreshWorld(); };
        panel.Children.Add(overlay);
        panel.Children.Add(Named(Button("关闭高亮标记", () => { overlay.SelectedIndex = 0; _map.Overlay = 0; _map.RefreshWorld(); }), "map-highlights-off"));
    }

    private void BuildGameGuide(StackPanel panel)
    {
        panel.Children.Add(Text("玩法说明", 18, Mint));
        panel.Children.Add(Paragraph("领地与建造\n普通建筑须在本城镇已登记土地上建设。居民走到与城镇相连的边界驻留后登记；人口只提高可占领范围。领地绘制可扩展既有边界，远处的独立笔刷不会产生游离领地。桥梁、道路和山路供所有人通行；船坞、码头在本城镇陆岸旁的水中建设。"));
        panel.Children.Add(Paragraph("水与河流\n河湖提供无限淡水，居民仍须取水并搬运。陆地每日供水额度按降水、距河湖距离和干旱计算，未使用额度不会累计。小溪通常一格宽，可涉水；河与江通常两至五格宽，需要桥梁或舟船。"));
        panel.Children.Add(Paragraph("生产与捕鱼\n地形决定食物、木材和矿石产出，肥力、种族适应与居民状态另有影响。雨林木材产出高于森林，森林高于疏林。岸边可以捕鱼；渔民在掌握驿路运输、家园已有舟船时借船出航，在可见鱼群捕鱼后返岸交付，鱼群实际减少。船坞造出的舟船必须搬回仓库后才可借用。"));
        panel.Children.Add(Paragraph("居民与通信\n城镇按人数配置少量信使，代表负责本地事务。居民会完成当前采集、施工和返程任务，休息恢复后再出发；危险和生存需要仍能打断任务。消息由当面交流、信使或通信设施传播，查看世界不会改变居民的知识。"));
        panel.Children.Add(Paragraph("地图标记\n在世界概览或道路与建筑页面选择高亮类型，选择关闭后保持关闭。煤堆表示煤，油井表示石油，紫色矿晶表示稀土。所有高亮和图标设置只影响观察。"));
        panel.Children.Add(Button("返回世界概览", () => OpenInspector("overview")));
    }
}
