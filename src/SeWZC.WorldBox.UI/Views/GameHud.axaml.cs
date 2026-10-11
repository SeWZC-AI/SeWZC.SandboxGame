using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Interactivity;
using SeWZC.WorldBox.UI.ViewModels;
using Avalonia.Markup.Xaml;

namespace SeWZC.WorldBox.UI.Views;

/// <summary>绑定主界面状态的地图边缘 HUD。</summary>
public sealed partial class GameHud : UserControl
{
    /// <summary>加载时间、选择、动态和常用操作控件。</summary>
    public GameHud()
    {
        AvaloniaXamlLoader.Load(this);
        SizeChanged += (_, _) => Resize();
        DataContextChanged += (_, _) =>
        {
            if (_session is not null) _session.State.PropertyChanged -= OnStateChanged;
            _session = DataContext as GameSessionViewModel;
            if (_session is not null) _session.State.PropertyChanged += OnStateChanged;
            Resize();
        };
    }

    /// <summary>地图工具面板的 XAML 内容。</summary>
    public static readonly StyledProperty<Control?> ToolContentProperty = AvaloniaProperty.Register<GameHud, Control?>(nameof(ToolContent));

    /// <summary>地图工具面板的 XAML 内容。</summary>
    public Control? ToolContent { get => GetValue(ToolContentProperty); set => SetValue(ToolContentProperty, value); }

    private GameSessionViewModel? _session;
    private void OnStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(MainViewModel.InspectorVisible) or nameof(MainViewModel.ToolsVisible)) Resize();
    }
    private void Resize() => Resize(Bounds.Width, _session?.State.InspectorVisible == true || _session?.State.ToolsVisible == true);
    internal void ShowWorldMenu() => ((Flyout)Resources["WorldMenuFlyout"]!).ShowAt(this.FindControl<Button>("WorldMenu")!);
    private void OnWorldMenuClick(object? sender, RoutedEventArgs args)
    {
        ((Flyout)Resources["WorldMenuFlyout"]!).Hide();
        _session?.State.NavigateCommand.Execute(((Button)sender!).Tag);
    }

    internal void Resize(double width, bool horizontalCamera)
    {
        var compact = ShellLayoutViewModel.HasStackedDock(width);
        var dock = this.FindControl<Grid>("Dock")!;
        var observation = this.FindControl<Border>("ObservationDock")!;
        var time = this.FindControl<Border>("TimeDock")!;
        Grid.SetColumn(time, compact ? 0 : 2);
        Grid.SetColumnSpan(time, compact ? 3 : 1);
        Grid.SetRow(time, compact ? 1 : 0);
        time.HorizontalAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Right;
        observation.HorizontalAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        Grid.SetColumnSpan(observation, compact ? 3 : 1);
        dock.RowSpacing = compact ? 6 : 0;
        this.FindControl<TextBlock>("ViewpointLabel")!.IsVisible = width >= 540;
        this.FindControl<Border>("WorldReadout")!.MaxWidth = Math.Max(180, width - (horizontalCamera ? 140 : 90));
        var notice = this.FindControl<Border>("EventNotice")!;
        notice.Width = Math.Min(320, Math.Max(240, width - 20));
        notice.MaxHeight = 160;
        this.FindControl<Border>("SelectionSummary")!.Width = Math.Min(460, Math.Max(260, width - 20));
    }
}
