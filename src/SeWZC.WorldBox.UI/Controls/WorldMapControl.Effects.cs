using Avalonia;
using Avalonia.Media;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI.Controls;

public sealed partial class WorldMapControl
{
    private sealed record VisualEffect(WorldVisual Event, double Started, double Duration);
    private readonly List<VisualEffect> _effects = [];
    private long _seenVisualSequence;
    private IReadOnlyList<RoutePoint> _selectedRoute = [];
    public int RenderedEffectCount { get; private set; }
    public double RenderedEffectTime { get; private set; }
    public int RenderedRouteSegmentCount { get; private set; }
    public bool ShowResidentRoute { get; set; } = true;
    private static readonly IBrush[] Clothing = [Brush(0xFF4C718B), Brush(0xFF85699B), Brush(0xFFB57951), Brush(0xFF587C65), Brush(0xFFAA9A60), Brush(0xFF96636C)];
    private static readonly IBrush[] Hair = [Brush(0xFF513D31), Brush(0xFFC8A85A), Brush(0xFFAAA9A0), Brush(0xFF8A503C)];

    private void CaptureSelectedRoute() => _selectedRoute = SelectedResidentId is { } id && Engine is not null
        ? Engine.PreviewResidentRoute(id) : [];

    private void CaptureEffects()
    {
        if (Engine is null) return;
        var now = PresentationTime;
        _effects.RemoveAll(e => now - e.Started > e.Duration);
        foreach (var item in Engine.GetVisualsAfter(_seenVisualSequence))
            _effects.Add(new(item, now, item.Kind == WorldVisualKind.Meteor ? 2.8 : 1.5));
        _seenVisualSequence = Engine.VisualSequence;
        if (_effects.Count > 128) _effects.RemoveRange(0, _effects.Count - 128);
    }

    private bool HasAnimatedEffects(double now) => _fires.Count > 0 || _effects.Any(e => now - e.Started < e.Duration && Visible(new Rect((e.Event.X - e.Event.Radius) * TilePixels - 32, (e.Event.Y - e.Event.Radius) * TilePixels - 32, 64 + 2 * e.Event.Radius * TilePixels, 64 + 2 * e.Event.Radius * TilePixels))) ||
        _zoom >= 3 && Engine is not null && (VisibleResidents(Engine.State).Any(r => r.Activity == ResidentActivity.Working) ||
        Engine.State.Settlements.Any(t => (t.ShieldTicks > 0 || t.FertilityBoostTicks > 0) && Visible(new Rect(t.X * TilePixels - 32, t.Y * TilePixels - 32, 64, 64))));

    private static void Triangle(DrawingContext context, IBrush brush, Point a, Point b, Point c)
    {
        var geometry = new StreamGeometry();
        using (var draw = geometry.Open())
        {
            draw.BeginFigure(a, true); draw.LineTo(b); draw.LineTo(c); draw.EndFigure(true);
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
            RenderedEffectCount++; RenderedEffectTime = _renderFrameTime;
            using var opacity = context.PushOpacity(1 - p * .8);
            if (e.Kind == WorldVisualKind.Meteor)
            {
                var impact = Math.Clamp(p / .48, 0, 1);
                if (impact < 1)
                {
                    var rock = center + new Vector(-90 * (1 - impact), -120 * (1 - impact));
                    Triangle(context, FlameOuter, rock + new Vector(-3, 3), rock + new Vector(3, -3), rock + new Vector(-24, -35));
                    context.DrawEllipse(FlameInner, null, rock, 5, 5);
                    context.DrawEllipse(WoodBrush, null, rock, 3, 3);
                }
                else
                {
                    var wave = (p - .48) / .52;
                    context.DrawEllipse(null, new Pen(FlameInner, 1.6), center, 3 + wave * e.Radius * 8, 2 + wave * e.Radius * 6);
                    DrawSparks(context, center, wave, FlameOuter, 14, 10 + e.Radius * 7);
                }
                continue;
            }
            var brush = e.Kind switch
            {
                WorldVisualKind.Heal or WorldVisualKind.Harvest => HealingBrush,
                WorldVisualKind.Shield or WorldVisualKind.Plague => ArcaneBrush,
                WorldVisualKind.Drought or WorldVisualKind.Logging or WorldVisualKind.Construction => CargoBrush,
                _ => FlameInner
            };
            if (e.FromX >= 0)
            {
                var source = new Point((e.FromX + .5) * TilePixels, (e.FromY + .5) * TilePixels);
                var end = source + (center - source) * Math.Min(1, p * 4 + .1);
                context.DrawLine(new Pen(brush, e.Kind == WorldVisualKind.Ember ? 2 : .8), source, end);
                context.DrawEllipse(brush, null, end, 1.8, 1.8);
            }
            if (e.Kind == WorldVisualKind.Battle)
            {
                var spread = 3 + p * 5;
                context.DrawLine(new Pen(StoneBrush, 1.5), center + new Vector(-spread, -spread), center + new Vector(spread, spread));
                context.DrawLine(new Pen(FlameInner, 1.5), center + new Vector(-spread, spread), center + new Vector(spread, -spread));
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
                context.DrawEllipse(null, new Pen(brush, 1.2), center, 2 + p * (6 + e.Radius * 5), 2 + p * (4 + e.Radius * 3));
                DrawSparks(context, center, p, brush, 8, 6 + e.Radius * 4);
            }
        }
    }

