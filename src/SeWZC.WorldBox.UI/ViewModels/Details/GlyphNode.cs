using Avalonia.Media;

namespace SeWZC.WorldBox.UI.ViewModels.Details;

/// <summary>按钮图标的路径与描边。</summary>
internal sealed class GlyphNode : VisualNode
{
    private Geometry? _data;
    private Stretch _stretch = Stretch.Uniform;
    private IBrush? _stroke;
    private double _strokeThickness = 1;

    public Geometry? Data
    {
        get => _data;
        set => Set(ref _data, value);
    }

    public IBrush? Stroke
    {
        get => _stroke;
        set => Set(ref _stroke, value);
    }

    public double StrokeThickness
    {
        get => _strokeThickness;
        set => Set(ref _strokeThickness, value);
    }

    public Stretch Stretch
    {
        get => _stretch;
        set => Set(ref _stretch, value);
    }
}
