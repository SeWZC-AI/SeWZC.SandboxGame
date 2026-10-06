using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI.Controls;

/// <summary>地图工具分类、选项和笔刷设置。</summary>
public abstract class ToolCategory
{
    private static readonly IReadOnlyList<string> BrushSizes = Array.AsReadOnly(new[] { "小笔刷", "中笔刷", "大笔刷" });
    private protected ToolCategory() { }
    public abstract string Id { get; }
    public abstract string Title { get; }
    public abstract string Hint { get; }
    public abstract IReadOnlyList<MapToolChoice> Choices { get; }
    public virtual IReadOnlyList<string> BrushLabels => BrushSizes;
    public virtual int DefaultBrushIndex => 0;
    public virtual bool HasBrush => true;
    public virtual bool IsConstruction => false;

    public static ToolCategory Terrain { get; } = new TerrainCategory();
    public static ToolCategory Life { get; } = new LifeCategory();
    public static ToolCategory Disaster { get; } = new DisasterCategory();
    public static ToolCategory Build { get; } = new BuildCategory();

    public virtual void SetBrush(WorldMapControl map, int index)
    {
        map.BrushRadius = Size(index);
    }

    private static int Size(int index)
    {
        return index switch
        {
            0 => 2,
            1 => 5,
            _ => 10,
        };
    }

    private static MapToolChoice[] BuildToolChoices()
    {
        return new MapToolChoice[]
        {
            new(MapTool.ForBuilding(BuildingKind.Farm), "农场", "#ADBB75"),
            new(MapTool.ForBuilding(BuildingKind.Workshop), "工坊", "#CEB294"),
            new(MapTool.ForBuilding(BuildingKind.Academy), "学舍", "#91B0C8"),
            new(MapTool.ForBuilding(BuildingKind.Waystation), "驿站", "#CEAB76"),
            new(MapTool.ForBuilding(BuildingKind.Bridge), "桥梁", "#99AAC8"),
            new(MapTool.ForBuilding(BuildingKind.MountainPass), "山路", "#B598D1"),
            new(MapTool.ForBuilding(BuildingKind.Dock), "码头", "#91C7B1"), new(MapTool.Road, "道路", "#B0A28B"),
            new(MapTool.Rail, "铁路", "#ADC1D3"),
        }.Concat(Enum.GetValues<BuildingKind>()
            .Where(k => k is not (BuildingKind.TownCenter or BuildingKind.Farm or BuildingKind.Workshop
                or BuildingKind.Academy or BuildingKind.Waystation or BuildingKind.Bridge or BuildingKind.MountainPass
                or BuildingKind.Dock)).Select(k => new MapToolChoice(MapTool.ForBuilding(k),
                WorldEngine.BuildingName(k),
                AdvancementRules.For(k)?.Magic == true ? "#B598D1" : "#91B0C8"))).ToArray();
    }

    private sealed class TerrainCategory : ToolCategory
    {
        public override string Id => "terrain";
        public override string Title => "塑造山海";
        public override string Hint => "绘制地形时自动暂停";

