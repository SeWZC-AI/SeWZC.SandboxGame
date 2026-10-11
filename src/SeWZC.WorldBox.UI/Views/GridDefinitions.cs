using Avalonia;
using Avalonia.Controls;

namespace SeWZC.WorldBox.UI.Views;

/// <summary>为 Grid 的行列集合提供 XAML 绑定入口。</summary>
internal sealed class GridDefinitions : AvaloniaObject
{
    public static readonly AttachedProperty<ColumnDefinitions?> ColumnsProperty =
        AvaloniaProperty.RegisterAttached<GridDefinitions, Grid, ColumnDefinitions?>("Columns");

    public static readonly AttachedProperty<RowDefinitions?> RowsProperty =
        AvaloniaProperty.RegisterAttached<GridDefinitions, Grid, RowDefinitions?>("Rows");

    static GridDefinitions()
    {
        ColumnsProperty.Changed.AddClassHandler<Grid>((grid, args) =>
            grid.ColumnDefinitions = (ColumnDefinitions)args.NewValue!);
        RowsProperty.Changed.AddClassHandler<Grid>((grid, args) =>
            grid.RowDefinitions = (RowDefinitions)args.NewValue!);
    }

    public static ColumnDefinitions? GetColumns(Grid grid)
    {
        return grid.GetValue(ColumnsProperty);
    }

    public static void SetColumns(Grid grid, ColumnDefinitions? value)
    {
        grid.SetValue(ColumnsProperty, value);
    }

    public static RowDefinitions? GetRows(Grid grid)
    {
        return grid.GetValue(RowsProperty);
    }

    public static void SetRows(Grid grid, RowDefinitions? value)
    {
        grid.SetValue(RowsProperty, value);
    }
}
