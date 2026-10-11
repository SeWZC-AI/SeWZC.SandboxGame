using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Layout;
using Avalonia.Media;

namespace SeWZC.WorldBox.UI.ViewModels.Details;

/// <summary>详情与表单的展示元素状态。</summary>
internal abstract class VisualNode : INotifyPropertyChanged
{
    private string _name = "";
    public string Name { get => _name; set => Set(ref _name, value); }
    private string _automationId = "";
    public string AutomationId { get => _automationId; set => Set(ref _automationId, value); }
    private string? _accessibleName = null;
    public string? AccessibleName { get => _accessibleName; set => Set(ref _accessibleName, value); }
    private object? _tag = null;
    public object? Tag { get => _tag; set => Set(ref _tag, value); }
    private object? _toolTip = null;
    public object? ToolTip { get => _toolTip; set => Set(ref _toolTip, value); }
    private bool _isVisible = true;
    public bool IsVisible { get => _isVisible; set => Set(ref _isVisible, value); }
    private bool _isEnabled = true;
    public bool IsEnabled { get => _isEnabled; set => Set(ref _isEnabled, value); }
    private bool _clipToBounds = false;
    public bool ClipToBounds { get => _clipToBounds; set => Set(ref _clipToBounds, value); }
    private double _opacity = 1;
    public double Opacity { get => _opacity; set => Set(ref _opacity, value); }
    private double _width = double.NaN;
    public double Width { get => _width; set => Set(ref _width, value); }
    private double _height = double.NaN;
    public double Height { get => _height; set => Set(ref _height, value); }
    private double _minWidth = 0;
    public double MinWidth { get => _minWidth; set => Set(ref _minWidth, value); }
    private double _minHeight = 0;
    public double MinHeight { get => _minHeight; set => Set(ref _minHeight, value); }
    private double _maxWidth = double.PositiveInfinity;
    public double MaxWidth { get => _maxWidth; set => Set(ref _maxWidth, value); }
    private double _maxHeight = double.PositiveInfinity;
    public double MaxHeight { get => _maxHeight; set => Set(ref _maxHeight, value); }
    private Thickness _margin = default;
    public Thickness Margin { get => _margin; set => Set(ref _margin, value); }
    private Thickness _padding = default;
    public Thickness Padding { get => _padding; set => Set(ref _padding, value); }
    private Thickness _borderThickness = default;
    public Thickness BorderThickness { get => _borderThickness; set => Set(ref _borderThickness, value); }
    private CornerRadius _cornerRadius = default;
    public CornerRadius CornerRadius { get => _cornerRadius; set => Set(ref _cornerRadius, value); }
    private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Stretch;
    public HorizontalAlignment HorizontalAlignment { get => _horizontalAlignment; set => Set(ref _horizontalAlignment, value); }
    private VerticalAlignment _verticalAlignment = VerticalAlignment.Stretch;
    public VerticalAlignment VerticalAlignment { get => _verticalAlignment; set => Set(ref _verticalAlignment, value); }
    private HorizontalAlignment _horizontalContentAlignment = HorizontalAlignment.Left;
    public HorizontalAlignment HorizontalContentAlignment { get => _horizontalContentAlignment; set => Set(ref _horizontalContentAlignment, value); }
    private VerticalAlignment _verticalContentAlignment = VerticalAlignment.Center;
    public VerticalAlignment VerticalContentAlignment { get => _verticalContentAlignment; set => Set(ref _verticalContentAlignment, value); }
    private IBrush? _background = null;
    public IBrush? Background { get => _background; set => Set(ref _background, value); }
    private IBrush? _foreground = null;
    public IBrush? Foreground { get => _foreground; set => Set(ref _foreground, value); }
    private IBrush? _borderBrush = null;
    public IBrush? BorderBrush { get => _borderBrush; set => Set(ref _borderBrush, value); }
    private double _fontSize = 12;
    public double FontSize { get => _fontSize; set => Set(ref _fontSize, value); }
    private FontWeight _fontWeight = FontWeight.Normal;
    public FontWeight FontWeight { get => _fontWeight; set => Set(ref _fontWeight, value); }
    private int _column = 0;
    public int Column { get => _column; set => Set(ref _column, value); }
    private int _row = 0;
    public int Row { get => _row; set => Set(ref _row, value); }
    private int _columnSpan = 1;
    public int ColumnSpan { get => _columnSpan; set => Set(ref _columnSpan, value); }
    private int _rowSpan = 1;
    public int RowSpan { get => _rowSpan; set => Set(ref _rowSpan, value); }
    private double _left, _top;
    public double Left { get => _left; set => Set(ref _left, value); }
    public double Top { get => _top; set => Set(ref _top, value); }
    public event PropertyChangedEventHandler? PropertyChanged;
    internal event EventHandler? Detached;
    internal VisualNode? Parent { get; set; }
    internal void Detach() => Detached?.Invoke(this, EventArgs.Empty);
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
    protected void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    protected void Adopt(object? child) { if (child is VisualNode node) node.Parent = this; }
}
