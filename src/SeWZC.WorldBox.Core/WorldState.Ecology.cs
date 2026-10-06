using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>地格内野生动物和设施养殖动物的物种。</summary>
public enum WildlifeKind
{
    /// <summary>无动物。</summary>
    None,
    /// <summary>野兔。</summary>
    Rabbit,
    /// <summary>鹿。</summary>
    Deer,
    /// <summary>野猪。</summary>
    Boar,
    /// <summary>山羊。</summary>
    Goat,
    /// <summary>狼。</summary>
    Wolf,
    /// <summary>水鸟。</summary>
    Waterfowl,
    /// <summary>植食小鱼。</summary>
    Fish,
    /// <summary>狐狸。</summary>
    Fox,
    /// <summary>棕熊。</summary>
    Bear,
    /// <summary>野牛。</summary>
    Bison,
    /// <summary>牦牛。</summary>
    Yak,
    /// <summary>跳鼠。</summary>
    Jerboa,
    /// <summary>羚羊。</summary>
    Gazelle,
    /// <summary>野骆驼。</summary>
    Camel,
    /// <summary>耳廓狐。</summary>
    Fennec,
    /// <summary>胡狼。</summary>
    Jackal,
    /// <summary>狮子。</summary>
    Lion,
    /// <summary>水豚。</summary>
    Capybara,
    /// <summary>河马。</summary>
    Hippo,
    /// <summary>水獭。</summary>
    Otter,
    /// <summary>鳄鱼。</summary>
    Crocodile,
    /// <summary>草鱼。</summary>
    GrassCarp,
    /// <summary>海牛。</summary>
    Manatee,
    /// <summary>掠食小鱼。</summary>
    PredatoryFish,
    /// <summary>鲈鱼。</summary>
    Pike,
    /// <summary>鲨鱼。</summary>
    Shark,
    /// <summary>海龟。</summary>
    SeaTurtle,
    /// <summary>海洋海牛。</summary>
    SeaCow,
    /// <summary>麝牛。</summary>
    MuskOx,
    /// <summary>北极熊。</summary>
    PolarBear,
    /// <summary>雪豹。</summary>
    SnowLeopard,
}

/// <summary>动物体型等级，用于生物量和捕食关系计算。</summary>
public enum AnimalSize
{
    /// <summary>小型。</summary>
    Small,
    /// <summary>中型。</summary>
    Medium,
    /// <summary>大型。</summary>
    Large,
}

/// <summary>动物的食性类别。</summary>
public enum AnimalDiet
{
    /// <summary>食草。</summary>
    Herbivore,
    /// <summary>食肉。</summary>
    Carnivore,
}

/// <summary>局部资源冲突的升级或解决阶段。</summary>
public enum ConflictStage
{
    /// <summary>争执。</summary>
    Dispute,
    /// <summary>对峙。</summary>
    Confrontation,
    /// <summary>暴力冲突。</summary>
    Violence,
    /// <summary>已经解决。</summary>
    Resolved,
}

/// <summary>局部冲突影响到的参与者范围。</summary>
public enum ConflictScope
{
    /// <summary>个人之间。</summary>
    Individual,
    /// <summary>群体之间。</summary>
    Group,
    /// <summary>聚落层面。</summary>
    Settlement,
}

public sealed partial class Tile
{
    // 用 255 区分尚未计算与有效的空物种结果，避免空地反复计算。
    private byte _edibleLandAnimal = byte.MaxValue, _edibleWaterAnimal = byte.MaxValue;
    private WildlifePopulations _otherWildlife;
    private WildlifeKind _wildlife;
    private double _wildlifePopulation;

    /// <summary>单独存储的主种群物种。</summary>
    [JsonRequired]
    public WildlifeKind Wildlife
    {
        get => _wildlife;
        set
        {
            if (_wildlife != value) InvalidateEdibleAnimals();
            _wildlife = value;
        }
    }

