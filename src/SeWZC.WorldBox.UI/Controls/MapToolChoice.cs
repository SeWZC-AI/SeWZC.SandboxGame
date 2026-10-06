namespace SeWZC.WorldBox.UI.Controls;

/// <summary>地图工具选项及其显示名称和颜色。</summary>
/// <param name="Key">选项对应的地图工具。</param>
/// <param name="Label">选项的中文显示名称。</param>
/// <param name="Color">选项的显示颜色。</param>
public sealed record MapToolChoice(MapTool Key, string Label, string Color);
