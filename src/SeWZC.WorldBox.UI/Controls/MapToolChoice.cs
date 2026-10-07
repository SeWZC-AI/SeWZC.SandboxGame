namespace SeWZC.WorldBox.UI.Controls;

/// <summary>工具面板中的一个可选项。</summary>
/// <param name="Key">选项对应的地图工具。</param>
/// <param name="Label">选项的中文显示名称。</param>
/// <param name="Color">选项的显示颜色。</param>
public sealed record MapToolChoice(MapTool Key, string Label, string Color);