    /// <summary>主种群数量，允许小数表示连续种群变化。</summary>
    [JsonRequired]
    public double WildlifePopulation
    {
        get => _wildlifePopulation;
        set
        {
            if (_wildlifePopulation != value) InvalidateEdibleAnimals();
            _wildlifePopulation = value;
        }
    }

    /// <summary>除主种群外，按物种存储的动物数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public WildlifePopulations OtherWildlife
    {
        get => _otherWildlife;
        set
        {
            _otherWildlife = value;
            InvalidateEdibleAnimals();
        }
    }

    /// <summary>当前数量大于零的物种位掩码，位序对应物种编号。</summary>
    [JsonIgnore]
    public int WildlifeMask => OtherWildlife.ActiveMask | (WildlifePopulation > 0 ? 1 << (int)Wildlife : 0);

    private void InvalidateEdibleAnimals()
    {
        _edibleLandAnimal = _edibleWaterAnimal = byte.MaxValue;
    }

    internal WildlifeKind EdibleAnimal(bool aquatic)
    {
        ref var cached = ref aquatic ? ref _edibleWaterAnimal : ref _edibleLandAnimal;
        if (cached != byte.MaxValue) return (WildlifeKind)cached;
        cached = (byte)WildlifeKind.None;
        var largest = 0d;
        foreach (var kind in AnimalRules.EdibleAnimals(aquatic))
        {
            var population = AnimalPopulation(kind);
            var biomass = population * AnimalRules.For(kind).BodyMass;
            if (population >= .05 && biomass > largest)
            {
                cached = (byte)kind;
                largest = biomass;
            }
        }

        return (WildlifeKind)cached;
    }

    /// <summary>查询此格指定物种的数量，同时覆盖主种群和其他种群。</summary>
    /// <param name="kind">动物物种。</param>
    public double AnimalPopulation(WildlifeKind kind)
    {
        return kind == Wildlife ? WildlifePopulation : OtherWildlife.Get(kind);
    }

    internal void SetAnimalPopulation(WildlifeKind kind, double population)
    {
        if (kind == Wildlife) WildlifePopulation = population;
        else if (Wildlife == WildlifeKind.None && population > 0)
        {
            var others = OtherWildlife;
            others.Set(kind, 0);
            OtherWildlife = others;
            Wildlife = kind;
            WildlifePopulation = population;
        }
        else
        {
            var others = OtherWildlife;
            others.Set(kind, population);
            OtherWildlife = others;
        }
    }
}

public sealed partial class WorldState
{
    /// <summary>当前保留的局部资源冲突记录。</summary>
    [JsonRequired]
    public List<LocalConflict> Conflicts { get; set; } = [];
}

/// <summary>居民之间资源冲突的参与者、紧张度、位置和阶段记录。</summary>
public sealed class LocalConflict
{
    /// <summary>局部冲突的稳定 ID。</summary>
    public int Id { get; set; }
    /// <summary>最初参与冲突的第一位居民 ID。</summary>
    public int FirstResidentId { get; set; }
    /// <summary>最初参与冲突的第二位居民 ID。</summary>
    public int SecondResidentId { get; set; }
    /// <summary>关联或归属聚落的稳定 ID。</summary>
    public int SettlementId { get; set; }
    /// <summary>所在地点的横向地格坐标。</summary>
    public int X { get; set; }
    /// <summary>所在地点的纵向地格坐标。</summary>
    public int Y { get; set; }
    /// <summary>冲突目前影响的参与者范围。</summary>
    public ConflictScope Scope { get; set; }
    /// <summary>当前冲突阶段。</summary>
    public ConflictStage Stage { get; set; }
    /// <summary>当前紧张程度，影响升级或缓和。</summary>
    public double Tension { get; set; }
    /// <summary>冲突开始的模拟日序。</summary>
    public long StartedTick { get; set; }
    /// <summary>进入当前阶段的模拟日序。</summary>
    public long StageStartedTick { get; set; }
    /// <summary>最近一次冲突状态变化的模拟日序。</summary>
    public long LastChangedTick { get; set; }
    /// <summary>最近一次冲突关联的事件 ID。</summary>
    public int LastEventId { get; set; }
    /// <summary>当前参与冲突的居民 ID 集合。</summary>
    public List<int> Participants { get; set; } = [];
}

