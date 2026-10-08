namespace SeWZC.WorldBox.Core.Tests;

/// <summary>研究操作向对应处理器递送参数的检查。</summary>
public sealed class ResearchActionTests
{
    /// <summary>铁路入口保留当前聚落上下文。</summary>
    [Fact]
    public void Rail_passes_the_settlement_to_the_rail_editor()
    {
        var handler = new RecordingHandler();

        ResearchAction.Rail.Invoke(handler, 37);

        Assert.Equal(37, handler.RailSettlement);
        Assert.Equal(0, handler.WaygateCalls);
    }

    /// <summary>折跃入口只打开对应编辑器。</summary>
    [Fact]
    public void Waygate_opens_only_the_waygate_editor()
    {
        var handler = new RecordingHandler();

        ResearchAction.Waygate.Invoke(handler, 51);

        Assert.Null(handler.RailSettlement);
        Assert.Equal(1, handler.WaygateCalls);
    }

    private sealed class RecordingHandler : IResearchActionHandler
    {
        public int? RailSettlement { get; private set; }
        public int WaygateCalls { get; private set; }

        public void ShowRailEditor(int settlementId)
        {
            RailSettlement = settlementId;
        }

        public void ShowWaygateEditor()
        {
            WaygateCalls++;
        }
    }
}
