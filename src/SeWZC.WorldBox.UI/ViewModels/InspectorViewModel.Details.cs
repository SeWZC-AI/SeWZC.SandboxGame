using Avalonia;
using Avalonia.Layout;
using Avalonia.Media;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;
using SeWZC.WorldBox.UI.ViewModels.Details;

namespace SeWZC.WorldBox.UI.ViewModels;

internal sealed partial class InspectorViewModel
{
    internal readonly Dictionary<string, bool> _expandedDetails = [];

    internal StackNode FoldSection(StackNode parent, string title, string id, bool expanded = false)
    {
        var content = new StackNode { Spacing = 3, Margin = new Thickness(0) };
        var fold = Named(new ExpanderNode
        {
            Header = Text(title, 12, Mint),
            Content = content,
            Margin = new Thickness(0),
            IsExpanded = _expandedDetails.GetValueOrDefault(id, expanded),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        }, id);
        fold.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ExpanderNode.IsExpanded))
            {
                _expandedDetails[id] = fold.IsExpanded;
                RefreshInspector();
            }
        };
        parent.Children.Add(fold);
        return content;
    }

}