// 使用值类型避免每格分配集合；稀疏存档按物种编号保存精确的非零数量。
/// <summary>按物种保存的动物数量，可与地格主种群合并查询。</summary>
[JsonConverter(typeof(WildlifePopulationsJsonConverter))]
public struct WildlifePopulations : IEquatable<WildlifePopulations>
{
    /// <summary>野兔的种群数量，允许小数。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Rabbit { get; set; }

    /// <summary>鹿的种群数量，允许小数。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Deer { get; set; }

    /// <summary>野猪的种群数量，允许小数。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Boar { get; set; }

    /// <summary>山羊的种群数量，允许小数。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Goat { get; set; }

    /// <summary>狼的种群数量，允许小数。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Wolf { get; set; }

    /// <summary>水鸟的种群数量，允许小数。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Waterfowl { get; set; }

    /// <summary>植食小鱼的种群数量，允许小数。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Fish { get; set; }

    /// <summary>狐狸的种群数量，允许小数。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Fox { get; set; }

    /// <summary>棕熊的种群数量，允许小数。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Bear { get; set; }

    /// <summary>野牛的种群数量，允许小数。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Bison { get; set; }

    /// <summary>牦牛的种群数量，允许小数。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Yak { get; set; }

    /// <summary>跳鼠的种群数量，允许小数。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Jerboa { get; set; }

    /// <summary>羚羊的种群数量，允许小数。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Gazelle { get; set; }

    /// <summary>野骆驼的种群数量，允许小数。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Camel { get; set; }

    /// <summary>耳廓狐的种群数量，允许小数。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Fennec { get; set; }

    /// <summary>胡狼的种群数量，允许小数。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Jackal { get; set; }

    /// <summary>狮子的种群数量，允许小数。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Lion { get; set; }

    /// <summary>水豚的种群数量，允许小数。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Capybara { get; set; }

    /// <summary>河马的种群数量，允许小数。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Hippo { get; set; }

    /// <summary>水獭的种群数量，允许小数。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Otter { get; set; }

    /// <summary>鳄鱼的种群数量，允许小数。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Crocodile { get; set; }

    /// <summary>草鱼的种群数量，允许小数。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double GrassCarp { get; set; }

    /// <summary>海牛的种群数量，允许小数。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Manatee { get; set; }

    /// <summary>掠食小鱼的种群数量，允许小数。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double PredatoryFish { get; set; }

    /// <summary>鲈鱼的种群数量，允许小数。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Pike { get; set; }

    /// <summary>鲨鱼的种群数量，允许小数。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Shark { get; set; }

    /// <summary>海龟的种群数量，允许小数。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double SeaTurtle { get; set; }

    /// <summary>海洋海牛的种群数量，允许小数。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double SeaCow { get; set; }

    /// <summary>麝牛的种群数量，允许小数。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double MuskOx { get; set; }

    /// <summary>北极熊的种群数量，允许小数。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double PolarBear { get; set; }

    /// <summary>雪豹的种群数量，允许小数。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double SnowLeopard { get; set; }

