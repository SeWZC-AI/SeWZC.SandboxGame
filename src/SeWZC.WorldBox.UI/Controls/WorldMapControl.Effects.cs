using Avalonia;
using Avalonia.Media;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI.Controls;

public sealed partial class WorldMapControl
{
    private static readonly IBrush[] Clothing =
    [
        Brush(0xFF4C718B), Brush(0xFF85699B), Brush(0xFFB57951), Brush(0xFF587C65), Brush(0xFFAA9A60), Brush(0xFF96636C),
    ];

    private static readonly IBrush[] Hair =
        [Brush(0xFF513D31), Brush(0xFFC8A85A), Brush(0xFFAAA9A0), Brush(0xFF8A503C)];

    private readonly List<VisualEffect> _effects = [];
    private long _seenVisualSequence;
    private IReadOnlyList<RoutePoint> _selectedRoute = [];
    public int RenderedEffectCount { get; private set; }
    public double RenderedEffectTime { get; private set; }
    public int RenderedRouteSegmentCount { get; private set; }
    public bool ShowResidentRoute { get; set; } = true;

    private void CaptureSelectedRoute()
    {
        _selectedRoute = SelectedResidentId is { } id && Engine is not null
            ? Engine.PreviewResidentRoute(id)
            : [];
    }

    private void CaptureEffects()
    {
        if (Engine is null) return;
        var now = PresentationTime;
        _effects.RemoveAll(e => now - e.Started > e.Duration);
        foreach (var item in Engine.GetVisualsAfter(_seenVisualSequence))
            _effects.Add(new VisualEffect(item, now, item.Kind == WorldVisualKind.Meteor ? 2.8 : 1.5));
        _seenVisualSequence = Engine.VisualSequence;
        if (_effects.Count > 128) _effects.RemoveRange(0, _effects.Count - 128);
    }

    private bool HasAnimatedEffects(double now)
    {
        return _zoom >= 3 && _waterStreams.Count > 0 || _fires.Count > 0 || _effects.Any(e =>
                   now - e.Started < e.Duration && Visible(new Rect((e.Event.X - e.Event.Radius) * TilePixels - 32,
                       (e.Event.Y - e.Event.Radius) * TilePixels - 32, 64 + 2 * e.Event.Radius * TilePixels,
                       64 + 2 * e.Event.Radius * TilePixels))) ||
               _zoom >= 3 && Engine is not null &&
               (VisibleResidents(Engine.State).Any(r => r.Activity == ResidentActivity.Working) ||
                Engine.State.Settlements.Any(t =>
                    (t.ShieldTicks > 0 || t.FertilityBoostTicks > 0) &&
                    Visible(new Rect(t.X * TilePixels - 32, t.Y * TilePixels - 32, 64, 64))));
    }

    private static void Triangle(DrawingContext context, IBrush brush, Point a, Point b, Point c)
    {
        var geometry = new StreamGeometry();
        using (var draw = geometry.Open())
        {
            draw.BeginFigure(a);
            draw.LineTo(b);
            draw.LineTo(c);
            draw.EndFigure(true);
        }

        context.DrawGeometry(brush, null, geometry);
    }

