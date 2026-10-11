using System.Collections.ObjectModel;

namespace SeWZC.WorldBox.UI.ViewModels.Details;

/// <summary>研究树节点与通道标题的呈现坐标。</summary>
internal sealed class CanvasNode : VisualNode
{
    public CanvasNode()
    {
        Children.CollectionChanged += (_, args) =>
        {
            if (args.NewItems is not null)
                foreach (var child in args.NewItems)
                    Adopt(child);
        };
    }

    public ObservableCollection<VisualNode> Children { get; } = [];
}