    // 显式逐物种比较，避免省略默认值时为每格装箱并反射查询种群。
    /// <summary>逐物种比较动物数量是否相等。</summary>
    /// <param name="other">用于比较的同类型值。</param>
    public readonly bool Equals(WildlifePopulations other)
    {
        return Rabbit.Equals(other.Rabbit) && Deer.Equals(other.Deer) && Boar.Equals(other.Boar) &&
               Goat.Equals(other.Goat) && Wolf.Equals(other.Wolf) && Waterfowl.Equals(other.Waterfowl) &&
               Fish.Equals(other.Fish) && Fox.Equals(other.Fox) && Bear.Equals(other.Bear) &&
               Bison.Equals(other.Bison) && Yak.Equals(other.Yak) && Jerboa.Equals(other.Jerboa) &&
               Gazelle.Equals(other.Gazelle) && Camel.Equals(other.Camel) && Fennec.Equals(other.Fennec) &&
               Jackal.Equals(other.Jackal) && Lion.Equals(other.Lion) && Capybara.Equals(other.Capybara) &&
               Hippo.Equals(other.Hippo) && Otter.Equals(other.Otter) && Crocodile.Equals(other.Crocodile) &&
               GrassCarp.Equals(other.GrassCarp) && Manatee.Equals(other.Manatee) &&
               PredatoryFish.Equals(other.PredatoryFish) &&
               Pike.Equals(other.Pike) && Shark.Equals(other.Shark) && SeaTurtle.Equals(other.SeaTurtle) &&
               SeaCow.Equals(other.SeaCow) && MuskOx.Equals(other.MuskOx) && PolarBear.Equals(other.PolarBear) &&
               SnowLeopard.Equals(other.SnowLeopard);
    }

    /// <summary>逐物种比较动物数量是否相等。</summary>
    /// <param name="obj">用于比较的对象，空值或其他类型均不相等。</param>
    public readonly override bool Equals(object? obj)
    {
        return obj is WildlifePopulations other && Equals(other);
    }

