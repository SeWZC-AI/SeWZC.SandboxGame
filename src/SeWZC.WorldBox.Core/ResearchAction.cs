namespace SeWZC.WorldBox.Core;

/// <summary>研究解锁的操作入口。</summary>
public abstract class ResearchAction
{
    private protected ResearchAction() { }
    /// <summary>操作入口的中文显示名称。</summary>
    public abstract string Name { get; }
    /// <summary>操作入口的稳定标识。</summary>
    public abstract string Id { get; }
    /// <summary>打开聚落铁路建设编辑器的操作。</summary>
    public static ResearchAction Rail { get; } = new RailAction();
    /// <summary>打开折跃门旅行编辑器的操作。</summary>
    public static ResearchAction Waygate { get; } = new WaygateAction();
    /// <summary>通过界面处理器执行研究解锁操作。</summary>
    public abstract void Invoke(IResearchActionHandler handler, int settlementId);

    private sealed class RailAction : ResearchAction
    {
        public override string Name => "铺设铁路";
        public override string Id => "rail";

        public override void Invoke(IResearchActionHandler handler, int settlementId)
        {
            handler.ShowRailEditor(settlementId);
        }
    }

    private sealed class WaygateAction : ResearchAction
    {
        public override string Name => "使用折跃门";
        public override string Id => "waygate";

        public override void Invoke(IResearchActionHandler handler, int settlementId)
        {
            handler.ShowWaygateEditor();
        }
    }
}
