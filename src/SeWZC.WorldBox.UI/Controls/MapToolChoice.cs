namespace SeWZC.WorldBox.UI.Controls;

/// <summary>工具选项只在目录初始化时创建，页面切换复用同一不可变目录。</summary>
public sealed record MapToolChoice(MapTool Key, string Label, string Color);

