using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI.Controls;

public sealed partial class WorldMapControl
{
    public bool ShowWildlife { get; set; } = true;
    public bool ShowPlants { get; set; } = true;
    public bool ShowBuildingNames { get; set; } = true;
    public int RenderedPlantCount { get; private set; }
    public int RenderedBuildingLabelCount { get; private set; }
    public ResourceVisibility ResourceVisibility { get; set; } = ResourceVisibility.Researched;
    public HashSet<ResourceKind> VisibleResources { get; } = [ResourceKind.Coal, ResourceKind.Oil, ResourceKind.RareEarth];
    public int RenderedWildlifeCount { get; private set; }
    private static readonly IBrush WaterAnimalBrush = Brush(0xFFBFE9F0);

    private bool _ecologyDirty = true;
    private (int Left, int Right, int Top, int Bottom) _ecologyViewport;
    private readonly Dictionary<WildlifeKind, WriteableBitmap> _animalIcons = [];
    private readonly List<(WriteableBitmap Icon, Rect Bounds)> _wildlifeDraws = [];
    private readonly List<(WriteableBitmap Icon, Rect Bounds)> _depositDraws = [];
    private readonly Dictionary<ResourceKind, WriteableBitmap> _depositIcons = [];
    private readonly Dictionary<PlantKind, WriteableBitmap> _plantIcons = [];
    private readonly List<(WriteableBitmap Icon, Rect Bounds)> _plantDraws = [];
    private readonly List<(Point Start, Point End)> _waterStreams = [];
    public IImage AnimalPreview(WildlifeKind kind) => AnimalIcon(kind);
    public IImage PlantPreview(PlantKind kind) => PlantIcon(kind);

    private WriteableBitmap PlantIcon(PlantKind kind)
    {
        if (_plantIcons.TryGetValue(kind, out var cached)) return cached;
        var c = new PixelCanvas(20, 20); const uint leaf = 0x92BF78FF, dark = 0x426B47FF, wood = 0xC2A474FF;
        switch (kind)
        {
            case PlantKind.Trees:
                c.Rect(9, 10, 3, 9, wood); c.Rect(3, 5, 14, 7, dark); c.Rect(5, 2, 10, 8, leaf); c.Rect(8, 1, 4, 4, 0xBDD691FF); break;
            case PlantKind.Shrubs:
                c.Rect(3, 9, 14, 8, dark); c.Rect(5, 6, 10, 8, leaf); c.Rect(4, 10, 2, 2, 0xD39A80FF); c.Rect(13, 7, 2, 2, 0xD39A80FF); break;
            case PlantKind.Grass:
                c.Line(10, 18, 8, 3, leaf, 2); c.Line(9, 17, 2, 8, leaf, 2); c.Line(10, 18, 17, 5, leaf, 2); c.Line(10, 18, 15, 14, dark, 2); break;
            case PlantKind.Reeds:
                for (var x = 4; x < 18; x += 5) { c.Line(x, 18, x - 1, 4, leaf); c.Rect(x - 2, 2, 3, 7, wood); } break;
            case PlantKind.Crops:
                c.Line(10, 18, 10, 2, wood, 2); for (var y = 4; y < 13; y += 3) { c.Line(6, y - 1, 10, y + 2, 0xE6CC77FF, 2); c.Line(10, y + 2, 14, y - 1, 0xE6CC77FF, 2); } break;
        }
        cached = MakeBitmap(c, opaque: false); _plantIcons[kind] = cached; return cached;
    }
    private static readonly Pen WaterFlowPen = new(WaterAnimalBrush, .12);

