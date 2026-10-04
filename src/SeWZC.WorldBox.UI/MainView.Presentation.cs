using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    private static bool DetailsVisible(Control control) => !control.GetVisualAncestors().OfType<Expander>().Any(e => !e.IsExpanded);
    private bool IsEditingText() => TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox
        || TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Control control && control.GetVisualAncestors().Any(c => c is TextBox);

    private void BindSearch(TextBox input, Action<string> changed)
    {
        // Keep the focused editor attached and process a burst of IME/input events once.
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
        var viewKey = _inspectorKey;
        timer.Tick += (_, _) => { timer.Stop(); if (_inspectorKey == viewKey) RefreshInspector(); };
        input.TextChanged += (_, _) => { changed(input.Text ?? ""); timer.Stop(); timer.Start(); };
        input.DetachedFromVisualTree += (_, _) => timer.Stop();
    }

    private string ResidentTask(Resident person) => _engine.GetResidentTaskSummary(person.Id);
    private string BuildingTask(Building building) => !building.IsCompleted ? $"施工 {building.ConstructionProgress / building.ConstructionRequired:P0}"
        : !building.Enabled ? "已停用" : _engine.GetProductionStatus(building.Id).Split('\n')[0];
}

// Imported names and historical messages can retain the former separator.
// Normalize only presentation, keeping every saved name and event untouched.
internal static class DisplayFormat
{
    internal static string Text(string value) => value.Replace('\u00B7', ' ').Replace('\u2022', ' ')
        .Replace("→", "至").Replace("↔", "与"); // The bundled font has no arrow glyphs.
}
