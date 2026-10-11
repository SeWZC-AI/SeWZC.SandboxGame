using Avalonia;
using Avalonia.Media;

namespace SeWZC.WorldBox.UI.ViewModels.Details;

/// <summary>按钮图标的路径与描边。</summary>
internal sealed class GlyphNode : VisualNode
{
    private Geometry? _data = null;
    public Geometry? Data { get => _data; set => Set(ref _data, value); }
    private IBrush? _stroke = null;
    public IBrush? Stroke { get => _stroke; set => Set(ref _stroke, value); }
    private double _strokeThickness = 1;
    public double StrokeThickness { get => _strokeThickness; set => Set(ref _strokeThickness, value); }
    private Stretch _stretch = Stretch.Uniform;
    public Stretch Stretch { get => _stretch; set => Set(ref _stretch, value); }
}
