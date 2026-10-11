using Avalonia.Media;
using SeWZC.WorldBox.UI.Controls;

namespace SeWZC.WorldBox.UI.ViewModels;

/// <summary>工具分页中的一个可观察选项。</summary>
internal sealed class ToolSlotViewModel : ViewModelBase
{
    private MapToolChoice? _choice;
    private bool _selected;

    /// <summary>创建固定位置的工具槽。</summary>
    /// <param name="index">当前页中的位置。</param>
    /// <param name="select">选择工具的操作。</param>
    internal ToolSlotViewModel(int index, Action<MapTool> select)
    {
        AutomationId = "tool-slot-" + index;
        SelectCommand = new UiCommand(_ =>
        {
            if (_choice is { } choice)
                select(choice.Key);
        }, () => IsEnabled);
    }

    public string AutomationId { get; }
    public string Label => _choice?.Label ?? "—";
    public string Hint => _choice?.Label ?? "此分组没有更多工具";
    public bool IsEnabled => _choice is not null;
    public IBrush Swatch => Brush.Parse(_choice?.Color ?? "#2A3C46");
    public IBrush Background => Brush.Parse(_selected ? "#2C423F" : "#111E29");
    public IBrush BorderBrush => _selected ? Brush.Parse("#B8E9BC") : Brushes.Transparent;
    public UiCommand SelectCommand { get; }
    internal MapTool? Tool => _choice?.Key;

    internal void Show(MapToolChoice? choice)
    {
        _choice = choice;
        _selected = false;
        Notify(nameof(Label));
        Notify(nameof(Hint));
        Notify(nameof(IsEnabled));
        Notify(nameof(Swatch));
        Notify(nameof(Background));
        Notify(nameof(BorderBrush));
        SelectCommand.Refresh();
    }

    internal void Select(MapTool? tool)
    {
        _selected = _choice is not null && _choice.Key == tool;
        Notify(nameof(Background));
        Notify(nameof(BorderBrush));
    }
}
