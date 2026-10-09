using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;
using Path = Avalonia.Controls.Shapes.Path;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    private Action<int, int>? _mapPick;
    private WorldEngine? _mapPickEngine;
    private int _mapPickGeneration;
    private bool _mapPickInspectorVisible, _mapPickToolsOpen;
    private Control? _mapPickModal;
    private WorldMapControl.MapSelectionState _mapPickSelection;
    private MapTool _mapPickTool = MapTool.Pan;

    private void AddMapPicker(StackPanel panel, NumericUpDown x, NumericUpDown y, Action? onLocationPicked = null)
    {
        panel.Children.Add(Named(Button("从地图选点", () =>
        {
            if (!_modal.IsVisible || _modal.Child is null)
                return;
            _mapPickGeneration = _modalGeneration;
            _mapPickEngine = _engine;
            _mapPickModal = _modal.Child;
            _mapPickInspectorVisible = _mobilePanel;
            _mapPickToolsOpen = _toolsOpen;
            _mapPickTool = _map.ActiveTool;
            _mapPickSelection = _map.CaptureMapSelection();
            _mapPick = (xx, yy) =>
            {
                x.Value = xx;
                y.Value = yy;
                onLocationPicked?.Invoke();
            };
            _map.PickingLocation = true;
            _map.ActiveTool = MapTool.Inspect;
            _modal.IsVisible = false;
            _mobilePanel = false;
            _toolsOpen = false;
            _map.IsSimulationPaused = true;
            ApplyLayout();
            RefreshUi();
            SetStatus("点选目标地格，按 Escape 返回表单；时间暂时停止");
        }), "map-pick-" + x.Name));
    }

    private void FinishMapPick(int? x = null, int? y = null)
    {
        if (_mapPick is null)
            return;
        var assignLocation = _mapPick;
        var current = _mapPickGeneration == _modalGeneration && ReferenceEquals(_mapPickEngine, _engine) &&
                      ReferenceEquals(_mapPickModal, _modal.Child);
        CancelMapPick(current);
        if (!current)
            return;
        if (x.HasValue && y.HasValue)
            assignLocation(x.Value, y.Value);
        _modal.IsVisible = true;
        _map.IsSimulationPaused = true;
        RefreshUi();
        SetStatus(x.HasValue && y.HasValue ? "已选定位置，可继续编辑表单" : "已返回表单，位置未改变");
    }

    private void CancelMapPick(bool restorePresentation = false)
    {
        if (_mapPick is null)
            return;
        _mapPick = null;
        _mapPickEngine = null;
        _mapPickModal = null;
        _map.PickingLocation = false;
        _map.ActiveTool = restorePresentation ? _mapPickTool : MapTool.Pan;
        if (restorePresentation)
        {
            _map.RestoreMapSelection(_mapPickSelection);
            _mobilePanel = _mapPickInspectorVisible;
            _toolsOpen = _mapPickToolsOpen;
            ApplyLayout();
        }
    }

    private static Button IconButton(string icon, Action action, string label, string id)
    {
        var path = icon switch
        {
            "plus" => "M 3,8 L 13,8 M 8,3 L 8,13",
            "minus" => "M 3,7.8 L 13,7.8 L 13,8.2 L 3,8.2 Z",
            "back" => "M 10,3 L 5,8 L 10,13",
            "undo" => "M 5,3 L 2,6 L 5,9 M 2,6 L 9,6 C 12,6 14,8 14,11 C 14,13 12,15 9,15",
            _ => "M 4,4 L 12,12 M 12,4 L 4,12",
        };
        var shape = new Path
        {
            Data = Geometry.Parse(path),
            Stroke = Mint,
            StrokeThickness = 1.8,
            Width = 14,
            Height = 14,
            Stretch = Stretch.Uniform,
        };
        var button = Named(new Button
        {
            Content = shape,
            Width = 30,
            Height = 30,
            MinHeight = 30,
            Padding = new Thickness(7),
            Margin = new Thickness(2, 0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        }, id);
        AutomationProperties.SetName(button, label);
        ToolTip.SetTip(button, label);
        button.Click += (_, _) => action();
        return button;
    }

    private static ComboBox ObjectField(StackPanel panel, string label, IEnumerable<(int Id, string Name)> values,
        int selected, string id, bool optional = false, bool historical = false)
    {
        panel.Children.Add(Text(label, 12, Muted));
        var entries = values.Select(v => new EntityChoice(v.Id, v.Name)).ToList();
        if (optional)
            entries.Insert(0, new EntityChoice(0, "无"));
        if (historical && entries.All(entry => entry.Id != selected))
            entries.Add(new EntityChoice(selected, $"历史记录 #{selected}（已不存在）"));
        var picker = Named(new ComboBox
        {
            ItemsSource = entries,
            SelectedItem = entries.FirstOrDefault(e => e.Id == selected),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 36,
        }, id);
        panel.Children.Add(picker);
        return picker;
    }

    private static int Integer(ComboBox field)
    {
        return (field.SelectedItem as EntityChoice)?.Id ?? throw new ArgumentException("请选择有效的对象。");
    }

    private NumericUpDown Field(StackPanel panel, string label, double value, string id, double? maximumOverride = null)
    {
        panel.Children.Add(Text(label, 12, Muted));
        var ratio = label.EndsWith("0–1") || label.Contains("0 至 1");
        var impact = label.Contains("−1");
        var integer = label.Contains("tick 序") || label.Contains("次数") || label.Contains("编号") ||
                      label.EndsWith(" X") ||
                      label.EndsWith(" Y");
        var maximum = maximumOverride ?? (ratio || impact ? 1
            : label.Contains("0–100") || label is "疲劳" or "社交需求" or "魔法天赋" or "魔法训练" ? 100
            : label is "年龄" or "魔力" ? 1000
            : label.StartsWith("疫病") ? 10000d / SimulationTime.TicksPerDay
            : label.EndsWith(" X") ? _engine.State.Width - 1
            : label.EndsWith(" Y") ? _engine.State.Height - 1
            : label.Contains("保持日数") ? 100_000d / SimulationTime.TicksPerDay
            : label.Contains("tick 序") ? _engine.State.Tick : 1_000_000);
        var box = Named(new NumericUpDown
        {
            Minimum = impact ? -1 : 0,
            Maximum = (decimal)maximum,
            Increment = ratio || impact ? .05m : integer ? 1 : .1m,
            Value = (decimal)value,
            Tag = value,
            FormatString = integer ? "0" : "0.##",
            NumberFormat = CultureInfo.InvariantCulture.NumberFormat,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 36,
        }, id);
        panel.Children.Add(box);
        return box;
    }

    private static double Number(NumericUpDown field)
    {
        var value = field.Value ?? throw new ArgumentException("请输入有效数值。");
        // 输入未改变时保留存储的精确浮点数，避免格式化显示造成隐式编辑。
        return field.Tag is double original && value == (decimal)original ? original : (double)value;
    }

    private static int Integer(NumericUpDown field)
    {
        var value = field.Value ?? throw new ArgumentException("请输入整数。");
        if (value != decimal.Truncate(value) || value < int.MinValue || value > int.MaxValue)
            throw new ArgumentException("请输入范围内的整数。");
        return (int)value;
    }

    private static void AddDatePreview(StackPanel panel, NumericUpDown input, string label)
    {
        var preview = Paragraph("");

        void Update()
        {
            preview.Text = input.Value is { } value ? $"{label}：{DateLabel((long)value)}" : "请选择日期";
        }

        input.ValueChanged += (_, _) => Update();
        Update();
        panel.Children.Add(preview);
    }

    private sealed record EntityChoice(int Id, string Label)
    {
        public override string ToString()
        {
            return Label;
        }
    }
}
