using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace SeWZC.WorldBox.UI.ViewModels.Details;

/// <summary>按行列排列的详情内容。</summary>
internal sealed class GridNode : VisualNode
{
    private ColumnDefinitions _columnDefinitions = new();
    private double _columnSpacing;
    private RowDefinitions _rowDefinitions = new();
    private double _rowSpacing;

    public GridNode()
    {
        Children.CollectionChanged += (_, args) =>
        {
            if (args.NewItems is not null)
                foreach (var child in args.NewItems)
                    Adopt(child);
        };
    }

    public ColumnDefinitions ColumnDefinitions
    {
        get => _columnDefinitions;
        set => Set(ref _columnDefinitions, value);
    }

    public RowDefinitions RowDefinitions
    {
        get => _rowDefinitions;
        set => Set(ref _rowDefinitions, value);
    }

    public double ColumnSpacing
    {
        get => _columnSpacing;
        set => Set(ref _columnSpacing, value);
    }

    public double RowSpacing
    {
        get => _rowSpacing;
        set => Set(ref _rowSpacing, value);
    }

    public ObservableCollection<VisualNode> Children { get; } = [];

    public static void SetColumn(VisualNode item, int value)
    {
        item.Column = value;
    }

    public static void SetRow(VisualNode item, int value)
    {
        item.Row = value;
    }

    public static void SetColumnSpan(VisualNode item, int value)
    {
        item.ColumnSpan = value;
    }

    public static void SetRowSpan(VisualNode item, int value)
    {
        item.RowSpan = value;
    }
}
