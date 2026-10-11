using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;
using SeWZC.WorldBox.UI.ViewModels.Details;

namespace SeWZC.WorldBox.UI.ViewModels;

internal abstract partial class WorldPresentationModel
{
    internal static readonly IBrush Ink = Brush.Parse("#111E29"), Panel = Brush.Parse("#172632"),
        Line = Brush.Parse("#2A3C46"), Muted = Brush.Parse("#8FA8AE"), Mint = Brush.Parse("#B8E9BC");
    internal string BuildingLabel(Building building)
    {
        var name = WorldEngine.BuildingName(building.Kind);
        var duplicates =
            _engine.State.Society.Buildings.Count(b =>
                b.SettlementId == building.SettlementId && b.Kind == building.Kind) > 1;
        return duplicates ? $"{name}（{building.X}, {building.Y}）" : name;
    }

    internal static string EffectLabel(IReadOnlyList<EffectInfo> effects)
    {
        return effects.Count == 0 ? "当前无额外加成或减益" : string.Join("\n\n", effects);
    }

    internal static ButtonNode IconButton(string icon, Action action, string label, string id)
    {
        var path = icon switch
        {
            "plus" => "M 3,8 L 13,8 M 8,3 L 8,13",
            "minus" => "M 3,7.8 L 13,7.8 L 13,8.2 L 3,8.2 Z",
            "back" => "M 10,3 L 5,8 L 10,13",
            "undo" => "M 5,3 L 2,6 L 5,9 M 2,6 L 9,6 C 12,6 14,8 14,11 C 14,13 12,15 9,15",
            _ => "M 4,4 L 12,12 M 12,4 L 4,12",
        };
        var shape = new GlyphNode
        {
            Data = Geometry.Parse(path),
            Stroke = Mint,
            StrokeThickness = 1.8,
            Width = 14,
            Height = 14,
            Stretch = Stretch.Uniform,
        };
        var button = Named(new ButtonNode
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
        button.AccessibleName = label;
        button.ToolTip = label;
        button.Click += (_, _) => action();
        return button;
    }

    internal static ChoiceNode ObjectField(StackNode panel, string label, IEnumerable<(int Id, string Name)> values,
        int selected, string id, bool optional = false, bool historical = false)
    {
        panel.Children.Add(Text(label, 12, Muted));
        var entries = values.Select(v => new EntityChoice(v.Id, v.Name)).ToList();
        if (optional)
            entries.Insert(0, new EntityChoice(0, "无"));
        if (historical && entries.All(entry => entry.Id != selected))
            entries.Add(new EntityChoice(selected, $"历史记录 #{selected}（已不存在）"));
        var picker = Named(new ChoiceNode
        {
            ItemsSource = entries,
            SelectedItem = entries.FirstOrDefault(e => e.Id == selected),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 36,
        }, id);
        panel.Children.Add(picker);
        return picker;
    }

    internal static int Integer(ChoiceNode field)
    {
        return (field.SelectedItem as EntityChoice)?.Id ?? throw new ArgumentException("请选择有效的对象。");
    }

    internal NumberNode Field(StackNode panel, string label, double value, string id, double? maximumOverride = null)
    {
        panel.Children.Add(Text(label, 12, Muted));
        var ratio = label.EndsWith("0–1") || label.Contains("0 至 1");
        var impact = label.Contains("−1");
        var integer = label.Contains("刻序") || label.Contains("次数") || label.Contains("编号") ||
                      label.EndsWith(" X") ||
                      label.EndsWith(" Y");
        var maximum = maximumOverride ?? (ratio || impact ? 1
            : label.Contains("0–100") || label is "疲劳" or "社交需求" or "魔法天赋" or "魔法训练" ? 100
            : label is "年龄" or "魔力" ? 1000
            : label.StartsWith("疫病") ? 10000d / SimulationTime.TicksPerDay
            : label.EndsWith(" X") ? _engine.State.Width - 1
            : label.EndsWith(" Y") ? _engine.State.Height - 1
            : label.Contains("保持日数") ? 100_000d / SimulationTime.TicksPerDay
            : label.Contains("刻序") ? _engine.State.Tick : 1_000_000);
        var box = Named(new NumberNode
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

    internal static double Number(NumberNode field)
    {
        var value = field.Value ?? throw new ArgumentException("请输入有效数值。");
        // 输入未改变时保留存储的精确浮点数，避免格式化显示造成隐式编辑。
        return field.Tag is double original && value == (decimal)original ? original : (double)value;
    }

    internal static int Integer(NumberNode field)
    {
        var value = field.Value ?? throw new ArgumentException("请输入整数。");
        if (value != decimal.Truncate(value) || value < int.MinValue || value > int.MaxValue)
            throw new ArgumentException("请输入范围内的整数。");
        return (int)value;
    }

    internal static void AddDatePreview(StackNode panel, NumberNode input, string label)
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

    internal sealed record EntityChoice(int Id, string Label)
    {
        public override string ToString()
        {
            return Label;
        }
    }

    internal string NationName(int id)
    {
        return _engine.State.Nations.FirstOrDefault(n => n.Id == id)?.Name ?? (id == 0 ? "世界" : $"国家 #{id}（已消亡）");
    }

    internal string ResidentName(int id)
    {
        return _engine.GetResident(id)?.Name ?? (id == 0 ? "未指定" : $"居民 #{id}");
    }

    internal string TownName(int id)
    {
        return _engine.State.Settlements.FirstOrDefault(t => t.Id == id)?.Name ?? (id == 0 ? "无" : $"聚落 #{id}");
    }

    internal static string DateLabel(long tick) => GameText.Date(tick);

    internal static string StockLabel(ResourceStock stock)
    {
        return ResourceStock.Format(stock) is { Length: > 0 } text ? text : "暂无库存";
    }

    internal static string ImportanceName(EventImportance value)
    {
        return value switch
        {
            EventImportance.Routine => "普通",
            EventImportance.Notable => "重要日常",
            EventImportance.Major => "重大",
            _ => "历史转折",
        };
    }

    internal static string GoalName(AgentGoalKind value)
    {
        return value switch
        {
            AgentGoalKind.Idle => "重新选择任务",
            AgentGoalKind.Eat => "寻找食物",
            AgentGoalKind.Gather => "采集资源",
            AgentGoalKind.Work => "生产劳动",
            AgentGoalKind.Rest => "休息恢复",
            AgentGoalKind.Rescue => "背负送回住宅救助",
            AgentGoalKind.Flee => "逃离危险",
            AgentGoalKind.Socialize => "交流消息",
            AgentGoalKind.DeliverMessage => "传递消息",
            AgentGoalKind.Trade => "运输货物",
            AgentGoalKind.Petition => "表达诉求",
            AgentGoalKind.Study => "学习研究",
            AgentGoalKind.TrainMagic => "魔法训练",
            AgentGoalKind.March => "执行军令",
            AgentGoalKind.Migrate => "迁往新家园",
            AgentGoalKind.Explore => "实地探索",
            AgentGoalKind.ClaimLand => "占领地块",
            AgentGoalKind.FetchWater => "打水或寻找水源",
            AgentGoalKind.Hunt => "狩猎",
            AgentGoalKind.Fish => "捕鱼",
            AgentGoalKind.Sleep => "睡眠",
            AgentGoalKind.ExtinguishFire => "用水扑救火灾",
            _ => "返回家园",
        };
    }

    internal static string EventKindName(WorldEventKind value)
    {
        return value switch
        {
            WorldEventKind.Founding => "建国定居",
            WorldEventKind.Growth => "发展人口",
            WorldEventKind.Trade => "贸易运输",
            WorldEventKind.Diplomacy => "外交",
            WorldEventKind.War => "战争",
            WorldEventKind.Disaster => "灾害",
            WorldEventKind.Death => "死亡",
            WorldEventKind.Editor => "玩家编辑",
            WorldEventKind.Personal => "个人经历",
            WorldEventKind.Communication => "消息通信",
            WorldEventKind.Culture => "文化",
            WorldEventKind.Policy => "制度政策",
            WorldEventKind.Research => "研究",
            WorldEventKind.Construction => "建设",
            _ => "魔法",
        };
    }

    internal static bool DetailsVisible(ViewModels.Details.VisualNode node)
    {
        for (var parent = node.Parent; parent is not null; parent = parent.Parent)
            if (parent is ViewModels.Details.ExpanderNode { IsExpanded: false })
                return false;
        return true;
    }

    internal string ResidentTask(Resident person)
    {
        return _engine.GetResidentTaskSummary(person.Id);
    }

    internal string BuildingTask(Building building)
    {
        return !building.IsCompleted ? $"施工 {building.ConstructionProgress / building.ConstructionRequired:P0}"
            : !building.Enabled ? "已停用" : _engine.GetProductionStatus(building.Id).Split('\n')[0];
    }

    internal static string FactKindName(AgentFactKind kind)
    {
        return kind switch
        {
            AgentFactKind.FoodSupply => "粮食供给",
            AgentFactKind.Danger => "危险",
            AgentFactKind.SettlementLocation => "聚落位置",
            AgentFactKind.ReliefRequest => "救济请求",
            AgentFactKind.Policy => "政策",
            AgentFactKind.WarOrder => "战争命令",
            AgentFactKind.PeaceOrder => "和平命令",
            AgentFactKind.Culture => "文化",
            AgentFactKind.Research => "研究",
            AgentFactKind.TradeExchange => "贸易往来",
            AgentFactKind.DiplomaticNotice => "外交声明",
            AgentFactKind.WarReport => "前线战报",
            AgentFactKind.WaterSource => "取水地点",
            AgentFactKind.FoundingSite => "建村勘察",
            _ => "个人记忆",
        };
    }

    internal static string ExperienceName(PersonalExperienceKind experience)
    {
        return experience switch
        {
            PersonalExperienceKind.Hardship => "艰难遭遇",
            PersonalExperienceKind.Achievement => "取得成就",
            PersonalExperienceKind.Kindness => "得到善意",
            PersonalExperienceKind.Betrayal => "遭遇背叛",
            PersonalExperienceKind.Learning => "学习成长",
            _ => "中性经历",
        };
    }

    internal static void AddDatePreview(StackNode panel, TextInputNode input, string label)
    {
        var preview = Paragraph("");

        void Update()
        {
            preview.Text = long.TryParse(input.Text, out var tick) && tick >= 0
                ? $"{label}：{DateLabel(tick)}"
                : $"{label}：请输入非负刻序";
        }

        input.TextChanged += (_, _) => Update();
        Update();
        panel.Children.Add(preview);
    }

    internal static TextInputNode Field(StackNode panel, string label, object value, string id)
    {
        panel.Children.Add(Text(label, 12, Muted));
        var text = value is IFormattable format
            ? format.ToString(null, CultureInfo.InvariantCulture)
            : value.ToString();
        var box = Named(new TextInputNode { Text = text, HorizontalAlignment = HorizontalAlignment.Stretch }, id);
        panel.Children.Add(box);
        return box;
    }

    internal static ChoiceNode EnumField<T>(StackNode panel, string label, T value, Func<T, string> name, string id)
        where T : struct, Enum
    {
        panel.Children.Add(Text(label, 12, Muted));
        var picker = Named(new ChoiceNode
        {
            ItemsSource = Enum.GetValues<T>(),
            SelectedItem = value,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            DisplayLabel = item => name((T)item),
        }, id);
        panel.Children.Add(picker);
        return picker;
    }

    internal static double Number(TextInputNode field)
    {
        if (!double.TryParse(field.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
            !double.IsFinite(value))
            throw new ArgumentException("请输入有效数值。");
        return value;
    }

    internal static int Integer(TextInputNode field)
    {
        if (!int.TryParse(field.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            throw new ArgumentException("请输入有效整数。");
        return value;
    }

    internal NumberNode[] StockFields(StackNode panel, ResourceStock stock, string prefix)
    {
        return
        [
            .. ResourceStock.Kinds.Select(kind => Field(panel, ResourceStock.Name(kind), stock.Get(kind),
                prefix + "-" + kind.ToString().ToLowerInvariant())),
        ];
    }

    internal static ResourceStock ReadStock(NumberNode[] fields)
    {
        var stock = new ResourceStock();
        for (var i = 0; i < fields.Length; i++)
            stock = stock.WithAmount(ResourceStock.Kinds[i], Number(fields[i]));
        return stock;
    }

    internal string CultureName(int id)
    {
        return _engine.State.Society.Cultures.FirstOrDefault(c => c.Id == id)?.Name ?? $"文化 #{id}";
    }

    internal static string InstitutionName(InstitutionKind value)
    {
        return value switch
        {
            InstitutionKind.Council => "居民议事会",
            InstitutionKind.Monarchy => "君主制",
            _ => "行会议会",
        };
    }

    internal static string SpellName(SpellKind value)
    {
        return WorldEngine.SpellName(value);
    }

    internal ViewModels.Details.StackNode ModalPanel(string title, string description)
    {
        var panel = new ViewModels.Details.StackNode { Spacing = 5, MaxWidth = 460 };
        var header = new ViewModels.Details.GridNode { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(Text(title, 17, null, true));
        var close = IconButton("close", CloseModal, "关闭窗口", "modal-close");
        ViewModels.Details.GridNode.SetColumn(close, 1);
        header.Children.Add(close);
        panel.Children.Add(header);
        panel.Children.Add(Paragraph(description));
        return panel;
    }

    internal static string FriendlyError(Exception ex)
    {
        return ex.Message.Length > 160 ? ex.Message[..160] : ex.Message;
    }

    internal static ViewModels.Details.TextNode Text(string text, double size = 13, IBrush? color = null, bool bold = false)
    {
        return new ViewModels.Details.TextNode
        {
            Text = text,
            FontSize = size,
            Foreground = color ?? Brush.Parse("#E9EFEB"),
            FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    internal static ViewModels.Details.TextNode Paragraph(string text)
    {
        return new ViewModels.Details.TextNode
        {
            Text = text,
            FontSize = 12,
            Foreground = Muted,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 16,
        };
    }

    internal static ButtonNode Button(string label, Action action, string? tooltip = null, double minWidth = 0)
    {
        var button = Button(label, (UiCommand?)null, tooltip, minWidth);
        button.Click += (_, _) => action();
        return button;
    }

    internal static ButtonNode Button(string label, UiCommand? command, string? tooltip = null, double minWidth = 0)
    {
        var button = new ButtonNode(command)
        {
            Content = label,
            MinWidth = minWidth,
            MinHeight = 30,
            FontSize = 12,
            Padding = new Thickness(8, 3),
            Margin = new Thickness(2, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        if (tooltip is not null)
            button.ToolTip = tooltip;
        var id = label switch
        {
            "新世界" => "header-new-world",
            "存档" => "header-storage",
            "概览" => "header-overview",
            "+" => "map-zoom-in",
            "-" => "map-zoom-out",
            "全图" => "map-fit",
            "保存到本机" => "storage-save",
            "读取本机存档" => "storage-load",
            "导出世界文件" => "storage-export",
            "导入世界文件" => "storage-import",
            "撤销本轮编辑" => "storage-undo",
            "撤销" => "world-undo",
            "创造世界" => "world-create-apply",
            "应用变更" => "nation-apply",
            "编辑这个国家" => "nation-edit",
            _ => null,
        };
        if (id is not null)
            Named(button, id);
        return button;
    }

    internal static T Named<T>(T control, string id) where T : ViewModels.Details.VisualNode
    {
        control.Name = id.Replace('-', '_');
        control.AutomationId = id;
        return control;
    }

    internal static ViewModels.Details.BorderNode Card(ViewModels.Details.VisualNode child)
    {
        return new ViewModels.Details.BorderNode
        {
            Child = child,
            Background = Ink,
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(8),
        };
    }

    internal static ViewModels.Details.BorderNode StatCard(string label, string value, string hint)
    {
        var stack = new ViewModels.Details.StackNode { Spacing = 6 };
        stack.Children.Add(Text(label, 10, Muted));
        stack.Children.Add(Text(value, 28, Mint, true));
        stack.Children.Add(Text(hint, 9, Muted));
        return Card(stack);
    }

    internal static string RaceName(RaceKind race)
    {
        return race switch
        {
            RaceKind.Human => "人类",
            RaceKind.Elf => "精灵",
            RaceKind.Dwarf => "矮人",
            _ => "兽人",
        };
    }

    internal static string TerrainName(TerrainType terrain) => GameText.Terrain(terrain);

    internal static string ProfessionName(Profession job)
    {
        return WorldEngine.ProfessionName(job);
    }

    internal static string ActivityName(ResidentActivity activity)
    {
        return activity switch
        {
            ResidentActivity.Wandering => "探索土地",
            ResidentActivity.Working => "正在工作",
            ResidentActivity.Hungry => "寻找食物",
            ResidentActivity.Marching => "正在行军",
            ResidentActivity.Sick => "正在养病",
            ResidentActivity.Eating => "正在吃喝",
            ResidentActivity.Sleeping => "正在睡觉",
            ResidentActivity.Unconscious => "身体储备耗尽，正在昏迷",
            ResidentActivity.Resting => "正在休息",
            ResidentActivity.Talking => "交换消息",
            ResidentActivity.Delivering => "执行运输",
            ResidentActivity.Studying => "正在学习",
            ResidentActivity.Casting => "正在施法",
            _ => "躲避危险",
        };
    }

}
