using Avalonia;
using Avalonia.Controls;
using SeWZC.WorldBox.UI.ViewModels.Details;

namespace SeWZC.WorldBox.UI.Views;

/// <summary>将呈现元素的卸载通知交回展示状态。</summary>
internal sealed class NodeLifetime : AvaloniaObject
{
    public static readonly AttachedProperty<VisualNode?> NodeProperty =
        AvaloniaProperty.RegisterAttached<NodeLifetime, Control, VisualNode?>("Node");

    static NodeLifetime()
    {
        NodeProperty.Changed.AddClassHandler<Control>((control, args) =>
        {
            if (args.NewValue is VisualNode node)
                control.DetachedFromVisualTree += (_, _) => node.Detach();
        });
    }

    public static VisualNode? GetNode(Control control)
    {
        return control.GetValue(NodeProperty);
    }

    public static void SetNode(Control control, VisualNode? value)
    {
        control.SetValue(NodeProperty, value);
    }
}
