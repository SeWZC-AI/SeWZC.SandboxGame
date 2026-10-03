using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    // Read-only geometry and visible UI state. Browser access is separately gated
    // behind an explicit test URL; actions still use real pointer/keyboard input.
    public string GetAutomationSnapshotJson()
    {
        var controls = new List<UiAutomationControl>();
        foreach (var control in this.GetVisualDescendants().OfType<Control>())
        {
            var id = AutomationProperties.GetAutomationId(control);
            if (string.IsNullOrWhiteSpace(id)) continue;
            var origin = control.TranslatePoint(default, this);
            var bounds = origin is { } point ? new Rect(point, control.Bounds.Size) : default;
            var visibleBounds = bounds.Intersect(new Rect(Bounds.Size));
            foreach (var ancestor in control.GetVisualAncestors())
            {
                if (ancestor == this) break;
                if (!ancestor.ClipToBounds) continue;
                if (ancestor.TranslatePoint(default, this) is { } ancestorOrigin)
                    visibleBounds = visibleBounds.Intersect(new Rect(ancestorOrigin, ancestor.Bounds.Size));
            }
            if (control is Expander && control.GetVisualDescendants().OfType<ToggleButton>().FirstOrDefault() is { } toggle
                && toggle.TranslatePoint(default, this) is { } headerOrigin)
            { bounds = new Rect(headerOrigin, toggle.Bounds.Size); visibleBounds = visibleBounds.Intersect(bounds); }
            controls.Add(new UiAutomationControl
            {
                Id = id,
                X = bounds.X, Y = bounds.Y, Width = bounds.Width, Height = bounds.Height,
                Visible = origin is not null && control.IsEffectivelyVisible &&
                          visibleBounds.Width > 0 && visibleBounds.Height > 0 && visibleBounds.Contains(bounds.Center),
                Enabled = control.IsEffectivelyEnabled && control.IsHitTestVisible,
                Value = control switch
                {
                    NumericUpDown number => number.Value?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    TextBox input => input.Text,
                    ComboBox combo => combo.SelectedIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    CheckBox check => check.IsChecked?.ToString(),
                    Expander expander => expander.IsExpanded.ToString(),
                    TextBlock text => text.Text,
                    Button { Content: string label } => label,
                    Button button => string.Join(" ", button.GetVisualDescendants().OfType<TextBlock>()
                        .Select(text => text.Text).Where(text => !string.IsNullOrWhiteSpace(text)).Distinct()),
                    _ => null
                }
            });
        }

        var mapPosition = _map.TranslatePoint(default, this) ?? default;
        var tile0 = _map.GetTileScreenPosition(0, 0);
        var tile1 = _map.GetTileScreenPosition(1, 0);
        UiAutomationPoint? residentPoint = _map.TryGetResidentScreenPosition(_selectedResidentId, out var rendered)
            ? new UiAutomationPoint { X = mapPosition.X + rendered.X, Y = mapPosition.Y + rendered.Y }
            : null;
        var snapshot = new UiAutomationSnapshot
        {
            Ready = _ready, Paused = _paused, Saving = _saving, Speed = _speed, ActiveTool = _map.ActiveTool,
            RenderedWildlifeCount = _map.RenderedWildlifeCount, RenderedEffectCount = _map.RenderedEffectCount, RenderedEffectTime = _map.RenderedEffectTime, RenderedRouteSegmentCount = _map.RenderedRouteSegmentCount,
            WorldTick = _engine.State.Tick, SelectedResidentPoint = residentPoint,
            Category = _category, Inspector = _inspectorMode, Status = _status.Text,
            SelectedBuildingId = _map.SelectedBuildingId, SelectionKind = _mapSelectionKind,
            SelectedResidentId = _selectedResidentId, SelectedNationId = _selectedNationId,
            ModalOpen = _modal.IsVisible, Width = Bounds.Width, Height = Bounds.Height,
            ToolsOpen = _toolsOpen, InspectorOpen = _mobilePanel, PendingPlacement = _map.HasPendingPlacement,
            ToolSlots = _slotTools.ToArray(),
            Controls = controls,
            Map = new UiAutomationMap
            {
                X = mapPosition.X, Y = mapPosition.Y, Width = _map.Bounds.Width, Height = _map.Bounds.Height,
                Tile0CenterX = mapPosition.X + tile0.X, Tile0CenterY = mapPosition.Y + tile0.Y,
                TileSize = tile1.X - tile0.X
            }
        };
        return JsonSerializer.Serialize(snapshot, UiAutomationJsonContext.Default.UiAutomationSnapshot);
    }
}

internal sealed class UiAutomationSnapshot
{
    public int RenderedWildlifeCount { get; init; }
    public int RenderedEffectCount { get; init; }
    public double RenderedEffectTime { get; init; }
    public int RenderedRouteSegmentCount { get; init; }
    public bool ToolsOpen { get; init; }
    public bool InspectorOpen { get; init; }
    public bool PendingPlacement { get; init; }
    public bool Ready { get; init; }
    public bool Paused { get; init; }
    public bool Saving { get; init; }
    public long WorldTick { get; init; }
    public UiAutomationPoint? SelectedResidentPoint { get; init; }
    public int Speed { get; init; }
    public string ActiveTool { get; init; } = "";
    public string Category { get; init; } = "";
    public string Inspector { get; init; } = "";
    public string? Status { get; init; }
    public int? SelectedBuildingId { get; init; }
    public string? SelectionKind { get; init; }
    public int SelectedResidentId { get; init; }
    public int SelectedNationId { get; init; }
    public bool ModalOpen { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public List<UiAutomationControl> Controls { get; init; } = [];
    public string?[] ToolSlots { get; init; } = [];
    public UiAutomationMap Map { get; init; } = new();
}

internal sealed class UiAutomationControl
{
    public string Id { get; init; } = "";
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public bool Visible { get; init; }
    public bool Enabled { get; init; }
    public string? Value { get; init; }
}

internal sealed class UiAutomationMap
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public double Tile0CenterX { get; init; }
    public double Tile0CenterY { get; init; }
    public double TileSize { get; init; }
}

internal sealed class UiAutomationPoint
{
    public double X { get; init; }
    public double Y { get; init; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(UiAutomationSnapshot))]
internal partial class UiAutomationJsonContext : JsonSerializerContext;
