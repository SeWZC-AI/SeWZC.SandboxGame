namespace SeWZC.WorldBox.Core;

/// <summary>可组合的动物栖息地类别，用于匹配地形环境。</summary>
[Flags]
public enum AnimalHabitat
{
    /// <summary>无匹配栖息地。</summary>
    None = 0,

    /// <summary>草地及适宜绿色植被地带。</summary>
    Green = 1,

    /// <summary>森林地带。</summary>
    Forest = 2,

    /// <summary>高地和山地。</summary>
    High = 4,

    /// <summary>寒冷地带。</summary>
    Cold = 8,

    /// <summary>干旱地带。</summary>
    Dry = 16,

    /// <summary>湿地。</summary>
    Wet = 32,

    /// <summary>淡水水域。</summary>
    Fresh = 64,

    /// <summary>海洋水域。</summary>
    Marine = 128,
}
