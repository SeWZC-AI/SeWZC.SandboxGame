using Avalonia.Media;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.ViewModels.Details;

namespace SeWZC.WorldBox.UI.ViewModels;

/// <summary>居民、文明、建筑与世界编辑表单的草稿和提交命令。</summary>
internal sealed partial class WorldEditorViewModel : WorldPresentationModel
{
    internal WorldEditorViewModel(PresentationRequests requests) : base(requests) { }

    private TextNode LiveText(Func<string> value, double size = 12, IBrush? color = null)
    {
        return _requests.Inspector.LiveText(value, size, color);
    }

    private void RefreshInspector(bool force = false)
    {
        _requests.Inspector.RefreshInspector(force);
    }

    private void ReplaceWorld(WorldEngine engine)
    {
        _requests.ReplaceWorld(engine);
    }

    private Task SaveAsync(bool manual)
    {
        return _requests.Storage.SaveAsync(manual);
    }

    private Task ExportWorldAsync()
    {
        var source = _engine;
        var generation = _requests.ModalGeneration();
        return _requests.Storage.ExportWorldAsync(
            () => ReferenceEquals(source, _engine) && generation == _requests.ModalGeneration() &&
                  _requests.ModalVisible(), CloseModal);
    }

    internal void ShowRules()
    {
        var panel = ModalPanel("世界规则", "规则会随世界保存。关闭自主行为停止新的选择，已有项目与成果保留。");
        BuildWorldRules(panel);
        OpenModal(panel);
    }
}
