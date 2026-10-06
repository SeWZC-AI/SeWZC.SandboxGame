namespace SeWZC.WorldBox.Core;

/// <summary>研究解锁的额外操作通过多态分派；名称仅用于显示。</summary>
public abstract class ResearchAction
{
    private protected ResearchAction() { }
    public abstract string Name { get; }
    public abstract string Id { get; }
    public abstract void Invoke(IResearchActionHandler handler, int settlementId);
    public static ResearchAction Rail { get; } = new RailAction();
    public static ResearchAction Waygate { get; } = new WaygateAction();

    private sealed class RailAction : ResearchAction
    {
        public override string Name => "铺设铁路";
        public override string Id => "rail";
        public override void Invoke(IResearchActionHandler handler, int settlementId) => handler.ShowRailEditor(settlementId);
    }

    private sealed class WaygateAction : ResearchAction
    {
        public override string Name => "使用折跃门";
        public override string Id => "waygate";
        public override void Invoke(IResearchActionHandler handler, int settlementId) => handler.ShowWaygateEditor();
    }
}
