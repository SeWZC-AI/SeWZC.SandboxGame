using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI.Controls;

public sealed partial class WorldMapControl
{
    private readonly Dictionary<int, Building> _activityBuildings = [];

    private readonly Dictionary<ResidentTaskIcon, WriteableBitmap> _activityIcons = [];
    private readonly Dictionary<(RaceKind, BuildingKind), WriteableBitmap> _buildingIcons = [];

    private readonly Dictionary<(RaceKind, Profession, int), WriteableBitmap> _personIcons = [];
    private readonly List<Building> _sceneBuildings = [];
    private readonly List<SceneSprite> _sceneSprites = [];
    private readonly Dictionary<int, RaceKind> _settlementStyles = [];
    private int _architecturePopulation, _architectureTownCount;
    private WorldState? _architectureState;
    private long _architectureYear = -1;
    private (int Left, int Top, int Right, int Bottom) _sceneBuildingViewport;
    private bool _sceneBuildingsDirty = true;

    private void BuildScene(WorldState state)
    {
        var viewport = VisibleTiles(state);
        if (_sceneBuildingsDirty || viewport != _sceneBuildingViewport)
        {
            _sceneBuildingsDirty = false;
            _sceneBuildingViewport = viewport;
            _sceneBuildings.Clear();
            foreach (var building in state.Society.Buildings)
                if (building.X >= viewport.Left && building.X <= viewport.Right && building.Y >= viewport.Top &&
                    building.Y <= viewport.Bottom)
                    _sceneBuildings.Add(building);
        }

        _sceneSprites.Clear();
        foreach (var building in _sceneBuildings)
            if (Visible(BuildingBounds(building)))
            {
                _sceneSprites.Add(new SceneSprite((building.Y + .5) * TilePixels + 2, building.Id, building, null,
                    default));
            }

        foreach (var person in VisibleResidents(state))
        {
            if (person.TravelMode == TravelMode.Aircraft) continue;
            var position = _residentMotion.TryGetValue(person.Id, out var motion)
                ? motion.Position(_renderMotionTime)
                : new Point(person.X, person.Y);
            var centre = new Point((position.X + .5) * TilePixels, (position.Y + .5) * TilePixels);
            if (Visible(new Rect(centre.X - 6, centre.Y - 7, 12, 11)))
                _sceneSprites.Add(new SceneSprite(centre.Y + 2.2, person.Id, null, person, position));
        }

        _sceneSprites.Sort(static (a, b) => a.GroundY != b.GroundY ? a.GroundY.CompareTo(b.GroundY)
            : a.Building is null != b.Building is null ? (a.Building is null).CompareTo(b.Building is null)
            : a.Id.CompareTo(b.Id));
    }

    private void DrawNearScene(DrawingContext context, WorldState state)
    {
        BuildScene(state);
        foreach (var sprite in _sceneSprites)
            if (sprite.Building is { } building) DrawBuilding(context, building);
            else if (sprite.Resident is { } person)
            {
                if (ShowVehicle(person))
                {
                    DrawVehicle(context, person,
                        new Point((sprite.Position.X + .5) * TilePixels, (sprite.Position.Y + .5) * TilePixels));
                }
                else DrawResidentSprite(context, person, sprite.Position);
            }
    }

    /// <summary>取得种族与职业对应的居民预览图。</summary>
    /// <param name="race">决定居民外观的种族。</param>
    /// <param name="job">职业。</param>
    public IImage ResidentPreview(RaceKind race, Profession job)
    {
        return ResidentIcon(race, job, 0);
    }

    /// <summary>取得种族风格与建筑类型对应的建筑预览图。</summary>
    /// <param name="race">决定建筑外观风格的种族。</param>
    /// <param name="kind">设施类别。</param>
    public IImage BuildingPreview(RaceKind race, BuildingKind kind)
    {
        return BuildingIcon(race, kind);
    }