    private static void DrawSparks(DrawingContext context, Point center, double progress, IBrush brush, int count, double radius)
    {
        for (var i = 0; i < count; i++)
        {
            var angle = i * Math.PI * 2 / count;
            var point = center + new Vector(Math.Cos(angle), Math.Sin(angle)) * (2 + progress * radius);
            context.DrawEllipse(brush, null, point, .6 + (1 - progress), .6);
        }
    }

    private static readonly IBrush[] Leaves = [Brush(0xFF375F43), Brush(0xFF547B48), Brush(0xFF749458)];
    private void DrawTerrainDetails(DrawingContext context, WorldState state)
    {
        if (_zoom < 7) return;
        var size = TilePixels * _zoom;
        var minX = Math.Clamp((int)Math.Floor(-_origin.X / size) - 1, 0, state.Width - 1);
        var minY = Math.Clamp((int)Math.Floor(-_origin.Y / size) - 1, 0, state.Height - 1);
        var maxX = Math.Clamp((int)Math.Ceiling((Bounds.Width - _origin.X) / size), 0, state.Width - 1);
        var maxY = Math.Clamp((int)Math.Ceiling((Bounds.Height - _origin.Y) / size), 0, state.Height - 1);
        for (var ty = minY; ty <= maxY; ty++) for (var tx = minX; tx <= maxX; tx++)
        {
            var tile = state.Tiles[ty * state.Width + tx];
            var noise = PixelCanvas.Noise(tx, ty, state.Seed);
            var x = tx * TilePixels; var y = ty * TilePixels;
            if (tile.Terrain == TerrainType.Forest && tile.ResourceAmount >= 25 && tile.RoadLevel == 0)
            {
                var trunk = new Point(x + 3.5, y + 6);
                context.DrawLine(new Pen(WoodBrush, .65), trunk, trunk + new Vector(0, -4));
                if (noise % 3 == 1)
                {
                    for (var level = 0; level < 3; level++)
                        Triangle(context, Leaves[level], new(x + 1 + level * .65, y + 5 - level), new(x + 6 - level * .65, y + 5 - level), new(x + 3.5, y + .6 + level * .4));
                }
                else
                {
                    var fullness = tile.ResourceAmount < 50 ? .8 : 1.3;
                    for (var i = 0; i < 4; i++)
                        context.DrawEllipse(Leaves[(i + noise) % 3], null, new Point(x + 2 + i % 2 * 2, y + 2 + i / 2 * 1.5), fullness + .4, fullness);
                }
                context.DrawLine(new Pen(Leaves[2], .2), new(x + 3, y + 1.3), new(x + 2.2, y + 2.6));
            }
            else if (tile.Terrain is TerrainType.Grass or TerrainType.Wetland or TerrainType.Tundra && tile.RoadLevel == 0)
            {
                var px = x + 1 + noise % 6; var py = y + 2 + (noise >> 4) % 5;
                context.DrawLine(new Pen(Leaves[0], .18), new(px, py), new(px - .3, py - 1));
                context.DrawLine(new Pen(Leaves[1], .2), new(px, py), new(px + .6, py - .8));
                if (tile.ResourceAmount > 50 && noise % 5 == 0)
                    context.DrawEllipse(noise % 2 == 0 ? HealingBrush : CargoBrush, null, new(px + .6, py - 1), .28, .25);
            }
            else if (tile.Terrain is TerrainType.Mountain or TerrainType.Hills)
            {
                context.DrawLine(new Pen(StoneBrush, .2), new(x + 3.2, y + 2), new(x + 2, y + 5));
                context.DrawLine(new Pen(WoodBrush, .2), new(x + 4, y + 4), new(x + 4.8, y + 5));
            }
        }
    }

