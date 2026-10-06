using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    private static bool DetailsVisible(Control control)
    {
        return !control.GetVisualAncestors().OfType<Expander>().Any(e => !e.IsExpanded);
    }

    private bool IsEditingText()
    {
        return TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox
               || TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Control control &&
               control.GetVisualAncestors().Any(c => c is TextBox);
    }

    private void BindSearch(TextBox input, Action<string> changed)
    {
        // 搜索刷新合并连续输入事件，并保留获得焦点的编辑器，避免打断输入法组合。
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
        var viewKey = _inspectorKey;
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (_inspectorKey == viewKey) RefreshInspector();
        };
        input.TextChanged += (_, _) =>
        {
            changed(input.Text ?? "");
            timer.Stop();
            timer.Start();
        };
        input.DetachedFromVisualTree += (_, _) => timer.Stop();
    }

    private string ResidentTask(Resident person)
    {
        return _engine.GetResidentTaskSummary(person.Id);
    }

    private string BuildingTask(Building building)
    {
        return !building.IsCompleted ? $"施工 {building.ConstructionProgress / building.ConstructionRequired:P0}"
            : !building.Enabled ? "已停用" : _engine.GetProductionStatus(building.Id).Split('\n')[0];
    }
}

// 只在呈现时替换旧分隔符，避免修改存档中的名称和历史事件。
internal static class DisplayFormat
{
    internal static string Text(string value)
    {
        return value.Replace('\u00B7', ' ').Replace('\u2022', ' ')
            .Replace("→", "至").Replace("↔", "与");
        // 内置字体缺少箭头字形，使用已有字符避免显示缺字方框。
    }
}