    private void DrawEffects(DrawingContext context)
    {
        foreach (var effect in _effects)
        {
            var age = _renderFrameTime - effect.Started;
            if (age >= effect.Duration) continue;
            var e = effect.Event;
            var center = new Point((e.X + .5) * TilePixels, (e.Y + .5) * TilePixels);
            var p = Math.Clamp(age / effect.Duration, 0, 1);
            var extent = Math.Max(140, e.Radius * 8 + 10);
            if (!Visible(new Rect(center.X - extent, center.Y - extent, extent * 2, extent * 2))) continue;
            RenderedEffectCount++;
            RenderedEffectTime = _renderFrameTime;
            using var opacity = context.PushOpacity(1 - p * .8);
            if (e.Kind == WorldVisualKind.Meteor)
            {
                var impact = Math.Clamp(p / .48, 0, 1);
                if (impact < 1)
                {
                    var rock = center + new Vector(-90 * (1 - impact), -120 * (1 - impact));
                    Triangle(context, FlameOuter, rock + new Vector(-3, 3), rock + new Vector(3, -3),
                        rock + new Vector(-24, -35));
                    context.DrawEllipse(FlameInner, null, rock, 5, 5);
                    context.DrawEllipse(WoodBrush, null, rock, 3, 3);
                }
                else
                {
                    var wave = (p - .48) / .52;
                    context.DrawEllipse(null, new Pen(FlameInner, 1.6), center, 3 + wave * e.Radius * 8,
                        2 + wave * e.Radius * 6);
                    DrawSparks(context, center, wave, FlameOuter, 14, 10 + e.Radius * 7);
                }

                continue;
            }

            var brush = e.Kind switch
            {
                WorldVisualKind.Heal or WorldVisualKind.Harvest => HealingBrush,
                WorldVisualKind.Frost or WorldVisualKind.Rain => Brush(0xFF95DEEA),
                WorldVisualKind.Lightning => Brush(0xFFE9EEA9),
                WorldVisualKind.Waygate => ArcaneBrush,
                WorldVisualKind.Shield or WorldVisualKind.Plague => ArcaneBrush,
                WorldVisualKind.Drought or WorldVisualKind.Logging or WorldVisualKind.Construction => CargoBrush,
                _ => FlameInner,
            };
            if (e.FromX >= 0)
            {
                var source = new Point((e.FromX + .5) * TilePixels, (e.FromY + .5) * TilePixels);
                var end = source + (center - source) * Math.Min(1, p * 4 + .1);
                context.DrawLine(new Pen(brush, e.Kind == WorldVisualKind.Ember ? 2 : .8), source, end);
                context.DrawEllipse(brush, null, end, 1.8, 1.8);
            }

            if (e.Kind == WorldVisualKind.Rain)
            {
                for (var i = 0; i < 12; i++)
                {
                    var drop = center + new Vector((i % 4 - 1.5) * 8, (i / 4 - 1) * 7 + p * 15 - 12);
                    context.DrawLine(new Pen(brush, .8), drop, drop + new Vector(-2, 4));
                }
            }
            else if (e.Kind == WorldVisualKind.Lightning)
            {
                context.DrawLine(new Pen(brush, 1.4), center + new Vector(0, -14), center + new Vector(-4, -2));
                context.DrawLine(new Pen(brush, 1.4), center + new Vector(-4, -2), center + new Vector(4, -2));
                context.DrawLine(new Pen(brush, 1.4), center + new Vector(4, -2), center + new Vector(0, 10));
            }
            else if (e.Kind == WorldVisualKind.Battle)
            {
                var spread = 3 + p * 5;
                context.DrawLine(new Pen(StoneBrush, 1.5), center + new Vector(-spread, -spread),
                    center + new Vector(spread, spread));
                context.DrawLine(new Pen(FlameInner, 1.5), center + new Vector(-spread, spread),
                    center + new Vector(spread, -spread));
                DrawSparks(context, center, p, FlameOuter, 7, 12);
            }
            else if (e.Kind == WorldVisualKind.Heal)
            {
                var y = center.Y - 5 - p * 12;
                context.DrawRectangle(brush, null, new Rect(center.X - 3, y, 6, 1.4));
                context.DrawRectangle(brush, null, new Rect(center.X - .7, y - 2.3, 1.4, 6));
                context.DrawEllipse(null, new Pen(brush, .8), center, 3 + p * 9, 2 + p * 6);
            }
            else
            {
                context.DrawEllipse(null, new Pen(brush, 1.2), center, 2 + p * (6 + e.Radius * 5),
                    2 + p * (4 + e.Radius * 3));
                DrawSparks(context, center, p, brush, 8, 6 + e.Radius * 4);
            }
        }
    }

    private static void DrawSparks(DrawingContext context, Point center, double progress, IBrush brush, int count,
        double radius)
    {
        for (var i = 0; i < count; i++)
        {
            var angle = i * Math.PI * 2 / count;
            var point = center + new Vector(Math.Cos(angle), Math.Sin(angle)) * (2 + progress * radius);
            context.DrawEllipse(brush, null, point, .6 + (1 - progress), .6);
        }
    }

    private void DrawResidentSprite(DrawingContext context, Resident resident, Point position)
    {
        _residentMotion.TryGetValue(resident.Id, out var motion);
        var x = (position.X + .5) * TilePixels;
        var y = (position.Y + .5) * TilePixels;
        var moving = motion?.IsMoving(_renderMotionTime) == true;
        var phase = ((int)(_renderFrameTime * (moving ? 6 : 4)) + resident.Id) % 2;
        var pose = moving
            ? 1 + phase
            : resident.Activity is ResidentActivity.Resting or ResidentActivity.Sick
                ? 5
                : resident.Activity is ResidentActivity.Working or ResidentActivity.Studying or ResidentActivity.Casting
                    ? 3 + phase
                    : 0;
        var scale = resident.Age < 14 ? .7 : 1;
        context.DrawEllipse(ShadowBrush, null, new Point(x, y + 2.2), 1.6 * scale, .4 * scale);
        context.DrawImage(ResidentIcon(resident.Race, resident.Profession, pose),
            new Rect(x - 3.2 * scale, y + 2.2 - 8 * scale, 6.4 * scale, 8 * scale));
        if (resident.PersonalWard > 0)
            context.DrawEllipse(null, new Pen(ArcaneBrush, .35), new Point(x, y - 1.5), 4 * scale, 5 * scale);
        if (resident.FrozenUntilTick > (Engine?.State.Tick ?? 0))
            context.DrawRectangle(null, new Pen(Brush(0xFF95DEEA), .6),
                new Rect(x - 3.5 * scale, y - 6 * scale, 7 * scale, 8 * scale), 1, 1);
        DrawActivityBadge(context, resident, x + 2.7, y - 5.5, moving);
    }

    private void DrawTownEffects(DrawingContext context, WorldState state)
    {
        if (_zoom < 3) return;
        foreach (var town in state.Settlements.Where(t => t.ShieldTicks > 0 || t.FertilityBoostTicks > 0))
        {
            var center = new Point((town.X + .5) * TilePixels, (town.Y + .5) * TilePixels);
            if (!Visible(new Rect(center.X - 24, center.Y - 24, 48, 48))) continue;
            var phase = _renderFrameTime * 2;
            var brush = town.ShieldTicks > 0 ? ArcaneBrush : HealingBrush;
            for (var i = 0; i < 6; i++)
                context.DrawEllipse(brush, null,
                    center + new Vector(Math.Cos(phase + i * Math.PI / 3) * 13, Math.Sin(phase + i * Math.PI / 3) * 9),
                    .7, .7);
        }
    }

    private sealed record VisualEffect(WorldVisual Event, double Started, double Duration);
}