    private WriteableBitmap AnimalIcon(WildlifeKind kind)
    {
        if (_animalIcons.TryGetValue(kind, out var icon)) return icon;
        var canvas = new PixelCanvas(32, 32);
        var color = kind switch
        {
            WildlifeKind.Fish => 0x86CEDBFFu, WildlifeKind.Waterfowl => 0xE2EBDBFFu, WildlifeKind.Wolf => 0xB1B9BAFFu,
            WildlifeKind.Boar => 0xA8805FFFu, WildlifeKind.Goat => 0xD9D1B5FFu, WildlifeKind.Deer => 0xDCB578FFu, _ => AnimalRules.For(kind).Diet == AnimalDiet.Carnivore ? 0xB39179FFu : 0xCCB285FFu
        };
        void Box(double x, double y, double w, double h) => canvas.Rect((int)((x + 4) * 4), (int)((y + 4) * 4), Math.Max(1, (int)(w * 4)), Math.Max(1, (int)(h * 4)), color);
        Box(-2.5, -1.1, 4, 1.9); Box(1, -2, 1.5, 1.5);
        if (AnimalRules.For(kind).Aquatic && kind != WildlifeKind.Waterfowl) { Box(-3.5, -1.7, 1, 3); Box(-.7, -2, 1, 1); }
        else if (kind == WildlifeKind.Waterfowl) { Box(-1, -2, 1.8, .7); color = 0xE1B952FF; Box(2, -1.8, 1.5, .5); }
        else
        {
            Box(-2, .7, .7, 1.2); Box(.5, .7, .7, 1.2);
            if (kind == WildlifeKind.Rabbit) { Box(1.1, -4, .5, 2); Box(2, -3.6, .5, 1.6); }
            else if (kind == WildlifeKind.Deer) { Box(.8, -3.5, .4, 1.5); Box(2, -3.5, .4, 1.5); Box(.2, -3.4, 2.8, .4); Box(.1, -4, .4, 1); Box(2.6, -4, .4, 1); }
            else if (kind == WildlifeKind.Goat) { color = 0x877565FF; Box(1, -3.6, .4, 1.5); Box(2, -3.6, .4, 1.5); Box(.8, -3.6, 1.5, .4); color = 0xEAE1CCFF; Box(1.5, -.6, .6, 1.2); }
            else if (kind is WildlifeKind.Wolf or WildlifeKind.Fox or WildlifeKind.Fennec or WildlifeKind.Jackal or WildlifeKind.Lion or WildlifeKind.SnowLeopard) { Box(1, -3, .6, 1); Box(-3.5, -1.3, 1.2, .5); }
            else if (kind == WildlifeKind.Boar) { Box(2.2, -1.2, 1, .6); Box(-3.2, -.7, .8, .4); }
        }
        if (kind is WildlifeKind.Bison or WildlifeKind.Yak or WildlifeKind.MuskOx) { color = 0x695644FF; Box(-2, -2, 2.5, 1); Box(.8, -2.8, .5, .8); Box(2, -2.8, .5, .8); }
        if (kind == WildlifeKind.Camel) { Box(-1.8, -2.4, 1.3, 1.5); Box(-.1, -2.4, 1.3, 1.5); }
        if (kind is WildlifeKind.Bear or WildlifeKind.PolarBear) { Box(-2.7, -1.5, 4.5, 2.5); Box(1, -2.7, .7, .7); Box(2, -2.7, .7, .7); }
        if (kind == WildlifeKind.Shark) { Box(-.4, -3.2, .6, 1.5); }
        if (kind is WildlifeKind.SeaTurtle or WildlifeKind.SeaCow or WildlifeKind.Manatee) { color = 0x68AA99FF; Box(-1.5, -.2, .8, 2); Box(.5, -.2, .8, 2); }
        if (AnimalRules.For(kind).Diet == AnimalDiet.Carnivore) { color = 0xD98768FF; Box(2, -.5, .6, .6); }
        color = 0x354139FF; Box(1.8, -1.7, .3, .3);
        icon = MakeBitmap(canvas, opaque: false); _animalIcons[kind] = icon; return icon;
    }

    private WriteableBitmap DepositIcon(ResourceKind resource)
    {
        if (_depositIcons.TryGetValue(resource, out var icon)) return icon;
        var c = new PixelCanvas(20, 20);
        if (resource == ResourceKind.Coal)
        { c.Rect(2, 14, 16, 4, 0x475463FF); c.Rect(4, 9, 7, 7, 0x25323FFF); c.Rect(10, 5, 6, 10, 0x344453FF); c.Rect(11, 6, 3, 2, 0x83909DFF); }
        else if (resource == ResourceKind.Oil)
        { c.Rect(2, 16, 16, 2, 0x9FBECBFF); c.Line(5, 16, 10, 2, 0x336B91FF, 2); c.Line(10, 2, 15, 16, 0x336B91FF, 2); c.Rect(6, 10, 9, 2, 0x90D1E5FF); c.Rect(8, 5, 5, 2, 0x90D1E5FF); }
        else
        { c.Rect(3, 12, 14, 5, 0x655379FF); c.Line(7, 14, 7, 4, 0xCA9BEAFF, 4); c.Line(13, 15, 13, 7, 0x9871C9FF, 4); c.Rect(6, 4, 2, 6, 0xF1D6FFFF); }
        icon = MakeBitmap(c, opaque: false); _depositIcons[resource] = icon; return icon;
    }

