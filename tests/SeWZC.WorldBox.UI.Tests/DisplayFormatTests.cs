using SeWZC.WorldBox.UI;

namespace SeWZC.WorldBox.UI.Tests;

/// <summary>展示文字的字符替换检查。</summary>
public sealed class DisplayFormatTests
{
    /// <summary>格式化只替换无法展示的分隔符和箭头。</summary>
    [Theory]
    [InlineData("", "")]
    [InlineData("名称\n资源 12.5", "名称\n资源 12.5")]
    [InlineData("甲·乙", "甲 乙")]
    [InlineData("甲•乙", "甲 乙")]
    [InlineData("甲→乙", "甲至乙")]
    [InlineData("甲↔乙", "甲与乙")]
    [InlineData("甲·乙•丙→丁↔戊", "甲 乙 丙至丁与戊")]
    public void Text_replaces_only_the_display_symbols(string input, string expected)
    {
        Assert.Equal(expected, DisplayFormat.Text(input));
    }
}
