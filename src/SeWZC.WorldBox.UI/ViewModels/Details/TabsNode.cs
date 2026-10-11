
namespace SeWZC.WorldBox.UI.ViewModels.Details;

/// <summary>编辑表单的分页及当前页。</summary>
internal sealed class TabsNode : VisualNode
{
    private IReadOnlyList<TabNode> _itemsSource = Array.Empty<TabNode>();
    public IReadOnlyList<TabNode> ItemsSource { get => _itemsSource; set => Set(ref _itemsSource, value); }
    private int _selectedIndex = 0;
    public int SelectedIndex { get => _selectedIndex; set => Set(ref _selectedIndex, value); }
}
