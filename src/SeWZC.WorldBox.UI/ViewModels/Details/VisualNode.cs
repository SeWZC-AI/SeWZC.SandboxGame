using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Layout;
using Avalonia.Media;

namespace SeWZC.WorldBox.UI.ViewModels.Details;

/// <summary>详情与表单的展示元素状态。</summary>
internal abstract class VisualNode : INotifyPropertyChanged
{
    private string? _accessibleName;
    private string _automationId = "";
    private IBrush? _background;
    private IBrush? _borderBrush;
    private Thickness _borderThickness;
    private bool _clipToBounds;
    private int _column;
    private int _columnSpan = 1;
    private CornerRadius _cornerRadius;
    private double _fontSize = 12;
    private FontWeight _fontWeight = FontWeight.Normal;
    private IBrush? _foreground;
    private double _height = double.NaN;
    private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Stretch;
    private HorizontalAlignment _horizontalContentAlignment = HorizontalAlignment.Left;
    private bool _isEnabled = true;
    private bool _isVisible = true;
    private double _left, _top;
    private Thickness _margin;
    private double _maxHeight = double.PositiveInfinity;
    private double _maxWidth = double.PositiveInfinity;
    private double _minHeight;
    private double _minWidth;
    private string _name = "";
    private double _opacity = 1;
    private Thickness _padding;
    private int _row;
    private int _rowSpan = 1;
    private object? _tag;
    private object? _toolTip;
    private VerticalAlignment _verticalAlignment = VerticalAlignment.Stretch;
    private VerticalAlignment _verticalContentAlignment = VerticalAlignment.Center;
    private double _width = double.NaN;

    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    public string AutomationId
    {
        get => _automationId;
        set => Set(ref _automationId, value);
    }

    public string? AccessibleName
    {
        get => _accessibleName;
        set => Set(ref _accessibleName, value);
    }

    public object? Tag
    {
        get => _tag;
        set => Set(ref _tag, value);
    }

    public object? ToolTip
    {
        get => _toolTip;
        set => Set(ref _toolTip, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => Set(ref _isVisible, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => Set(ref _isEnabled, value);
    }

    public bool ClipToBounds
    {
        get => _clipToBounds;
        set => Set(ref _clipToBounds, value);
    }

    public double Opacity
    {
        get => _opacity;
        set => Set(ref _opacity, value);
    }

    public double Width
    {
        get => _width;
        set => Set(ref _width, value);
    }

    public double Height
    {
        get => _height;
        set => Set(ref _height, value);
    }

    public double MinWidth
    {
        get => _minWidth;
        set => Set(ref _minWidth, value);
    }

    public double MinHeight
    {
        get => _minHeight;
        set => Set(ref _minHeight, value);
    }

    public double MaxWidth
    {
        get => _maxWidth;
        set => Set(ref _maxWidth, value);
    }

    public double MaxHeight
    {
        get => _maxHeight;
        set => Set(ref _maxHeight, value);
    }

    public Thickness Margin
    {
        get => _margin;
        set => Set(ref _margin, value);
    }

    public Thickness Padding
    {
        get => _padding;
        set => Set(ref _padding, value);
    }

    public Thickness BorderThickness
    {
        get => _borderThickness;
        set => Set(ref _borderThickness, value);
    }

    public CornerRadius CornerRadius
    {
        get => _cornerRadius;
        set => Set(ref _cornerRadius, value);
    }

    public HorizontalAlignment HorizontalAlignment
    {
        get => _horizontalAlignment;
        set => Set(ref _horizontalAlignment, value);
    }

    public VerticalAlignment VerticalAlignment
    {
        get => _verticalAlignment;
        set => Set(ref _verticalAlignment, value);
    }

    public HorizontalAlignment HorizontalContentAlignment
    {
        get => _horizontalContentAlignment;
        set => Set(ref _horizontalContentAlignment, value);
    }

    public VerticalAlignment VerticalContentAlignment
    {
        get => _verticalContentAlignment;
        set => Set(ref _verticalContentAlignment, value);
    }

    public IBrush? Background
    {
        get => _background;
        set => Set(ref _background, value);
    }

    public IBrush? Foreground
    {
        get => _foreground;
        set => Set(ref _foreground, value);
    }

    public IBrush? BorderBrush
    {
        get => _borderBrush;
        set => Set(ref _borderBrush, value);
    }

    public double FontSize
    {
        get => _fontSize;
        set => Set(ref _fontSize, value);
    }

    public FontWeight FontWeight
    {
        get => _fontWeight;
        set => Set(ref _fontWeight, value);
    }

    public int Column
    {
        get => _column;
        set => Set(ref _column, value);
    }

    public int Row
    {
        get => _row;
        set => Set(ref _row, value);
    }

    public int ColumnSpan
    {
        get => _columnSpan;
        set => Set(ref _columnSpan, value);
    }

    public int RowSpan
    {
        get => _rowSpan;
        set => Set(ref _rowSpan, value);
    }

    public double Left
    {
        get => _left;
        set => Set(ref _left, value);
    }

    public double Top
    {
        get => _top;
        set => Set(ref _top, value);
    }

    internal VisualNode? Parent { get; set; }
    public event PropertyChangedEventHandler? PropertyChanged;
    internal event EventHandler? Detached;

    internal void Detach()
    {
        Detached?.Invoke(this, EventArgs.Empty);
    }

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    protected void Notify(string name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    protected void Adopt(object? child)
    {
        if (child is VisualNode node)
            node.Parent = this;
    }
}
