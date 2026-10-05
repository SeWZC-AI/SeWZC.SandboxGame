using Avalonia;
using Avalonia.Media;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI.Controls;

public sealed partial class WorldMapControl
{
    private bool ShowVehicle(Resident person)
    {
        return person.TravelMode == TravelMode.Aircraft
               || (person.TravelMode == TravelMode.Boat && Engine is not null &&
                   WorldEngine.IsWaterTerrain(Engine.State.Tiles[person.Y * Engine.State.Width + person.X].Terrain));
    }

    private void DrawVehicles(DrawingContext context, WorldState state)
    {
        foreach (var person in VisibleResidents(state))
        {
            if (!ShowVehicle(person) || (_zoom >= 3 && person.TravelMode != TravelMode.Aircraft)) continue;
            var point = ResidentMapPosition(person.Id, person.X, person.Y);
            if (!Visible(new Rect(point.X - 6, point.Y - 6, 12, 12))) continue;
            DrawVehicle(context, person, point);
        }
    }

    private void DrawVehicle(DrawingContext context, Resident person, Point point)
    {
        if (person.TravelMode == TravelMode.Aircraft)
        {
            context.DrawEllipse(ShadowBrush, null, new Point(point.X + 2, point.Y + 4), 4, 1);
            var angle = Math.Atan2(person.Y - person.FromY, person.X - person.FromX);
            using (context.PushTransform(Matrix.CreateRotation(angle) * Matrix.CreateTranslation(point.X, point.Y)))
            {
                context.DrawRectangle(StoneBrush, null, new Rect(-4, -.7, 8, 1.4));
                context.DrawRectangle(AcademyBrush, null, new Rect(-.8, -4, 1.8, 8));
                context.DrawRectangle(StoneBrush, null, new Rect(-3.5, -2, 1, 4));
                context.DrawRectangle(MessageBrush, null, new Rect(2, -.5, 1.5, 1));
            }
        }
        else
        {
            context.DrawEllipse(WoodBrush, null, point, 4, 1.7);
            context.DrawLine(new Pen(MessageBrush, .5), new Point(point.X, point.Y), new Point(point.X, point.Y - 5));
            Triangle(context, MessageBrush, new Point(point.X, point.Y - 5), new Point(point.X, point.Y - 1),
                new Point(point.X + 3, point.Y - 1));
        }
    }
}