    private void DrawCloseDetails(DrawingContext context, WorldState state)
    {
        if (_zoom < 3) return;
        // Only visible residents get detailed geometry; world overview retains batched silhouettes.
        foreach (var resident in VisibleResidents(state))
        {
            var position = _residentMotion.TryGetValue(resident.Id, out var motion) ? motion.Position(_renderMotionTime) : new Point(resident.X, resident.Y);
            var x = (position.X + .5) * TilePixels; var y = (position.Y + .5) * TilePixels;
            if (!Visible(new Rect(x - 5, y - 7, 10, 12))) continue;
            if (ShowVehicle(resident)) continue;
            var child = resident.Age < 14;
            var tall = child ? .68 : resident.Race == RaceKind.Dwarf ? .82 : resident.Race == RaceKind.Elf ? 1.15 : 1;
            var moving = motion?.IsMoving(_renderMotionTime) == true;
            var gait = moving ? Math.Sin(_renderFrameTime * 12 + resident.Id) * .6 : 0;
            void Box(IBrush brush, double dx, double dy, double w, double h) => context.DrawRectangle(brush, null, new Rect(x + dx, y + dy * tall, w, h * tall));
            context.DrawEllipse(ShadowBrush, null, new Point(x, y + 2.7), 2.2, .7);
            Box(Clothing[(resident.Id % Clothing.Length)], -1.1, -.1, 2.2, 2.4);
            Box(WoodBrush, -1, 1.7 + gait, .75, 1.5);
            Box(WoodBrush, .3, 1.7 - gait, .75, 1.5);
            Box(resident.Race == RaceKind.Orc ? ResidentBrushes[3] : HeadBrush, -1, -1.6, 2, 1.7);
            Box(Hair[resident.Id % Hair.Length], -1.1, -1.9, 2.2, .65);
            if (_zoom >= 7) { Box(WoodBrush, -.55, -1, .23, .25); Box(WoodBrush, .4, -1, .23, .25); }
            if (resident.Race == RaceKind.Elf) { Box(HeadBrush, -1.45, -1, .45, .35); Box(HeadBrush, 1, -1, .45, .35); }
            if (resident.Race == RaceKind.Dwarf) Box(Hair[resident.Id % Hair.Length], -.8, -.2, 1.6, 1.1);
            var work = resident.Activity == ResidentActivity.Working ? Math.Sin(_renderFrameTime * 9 + resident.Id) : 0;
            switch (resident.Profession)
            {
                case Profession.Lumberjack:
                case Profession.Miner:
                case Profession.Builder:
                    context.DrawLine(new Pen(WoodBrush, .45), new Point(x + 1, y + 1), new Point(x + 2.5 + work, y - 2));
                    Box(StoneBrush, 2 + work, -2.5, resident.Profession == Profession.Miner ? 2 : 1.3, .8);
                    break;
                case Profession.Farmer:
                    Box(FarmBrush, -1.6, -2, 3.2, .45); Box(FarmBrush, -.7, -2.6, 1.4, .7);
                    break;
                case Profession.Soldier:
                    Box(StoneBrush, -1.2, -1.9, 2.4, .8); Box(NationBrush(resident.NationId), -1.8, 0, 1, 1.8);
                    Box(StoneBrush, 1.5, -1.5, .4, 3.5); break;
                case Profession.Mage:
                    Triangle(context, ArcaneBrush, new(x - 1.6, y - 1.7), new(x + 1.6, y - 1.7), new(x + .3, y - 4));
                    Box(WoodBrush, 1.8, -2.5, .35, 5); context.DrawEllipse(ArcaneBrush, null, new(x + 2, y - 2.8), .7, .7); break;
                case Profession.Scholar:
                case Profession.Representative:
                    Box(MessageBrush, 1, .2, 1.7, 1.5); Box(WoodBrush, 1.8, .2, .2, 1.5); break;
                case Profession.Messenger:
                case Profession.Trader:
                    Box(CargoBrush, -1.7, .2, 1.1, 1.7); Box(WoodBrush, -.8, 0, 2, .3); break;
            }
            if (resident.SicknessTicks > 0)
                context.DrawEllipse(null, new Pen(ArcaneBrush, .3), new(x, y - 3.3), 1.2, .7);
        }
        foreach (var b in state.Society.Buildings)
        {
            var x = (b.X + .5) * TilePixels; var y = (b.Y + .5) * TilePixels;
            if (!Visible(new Rect(x - 8, y - 16, 16, 24))) continue;
            if (!b.IsCompleted)
            {
                var fraction = b.ConstructionProgress / Math.Max(1, b.ConstructionRequired);
                context.DrawRectangle(WallBrush, null, new Rect(x - 3, y + 2 - fraction * 7, 6, Math.Max(.1, fraction * 7)));
                context.DrawLine(new Pen(WoodBrush, .55), new(x - 4, y + 2), new(x + 4, y - 7));
                continue;
            }
            if (b.Kind != BuildingKind.Farm)
            {
                context.DrawRectangle(FlameInner, null, new Rect(x - 2.6, y - 2, .8, 1.2));
                context.DrawRectangle(FlameInner, null, new Rect(x + 1.8, y - 2, .8, 1.2));
                if (_zoom >= 7)
                    for (var row = 0; row < 3; row++) context.DrawLine(new Pen(ShadowBrush, .18), new(x - 3, y + row), new(x + 3, y + row));
            }
            if (b.Health < 80) context.DrawLine(new Pen(WoodBrush, .5), new(x - 1, y - 3), new(x + 1, y + 2));
        }
        foreach (var town in state.Settlements.Where(t => t.ShieldTicks > 0 || t.FertilityBoostTicks > 0))
        {
            var center = new Point((town.X + .5) * TilePixels, (town.Y + .5) * TilePixels);
            if (!Visible(new Rect(center.X - 24, center.Y - 24, 48, 48))) continue;
            var phase = _renderFrameTime * 2;
            var brush = town.ShieldTicks > 0 ? ArcaneBrush : HealingBrush;
            for (var i = 0; i < 6; i++)
                context.DrawEllipse(brush, null, center + new Vector(Math.Cos(phase + i * Math.PI / 3) * 13, Math.Sin(phase + i * Math.PI / 3) * 9), .7, .7);
        }
    }
}
