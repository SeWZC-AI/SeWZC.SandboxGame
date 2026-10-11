using Avalonia;
using Avalonia.Media;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.ViewModels.Details;

namespace SeWZC.WorldBox.UI.ViewModels;

/// <summary>为 XAML 设计器提供示例数据。</summary>
internal static class DesignData
{
    private static readonly Lazy<MainViewModel> MainSample = new(CreateMain);
    private static readonly Lazy<ResearchGraphViewModel> GraphSample = new(CreateGraph);
    public static MainViewModel Main => MainSample.Value;
    public static StackNode Details => CreateDetails();
    public static ModalViewModel Dialog => CreateDialog();
    public static ResearchGraphViewModel Graph => GraphSample.Value;

    private static StackNode CreateDetails()
    {
        var content = new StackNode { Spacing = 6, Margin = new Thickness(6) };
        content.Children.Add(new TextNode { Text = "居民档案", FontSize = 17, Foreground = Brush.Parse("#B8E9BC") });
        content.Children.Add(new TextNode { Text = "当前活动：正在返回家园交付粮食", Foreground = Brush.Parse("#E9EFEB") });
        content.Children.Add(new TextInputNode { Text = "示例居民", MinHeight = 36 });
        content.Children.Add(new NumberNode { Value = 100, Minimum = 0, Maximum = 100, MinHeight = 36 });
        content.Children.Add(new ChoiceNode { ItemsSource = new[] { "家园", "附近聚落" }, SelectedIndex = 0, MinHeight = 36 });
        content.Children.Add(new CheckNode { Content = "跟随居民", IsChecked = true });
        return content;
    }

    private static ModalViewModel CreateDialog() => new(
        new TextNode { Text = "编辑居民档案", FontSize = 17, Foreground = Brush.Parse("#E9EFEB") },
        CreateDetails(), new ButtonNode { Content = "应用档案变更", Padding = new Thickness(8, 3), MinHeight = 30 },
        new TextNode { Text = "修改将在暂停的世界中生效", Foreground = Brush.Parse("#E7BD87") }, () => { });

    private static MainViewModel CreateMain()
    {
        var model = new MainViewModel(WorldEngine.Create(42, 32, 32, false)) { IsPaused = true };
        model.ShowEvent(new EventGroup([new WorldEvent { Id = 1, Message = "示例：居民抵达家园，交付实际携带的粮食" }]));
        model.InspectorTitle = "居民档案（示例）";
        model.InspectorContent = CreateDetails();
        model.InspectorVisible = true;
        return model;
    }
    private static ResearchGraphViewModel CreateGraph()
    {
        var graph = new ResearchGraphViewModel { Selected = Advancement.Agriculture };
        graph.SetNodes(ResearchRules.All.ToDictionary(research => research, research =>
            new ButtonNode { Content = new TextNode { Text = research.Name, Foreground = Brush.Parse("#E9EFEB") } }));
        return graph;
    }

}
