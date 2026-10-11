using Avalonia;
using Avalonia.Layout;
namespace SeWZC.WorldBox.UI.ViewModels;
/// <summary>依据窗口大小和面板状态计算地图边缘布局。</summary>
internal sealed class ShellLayoutViewModel : ViewModelBase
{
    private readonly GameSessionViewModel _session;
    private Orientation _cameraOrientation;
    private double _inspectorWidth, _inspectorMaxHeight, _inspectorHeight, _toolWidth, _toolMaxHeight, _placementMaxWidth;
    private Thickness _inspectorMargin;
    private VerticalAlignment _inspectorAlignment;
    private int _toolColumns, _toolRows;
    private bool _toolHintVisible;
    private double _contextWidth, _titleSize, _mapWidth;
    /// <summary>连接布局依据的会话状态。</summary>
    /// <param name="session">当前游戏会话。</param>
    internal ShellLayoutViewModel(GameSessionViewModel session) => _session = session;
    internal double Width { get; private set; } = 1440;
    internal double Height { get; private set; } = 920;
    internal bool IsCompact => Width < 980;
    internal static bool HasStackedDock(double width) => width < 600;
    public Orientation CameraOrientation { get => _cameraOrientation; private set => Set(ref _cameraOrientation, value); }
    public double InspectorWidth { get => _inspectorWidth; private set => Set(ref _inspectorWidth, value); }
    public double InspectorMaxHeight { get => _inspectorMaxHeight; private set => Set(ref _inspectorMaxHeight, value); }
    public double InspectorHeight { get => _inspectorHeight; private set => Set(ref _inspectorHeight, value); }
    public Thickness InspectorMargin { get => _inspectorMargin; private set => Set(ref _inspectorMargin, value); }
    public VerticalAlignment InspectorAlignment { get => _inspectorAlignment; private set => Set(ref _inspectorAlignment, value); }
    public int ToolColumns { get => _toolColumns; private set => Set(ref _toolColumns, value); }
    public int ToolRows { get => _toolRows; private set => Set(ref _toolRows, value); }
    public double ToolWidth { get => _toolWidth; private set => Set(ref _toolWidth, value); }
    public double ToolMaxHeight { get => _toolMaxHeight; private set => Set(ref _toolMaxHeight, value); }
    public double PlacementMaxWidth { get => _placementMaxWidth; private set => Set(ref _placementMaxWidth, value); }
    public bool ToolHintVisible { get => _toolHintVisible; private set => Set(ref _toolHintVisible, value); }
    public double ContextWidth { get => _contextWidth; private set => Set(ref _contextWidth, value); }
    public double TitleSize { get => _titleSize; private set => Set(ref _titleSize, value); }
    internal void Resize(double width, double height, double mapWidth)
    {
        Width = width; Height = height; _mapWidth = mapWidth;
        Refresh();
    }
    internal void Refresh()
    {
        var state = _session.State;
        state.IsCompactHud = IsCompact;
        state.IsShortHud = Height < 540;
        var stacked = HasStackedDock(Width);
        _session.Map.ScaleBottomInset = stacked ? 138 : 88;
        CameraOrientation = state.InspectorVisible || state.ToolsVisible ? Orientation.Horizontal : Orientation.Vertical;
        InspectorWidth = IsCompact ? Math.Max(280, Width - 20)
            : state.InspectorMode == "research" && _session.Inspector._researchExpanded ? Math.Max(360, Width - 20) : 360;
        var top = IsCompact && state.CanReturnObservation ? 130 : 90;
        var bottom = stacked ? 126 : 90;
        var space = Math.Max(0, Height - top - bottom);
        InspectorMaxHeight = IsCompact && !state.InspectorExpanded ? Math.Min(space, Math.Clamp(space * .7, 140, 430)) : space;
        InspectorHeight = state.InspectorExpanded ? space : double.NaN;
        InspectorAlignment = IsCompact ? VerticalAlignment.Bottom : VerticalAlignment.Top;
        InspectorMargin = new Thickness(10, top, 10, bottom);
        var compactTools = Width < 700;
        ToolColumns = compactTools ? 4 : 8;
        ToolRows = compactTools ? 2 : 1;
        ToolWidth = Math.Max(280, Math.Min(650, _mapWidth - 20));
        ToolMaxHeight = Math.Max(80, Height - (stacked ? 240 : 180) - (state.CanReturnObservation ? 40 : 0));
        PlacementMaxWidth = Math.Max(120, Math.Min(280, _mapWidth - 60));
        ToolHintVisible = Width > 600;
        ContextWidth = compactTools ? 108 : 132;
        TitleSize = compactTools ? 12 : 13;
        _session.Dialogs.UpdateBounds();
        _session.Navigation.RefreshSelectionSummary();
    }
}