    /// <summary>根据各物种数量计算哈希值。</summary>
    public readonly override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Rabbit);
        hash.Add(Deer);
        hash.Add(Boar);
        hash.Add(Goat);
        hash.Add(Wolf);
        hash.Add(Waterfowl);
        hash.Add(Fish);
        hash.Add(Fox);
        hash.Add(Bear);
        hash.Add(Bison);
        hash.Add(Yak);
        hash.Add(Jerboa);
        hash.Add(Gazelle);
        hash.Add(Camel);
        hash.Add(Fennec);
        hash.Add(Jackal);
        hash.Add(Lion);
        hash.Add(Capybara);
        hash.Add(Hippo);
        hash.Add(Otter);
        hash.Add(Crocodile);
        hash.Add(GrassCarp);
        hash.Add(Manatee);
        hash.Add(PredatoryFish);
        hash.Add(Pike);
        hash.Add(Shark);
        hash.Add(SeaTurtle);
        hash.Add(SeaCow);
        hash.Add(MuskOx);
        hash.Add(PolarBear);
        hash.Add(SnowLeopard);
        return hash.ToHashCode();
    }

    /// <summary>数量大于零的物种位掩码，位序对应物种编号。</summary>
    [JsonIgnore]
    public readonly int ActiveMask => (Rabbit > 0 ? 1 << (int)WildlifeKind.Rabbit : 0) |
                                      (Deer > 0 ? 1 << (int)WildlifeKind.Deer : 0) |
                                      (Boar > 0 ? 1 << (int)WildlifeKind.Boar : 0) |
                                      (Goat > 0 ? 1 << (int)WildlifeKind.Goat : 0) |
                                      (Wolf > 0 ? 1 << (int)WildlifeKind.Wolf : 0) |
                                      (Waterfowl > 0 ? 1 << (int)WildlifeKind.Waterfowl : 0) |
                                      (Fish > 0 ? 1 << (int)WildlifeKind.Fish : 0) |
                                      (Fox > 0 ? 1 << (int)WildlifeKind.Fox : 0) |
                                      (Bear > 0 ? 1 << (int)WildlifeKind.Bear : 0) |
                                      (Bison > 0 ? 1 << (int)WildlifeKind.Bison : 0) |
                                      (Yak > 0 ? 1 << (int)WildlifeKind.Yak : 0) |
                                      (Jerboa > 0 ? 1 << (int)WildlifeKind.Jerboa : 0) |
                                      (Gazelle > 0 ? 1 << (int)WildlifeKind.Gazelle : 0) |
                                      (Camel > 0 ? 1 << (int)WildlifeKind.Camel : 0) |
                                      (Fennec > 0 ? 1 << (int)WildlifeKind.Fennec : 0) |
                                      (Jackal > 0 ? 1 << (int)WildlifeKind.Jackal : 0) |
                                      (Lion > 0 ? 1 << (int)WildlifeKind.Lion : 0) |
                                      (Capybara > 0 ? 1 << (int)WildlifeKind.Capybara : 0) |
                                      (Hippo > 0 ? 1 << (int)WildlifeKind.Hippo : 0) |
                                      (Otter > 0 ? 1 << (int)WildlifeKind.Otter : 0) |
                                      (Crocodile > 0 ? 1 << (int)WildlifeKind.Crocodile : 0) |
                                      (GrassCarp > 0 ? 1 << (int)WildlifeKind.GrassCarp : 0) |
                                      (Manatee > 0 ? 1 << (int)WildlifeKind.Manatee : 0) |
                                      (PredatoryFish > 0 ? 1 << (int)WildlifeKind.PredatoryFish : 0) |
                                      (Pike > 0 ? 1 << (int)WildlifeKind.Pike : 0) |
                                      (Shark > 0 ? 1 << (int)WildlifeKind.Shark : 0) |
                                      (SeaTurtle > 0 ? 1 << (int)WildlifeKind.SeaTurtle : 0) |
                                      (SeaCow > 0 ? 1 << (int)WildlifeKind.SeaCow : 0) |
                                      (MuskOx > 0 ? 1 << (int)WildlifeKind.MuskOx : 0) |
                                      (PolarBear > 0 ? 1 << (int)WildlifeKind.PolarBear : 0) |
                                      (SnowLeopard > 0 ? 1 << (int)WildlifeKind.SnowLeopard : 0);

    internal readonly void CopyTo(Span<double> destination)
    {
        destination[0] = 0;
        destination[(int)WildlifeKind.Rabbit] = Rabbit;
        destination[(int)WildlifeKind.Deer] = Deer;
        destination[(int)WildlifeKind.Boar] = Boar;
        destination[(int)WildlifeKind.Goat] = Goat;
        destination[(int)WildlifeKind.Wolf] = Wolf;
        destination[(int)WildlifeKind.Waterfowl] = Waterfowl;
        destination[(int)WildlifeKind.Fish] = Fish;
        destination[(int)WildlifeKind.Fox] = Fox;
        destination[(int)WildlifeKind.Bear] = Bear;
        destination[(int)WildlifeKind.Bison] = Bison;
        destination[(int)WildlifeKind.Yak] = Yak;
        destination[(int)WildlifeKind.Jerboa] = Jerboa;
        destination[(int)WildlifeKind.Gazelle] = Gazelle;
        destination[(int)WildlifeKind.Camel] = Camel;
        destination[(int)WildlifeKind.Fennec] = Fennec;
        destination[(int)WildlifeKind.Jackal] = Jackal;
        destination[(int)WildlifeKind.Lion] = Lion;
        destination[(int)WildlifeKind.Capybara] = Capybara;
        destination[(int)WildlifeKind.Hippo] = Hippo;
        destination[(int)WildlifeKind.Otter] = Otter;
        destination[(int)WildlifeKind.Crocodile] = Crocodile;
        destination[(int)WildlifeKind.GrassCarp] = GrassCarp;
        destination[(int)WildlifeKind.Manatee] = Manatee;
        destination[(int)WildlifeKind.PredatoryFish] = PredatoryFish;
        destination[(int)WildlifeKind.Pike] = Pike;
        destination[(int)WildlifeKind.Shark] = Shark;
        destination[(int)WildlifeKind.SeaTurtle] = SeaTurtle;
        destination[(int)WildlifeKind.SeaCow] = SeaCow;
        destination[(int)WildlifeKind.MuskOx] = MuskOx;
        destination[(int)WildlifeKind.PolarBear] = PolarBear;
        destination[(int)WildlifeKind.SnowLeopard] = SnowLeopard;
    }

    /// <summary>读取指定物种的数量，无对应物种时返回零。</summary>
    /// <param name="kind">动物物种。</param>
    public readonly double Get(WildlifeKind kind)
    {
        return kind switch
        {
            WildlifeKind.Rabbit => Rabbit,
            WildlifeKind.Deer => Deer,
            WildlifeKind.Boar => Boar,
            WildlifeKind.Goat => Goat,
            WildlifeKind.Wolf => Wolf,
            WildlifeKind.Waterfowl => Waterfowl,
            WildlifeKind.Fish => Fish,
            WildlifeKind.Fox => Fox,
            WildlifeKind.Bear => Bear,
            WildlifeKind.Bison => Bison,
            WildlifeKind.Yak => Yak,
            WildlifeKind.Jerboa => Jerboa,
            WildlifeKind.Gazelle => Gazelle,
            WildlifeKind.Camel => Camel,
            WildlifeKind.Fennec => Fennec,
            WildlifeKind.Jackal => Jackal,
            WildlifeKind.Lion => Lion,
            WildlifeKind.Capybara => Capybara,
            WildlifeKind.Hippo => Hippo,
            WildlifeKind.Otter => Otter,
            WildlifeKind.Crocodile => Crocodile,
            WildlifeKind.GrassCarp => GrassCarp,
            WildlifeKind.Manatee => Manatee,
            WildlifeKind.PredatoryFish => PredatoryFish,
            WildlifeKind.Pike => Pike,
            WildlifeKind.Shark => Shark,
            WildlifeKind.SeaTurtle => SeaTurtle,
            WildlifeKind.SeaCow => SeaCow,
            WildlifeKind.MuskOx => MuskOx,
            WildlifeKind.PolarBear => PolarBear,
            WildlifeKind.SnowLeopard => SnowLeopard,
            _ => 0,
        };
    }

    /// <summary>替换指定物种的数量，不处理 <c>None</c>。</summary>
    /// <param name="kind">动物物种。</param>
    /// <param name="population">要设置的动物数量，允许小数。</param>
    public void Set(WildlifeKind kind, double population)
    {
        switch (kind)
        {
            case WildlifeKind.Rabbit: Rabbit = population; break;
            case WildlifeKind.Deer: Deer = population; break;
            case WildlifeKind.Boar: Boar = population; break;
            case WildlifeKind.Goat: Goat = population; break;
            case WildlifeKind.Wolf: Wolf = population; break;
            case WildlifeKind.Waterfowl: Waterfowl = population; break;
            case WildlifeKind.Fish: Fish = population; break;
            case WildlifeKind.Fox: Fox = population; break;
            case WildlifeKind.Bear: Bear = population; break;
            case WildlifeKind.Bison: Bison = population; break;
            case WildlifeKind.Yak: Yak = population; break;
            case WildlifeKind.Jerboa: Jerboa = population; break;
            case WildlifeKind.Gazelle: Gazelle = population; break;
            case WildlifeKind.Camel: Camel = population; break;
            case WildlifeKind.Fennec: Fennec = population; break;
            case WildlifeKind.Jackal: Jackal = population; break;
            case WildlifeKind.Lion: Lion = population; break;
            case WildlifeKind.Capybara: Capybara = population; break;
            case WildlifeKind.Hippo: Hippo = population; break;
            case WildlifeKind.Otter: Otter = population; break;
            case WildlifeKind.Crocodile: Crocodile = population; break;
            case WildlifeKind.GrassCarp: GrassCarp = population; break;
            case WildlifeKind.Manatee: Manatee = population; break;
            case WildlifeKind.PredatoryFish: PredatoryFish = population; break;
            case WildlifeKind.Pike: Pike = population; break;
            case WildlifeKind.Shark: Shark = population; break;
            case WildlifeKind.SeaTurtle: SeaTurtle = population; break;
            case WildlifeKind.SeaCow: SeaCow = population; break;
            case WildlifeKind.MuskOx: MuskOx = population; break;
            case WildlifeKind.PolarBear: PolarBear = population; break;
            case WildlifeKind.SnowLeopard: SnowLeopard = population; break;
        }
    }
}
