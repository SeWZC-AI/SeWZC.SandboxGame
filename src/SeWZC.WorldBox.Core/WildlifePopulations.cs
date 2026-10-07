using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>按物种记录的一组动物种群数量。</summary>
[JsonConverter(typeof(WildlifePopulationsJsonConverter))]
public readonly record struct WildlifePopulations
{
    /// <summary>野兔数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Rabbit { get; init; }

    /// <summary>鹿数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Deer { get; init; }

    /// <summary>野猪数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Boar { get; init; }

    /// <summary>山羊数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Goat { get; init; }

    /// <summary>狼数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Wolf { get; init; }

    /// <summary>水鸟数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Waterfowl { get; init; }

    /// <summary>植食小鱼数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Fish { get; init; }

    /// <summary>狐狸数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Fox { get; init; }

    /// <summary>棕熊数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Bear { get; init; }

    /// <summary>野牛数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Bison { get; init; }

    /// <summary>牦牛数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Yak { get; init; }

    /// <summary>跳鼠数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Jerboa { get; init; }

    /// <summary>羚羊数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Gazelle { get; init; }

    /// <summary>野骆驼数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Camel { get; init; }

    /// <summary>耳廓狐数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Fennec { get; init; }

    /// <summary>胡狼数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Jackal { get; init; }

    /// <summary>狮子数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Lion { get; init; }

    /// <summary>水豚数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Capybara { get; init; }

    /// <summary>河马数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Hippo { get; init; }

    /// <summary>水獭数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Otter { get; init; }

    /// <summary>鳄鱼数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Crocodile { get; init; }

    /// <summary>草鱼数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double GrassCarp { get; init; }

    /// <summary>海牛数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Manatee { get; init; }

    /// <summary>掠食小鱼数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double PredatoryFish { get; init; }

    /// <summary>鲈鱼数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Pike { get; init; }

    /// <summary>鲨鱼数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Shark { get; init; }

    /// <summary>海龟数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double SeaTurtle { get; init; }

    /// <summary>海洋海牛数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double SeaCow { get; init; }

    /// <summary>麝牛数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double MuskOx { get; init; }

    /// <summary>北极熊数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double PolarBear { get; init; }

    /// <summary>雪豹数量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double SnowLeopard { get; init; }

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

    /// <summary>返回替换指定物种数量后的值，不处理 <c>None</c>。</summary>
    /// <param name="kind">动物物种。</param>
    /// <param name="population">要设置的动物数量，允许小数。</param>
    public WildlifePopulations WithPopulation(WildlifeKind kind, double population) => kind switch
    {
        WildlifeKind.Rabbit => this with { Rabbit = population },
        WildlifeKind.Deer => this with { Deer = population },
        WildlifeKind.Boar => this with { Boar = population },
        WildlifeKind.Goat => this with { Goat = population },
        WildlifeKind.Wolf => this with { Wolf = population },
        WildlifeKind.Waterfowl => this with { Waterfowl = population },
        WildlifeKind.Fish => this with { Fish = population },
        WildlifeKind.Fox => this with { Fox = population },
        WildlifeKind.Bear => this with { Bear = population },
        WildlifeKind.Bison => this with { Bison = population },
        WildlifeKind.Yak => this with { Yak = population },
        WildlifeKind.Jerboa => this with { Jerboa = population },
        WildlifeKind.Gazelle => this with { Gazelle = population },
        WildlifeKind.Camel => this with { Camel = population },
        WildlifeKind.Fennec => this with { Fennec = population },
        WildlifeKind.Jackal => this with { Jackal = population },
        WildlifeKind.Lion => this with { Lion = population },
        WildlifeKind.Capybara => this with { Capybara = population },
        WildlifeKind.Hippo => this with { Hippo = population },
        WildlifeKind.Otter => this with { Otter = population },
        WildlifeKind.Crocodile => this with { Crocodile = population },
        WildlifeKind.GrassCarp => this with { GrassCarp = population },
        WildlifeKind.Manatee => this with { Manatee = population },
        WildlifeKind.PredatoryFish => this with { PredatoryFish = population },
        WildlifeKind.Pike => this with { Pike = population },
        WildlifeKind.Shark => this with { Shark = population },
        WildlifeKind.SeaTurtle => this with { SeaTurtle = population },
        WildlifeKind.SeaCow => this with { SeaCow = population },
        WildlifeKind.MuskOx => this with { MuskOx = population },
        WildlifeKind.PolarBear => this with { PolarBear = population },
        WildlifeKind.SnowLeopard => this with { SnowLeopard = population },
        _ => this,
    };
}
