using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace SeWZC.WorldBox.UI.ViewModels.Details;

/// <summary>按行列排列的详情内容。</summary>
internal sealed class GridNode : VisualNode
{
    private ColumnDefinitions _columnDefinitions = new();
    public ColumnDefinitions ColumnDefinitions { get => _columnDefinitions; set => Set(ref _columnDefinitions, value); }
    private RowDefinitions _rowDefinitions = new();
    public RowDefinitions RowDefinitions { get => _rowDefinitions; set => Set(ref _rowDefinitions, value); }
    private double _columnSpacing = 0;
    public double ColumnSpacing { get => _columnSpacing; set => Set(ref _columnSpacing, value); }
    private double _rowSpacing = 0;
    public double RowSpacing { get => _rowSpacing; set => Set(ref _rowSpacing, value); }
    public ObservableCollection<VisualNode> Children { get; } = [];
    public GridNode() => Children.CollectionChanged += (_, args) =>
    {
        if (args.NewItems is not null) foreach (var child in args.NewItems) Adopt(child);
    };
    public static void SetColumn(VisualNode item, int value) => item.Column = value;
    public static void SetRow(VisualNode item, int value) => item.Row = value;
    public static void SetColumnSpan(VisualNode item, int value) => item.ColumnSpan = value;
    public static void SetRowSpan(VisualNode item, int value) => item.RowSpan = value;
}
