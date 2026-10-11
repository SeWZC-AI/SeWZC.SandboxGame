using System.Collections.ObjectModel;
using Avalonia.Layout;

namespace SeWZC.WorldBox.UI.ViewModels.Details;

/// <summary>按顺序排列的详情字段与行。</summary>
internal sealed class StackNode : VisualNode
{
    private Orientation _orientation = Orientation.Vertical;
    private double _spacing;

    public StackNode()
    {
        Children.CollectionChanged += (_, args) =>
        {
            if (args.NewItems is not null)
                foreach (var child in args.NewItems)
                    Adopt(child);
        };
    }

    public Orientation Orientation
    {
        get => _orientation;
        set => Set(ref _orientation, value);
    }

    public double Spacing
    {
        get => _spacing;
        set => Set(ref _spacing, value);
    }

    public ObservableCollection<VisualNode> Children { get; } = [];
}
