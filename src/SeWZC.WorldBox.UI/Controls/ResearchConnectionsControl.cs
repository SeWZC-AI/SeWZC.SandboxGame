using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.ViewModels;

namespace SeWZC.WorldBox.UI.Controls;

/// <summary>绘制研究分支背景、连接与所选路径。</summary>
public sealed class ResearchConnectionsControl : Control
{

    private static readonly IBrush Locked = Brush.Parse("#496075"),
        Done = Brush.Parse("#65B995"),
        Path = Brush.Parse("#F1CD83"),
        Magic = Brush.Parse("#A996D8");

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        if (DataContext is not ResearchGraphViewModel model) return;
        var ancestors = new HashSet<Advancement>();

        void Visit(Advancement kind)
        {
            if (!ancestors.Add(kind))
                return;
            foreach (var p in kind.Prerequisites)
                Visit(p);
        }

        if (model.ShowFullPath)
            Visit(model.Selected);
        else
        {
            ancestors.Add(model.Selected);
            foreach (var p in model.Selected.Prerequisites)
                ancestors.Add(p);
        }

        using var scale = context.PushTransform(Matrix.CreateScale(model.Zoom, model.Zoom));
        var laneBottom = model.Layout.Nodes.Max(n => n.Value.Bottom) + 16;
        foreach (var lane in model.Layout.Lanes)
            context.DrawRectangle(Brush.Parse("#10212D"), null,
                new Rect(lane.Left, 38, lane.Width, laneBottom - 38), 8, 8);
        // 选中前置路径最后绘制，使交叉处仍能清晰追踪。
        foreach (var edge in
                 model.Layout.Edges.OrderBy(e => ancestors.Contains(e.To) && ancestors.Contains(e.From)))
        {
            var selected = model.ShowFullPath
                ? ancestors.Contains(edge.To) && ancestors.Contains(edge.From)
                : edge.To == model.Selected;
            var brush = selected ? Path
                : model.IsCompleted(edge.From) && model.IsCompleted(edge.To) ? Done
                : edge.To.Magic ? Magic : Locked;
            var pen = new Pen(brush, selected ? 2.4 : 1.6);
            var geometry = new StreamGeometry();
            using (var path = geometry.Open())
            {
                path.BeginFigure(edge.Points[0], false);
                if (edge.Points.Length == 4)
                    path.CubicBezierTo(edge.Points[1], edge.Points[2], edge.Points[3]);
                else
                {
                    foreach (var point in edge.Points.Skip(1))
                        path.LineTo(point);
                }

                path.EndFigure(false);
            }

            context.DrawGeometry(null, pen, geometry);
            var tip = edge.Points[^1];
            context.DrawLine(pen, tip, tip + new Vector(-4, -6));
            context.DrawLine(pen, tip, tip + new Vector(4, -6));
        }
    }
}