    private void DrawEcology(DrawingContext context, WorldState state)
    {
        RenderedWildlifeCount = 0; RenderedPlantCount = 0;
        if (_zoom < 3) return;
        var viewport = VisibleTiles(state);
        if (_ecologyDirty || viewport != _ecologyViewport)
        {
            _ecologyDirty = false; _ecologyViewport = viewport;
            _wildlifeDraws.Clear(); _depositDraws.Clear(); _plantDraws.Clear(); _waterStreams.Clear();
            for (var y = viewport.Top; y <= viewport.Bottom; y++)
                for (var x = viewport.Left; x <= viewport.Right; x++)
                {
                    var tile = state.Tiles[y * state.Width + x];
                    if (tile.Deposit is { } resource && VisibleResources.Contains(resource) && Engine!.IsDepositVisible(tile, ResourceVisibility))
                        _depositDraws.Add((DepositIcon(resource), new Rect((x + .62) * TilePixels, (y + .05) * TilePixels, 2.8, 2.8)));
                    if (ShowPlants)
                    {
                        var slot = 0;
        foreach (var (kind, cover, _) in PlantResources.At(tile))
                        {
                            var size = 2.8 * (.25 + .75 * cover);
                            _plantDraws.Add((PlantIcon(kind), new Rect((x + .23 + slot * .34) * TilePixels - size / 2, (y + .22) * TilePixels - size / 2, size, size)));
                            slot++;
                        }
                    }
                    if (ShowWildlife)
                    {
                        var slot = 0;
                        for (var group = 0; group < 6; group++)
                        {
                            var kind = WorldEngine.VisibleWildlife(tile, group);
                            if (kind == WildlifeKind.None) continue;
                            var population = tile.AnimalPopulation(kind);
                            var scale = .25 + .75 * Math.Clamp(population / Math.Max(1, WorldEngine.WildlifeCapacity(tile, kind)), 0, 1);
                            var size = (AnimalRules.For(kind).Size == AnimalSize.Large ? 3.2 : AnimalRules.For(kind).Size == AnimalSize.Small ? 2.2 : 2.8) * scale;
                            var cx = (x + .25 + slot % 3 * .28) * TilePixels; var cy = (y + .76 - slot / 3 * .3) * TilePixels;
                            _wildlifeDraws.Add((AnimalIcon(kind), new Rect(cx - size / 2, cy - size / 2, size, size)));
                            slot++;
                        }
                    }
                    if (tile.Terrain is TerrainType.River or TerrainType.Stream or TerrainType.LargeRiver && _waterStreams.Count < 120)
                    {
                        var vertical = y + 1 < state.Height && state.Tiles[(y + 1) * state.Width + x].Terrain is TerrainType.River or TerrainType.Stream or TerrainType.LargeRiver;
                        _waterStreams.Add((new Point((x + .3) * TilePixels, (y + .3) * TilePixels), new Point((x + (vertical ? .3 : .8)) * TilePixels, (y + (vertical ? .8 : .3)) * TilePixels)));
                    }
                }
        }
        foreach (var (start, end) in _waterStreams)
        {
            var phase = (_renderFrameTime * .35 + start.X * .1 + start.Y * .05) % 1;
            var point = start + (end - start) * phase;
            context.DrawLine(WaterFlowPen, point, point + (end - start) * .2);
        }
        foreach (var (icon, bounds) in _plantDraws) context.DrawImage(icon, bounds);
        RenderedPlantCount = _plantDraws.Count;
        foreach (var (icon, bounds) in _depositDraws) context.DrawImage(icon, bounds);
        foreach (var (icon, bounds) in _wildlifeDraws) context.DrawImage(icon, bounds);
        RenderedWildlifeCount = _wildlifeDraws.Count;
    }

}
