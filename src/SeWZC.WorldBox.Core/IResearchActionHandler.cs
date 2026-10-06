namespace SeWZC.WorldBox.Core;

/// <summary>由呈现层实现研究解锁的操作入口，核心不依赖具体界面。</summary>
public interface IResearchActionHandler
{
    void ShowRailEditor(int settlementId);
    void ShowWaygateEditor();
}

