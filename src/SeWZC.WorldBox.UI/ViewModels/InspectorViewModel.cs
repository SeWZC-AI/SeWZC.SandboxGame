using Avalonia.Threading;
using SeWZC.WorldBox.UI.ViewModels.Details;

namespace SeWZC.WorldBox.UI.ViewModels;

/// <summary>世界详情、列表、关注和研究的展示状态。</summary>
internal sealed partial class InspectorViewModel : WorldPresentationModel
{
    internal InspectorViewModel(PresentationRequests requests) : base(requests) { }
    internal StackNode? Content { get; private set; }
    internal ResearchGraphViewModel? ResearchGraph { get; private set; }

    internal string InspectorKey()
    {
        return
            $"{_inspectorMode}:{_selectedNationId}:{_selectedResidentId}:{_selectedTile}:{_eventDetailId}:{_inspectorSettlementId}:{_selectedBuildingId}";
    }

    private void BindSearch(TextInputNode input, Action<string> changed)
    {
        // 搜索刷新合并连续输入事件，并保留获得焦点的编辑器，避免打断输入法组合。
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
        var viewKey = _inspectorKey;
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (_inspectorKey == viewKey)
                RefreshInspector();
        };
        input.TextChanged += (_, _) =>
        {
            changed(input.Text ?? "");
            timer.Stop();
            timer.Start();
        };
        input.Detached += (_, _) => timer.Stop();
    }
}
