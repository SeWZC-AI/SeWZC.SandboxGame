using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

public enum WildlifeKind { None, Rabbit, Deer, Boar, Goat, Wolf, Waterfowl, Fish, Fox, Bear, Bison, Yak, Jerboa, Gazelle, Camel, Fennec, Jackal, Lion, Capybara, Hippo, Otter, Crocodile, GrassCarp, Manatee, PredatoryFish, Pike, Shark, SeaTurtle, SeaCow, MuskOx, PolarBear, SnowLeopard }
public enum AnimalSize { Small, Medium, Large }
public enum AnimalDiet { Herbivore, Carnivore }
public enum ConflictStage { Dispute, Confrontation, Violence, Resolved }
public enum ConflictScope { Individual, Group, Settlement }

public sealed partial class Tile
{
    [JsonRequired] public WildlifeKind Wildlife { get; set; }
    [JsonRequired] public double WildlifePopulation { get; set; }
    // A zero-valued population set is the safe default for existing format 8 worlds.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public WildlifePopulations OtherWildlife { get; set; }

    [JsonIgnore] public int WildlifeMask => OtherWildlife.ActiveMask | (WildlifePopulation > 0 ? 1 << (int)Wildlife : 0);

    public double AnimalPopulation(WildlifeKind kind) => kind == Wildlife ? WildlifePopulation : OtherWildlife.Get(kind);

    internal void SetAnimalPopulation(WildlifeKind kind, double population)
    {
        if (kind == Wildlife) WildlifePopulation = population;
        else if (Wildlife == WildlifeKind.None && population > 0)
        { var others = OtherWildlife; others.Set(kind, 0); OtherWildlife = others; Wildlife = kind; WildlifePopulation = population; }
        else { var others = OtherWildlife; others.Set(kind, population); OtherWildlife = others; }
    }
}

public sealed partial class WorldState
{
    [JsonRequired] public List<LocalConflict> Conflicts { get; set; } = [];
}

public sealed class LocalConflict
{
    public int Id { get; set; }
    public int FirstResidentId { get; set; }
    public int SecondResidentId { get; set; }
    public int SettlementId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public ConflictScope Scope { get; set; }
    public ConflictStage Stage { get; set; }
    public double Tension { get; set; }
    public long StartedTick { get; set; }
    public long StageStartedTick { get; set; }
    public long LastChangedTick { get; set; }
    public int LastEventId { get; set; }
    public List<int> Participants { get; set; } = [];
}

// Value storage avoids allocating a collection for every map tile. Zero fields are omitted.
public struct WildlifePopulations
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Rabbit { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Deer { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Boar { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Goat { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Wolf { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Waterfowl { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Fish { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Fox { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Bear { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Bison { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Yak { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Jerboa { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Gazelle { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Camel { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Fennec { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Jackal { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Lion { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Capybara { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Hippo { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Otter { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Crocodile { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double GrassCarp { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Manatee { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double PredatoryFish { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Pike { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double Shark { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double SeaTurtle { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double SeaCow { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double MuskOx { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double PolarBear { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public double SnowLeopard { get; set; }
    [JsonIgnore] public readonly int ActiveMask => (Rabbit > 0 ? 1 << (int)WildlifeKind.Rabbit : 0) | (Deer > 0 ? 1 << (int)WildlifeKind.Deer : 0) | (Boar > 0 ? 1 << (int)WildlifeKind.Boar : 0) | (Goat > 0 ? 1 << (int)WildlifeKind.Goat : 0) | (Wolf > 0 ? 1 << (int)WildlifeKind.Wolf : 0) | (Waterfowl > 0 ? 1 << (int)WildlifeKind.Waterfowl : 0) | (Fish > 0 ? 1 << (int)WildlifeKind.Fish : 0) | (Fox > 0 ? 1 << (int)WildlifeKind.Fox : 0) | (Bear > 0 ? 1 << (int)WildlifeKind.Bear : 0) | (Bison > 0 ? 1 << (int)WildlifeKind.Bison : 0) | (Yak > 0 ? 1 << (int)WildlifeKind.Yak : 0) | (Jerboa > 0 ? 1 << (int)WildlifeKind.Jerboa : 0) | (Gazelle > 0 ? 1 << (int)WildlifeKind.Gazelle : 0) | (Camel > 0 ? 1 << (int)WildlifeKind.Camel : 0) | (Fennec > 0 ? 1 << (int)WildlifeKind.Fennec : 0) | (Jackal > 0 ? 1 << (int)WildlifeKind.Jackal : 0) | (Lion > 0 ? 1 << (int)WildlifeKind.Lion : 0) | (Capybara > 0 ? 1 << (int)WildlifeKind.Capybara : 0) | (Hippo > 0 ? 1 << (int)WildlifeKind.Hippo : 0) | (Otter > 0 ? 1 << (int)WildlifeKind.Otter : 0) | (Crocodile > 0 ? 1 << (int)WildlifeKind.Crocodile : 0) | (GrassCarp > 0 ? 1 << (int)WildlifeKind.GrassCarp : 0) | (Manatee > 0 ? 1 << (int)WildlifeKind.Manatee : 0) | (PredatoryFish > 0 ? 1 << (int)WildlifeKind.PredatoryFish : 0) | (Pike > 0 ? 1 << (int)WildlifeKind.Pike : 0) | (Shark > 0 ? 1 << (int)WildlifeKind.Shark : 0) | (SeaTurtle > 0 ? 1 << (int)WildlifeKind.SeaTurtle : 0) | (SeaCow > 0 ? 1 << (int)WildlifeKind.SeaCow : 0) | (MuskOx > 0 ? 1 << (int)WildlifeKind.MuskOx : 0) | (PolarBear > 0 ? 1 << (int)WildlifeKind.PolarBear : 0) | (SnowLeopard > 0 ? 1 << (int)WildlifeKind.SnowLeopard : 0);
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

    public readonly double Get(WildlifeKind kind) => kind switch
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
        _ => 0
    };
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
