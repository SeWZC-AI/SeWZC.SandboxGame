namespace SeWZC.WorldBox.Core;

/// <summary>研究操作的界面处理接口。</summary>
public interface IResearchActionHandler
{
    /// <summary>打开指定聚落的铁路建设编辑器。</summary>
    void ShowRailEditor(int settlementId);

    /// <summary>打开居民折跃门旅行编辑器。</summary>
    void ShowWaygateEditor();
}
