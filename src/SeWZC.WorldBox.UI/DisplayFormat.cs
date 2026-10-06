namespace SeWZC.WorldBox.UI;

// 只在呈现时替换旧分隔符，避免修改存档中的名称和历史事件。
/// <summary>规范呈现文本中的分隔符及字体不支持的符号。</summary>
internal static class DisplayFormat
{
    internal static string Text(string value)
    {
        return value.Replace('\u00B7', ' ').Replace('\u2022', ' ')
            .Replace("→", "至").Replace("↔", "与");
        // 内置字体缺少箭头字形，使用已有字符避免显示缺字方框。
    }
}