        public override IReadOnlyList<MapToolChoice> Choices { get; } = Array.AsReadOnly(new MapToolChoice[]
        {
            new(MapTool.ForTerrain(TerrainType.Grass), "草地", "#8CAC69"),
            new(MapTool.ForTerrain(TerrainType.Forest), "森林", "#427D61"),
            new(MapTool.ForTerrain(TerrainType.Sand), "沙地", "#E6D09A"),
            new(MapTool.ForTerrain(TerrainType.Mountain), "山脉", "#9DABB0"),
            new(MapTool.ForTerrain(TerrainType.Water), "浅海", "#4A9CBA"),
            new(MapTool.ForTerrain(TerrainType.DeepWater), "深海", "#28556F"),
            new(MapTool.ForTerrain(TerrainType.Snow), "雪原", "#D4E8E7"),
            new(MapTool.ForTerrain(TerrainType.Hills), "丘陵", "#92905E"),
            new(MapTool.ForTerrain(TerrainType.Wetland), "湿地", "#58887D"),
            new(MapTool.ForTerrain(TerrainType.Desert), "荒漠", "#CEAE75"),
            new(MapTool.ForTerrain(TerrainType.River), "河流", "#428E9C"),
            new(MapTool.ForTerrain(TerrainType.Tundra), "苔原", "#99A88C"),
            new(MapTool.ForTerrain(TerrainType.Lake), "湖泊", "#559BA8"),
            new(MapTool.ForTerrain(TerrainType.DryFertile), "旱原", "#A3A66B"),
            new(MapTool.ForTerrain(TerrainType.Stream), "小溪", "#64A8B2"),
            new(MapTool.ForTerrain(TerrainType.LargeRiver), "江", "#397D99"),
            new(MapTool.ForTerrain(TerrainType.Meadow), "草甸", "#A8BE75"),
            new(MapTool.ForTerrain(TerrainType.Woodland), "疏林", "#73966B"),
            new(MapTool.ForTerrain(TerrainType.Rainforest), "雨林", "#35694F"),
            new(MapTool.ForTerrain(TerrainType.Savanna), "稀树草原", "#B4AB6B"),
            new(MapTool.ForTerrain(TerrainType.Scrub), "灌丛", "#9B9D72"),
            new(MapTool.ForTerrain(TerrainType.Floodplain), "河漫滩", "#85AF81"),
            new(MapTool.ForTerrain(TerrainType.AlpineMeadow), "高山草甸", "#91A992"),
        });
    }

    private sealed class LifeCategory : ToolCategory
    {
        public override string Id => "life";
        public override string Title => "播下文明";
        public override string Hint => "选择人数，在陆地投放居民";

        public override IReadOnlyList<MapToolChoice> Choices { get; } = Array.AsReadOnly(new MapToolChoice[]
        {
            new(MapTool.ForResidents(RaceKind.Human), "人类", "#DEBC85"),
            new(MapTool.ForResidents(RaceKind.Elf), "精灵", "#90C599"),
            new(MapTool.ForResidents(RaceKind.Dwarf), "矮人", "#BE9785"),
            new(MapTool.ForResidents(RaceKind.Orc), "兽人", "#A9B768"),
        });

        public override IReadOnlyList<string> BrushLabels { get; } =
            Array.AsReadOnly(new[] { "1 位居民", "12 位居民", "36 位居民" });

        public override int DefaultBrushIndex => 1;

        public override void SetBrush(WorldMapControl map, int index)
        {
            map.SpawnCount = index switch
            {
                0 => 1,
                1 => 12,
                _ => 36,
            };
        }
    }

    private sealed class DisasterCategory : ToolCategory
    {
        public override string Id => "disaster";
        public override string Title => "改变命运";
        public override string Hint => "点击世界，降下灾害";

        public override IReadOnlyList<MapToolChoice> Choices { get; } = Array.AsReadOnly(new MapToolChoice[]
        {
            new(MapTool.ForDisaster(DisasterKind.Fire), "火灾", "#F0A065"),
            new(MapTool.ForDisaster(DisasterKind.Drought), "干旱", "#D8C180"),
            new(MapTool.ForDisaster(DisasterKind.Plague), "疫病", "#B194C7"),
            new(MapTool.ForDisaster(DisasterKind.Meteor), "陨石", "#EC8758"),
        });

        public override IReadOnlyList<string> BrushLabels { get; } =
            Array.AsReadOnly(new[] { "范围 2 格", "范围 5 格", "范围 10 格" });

        public override void SetBrush(WorldMapControl map, int index)
        {
            map.DisasterRadius = Size(index);
        }
    }

    private sealed class BuildCategory : ToolCategory
    {
        public override string Id => "build";
        public override string Title => "建设与交通";
        public override string Hint => "选择归属聚落与建造方式";
        public override IReadOnlyList<MapToolChoice> Choices { get; } = Array.AsReadOnly(BuildToolChoices());
        public override bool HasBrush => false;
        public override bool IsConstruction => true;
    }
}
