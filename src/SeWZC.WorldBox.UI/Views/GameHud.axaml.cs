using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;

namespace SeWZC.WorldBox.UI.Views;

/// <summary>绑定主界面状态的地图边缘 HUD。</summary>
public sealed partial class GameHud : UserControl
{
    /// <summary>加载时间、选择、动态和常用操作控件。</summary>
    public GameHud()
    {
        AvaloniaXamlLoader.Load(this);
        if (Design.IsDesignMode)
            DataContext = ViewModels.PreviewModels.Hud();
    }

    /// <summary>地图工具面板的 XAML 内容。</summary>
    public static readonly StyledProperty<Control?> ToolContentProperty = AvaloniaProperty.Register<GameHud, Control?>(nameof(ToolContent));

    /// <summary>地图工具面板的 XAML 内容。</summary>
    public Control? ToolContent { get => GetValue(ToolContentProperty); set => SetValue(ToolContentProperty, value); }

    internal static bool HasStackedDock(double width) => width < 600;

    internal void Resize(double width, bool horizontalCamera)
    {
        var compact = HasStackedDock(width);
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
