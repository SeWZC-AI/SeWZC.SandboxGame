using System.Collections.ObjectModel;
using Avalonia.Layout;

namespace SeWZC.WorldBox.UI.ViewModels.Details;

/// <summary>可换行排列的操作或预览。</summary>
internal sealed class WrapNode : VisualNode
{
    private Orientation _orientation = Orientation.Horizontal;
    public Orientation Orientation { get => _orientation; set => Set(ref _orientation, value); }
    public ObservableCollection<VisualNode> Children { get; } = [];
    public WrapNode() => Children.CollectionChanged += (_, args) =>
    {
        if (args.NewItems is not null) foreach (var child in args.NewItems) Adopt(child);
    };
}
