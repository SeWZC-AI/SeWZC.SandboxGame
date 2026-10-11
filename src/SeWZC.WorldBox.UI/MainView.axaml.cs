using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;
using SeWZC.WorldBox.UI.ViewModels;
using SeWZC.WorldBox.UI.Views;

namespace SeWZC.WorldBox.UI;

/// <summary>桌面与浏览器共用的游戏主界面。</summary>
public sealed partial class MainView : UserControl
{
    private readonly WorldMapControl _map;
    private readonly GameSessionViewModel _session;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(50) };

    /// <summary>初始化游戏主界面。</summary>
    public MainView() : this(null) { }

    /// <summary>初始化游戏主界面。</summary>
    /// <param name="engine">初始世界引擎，空值时创建默认示例世界。</param>
    public MainView(WorldEngine? engine)
    {
        AvaloniaXamlLoader.Load(this);
        _map = this.FindControl<WorldMapControl>("Map")!;
        if (Design.IsDesignMode)
        {
            _session = DesignData.Session;
            _map.Engine = _session.State.Engine;
            _map.IsSimulationPaused = true;
        }
        else
        {
            _session = new GameSessionViewModel(new MainViewModel(engine ?? WorldEngine.Create(73921)),
                new MapPresentationAdapter(_map), () => App.Storage);
            DataContext = _session;
        }

        _session.FocusRequested += () => Focus();
        _session.WorldMenuRequested += () => this.FindControl<GameHud>("Hud")!.ShowWorldMenu();
        _session.LayoutRestorationRequested += action => Dispatcher.UIThread.Post(action, DispatcherPriority.Loaded);
        _session.ScheduleChanged += UpdateTimerInterval;
        SizeChanged += (_, _) => Resize();
        _map.SizeChanged += (_, _) => Resize();
        KeyDown += OnKeyDown;
        _timer.Tick += (_, _) => _session.Tick();
        AttachedToVisualTree += async (_, _) =>
        {
            if (Design.IsDesignMode)
                return;
            await _session.StartAsync();
            _session.ResetTimeAnchor();
            _timer.Start();
        };
        DetachedFromVisualTree += (_, _) => _timer.Stop();
    }

    private void Resize()
    {
        _session.Layout.Resize(Bounds.Width, Bounds.Height, _map.Bounds.Width);
    }

    private void UpdateTimerInterval()
    {
        _timer.Interval = TimeSpan.FromMilliseconds(_session.NextDelayMilliseconds);
    }

    private void OnKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key == Key.Escape)
        {
            _session.Interaction.Escape();
            args.Handled = true;
        }
        else if (args.Key == Key.Space && !IsEditingText() && !_session.Dialogs.IsVisible &&
                 !_session.Interaction.IsPickingLocation)
        {
            _session.TogglePause();
            args.Handled = true;
        }
    }

    private bool IsEditingText()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        return focused is TextBox || (focused is Control control &&
                                      control.GetVisualAncestors().Any(ancestor => ancestor is TextBox));
    }
}