    private void CaptureArchitecture(WorldState state)
    {
        if (ReferenceEquals(state, _architectureState) && _architectureYear == state.Tick / 120
                                                       && _architecturePopulation == state.Residents.Count &&
                                                       _architectureTownCount == state.Settlements.Count) return;
        _architectureState = state;
        _architectureYear = state.Tick / 120;
        _architecturePopulation = state.Residents.Count;
        _architectureTownCount = state.Settlements.Count;
        _settlementStyles.Clear();
        foreach (var group in state.Residents.GroupBy(r => r.SettlementId))
            _settlementStyles[group.Key] = group.GroupBy(r => r.Race).OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key).First().Key;
    }

    private WriteableBitmap ResidentIcon(RaceKind race, Profession job, int pose)
    {
        var key = (race, job, pose);
        if (_personIcons.TryGetValue(key, out var cached)) return cached;
        var c = new PixelCanvas(32, 40);
        var skin = race switch
        {
            RaceKind.Elf => 0xD6E4C2FFu,
            RaceKind.Dwarf => 0xD9B18AFFu,
            RaceKind.Orc => 0x91AD63FFu,
            _ => 0xF0D3A5FFu,
        };
        var hair = race switch
        {
            RaceKind.Elf => 0xE2DDAAFFu,
            RaceKind.Dwarf => 0xB67540FFu,
            RaceKind.Orc => 0x343B30FFu,
            _ => 0x624330FFu,
        };
        var shirt = job switch
        {
            Profession.Farmer => 0x87A55EFFu,
            Profession.Lumberjack => 0xB16842FFu,
            Profession.Miner => 0x687B91FFu,
            Profession.Soldier => 0x8397A5FFu,
            Profession.Builder => 0xD0A552FFu,
            Profession.Trader => 0x7F7656FFu,
            Profession.Messenger => 0xB56666FFu,
            Profession.Representative => 0xE0C078FFu,
            Profession.Scholar => 0x527AABFFu,
            Profession.Mage => 0x8E6CADFFu,
            Profession.Fisher => 0x679FAEFFu,
            Profession.Engineer => 0xE9B94DFFu,
            Profession.Physician => 0xE7ECEAFFu,
            Profession.Firefighter => 0xD25848FFu,
            Profession.Ranger => 0x597F5CFFu,
            Profession.Archivist => 0xA17C50FFu,
            Profession.Battlemage => 0x7655BAFFu,
            Profession.Surveyor => 0x63B4BAFFu,
            Profession.Gardener => 0x67A85AFFu,
            _ => 0x78928CFFu,
        };
        const uint wood = 0x63472FFF, metal = 0xC6D4D4FF, paper = 0xFFF0C8FF;
        var headY = race == RaceKind.Dwarf ? 13 : race == RaceKind.Elf ? 6 : 9;
        var width = race is RaceKind.Dwarf or RaceKind.Orc ? 12 : 9;
        var left = 16 - width / 2;
        if (race == RaceKind.Elf) c.Rect(left - 1, 18, width + 2, 16, 0x437B66FF);
        c.Rect(left, 19, width, 12, shirt);
        var step = pose is 1 or 3 ? 2 : pose is 2 or 4 ? -2 : 0;
        c.Rect(left + 1, 30, 3, pose == 5 ? 3 : 7 + step, wood);
        c.Rect(left + width - 4, 30, 3, pose == 5 ? 3 : 7 - step, wood);
        if (pose == 5)
        {
            c.Rect(left - 2, 32, 5, 3, wood);
            c.Rect(left + width - 3, 32, 5, 3, wood);
        }

        c.Rect(left, headY, width, 11, skin);
        c.Rect(left - 1, headY - 2, width + 2, 4, hair);
        c.Rect(left + 2, headY + 5, 1, 2, wood);
        c.Rect(left + width - 3, headY + 5, 1, 2, wood);
        if (race == RaceKind.Elf)
        {
            c.Line(left - 4, headY + 3, left, headY + 7, skin, 2);
            c.Line(left + width + 2, headY + 3, left + width - 1, headY + 7, skin, 2);
            c.Rect(left, headY + 9, 2, 12, hair);
        }

        if (race == RaceKind.Dwarf)
        {
            c.Rect(left, headY + 8, width, 8, hair);
            c.Rect(left + 2, headY + 16, width - 4, 3, hair);
            c.Rect(left + 3, headY + 10, width - 6, 1, skin);
        }

        if (race == RaceKind.Orc)
        {
            c.Rect(left + 1, headY + 9, 2, 4, paper);
            c.Rect(left + width - 3, headY + 9, 2, 4, paper);
            c.Rect(left, 23, width, 2, wood);
        }

        c.Rect(left - 2, 21, 3, 7, skin);
        c.Rect(left + width - 1, 21, 3, 7, skin);
        var swing = pose == 3 ? -4 : pose == 4 ? 2 : 0;
        switch (job)
        {
            case Profession.Farmer:
                c.Rect(left - 3, headY - 2, width + 6, 3, 0xD6BD78FF);
                c.Rect(left + 1, headY - 5, width - 2, 3, 0xB8A15FFF);
                c.Line(25, 33, 29 + swing / 2, 20 + swing, wood);
                c.Rect(23 + swing / 2, 19 + swing, 7, 2, metal);
                break;
            case Profession.Lumberjack:
                c.Line(23, 31, 29 + swing / 2, 16 + swing, wood, 2);
                c.Rect(25 + swing / 2, 14 + swing, 6, 5, metal);
                c.Rect(30 + swing / 2, 15 + swing, 1, 4, paper);
                break;
            case Profession.Miner:
                c.Line(23, 32, 27 + swing / 2, 16 + swing, wood, 2);
                c.Line(22 + swing / 2, 14 + swing, 30, 17 + swing, metal, 2);
                c.Rect(left - 1, headY - 2, width + 2, 3, 0xD1AE51FF);
                c.Rect(15, headY, 3, 2, paper);
                break;
            case Profession.Soldier:
                c.Rect(left - 1, headY - 3, width + 2, 5, metal);
                c.Rect(5, 22, 6, 10, 0x526D81FF);
                c.Rect(7, 23, 2, 8, metal);
                c.Rect(27, 17, 2, 15, metal);
                c.Rect(24, 28, 7, 2, wood);
                break;
            case Profession.Builder:
                c.Rect(left - 1, headY - 3, width + 2, 4, 0xE5B64FFF);
                c.Line(25, 29, 27 + swing, 20 + swing, wood, 2);
                c.Rect(24 + swing, 18 + swing, 6, 3, metal);
                break;
            case Profession.Trader:
                c.Rect(4, 20, 7, 12, 0xA88551FF);
                c.Rect(6, 18, 3, 2, wood);
                c.Line(4, 26, 10, 26, wood);
                c.Rect(24, 28, 5, 6, 0xD9B55DFF);
                break;
            case Profession.Messenger:
                c.Line(left, 21, left + width, 29, wood);
                c.Rect(23, 25, 7, 6, paper);
                c.Line(23, 25, 26, 28, 0xB55B55FF);
                c.Line(26, 28, 29, 25, 0xB55B55FF);
                break;
            case Profession.Representative:
                c.Line(left, 21, left + width - 1, 29, paper, 2);
                c.Rect(24, 23, 6, 10, paper);
                c.Line(25, 25, 28, 25, wood);
                break;
            case Profession.Scholar:
                c.Rect(left + 1, headY + 4, width - 2, 3, wood);
                c.Rect(left + 2, headY + 5, 2, 1, paper);
                c.Rect(21, 23, 9, 9, 0x355F8AFF);
                c.Rect(23, 24, 6, 7, paper);
                c.Line(26, 24, 26, 30, wood);
                break;
            case Profession.Mage:
                for (var row = 0; row < 9; row++) c.Rect(15 - row / 2, headY - 9 + row, 2 + row, 1, 0x8162A1FF);
                c.Line(28, 34, 28, 13, wood, 2);
                c.Rect(26, 10, 5, 5, 0xC7ABE6FF);
                c.Rect(28, 10, 1, 2, paper);
                break;
            case Profession.Fisher:
                c.Line(12, 15, 16, 2, 0xC6AB7AFF);
                c.Line(16, 2, 19, 12, 0xDEE9E9FF);
                break;
            case Profession.Engineer:
                c.Rect(left - 1, headY - 3, width + 2, 4, 0xE5B64FFF);
                c.Rect(24, 21, 4, 11, metal);
                c.Rect(22, 19, 8, 3, metal);
                c.Rect(25, 18, 2, 4, wood);
                break;
            case Profession.Physician:
                c.Rect(left, 24, width, 5, paper);
                c.Rect(24, 26, 6, 8, 0xE2DFCAFF);
                c.Rect(26, 26, 2, 8, 0xBD4949FF);
                c.Rect(24, 29, 6, 2, 0xBD4949FF);
                break;
            case Profession.Firefighter:
                c.Rect(left - 2, headY - 3, width + 4, 4, 0xD85244FF);
                c.Rect(24, 25, 6, 9, metal);
                c.Rect(25, 25, 4, 3, 0x63B4D0FF);
                break;
            case Profession.Ranger:
                c.Line(26, 16, 29, 24, wood, 2);
                c.Line(29, 24, 26, 33, wood, 2);
                c.Line(26, 16, 26, 33, paper);
                c.Line(22, 24, 31, 24, metal);
                break;
            case Profession.Archivist:
                c.Rect(21, 23, 9, 9, 0xA17C50FF);
                c.Rect(23, 24, 6, 7, paper);
                c.Rect(24, 27, 5, 1, wood);
                break;
            case Profession.Battlemage:
                c.Rect(left - 1, headY - 3, width + 2, 4, metal);
                c.Rect(5, 23, 6, 9, 0x7854AEFF);
                c.Line(28, 15, 28, 34, wood, 2);
                c.Rect(26, 11, 5, 6, 0x9EE2E7FF);
                break;
            case Profession.Surveyor:
                c.Line(23, 24, 30, 19, metal, 3);
                c.Rect(29, 17, 2, 4, paper);
                c.Rect(22, 27, 7, 7, paper);
                c.Line(23, 29, 28, 32, wood);
                break;
            case Profession.Gardener:
                c.Line(27, 20, 27, 32, wood);
                c.Rect(24, 18, 6, 6, 0x57A25CFF);
                c.Rect(24, 26, 6, 7, metal);
                break;
        }

        cached = MakeBitmap(c, false);
        _personIcons[key] = cached;
        return cached;
    }

    /// <summary>取得居民任务对应的图标位图。</summary>
    /// <param name="kind">居民任务图标类别。</param>
    public WriteableBitmap ActivityPreview(ResidentTaskIcon kind)
    {
        if (_activityIcons.TryGetValue(kind, out var icon)) return icon;
        var c = new PixelCanvas(24, 24);
        const uint light = 0xFFF3D8FF,
            blue = 0x70D9E8FF,
            gold = 0xFFD173FF,
            green = 0xAAD586FF,
            metal = 0xCDD9DFFF,
            brown = 0xBB8960FF;
        c.Rect(1, 1, 22, 22, 0x122938FF);
        c.Rect(2, 2, 20, 1, 0x6A8491FF);
        switch (kind)
        {
            case ResidentTaskIcon.Log:
                c.Line(6, 20, 16, 5, brown, 3);
                c.Rect(7, 4, 10, 7, metal);
                c.Rect(5, 5, 3, 5, light);
                break;
            case ResidentTaskIcon.Mine:
                c.Line(5, 20, 15, 5, brown, 3);
                c.Line(5, 5, 18, 5, metal, 2);
                c.Line(18, 5, 20, 10, metal, 2);
                c.Rect(16, 17, 4, 4, metal);
                break;
            case ResidentTaskIcon.Gather:
            case ResidentTaskIcon.Farm:
                c.Line(12, 6, 12, 20, green, 2);
                for (var i = 0; i < 3; i++)
                {
                    c.Line(7, 5 + i * 4, 12, 10 + i * 4, gold, 2);
                    c.Line(17, 5 + i * 4, 12, 10 + i * 4, gold, 2);
                }

                if (kind == ResidentTaskIcon.Farm) c.Line(4, 21, 20, 21, brown, 2);
                break;
            case ResidentTaskIcon.Build:
            case ResidentTaskIcon.Upgrade:
                c.Line(6, 20, 15, 7, brown, 3);
                c.Rect(8, 5, 11, 5, metal);
                if (kind == ResidentTaskIcon.Upgrade)
                {
                    c.Line(17, 19, 17, 12, green, 2);
                    c.Line(14, 15, 17, 12, green, 2);
                    c.Line(17, 12, 20, 15, green, 2);
                }

                break;
            case ResidentTaskIcon.Research:
                c.Rect(4, 6, 16, 13, light);
                c.Line(12, 6, 12, 19, blue, 2);
                c.Line(6, 9, 9, 9, brown);
                c.Line(15, 9, 18, 9, brown);
                break;
            case ResidentTaskIcon.Magic:
                c.Line(5, 20, 16, 8, 0xD9AEFFFF, 3);
                c.Line(16, 4, 16, 12, light, 2);
                c.Line(12, 8, 20, 8, light, 2);
                break;
            case ResidentTaskIcon.Heal:
                c.Rect(9, 5, 6, 15, green);
                c.Rect(5, 9, 15, 6, green);
                break;
            case ResidentTaskIcon.Pickup:
            case ResidentTaskIcon.Deliver:
            case ResidentTaskIcon.Trade:
                c.Rect(4, 8, 12, 11, gold);
                c.Line(10, 8, 10, 18, brown, 2);
                c.Line(4, 11, 15, 11, brown);
                var right = kind == ResidentTaskIcon.Deliver ? 16 : 20;
                var left = kind == ResidentTaskIcon.Deliver ? 20 : 16;
                c.Line(left, 5, right, 5, blue, 2);
                c.Line(right, 5, right == 20 ? 17 : 19, 2, blue, 2);
                break;
            case ResidentTaskIcon.Message:
            case ResidentTaskIcon.Talk:
                c.Rect(4, 6, 16, 11, light);
                c.Line(4, 6, 12, 12, blue, 2);
                c.Line(12, 12, 19, 6, blue, 2);
                if (kind == ResidentTaskIcon.Talk) c.Rect(6, 17, 3, 4, light);
                break;
            case ResidentTaskIcon.Claim:
                c.Line(6, 4, 6, 21, brown, 2);
                c.Rect(8, 4, 12, 8, green);
                c.Line(10, 8, 12, 10, light, 2);
                c.Line(12, 10, 17, 6, light, 2);
                break;
            case ResidentTaskIcon.Water:
                for (var i = 0; i < 6; i++) c.Rect(12 - i, 4 + i * 2, i * 2 + 1, 3, blue);
                c.Rect(6, 16, 13, 3, blue);
                c.Rect(8, 19, 9, 2, blue);
                c.Rect(9, 13, 2, 4, light);
                break;
            case ResidentTaskIcon.Hunt:
                c.Line(6, 4, 11, 9, brown, 2);
                c.Line(11, 9, 11, 15, brown, 2);
                c.Line(11, 15, 6, 20, brown, 2);
                c.Line(6, 4, 6, 20, light);
                c.Line(4, 12, 20, 12, gold, 2);
                c.Line(16, 8, 20, 12, gold, 2);
                c.Line(20, 12, 16, 16, gold, 2);
                break;
            case ResidentTaskIcon.Fish:
                c.Rect(6, 9, 11, 7, blue);
                c.Line(5, 12, 2, 9, blue, 2);
                c.Line(2, 9, 2, 16, blue, 2);
                c.Line(2, 16, 5, 12, blue, 2);
                c.Rect(14, 10, 2, 2, light);
                c.Line(17, 4, 20, 4, brown, 2);
                c.Line(20, 4, 20, 19, light);
                c.Line(20, 19, 17, 19, light);
                break;
            case ResidentTaskIcon.Rest:
                c.Rect(5, 4, 8, 15, blue);
                c.Rect(10, 3, 8, 12, 0x122938FF);
                c.Rect(17, 5, 3, 3, light);
                break;
            case ResidentTaskIcon.Eat:
                c.Rect(4, 9, 16, 9, gold);
                c.Line(7, 8, 16, 8, light, 2);
                c.Line(7, 12, 15, 12, light, 2);
                break;
            case ResidentTaskIcon.Flee:
            case ResidentTaskIcon.March:
                c.Line(6, 18, 16, 8, kind == ResidentTaskIcon.Flee ? gold : metal, 3);
                c.Rect(14, 5, 5, 6, metal);
                break;
            case ResidentTaskIcon.Smelt:
                c.Rect(4, 6, 16, 15, metal);
                c.Rect(7, 11, 10, 8, brown);
                c.Line(9, 17, 12, 12, gold, 3);
                c.Line(12, 12, 15, 17, gold, 3);
                break;
            case ResidentTaskIcon.Power:
                c.Line(14, 4, 8, 12, gold, 3);
                c.Line(8, 12, 16, 12, gold, 3);
                c.Line(16, 12, 10, 21, gold, 3);
                break;
            case ResidentTaskIcon.Craft:
                c.Rect(6, 6, 12, 12, metal);
                c.Rect(9, 9, 6, 6, brown);
                c.Rect(10, 3, 4, 3, metal);
                c.Rect(10, 18, 4, 3, metal);
                c.Rect(3, 10, 3, 4, metal);
                c.Rect(18, 10, 3, 4, metal);
                break;
            case ResidentTaskIcon.Ship:
                c.Line(3, 15, 7, 19, brown, 3);
                c.Line(7, 19, 17, 19, brown, 3);
                c.Line(17, 19, 21, 15, brown, 3);
                c.Line(11, 4, 11, 15, light, 2);
                for (var i = 0; i < 8; i++) c.Rect(13, 5 + i, Math.Max(1, i), 1, light);
                c.Line(3, 22, 21, 22, blue, 2);
                break;
            case ResidentTaskIcon.Plane:
                c.Line(12, 3, 12, 21, metal, 3);
                c.Line(3, 13, 21, 13, metal, 3);
                c.Line(7, 20, 17, 20, metal, 2);
                break;
            case ResidentTaskIcon.Crystal:
                c.Line(12, 3, 4, 12, 0xD9AEFFFF, 3);
                c.Line(4, 12, 12, 21, 0xD9AEFFFF, 3);
                c.Line(12, 21, 20, 12, 0xD9AEFFFF, 3);
                c.Line(20, 12, 12, 3, 0xD9AEFFFF, 3);
                c.Line(12, 6, 12, 18, light, 2);
                break;
            case ResidentTaskIcon.Runic:
                c.Line(12, 5, 12, 20, green, 3);
                c.Line(6, 9, 12, 14, green, 3);
                c.Line(18, 9, 12, 14, green, 3);
                c.Line(5, 4, 9, 4, 0xD9AEFFFF, 2);
                c.Line(19, 19, 19, 23, 0xD9AEFFFF, 2);
                break;
            case ResidentTaskIcon.Aether:
                c.Line(5, 6, 18, 6, 0xD9AEFFFF, 3);
                c.Line(15, 3, 18, 6, 0xD9AEFFFF, 3);
                c.Line(18, 18, 5, 18, blue, 3);
                c.Line(8, 21, 5, 18, blue, 3);
                c.Rect(10, 10, 4, 4, light);
                break;
            case ResidentTaskIcon.Extinguish:
                c.Rect(5, 13, 10, 8, blue);
                c.Line(5, 13, 15, 13, light, 2);
                c.Line(14, 8, 21, 12, blue, 2);
                c.Line(18, 5, 20, 9, gold, 2);
                c.Rect(19, 10, 3, 3, gold);
                break;
            default:
                c.Line(4, 12, 20, 12, blue, 3);
                c.Line(13, 5, 20, 12, blue, 3);
                c.Line(20, 12, 13, 19, blue, 3);
                break;
        }

        icon = MakeBitmap(c, false);
        _activityIcons[kind] = icon;
        return icon;
    }

    private void DrawActivityBadge(DrawingContext context, Resident resident, double x, double y, bool moving)
    {
        if (_zoom < 5 || Engine is null) return;
        var kind = Engine.GetResidentTaskIcon(resident,
            _activityBuildings.GetValueOrDefault(resident.Agent.Goal.TargetEntityId));
        context.DrawImage(ActivityPreview(kind), new Rect(x, y, 2.8, 2.8));
        if (moving) context.DrawLine(new Pen(MessageBrush, .22), new Point(x, y + 3.15), new Point(x + 2.8, y + 3.15));
    }

    private WriteableBitmap BuildingIcon(RaceKind race, BuildingKind kind)
    {
        if (_buildingIcons.TryGetValue((race, kind), out var cached)) return cached;
        var c = new PixelCanvas(40, 48);
        const uint timber = 0x6C4C31FF, metal = 0xB6C6C5FF, paper = 0xFFF0C8FF, green = 0x709957FF;
        var wall = race switch
        {
            RaceKind.Elf => 0xC5D3A4FFu,
            RaceKind.Dwarf => 0x929E9DFFu,
            RaceKind.Orc => 0x9A8060FFu,
            _ => 0xDEC9A0FFu,
        };
        var roof = race switch
        {
            RaceKind.Elf => 0x4E865EFFu,
            RaceKind.Dwarf => 0x617780FFu,
            RaceKind.Orc => 0x675948FFu,
            _ => 0xB8754BFFu,
        };

        void House()
        {
            c.Rect(6, 23, 28, 20, wall);
            var peak = race == RaceKind.Elf ? 7 : race == RaceKind.Dwarf ? 19 : 13;
            for (var y = peak; y <= 23; y++)
            {
                var half = Math.Min(17, (y - peak + 1) * 18 / Math.Max(1, 24 - peak));
                c.Rect(20 - half, y, half * 2, 1, roof);
            }

            c.Rect(17, 33, 7, 10, timber);
            c.Rect(9, 28, 4, 5, paper);
            c.Rect(27, 28, 4, 5, paper);
            if (race == RaceKind.Human)
            {
                c.Rect(6, 23, 2, 20, timber);
                c.Rect(32, 23, 2, 20, timber);
                c.Rect(6, 35, 28, 1, timber);
            }

            if (race == RaceKind.Dwarf)
            {
                for (var y = 25; y < 42; y += 5)
                    c.Line(6, y, 33, y, 0x627271FF);
            }

            if (race == RaceKind.Elf)
            {
                c.Line(7, 42, 4, 23, green, 2);
                c.Rect(3, 29, 5, 3, green);
                c.Rect(31, 25, 5, 2, green);
            }

            if (race == RaceKind.Orc)
            {
                c.Line(5, 43, 3, 20, timber, 2);
                c.Line(34, 43, 37, 20, timber, 2);
                c.Rect(11, 25, 2, 7, paper);
                c.Rect(28, 25, 2, 7, paper);
            }
        }

        switch (kind)
        {
            case BuildingKind.Farm:
            case BuildingKind.AutomatedFarm:
            case BuildingKind.RunicGarden:
            case BuildingKind.HerbGarden:
            case BuildingKind.HuntingCamp:
                c.Rect(3, 24, 34, 20, timber);
                c.Rect(5, 25, 30, 18, 0x5B7746FF);
                for (var row = 0; row < 3; row++)
                {
                    c.Rect(6, 27 + row * 5, 28, 2, green);
                    for (var col = 0; col < 5; col++)
                        c.Line(8 + col * 5, 29 + row * 5, 9 + col * 5, 26 + row * 5, 0xD5BB69FF);
                }

                if (kind == BuildingKind.AutomatedFarm)
                {
                    c.Rect(5, 19, 30, 3, metal);
                    c.Rect(7, 19, 2, 23, metal);
                    c.Rect(31, 19, 2, 23, metal);
                }

                if (kind == BuildingKind.RunicGarden)
                {
                    c.Rect(17, 12, 6, 10, 0xAB83C4FF);
                    c.Rect(19, 13, 2, 6, paper);
                }

                break;
            case BuildingKind.SignalTower:
                c.Rect(12, 17, 16, 27, metal);
                c.Rect(8, 16, 24, 4, roof);
                c.Rect(18, 4, 3, 13, timber);
                c.Line(10, 5, 28, 5, paper);
                c.Line(13, 9, 25, 9, paper);
                c.Rect(18, 35, 4, 9, timber);
                break;
            case BuildingKind.ArcaneSanctum:
            case BuildingKind.Crystallizer:
            case BuildingKind.AetherForge:
            case BuildingKind.SacredGrove:
            case BuildingKind.WarDrum:
                c.Rect(5, 37, 30, 7, metal);
                c.Rect(10, 21, 20, 16, wall);
                for (var y = 7; y < 25; y++)
                {
                    var half = y < 16 ? (y - 5) / 2 : (27 - y) / 2;
                    c.Rect(20 - half, y, half * 2, 1, 0xA67DC3FF);
                }

                c.Line(20, 9, 20, 23, paper);
                if (kind != BuildingKind.ArcaneSanctum)
                {
                    c.Rect(5, 21, 4, 16, roof);
                    c.Rect(31, 21, 4, 16, roof);
                }

                if (kind == BuildingKind.AetherForge)
                {
                    c.Rect(3, 10, 3, 27, metal);
                    c.Rect(34, 10, 3, 27, metal);
                }

                break;
            case BuildingKind.Foundry:
            case BuildingKind.PowerPlant:
            case BuildingKind.Fabricator:
            case BuildingKind.DwarvenForge:
                House();
                c.Rect(28, 6, 6, 24, metal);
                c.Rect(27, 4, 8, 3, timber);
                c.Rect(9, 31, 9, 10, kind == BuildingKind.Foundry ? 0xE9A04CFFu : 0x6594ACFFu);
                if (kind == BuildingKind.PowerPlant)
                {
                    c.Line(18, 24, 14, 30, paper, 2);
                    c.Line(14, 30, 21, 30, paper, 2);
                    c.Line(21, 30, 17, 36, paper, 2);
                }

                if (kind == BuildingKind.Fabricator)
                {
                    c.Rect(22, 30, 9, 9, metal);
                    c.Rect(24, 32, 5, 5, timber);
                }

                break;
            case BuildingKind.MountainPass:
                c.Rect(3, 29, 34, 12, metal);
                c.Line(3, 31, 36, 31, paper);
                c.Line(3, 39, 36, 39, paper);
                break;
            case BuildingKind.Bridge:
                c.Rect(4, 27, 32, 15, timber);
                for (var y = 27; y < 42; y += 3) c.Line(4, y, 35, y, wall);
                c.Rect(4, 25, 32, 2, metal);
                c.Rect(4, 42, 32, 2, metal);
                break;
            case BuildingKind.Dock:
                c.Rect(3, 34, 34, 7, timber);
                c.Rect(7, 32, 3, 13, metal);
                c.Rect(29, 32, 3, 13, metal);
                c.Line(19, 18, 19, 34, timber, 2);
                c.Rect(21, 20, 10, 7, paper);
                break;
            case BuildingKind.Airfield:
                c.Rect(3, 26, 34, 18, metal);
                c.Line(6, 36, 33, 36, paper, 2);
                c.Rect(5, 17, 11, 11, roof);
                c.Line(24, 26, 24, 39, paper, 2);
                c.Line(18, 30, 30, 30, paper, 2);
                break;
            case BuildingKind.Shipyard:
                c.Rect(3, 34, 34, 9, timber);
                c.Rect(5, 9, 3, 29, metal);
                c.Rect(5, 9, 28, 3, metal);
                c.Line(28, 12, 28, 27, timber);
                c.Rect(11, 30, 20, 5, roof);
                c.Line(14, 35, 28, 35, paper, 2);
                break;
            case BuildingKind.LumberCamp:
                c.Rect(5, 21, 20, 20, wall);
                c.Rect(3, 18, 24, 4, roof);
                for (var row = 0; row < 3; row++) c.Rect(24, 32 + row * 4, 13, 3, timber);
                c.Line(10, 27, 20, 39, timber, 2);
                c.Rect(6, 25, 10, 4, metal);
                break;
            case BuildingKind.Quarry:
            case BuildingKind.MiningHall:
                c.Rect(4, 26, 32, 17, metal);
                c.Rect(6, 23, 10, 8, wall);
                c.Rect(23, 21, 12, 12, wall);
                c.Line(12, 27, 25, 39, timber, 2);
                c.Line(6, 29, 21, 23, paper, 2);
                break;
            case BuildingKind.Well:
                c.Rect(9, 31, 22, 12, metal);
                c.Rect(13, 33, 14, 5, 0x467AA6FF);
                c.Rect(8, 16, 3, 25, timber);
                c.Rect(29, 16, 3, 25, timber);
                c.Rect(6, 14, 28, 4, roof);
                c.Line(20, 18, 20, 35, paper);
                break;
            case BuildingKind.Market:
            case BuildingKind.AssemblyHall:
            case BuildingKind.TradeGuild:
                c.Rect(5, 24, 3, 19, timber);
                c.Rect(32, 24, 3, 19, timber);
                c.Rect(4, 37, 32, 5, timber);
                for (var col = 0; col < 6; col++) c.Rect(2 + col * 6, 16, 6, 9, col % 2 == 0 ? roof : paper);
                c.Rect(9, 32, 7, 5, green);
                c.Rect(21, 32, 7, 5, 0xE9A04CFF);
                break;
            case BuildingKind.Watchtower:
                c.Rect(10, 8, 20, 13, wall);
                c.Rect(7, 6, 26, 3, roof);
                c.Rect(12, 21, 3, 22, timber);
                c.Rect(25, 21, 3, 22, timber);
                c.Line(14, 22, 26, 40, timber, 2);
                c.Rect(17, 11, 6, 6, paper);
                break;
            case BuildingKind.Reservoir:
                c.Rect(4, 30, 32, 13, metal);
                c.Rect(7, 32, 26, 8, 0x548DB5FF);
                c.Line(9, 35, 29, 35, paper);
                c.Rect(27, 16, 5, 14, wall);
                break;
            case BuildingKind.Hospital:
                House();
                c.Rect(17, 24, 6, 14, 0xBD4949FF);
                c.Rect(12, 28, 16, 6, 0xBD4949FF);
                c.Rect(6, 8, 5, 15, metal);
                break;
            case BuildingKind.Apothecary:
            case BuildingKind.AlchemyLab:
                House();
                c.Rect(16, 25, 8, 3, metal);
                c.Rect(14, 28, 12, 12, kind == BuildingKind.AlchemyLab ? 0xB68CD2FFu : 0x70B58AFFu);
                c.Rect(15, 28, 10, 3, paper);
                break;
            case BuildingKind.FireStation:
                House();
                c.Rect(4, 12, 6, 28, 0xB75449FF);
                c.Rect(7, 17, 2, 2, paper);
                c.Rect(11, 32, 5, 8, metal);
                c.Rect(12, 32, 3, 3, 0x63B4D0FF);
                break;
            case BuildingKind.Library:
                House();
                for (var row = 27; row < 39; row += 4)
                {
                    c.Rect(9, row, 22, 3, timber);
                    for (var x = 10; x < 30; x += 3) c.Rect(x, row, 2, 2, paper);
                }

                break;
            case BuildingKind.SurveyOffice:
                House();
                c.Line(23, 15, 34, 8, metal, 4);
                c.Rect(32, 5, 5, 5, paper);
                c.Line(27, 13, 27, 26, timber, 2);
                break;
            case BuildingKind.MachineWorkshop:
                House();
                c.Rect(10, 26, 20, 12, metal);
                c.Rect(17, 24, 6, 16, timber);
                c.Rect(12, 29, 16, 6, timber);
                c.Rect(18, 30, 4, 4, paper);
                break;
            case BuildingKind.Arsenal:
                House();
                c.Line(10, 33, 26, 26, metal, 4);
                c.Rect(8, 31, 5, 8, timber);
                c.Rect(24, 25, 8, 4, metal);
                break;
            case BuildingKind.Armory:
                House();
                c.Rect(14, 26, 12, 13, metal);
                c.Rect(17, 24, 6, 4, metal);
                c.Rect(17, 30, 6, 7, 0x5C788AFF);
                break;
            case BuildingKind.WardTower:
            case BuildingKind.StormSpire:
                c.Rect(13, 15, 14, 28, wall);
                c.Rect(10, 12, 20, 5, 0x795CA8FF);
                c.Rect(17, 4, 6, 13, kind == BuildingKind.WardTower ? 0xA99BD7FFu : 0x86D2E3FFu);
                c.Line(20, 5, 16, 12, paper);
                c.Line(16, 12, 23, 12, paper);
                break;
            case BuildingKind.GroveSanctuary:
                c.Rect(6, 36, 28, 7, timber);
                c.Rect(18, 10, 4, 28, timber);
                c.Rect(8, 8, 24, 15, green);
                c.Rect(4, 16, 32, 10, 0x5CA372FF);
                c.Rect(17, 19, 7, 7, 0xB49BDBFF);
                break;
            case BuildingKind.Waygate:
                c.Rect(6, 16, 7, 27, metal);
                c.Rect(27, 16, 7, 27, metal);
                c.Rect(10, 10, 20, 8, metal);
                c.Rect(13, 18, 14, 25, 0x6655A5FF);
                c.Rect(16, 21, 8, 20, 0x9D8AD2FF);
                c.Rect(18, 25, 4, 14, 0xCEE9EDFF);
                break;
            case BuildingKind.Pasture:
                c.Rect(3, 19, 34, 24, green);
                c.Rect(3, 19, 34, 2, timber);
                c.Rect(3, 41, 34, 2, timber);
                for (var x = 3; x <= 35; x += 8) c.Rect(x, 17, 2, 28, timber);
                c.Rect(10, 28, 12, 7, paper);
                c.Rect(21, 26, 6, 6, wall);
                c.Rect(11, 34, 2, 5, paper);
                c.Rect(20, 34, 2, 5, paper);
                break;
            case BuildingKind.Aquaculture:
                c.Rect(3, 22, 34, 21, metal);
                c.Rect(6, 25, 13, 15, 0x548DB5FF);
                c.Rect(22, 25, 12, 15, 0x548DB5FF);
                c.Rect(10, 31, 6, 3, paper);
                c.Rect(25, 32, 6, 3, paper);
                c.Rect(14, 30, 2, 5, paper);
                c.Rect(29, 31, 2, 5, paper);
                break;
            default:
                House();
                if (kind == BuildingKind.TownCenter)
                {
                    c.Rect(19, 2, 2, 13, timber);
                    c.Rect(21, 2, 10, 6, roof);
                    c.Rect(16, 25, 10, 8, paper);
                    c.Rect(19, 27, 3, 4, roof);
                }

                if (kind == BuildingKind.Workshop)
                {
                    c.Line(9, 28, 21, 39, timber, 2);
                    c.Rect(5, 25, 10, 5, metal);
                    c.Rect(27, 7, 5, 12, metal);
                }

                if (kind == BuildingKind.Granary)
                {
                    c.Rect(8, 28, 24, 12, 0xC49D57FF);
                    c.Line(12, 32, 28, 32, paper, 2);
                    c.Line(12, 37, 28, 37, paper, 2);
                }

                if (kind == BuildingKind.Housing)
                {
                    c.Rect(8, 26, 9, 7, paper);
                    c.Rect(24, 26, 9, 7, paper);
                    c.Rect(18, 36, 5, 8, roof);
                }

                if (kind == BuildingKind.Academy)
                {
                    c.Rect(10, 25, 20, 11, 0x406B8DFF);
                    c.Rect(12, 26, 16, 8, paper);
                    c.Line(20, 26, 20, 34, timber);
                }

                if (kind == BuildingKind.Infirmary)
                {
                    c.Rect(17, 24, 6, 15, 0xC4514FFF);
                    c.Rect(12, 28, 16, 6, 0xC4514FFF);
                }

                if (kind == BuildingKind.Waystation)
                {
                    c.Rect(32, 14, 2, 30, timber);
                    c.Rect(25, 15, 13, 8, paper);
                    c.Line(25, 15, 31, 19, roof);
                    c.Line(31, 19, 37, 15, roof);
                }

                if (kind == BuildingKind.Dock)
                {
                    c.Rect(3, 41, 34, 4, timber);
                    c.Line(30, 28, 30, 41, paper);
                    c.Rect(31, 29, 5, 6, paper);
                }

                break;
        }

        c.Rect(5, 44, 30, 2, roof);
        cached = MakeBitmap(c, false);
        _buildingIcons[(race, kind)] = cached;
        return cached;
    }

    private readonly record struct SceneSprite(
        double GroundY,
        int Id,
        Building? Building,
        Resident? Resident,
        Point Position);
}
