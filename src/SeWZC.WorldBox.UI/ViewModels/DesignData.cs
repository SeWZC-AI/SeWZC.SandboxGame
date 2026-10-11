using Avalonia;
using Avalonia.Media;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.ViewModels.Details;

namespace SeWZC.WorldBox.UI.ViewModels;

/// <summary>为 XAML 设计器提供示例数据。</summary>
internal static class DesignData
{
    private static readonly Lazy<DialogHostViewModel> DialogHostSample = new(CreateDialogHost);
    private static readonly Lazy<MapInteractionViewModel> InteractionSample = new(CreateInteraction);
    private static readonly Lazy<GameSessionViewModel> SessionSample = new(CreateSession);
    private static readonly Lazy<MainViewModel> MainSample = new(CreateMain);
    private static readonly Lazy<ResearchGraphViewModel> GraphSample = new(CreateGraph);
    public static GameSessionViewModel Session => SessionSample.Value;
    public static ToolPaletteViewModel Tools => Session.Tools;
    public static InspectorNavigationViewModel Navigation => Session.Navigation;
    public static MapInteractionViewModel Interaction => InteractionSample.Value;
    public static DialogHostViewModel Dialogs => DialogHostSample.Value;
    public static MainViewModel Main => MainSample.Value;
    public static StackNode Details => CreateDetails();
    public static ModalViewModel Dialog => CreateDialog();
    public static ResearchGraphViewModel Graph => GraphSample.Value;

    private static DialogHostViewModel CreateDialogHost()
    {
        var session = new GameSessionViewModel(new MainViewModel(WorldEngine.Create(42, 32, 32, false)),
            new DesignMap(), () => null);
        session.Dialogs.OpenModal(CreateDetails());
        return session.Dialogs;
    }

    private static MapInteractionViewModel CreateInteraction()
    {
        var session = new GameSessionViewModel(new MainViewModel(WorldEngine.Create(42, 32, 32, false)),
            new DesignMap(), () => null);
        session.Interaction.PlacementChanged("将投放 12 位居民，确认后生效");
        session.State.PlacementPending = true;
        return session.Interaction;
    }

    private static GameSessionViewModel CreateSession()
    {
        var session = new GameSessionViewModel(Main, new DesignMap(), () => null) { IsReady = true };
        session.State.InspectorTitle = "居民档案（示例）";
        session.State.InspectorContent = CreateDetails();
        session.State.InspectorVisible = true;
        session.Layout.Resize(1440, 920, 1440);
        return session;
    }

    private static StackNode CreateDetails()
    {
        var content = new StackNode { Spacing = 6, Margin = new Thickness(6) };
        content.Children.Add(new TextNode { Text = "居民档案", FontSize = 17, Foreground = Brush.Parse("#B8E9BC") });
        content.Children.Add(new TextNode { Text = "当前活动：正在返回家园交付粮食", Foreground = Brush.Parse("#E9EFEB") });
        content.Children.Add(new TextInputNode { Text = "示例居民", MinHeight = 36 });
        content.Children.Add(new NumberNode { Value = 100, Minimum = 0, Maximum = 100, MinHeight = 36 });
        content.Children.Add(new ChoiceNode
        {
            ItemsSource = new[] { "家园", "附近聚落" }, SelectedIndex = 0, MinHeight = 36,
        });
        content.Children.Add(new CheckNode { Content = "跟随居民", IsChecked = true });
        return content;
    }

    private static ModalViewModel CreateDialog()
    {
        return new ModalViewModel(
            new TextNode { Text = "编辑居民档案", FontSize = 17, Foreground = Brush.Parse("#E9EFEB") },
            CreateDetails(), new ButtonNode { Content = "应用档案变更", Padding = new Thickness(8, 3), MinHeight = 30 },
            new TextNode { Text = "修改将在暂停的世界中生效", Foreground = Brush.Parse("#E7BD87") }, () => { });
    }

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
