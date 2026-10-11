using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.ViewModels.Details;

namespace SeWZC.WorldBox.UI.Tests;

/// <summary>动态表单的选择语义与呈现状态。</summary>
public sealed class DetailPresentationTests
{
    /// <summary>中文标签只用于显示，读取字段仍得到实际枚举值。</summary>
    [Fact]
    public void Choosing_display_label_returns_original_enum_value()
    {
        var field = new ChoiceNode
        {
            ItemsSource = new[] { RaceKind.Human, RaceKind.Elf },
            DisplayLabel = value => (RaceKind)value == RaceKind.Human ? "人类" : "精灵",
            SelectedItem = RaceKind.Human,
        };
        field.SelectedIndex = 1;

        Assert.Equal("精灵", field.Choices[1].Label);
        Assert.Equal(RaceKind.Elf, field.SelectedItem);
    }

    /// <summary>候选集合刷新后保留仍可选择的对象，失效对象不沿用旧索引。</summary>
    [Fact]
    public void Refreshing_choices_preserves_available_selection_and_clears_removed_selection()
    {
        var field = new ChoiceNode { ItemsSource = new[] { 7, 9 }, SelectedItem = 9 };
        field.ItemsSource = new[] { 9, 11 };
        Assert.Equal(9, field.SelectedItem);
        Assert.Equal(0, field.SelectedIndex);

        field.ItemsSource = new[] { 11 };
        Assert.Null(field.SelectedItem);
        Assert.Equal(-1, field.SelectedIndex);
    }

    /// <summary>列表行重排保留原展示状态及通知订阅，不丢弃当前行。</summary>
    [Fact]
    public void Reordering_live_rows_keeps_row_state_and_notifications()
    {
        var rows = new StackNode();
        var first = new TextNode { Text = "第一行" };
        var second = new TextNode { Text = "第二行" };
        rows.Children.Add(first);
        rows.Children.Add(second);
        var changes = new List<string?>();
        second.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        rows.Children.Remove(second);
        rows.Children.Insert(0, second);
        second.Text = "已有行的更新";

        Assert.Same(second, rows.Children[0]);
        Assert.Contains(nameof(TextNode.Text), changes);
        Assert.Same(rows, second.Parent);
    }
}
