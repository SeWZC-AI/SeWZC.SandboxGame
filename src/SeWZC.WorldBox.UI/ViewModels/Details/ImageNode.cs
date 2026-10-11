using Avalonia.Media;

namespace SeWZC.WorldBox.UI.ViewModels.Details;

/// <summary>图标预览的图片与尺寸。</summary>
internal sealed class ImageNode : VisualNode
{
    private IImage? _source;
    private Stretch _stretch = Stretch.Uniform;

    public IImage? Source
    {
        get => _source;
        set => Set(ref _source, value);
    }

    public Stretch Stretch
    {
        get => _stretch;
        set => Set(ref _stretch, value);
    }
}
