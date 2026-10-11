using System.Collections;

namespace SeWZC.WorldBox.UI.ViewModels.Details;

/// <summary>选项列表与当前选择。</summary>
internal sealed class ChoiceNode : VisualNode
{
    private object[] _items = [];
    private Func<object, string>? _label;
    private int _selectedIndex = -1;

    public IEnumerable ItemsSource
    {
        get => _items;
        set
        {
            var selected = SelectedItem;
            _items = value.Cast<object>().ToArray();
            RefreshChoices();
            SelectedIndex = selected is null ? -1 : Array.IndexOf(_items, selected);
        }
    }

    public IReadOnlyList<ChoiceLabel> Choices { get; private set; } = [];

    public Func<object, string>? DisplayLabel
    {
        get => _label;
        set
        {
            _label = value;
            RefreshChoices();
        }
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (Set(ref _selectedIndex, value))
            {
                Notify(nameof(SelectedItem));
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public object? SelectedItem
    {
        get => _selectedIndex >= 0 && _selectedIndex < _items.Length ? _items[_selectedIndex] : null;
        set => SelectedIndex = value is null ? -1 : Array.IndexOf(_items, value);
    }

    public event EventHandler? SelectionChanged;

    private void RefreshChoices()
    {
        Choices = _items.Select(item => new ChoiceLabel(_label?.Invoke(item) ?? item.ToString() ?? "")).ToArray();
        Notify(nameof(Choices));
    }

    internal sealed record ChoiceLabel(string Label);
}
