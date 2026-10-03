using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    private Action<int, int>? _mapPick;

    private void AddMapPicker(StackPanel panel, NumericUpDown x, NumericUpDown y)
    {
        panel.Children.Add(Named(Button("从地图选点", () =>
        {
            _mapPick = (xx, yy) => { x.Value = xx; y.Value = yy; };
            _map.PickingLocation = true; _map.ActiveTool = "inspect";
            _modal.IsVisible = false; _mobilePanel = false; _toolsOpen = false;
            ApplyLayout(); SetStatus("点选目标地格，按 Escape 返回表单");
        }), "map-pick-" + x.Name));
    }

    private void FinishMapPick(int? x = null, int? y = null)
    {
        if (_mapPick is null) return;
        if (x.HasValue && y.HasValue) _mapPick(x.Value, y.Value);
        _mapPick = null; _map.PickingLocation = false; _map.ActiveTool = "pan";
        _modal.IsVisible = true;
    }

    private void CancelMapPick()
    {
        if (_mapPick is null) return;
        _mapPick = null; _map.PickingLocation = false; _map.ActiveTool = "pan";
    }
    private static Button IconButton(string icon, Action action, string label, string id)
    {
        var path = icon switch
        {
            "plus" => "M 3,8 L 13,8 M 8,3 L 8,13",
            "minus" => "M 3,7.8 L 13,7.8 L 13,8.2 L 3,8.2 Z",
            "back" => "M 10,3 L 5,8 L 10,13",
            _ => "M 4,4 L 12,12 M 12,4 L 4,12"
        };
        var shape = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse(path), Stroke = Mint, StrokeThickness = 1.8,
            Width = 16, Height = 16, Stretch = Stretch.Uniform };
        var button = Named(new Button { Content = shape, Width = 40, Height = 40, Padding = new Thickness(10),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center }, id);
        AutomationProperties.SetName(button, label); ToolTip.SetTip(button, label); button.Click += (_, _) => action(); return button;
    }

    private sealed record EntityChoice(int Id, string Label)
    {
        public override string ToString() => Label;
    }

    private static ComboBox ObjectField(StackPanel panel, string label, IEnumerable<(int Id, string Name)> values, int selected, string id, bool optional = false, bool historical = false)
    {
        panel.Children.Add(Text(label, 12, Muted));
        var entries = values.Select(v => new EntityChoice(v.Id, v.Name)).ToList();
        if (optional) entries.Insert(0, new(0, "无"));
        if (historical && entries.All(entry => entry.Id != selected)) entries.Add(new(selected, $"历史记录 #{selected}（已不存在）"));
        var picker = Named(new ComboBox { ItemsSource = entries, SelectedItem = entries.FirstOrDefault(e => e.Id == selected),
            HorizontalAlignment = HorizontalAlignment.Stretch, MinHeight = 36 }, id);
        panel.Children.Add(picker); return picker;
    }

    private static int Integer(ComboBox field) => (field.SelectedItem as EntityChoice)?.Id ?? throw new ArgumentException("请选择有效的对象。");

    private NumericUpDown Field(StackPanel panel, string label, double value, string id, double? maximumOverride = null)
    {
        panel.Children.Add(Text(label, 12, Muted));
        var ratio = label.EndsWith("0–1") || label.Contains("0 至 1");
        var impact = label.Contains("−1");
        var integer = label.Contains("日") || label.Contains("次数") || label.Contains("编号") || label.EndsWith(" X") || label.EndsWith(" Y");
        var maximum = maximumOverride ?? (ratio || impact ? 1 : label.Contains("0–100") || label is "疲劳" or "社交需求" or "魔法天赋" or "魔法训练" ? 100
            : label is "年龄" or "魔力" ? 1000 : label.StartsWith("疫病") ? 10000
            : label.EndsWith(" X") ? _engine.State.Width - 1 : label.EndsWith(" Y") ? _engine.State.Height - 1
            : label.Contains("保持日数") ? 100_000 : label.Contains("日序") ? _engine.State.Tick : 1_000_000);
        var box = Named(new NumericUpDown { Minimum = impact ? -1 : 0, Maximum = (decimal)maximum, Increment = ratio || impact ? .05m : integer ? 1 : .1m,
            Value = (decimal)value, Tag = value, FormatString = integer ? "0" : "0.##", NumberFormat = CultureInfo.InvariantCulture.NumberFormat,
            HorizontalAlignment = HorizontalAlignment.Stretch, MinHeight = 36 }, id);
        panel.Children.Add(box); return box;
    }

    private static double Number(NumericUpDown field)
    {
        var value = field.Value ?? throw new ArgumentException("请输入有效数值。");
        // Preserve the exact stored double when the user has not changed this field.
        return field.Tag is double original && value == (decimal)original ? original : (double)value;
    }
    private static int Integer(NumericUpDown field)
    {
        var value = field.Value ?? throw new ArgumentException("请输入整数。");
        if (value != decimal.Truncate(value) || value < int.MinValue || value > int.MaxValue) throw new ArgumentException("请输入范围内的整数。");
        return (int)value;
    }

    private static void AddDatePreview(StackPanel panel, NumericUpDown input, string label)
    {
        var preview = Paragraph("");
        void Update() => preview.Text = input.Value is { } value ? $"{label}：{DateLabel((long)value)}" : "请选择日期";
        input.ValueChanged += (_, _) => Update(); Update(); panel.Children.Add(preview);
    }
}
