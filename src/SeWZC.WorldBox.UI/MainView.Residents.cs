using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    private void BuildResidentInspector(StackPanel panel)
    {
        var id = _selectedResidentId;
        if (_engine.GetResident(id) is not { } resident) { panel.Children.Add(Paragraph("这位居民的记录已不在当前世界中。")); return; }
        Resident Current() => _engine.GetResident(id) ?? resident;
        panel.Children.Add(LiveText(() => $"{Current().Name}  #{id}", 18, Mint));
        panel.Children.Add(LiveText(() => $"{RaceName(Current().Race)} · {Current().Age:F1} 岁 · {ProfessionName(Current().Profession)}\n{NationName(Current().NationId)} / {TownName(Current().SettlementId)}\n文化：{CultureName(Current().CultureId)}"));
        var actions = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 5 };
        var locate = Named(Button("定位", () => { _map.FocusResident(id); if (_isCompact) { _mobilePanel = false; ApplyLayout(); } }), "resident-locate"); actions.Children.Add(locate);
        var follow = Named(new CheckBox { Content = Text("跟随", 12), IsChecked = _map.FollowSelectedResident }, "resident-follow");
        var syncingFollow = false;
        follow.IsCheckedChanged += (_, _) => { if (syncingFollow) return; _map.SelectResident(id, follow.IsChecked == true); if (follow.IsChecked == true) _map.FocusResident(id); };
        _inspectorUpdates.Add(() =>
        {
            var actual = _map.FollowSelectedResident;
            if (follow.IsChecked == actual) return;
            syncingFollow = true; follow.IsChecked = actual; syncingFollow = false;
        });
        Grid.SetColumn(follow, 1); actions.Children.Add(follow);
        var edit = Named(Button("编辑", () => ShowResidentEditor(id)), "resident-edit"); Grid.SetColumn(edit, 2); actions.Children.Add(edit); panel.Children.Add(actions);
        panel.Children.Add(LiveText(() => _engine.State.ArchivedResidents.Any(r => r.Id == id) ? "此人已离世：档案修改不会使其复生。" : ""));
        panel.Children.Add(Text("实际状态 · 世界当前事实", 12, Mint));
        panel.Children.Add(LiveText(() =>
        {
            var r = Current();
            return $"位置 {r.X}, {r.Y} · {ActivityName(r.Activity)}\n生命 {r.Health:F1} · 饥饿 {r.Hunger:F1} · 疫病 {r.SicknessTicks} 日\n疲劳 {r.Agent.Fatigue:F1} · 社交需求 {r.Agent.SocialNeed:F1}\n特质：{r.Trait}\n随身库存：{StockLabel(r.Inventory)}\n魔力 {r.Mana:F1} · 天赋 {r.MagicTalent:F1} · 训练 {r.MagicTraining:F1}\n军队 {(r.ArmyId == 0 ? "无" : r.ArmyId.ToString())} · 家园 {TownName(r.SettlementId)}";
        }));
        panel.Children.Add(Text("当前目标 · 居民自己的理由", 12, Mint));
        panel.Children.Add(LiveText(() =>
        {
            var goal = Current().Agent.Goal;
            return $"{GoalName(goal.Kind)} → {goal.TargetX}, {goal.TargetY}\n{goal.Reason}\n开始：{DateLabel(goal.StartedTick)} · {(goal.PlayerDirected ? "玩家指定" : "自主选择")}\n下次考虑：{DateLabel(Current().Agent.NextThinkTick)}";
        }));
        panel.Children.Add(Named(Button("编辑目标与人格", () => ShowGoalEditor(id)), "resident-goal-edit"));
        panel.Children.Add(Text("性格倾向", 12, Mint));
        panel.Children.Add(LiveText(() => { var p = Current().Agent.Personality; return $"勇气 {p.Courage:P0} · 勤勉 {p.Diligence:P0}\n社交 {p.Sociability:P0} · 抱负 {p.Ambition:P0}"; }));
        panel.Children.Add(Text("已知消息与记忆 · 可能过时或有误", 12, Mint));
        panel.Children.Add(Paragraph("下面是居民知道的内容，不等同于全世界的即时状态。改动只影响今后的认知与决策，不会重写已经发生的世界事件。"));
        LiveRows(panel, () => Current().Agent.Memory.OrderByDescending(f => f.LearnedTick).Take(40), fact => fact.Id.ToString(), fact => FactLabel(fact), fact => ShowMemoryEditor(id, fact.Id));
        panel.Children.Add(Named(Button("添加一条记忆", () => ShowMemoryEditor(id, null)), "resident-memory-add"));
        panel.Children.Add(LiveText(() => $"携带消息 {Current().Agent.CarriedMessages.Count} 条 · 目的地 {TownName(Current().Agent.DestinationSettlementId)}"));
        panel.Children.Add(Text("真实决策记录", 12, Mint));
        LiveRows(panel, () => Current().Agent.Decisions.AsEnumerable().Reverse().Take(20), d => $"{d.Tick}:{d.Goal}:{d.EvidenceFactId}", d => $"{DateLabel(d.Tick)} · {GoalName(d.Goal)}\n{d.Reason}\n评估 {d.Score:F2} · 依据记忆 #{d.EvidenceFactId}\n消息观察时间 {DateLabel(d.KnowledgeObservedTick)} · 来源 {ResidentName(d.SourceResidentId)}");
        panel.Children.Add(Text("个人履历", 12, Mint));
        LiveRows(panel, () => Current().History.Select((entry, index) => (entry, index)).Reverse().Take(40), item => item.index.ToString(), item => $"{ImportanceName(item.entry.Importance)} · {DateLabel(item.entry.Tick)}\n{item.entry.Text}\n经历类型 {ExperienceName(item.entry.Experience)} · 心理影响 {item.entry.Impact:+0.00;-0.00;0}{(item.entry.PlayerEdited ? " · 玩家编辑" : "")}", item => ShowHistoryEntryEditor(id, item.index));
        panel.Children.Add(Named(Button("添加个人经历", () => ShowHistoryEntryEditor(id, null)), "resident-history-add"));
        panel.Children.Add(Named(Button("高级历史编辑（全部字段）", () => ShowResidentJsonEditor(id, true)), "resident-history-edit"));
        panel.Children.Add(Named(Button("高级认知编辑（全部字段）", () => ShowResidentJsonEditor(id, false)), "resident-mind-json"));
    }

    private string FactLabel(AgentFact fact) => $"{FactKindName(fact.Kind)} · 置信度 {fact.Confidence:P0}\n{fact.Text}\n观察 {DateLabel(fact.ObservedTick)} · 获知 {DateLabel(fact.LearnedTick)}\n消息年龄 {Math.Max(0, _engine.State.Tick - fact.ObservedTick)} 日 · 经过 {fact.Hops} 次转述\n来源 {ResidentName(fact.SourceResidentId)} · 地点 {fact.X},{fact.Y} · 值 {fact.Value:F1}";
    private static string FactKindName(AgentFactKind kind) => kind switch { AgentFactKind.FoodSupply => "粮食供给", AgentFactKind.Danger => "危险", AgentFactKind.SettlementLocation => "聚落位置", AgentFactKind.ReliefRequest => "救济请求", AgentFactKind.Policy => "政策", AgentFactKind.WarOrder => "战争命令", AgentFactKind.PeaceOrder => "和平命令", AgentFactKind.Culture => "文化", AgentFactKind.Research => "研究", _ => "个人记忆" };

    private void ShowResidentEditor(int id)
    {
        var resident = _engine.GetResident(id); if (resident is null) return;
        _paused = true; _map.IsSimulationPaused = true; RefreshUi();
        var panel = ModalPanel("编辑居民档案", "变更先统一校验，再应用到暂停中的世界。姓名与属性可编辑；编号、当前动作、移动插值由模拟维护。归属通过居住聚落确定。");
        var identity = new StackPanel { Spacing = 10 }; var condition = new StackPanel { Spacing = 10 }; var belonging = new StackPanel { Spacing = 10 }; var magic = new StackPanel { Spacing = 10 }; var possessions = new StackPanel { Spacing = 10 };
        var tabs = Named(new TabControl { ItemsSource = new[] { Named(new TabItem { Header = Text("身份", 12), Content = identity }, "resident-tab-identity"), Named(new TabItem { Header = Text("生理", 12), Content = condition }, "resident-tab-condition"), Named(new TabItem { Header = Text("归属", 12), Content = belonging }, "resident-tab-belonging"), Named(new TabItem { Header = Text("魔法", 12), Content = magic }, "resident-tab-magic"), Named(new TabItem { Header = Text("物品", 12), Content = possessions }, "resident-tab-possessions") }, SelectedIndex = 0 }, "resident-editor-tabs");
        panel.Children.Add(tabs);
        var name = Field(identity, "姓名", resident.Name, "resident-name");
        var trait = Field(identity, "特质", resident.Trait, "resident-trait");
        var race = EnumField(identity, "种族", resident.Race, RaceName, "resident-race");
        var profession = EnumField(identity, "职业", resident.Profession, ProfessionName, "resident-profession");
        var culture = Field(belonging, "文化编号", resident.CultureId, "resident-culture");
        var home = Field(belonging, "居住聚落编号", resident.SettlementId, "resident-settlement");
        belonging.Children.Add(Paragraph(string.Join(" · ", _engine.State.Settlements.Select(t => $"{t.Id} {t.Name}"))));
        var age = Field(condition, "年龄", resident.Age, "resident-age");
        var health = Field(condition, "生命 0–100", resident.Health, "resident-health");
        var hunger = Field(condition, "饥饿 0–100", resident.Hunger, "resident-hunger");
        var sickness = Field(condition, "疫病剩余日数", resident.SicknessTicks, "resident-sickness");
        var x = Field(belonging, "位置 X", resident.X, "resident-x"); var y = Field(belonging, "位置 Y", resident.Y, "resident-y");
        var army = Field(belonging, "军队编号 · 0 表示无", resident.ArmyId, "resident-army");
        var mana = Field(magic, "魔力", resident.Mana, "resident-mana");
        var talent = Field(magic, "魔法天赋", resident.MagicTalent, "resident-magic-talent");
        var training = Field(magic, "魔法训练", resident.MagicTraining, "resident-magic-training");
        possessions.Children.Add(Text("随身库存", 13, Mint));
        var inventory = StockFields(possessions, resident.Inventory, "resident-inventory");
        panel.Children.Add(Named(Button("应用档案变更", () =>
        {
            try
            {
                var patch = new ResidentEdit
                {
                    Name = name.Text ?? "", Trait = trait.Text ?? "", Race = (RaceKind)race.SelectedItem!, Profession = (Profession)profession.SelectedItem!,
                    CultureId = Integer(culture), SettlementId = Integer(home), Age = Number(age), Health = Number(health), Hunger = Number(hunger), SicknessTicks = Integer(sickness),
                    X = Integer(x), Y = Integer(y), ArmyId = Integer(army), Mana = Number(mana), MagicTalent = Number(talent), MagicTraining = Number(training), Inventory = ReadStock(inventory)
                };
                BeginEdit(); _engine.EditResident(id, patch); CloseModal(); _map.RefreshWorld(); RefreshUi(true); SetStatus("居民档案已更新 · 可撤销");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { SetStatus("未应用变更：" + FriendlyError(ex)); }
        }), "resident-apply"));
        OpenModal(panel);
    }

    private AgentState CloneMind(int id) => JsonSerializer.Deserialize(_engine.ExportResidentMind(id), ResidentUiJsonContext.Default.AgentState) ?? throw new ArgumentException("认知数据为空。");
    private void ShowGoalEditor(int id)
    {
        var resident = _engine.GetResident(id); if (resident is null) return;
        _paused = true; _map.IsSimulationPaused = true;
        var panel = ModalPanel("目标、性格与需求", "目标在未来的行动中执行。目标地点、实体和聚落必须有效；自主思考仍会考虑危险与基本需求。");
        var mind = CloneMind(id);
        var goal = EnumField(panel, "当前目标", mind.Goal.Kind, GoalName, "resident-goal");
        var reason = Field(panel, "实际采用的理由", mind.Goal.Reason, "resident-goal-reason");
        var x = Field(panel, "目标 X", mind.Goal.TargetX, "resident-goal-x"); var y = Field(panel, "目标 Y", mind.Goal.TargetY, "resident-goal-y");
        var town = Field(panel, "目标聚落编号", mind.Goal.TargetSettlementId, "resident-goal-town");
        var entity = Field(panel, "目标实体编号", mind.Goal.TargetEntityId, "resident-goal-entity");
        var duration = Field(panel, "目标保持日数", Math.Max(24, mind.Goal.ReviewTick - _engine.State.Tick), "resident-goal-duration");
        var fatigue = Field(panel, "疲劳", mind.Fatigue, "resident-fatigue"); var social = Field(panel, "社交需求", mind.SocialNeed, "resident-social-need");
        var courage = Field(panel, "勇气 0–1", mind.Personality.Courage, "resident-courage");
        var diligence = Field(panel, "勤勉 0–1", mind.Personality.Diligence, "resident-diligence");
        var sociability = Field(panel, "社交 0–1", mind.Personality.Sociability, "resident-sociability");
        var ambition = Field(panel, "抱负 0–1", mind.Personality.Ambition, "resident-ambition");
        panel.Children.Add(Named(Button("应用目标与人格", () =>
        {
            try
            {
                mind.Goal.Kind = (AgentGoalKind)goal.SelectedItem!; mind.Goal.Reason = reason.Text ?? ""; mind.Goal.TargetX = Integer(x); mind.Goal.TargetY = Integer(y);
                mind.Goal.TargetSettlementId = Integer(town); mind.Goal.TargetEntityId = Integer(entity); mind.Goal.PlayerDirected = true; mind.Goal.StartedTick = _engine.State.Tick; mind.Goal.ReviewTick = _engine.State.Tick + Integer(duration);
                mind.Fatigue = Number(fatigue); mind.SocialNeed = Number(social); mind.Personality.Courage = Number(courage); mind.Personality.Diligence = Number(diligence); mind.Personality.Sociability = Number(sociability); mind.Personality.Ambition = Number(ambition);
                BeginEdit(); _engine.EditResident(id, new ResidentEdit { Agent = mind }); CloseModal(); RefreshUi(true); SetStatus("目标与人格已更新，将影响接下来的行动");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { SetStatus("未应用变更：" + FriendlyError(ex)); }
        }), "resident-goal-apply"));
        OpenModal(panel);
    }

    private void ShowMemoryEditor(int id, int? factId)
    {
        _paused = true; _map.IsSimulationPaused = true;
        var mind = CloneMind(id);
        var fact = factId.HasValue ? mind.Memory.FirstOrDefault(f => f.Id == factId) : null;
        var adding = fact is null;
        fact ??= new AgentFact { Id = 0, Kind = AgentFactKind.Personal, ObservedTick = _engine.State.Tick, LearnedTick = _engine.State.Tick, OriginResidentId = id, SourceResidentId = id, OriginProfession = _engine.GetResident(id)?.Profession ?? Profession.Child, X = _engine.GetResident(id)?.X ?? 0, Y = _engine.GetResident(id)?.Y ?? 0 };
        var panel = ModalPanel(adding ? "添加记忆" : "编辑记忆", "这是角色的认知记录，允许它与实际世界不同。修改将影响以后的决策与传播，不回算已经发生的战争、死亡或资源变化。");
        var kind = EnumField(panel, "记忆类型", fact.Kind, FactKindName, "memory-kind");
        var text = Field(panel, "内容", fact.Text, "memory-text"); text.AcceptsReturn = true; text.TextWrapping = Avalonia.Media.TextWrapping.Wrap; text.MinHeight = 88;
        var value = Field(panel, "数值", fact.Value, "memory-value"); var confidence = Field(panel, "置信度 0–1", fact.Confidence, "memory-confidence");
        var subject = Field(panel, "主题实体编号", fact.SubjectId, "memory-subject");
        var x = Field(panel, "地点 X", fact.X, "memory-x"); var y = Field(panel, "地点 Y", fact.Y, "memory-y");
        var observed = Field(panel, "观察日序（0 起）", fact.ObservedTick, "memory-observed"); var learned = Field(panel, "获知日序（0 起）", fact.LearnedTick, "memory-learned");
        AddDatePreview(panel, observed, "观察时间"); AddDatePreview(panel, learned, "获知时间");
        var origin = Field(panel, "最初观察者编号", fact.OriginResidentId, "memory-origin"); var source = Field(panel, "消息来源居民编号", fact.SourceResidentId, "memory-source");
        var hops = Field(panel, "转述次数", fact.Hops, "memory-hops");
        panel.Children.Add(Named(Button("保存记忆", () =>
        {
            try
            {
                fact.Kind = (AgentFactKind)kind.SelectedItem!; fact.Text = text.Text ?? ""; fact.Value = Number(value); fact.Confidence = Number(confidence); fact.SubjectId = Integer(subject);
                fact.X = Integer(x); fact.Y = Integer(y); fact.ObservedTick = Integer(observed); fact.LearnedTick = Integer(learned); fact.OriginResidentId = Integer(origin); fact.SourceResidentId = Integer(source); fact.Hops = Integer(hops);
                if (adding) mind.Memory.Add(fact);
                BeginEdit(); _engine.EditResident(id, new ResidentEdit { Agent = mind }); CloseModal(); RefreshUi(true); SetStatus("记忆已更新，世界历史保持原样");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { if (adding) mind.Memory.Remove(fact); SetStatus("未应用变更：" + FriendlyError(ex)); }
        }), "memory-apply"));
        if (!adding) panel.Children.Add(Named(Button("删除这条记忆", () =>
        {
            try { mind.Memory.Remove(fact); BeginEdit(); _engine.EditResident(id, new ResidentEdit { Agent = mind }); CloseModal(); RefreshUi(true); SetStatus("这条记忆已移除"); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { SetStatus(FriendlyError(ex)); }
        }), "memory-delete"));
        OpenModal(panel);
    }

    private static string ExperienceName(PersonalExperienceKind experience) => experience switch
    { PersonalExperienceKind.Hardship => "艰难遭遇", PersonalExperienceKind.Achievement => "取得成就", PersonalExperienceKind.Kindness => "得到善意", PersonalExperienceKind.Betrayal => "遭遇背叛", PersonalExperienceKind.Learning => "学习成长", _ => "中性经历" };
    private void ShowHistoryEntryEditor(int id, int? index)
    {
        _paused = true; _map.IsSimulationPaused = true;
        var history = JsonSerializer.Deserialize(_engine.ExportResidentHistory(id), ResidentUiJsonContext.Default.ListResidentHistoryEntry) ?? [];
        var adding = !index.HasValue || index < 0 || index >= history.Count;
        var entry = adding ? new ResidentHistoryEntry { Tick = _engine.State.Tick, PlayerEdited = true } : history[index!.Value];
        var panel = ModalPanel(adding ? "添加个人经历" : "编辑个人经历", "这份个人履历独立于世界编年史。经历类型与影响数值改变今后的性格倾向；文字用于记录，不会被自动理解成新的世界事实。过去的资源、死亡、战争不回算。");
        var text = Field(panel, "经历内容", entry.Text, "history-entry-text"); text.AcceptsReturn = true; text.TextWrapping = Avalonia.Media.TextWrapping.Wrap; text.MinHeight = 88;
        var tick = Field(panel, "发生日序（0 起）", entry.Tick, "history-entry-tick"); AddDatePreview(panel, tick, "发生时间");
        var importance = EnumField(panel, "重要程度", entry.Importance, ImportanceName, "history-entry-importance");
        var experience = EnumField(panel, "经历类型", entry.Experience, ExperienceName, "history-entry-experience");
        var impact = Field(panel, "心理影响 −1 至 1", entry.Impact, "history-entry-impact");
        panel.Children.Add(Paragraph("艰难与成就影响勇气，善意与背叛影响社交，学习影响勤勉。正负数值决定影响方向。"));
        panel.Children.Add(Named(Button("保存个人经历", () =>
        {
            try
            {
                entry.Text = text.Text ?? ""; entry.Tick = Integer(tick); entry.Importance = (EventImportance)importance.SelectedItem!; entry.Experience = (PersonalExperienceKind)experience.SelectedItem!; entry.Impact = Number(impact); entry.PlayerEdited = true;
                if (adding) history.Add(entry);
                BeginEdit(); _engine.EditResident(id, new ResidentEdit { History = history }); CloseModal(); RefreshUi(true); SetStatus("个人经历已更新，将影响今后的性格与行为");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { if (adding) history.Remove(entry); SetStatus("未应用变更：" + FriendlyError(ex)); }
        }), "history-entry-apply"));
        if (!adding) panel.Children.Add(Named(Button("删除这条个人经历", () =>
        {
            try { history.Remove(entry); BeginEdit(); _engine.EditResident(id, new ResidentEdit { History = history }); CloseModal(); RefreshUi(true); SetStatus("个人经历已移除，世界历史未改变"); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { SetStatus(FriendlyError(ex)); }
        }), "history-entry-delete"));
        OpenModal(panel);
    }

    private void ShowResidentJsonEditor(int id, bool history)
    {
        _paused = true; _map.IsSimulationPaused = true;
        var panel = ModalPanel(history ? "编辑个人经历与历史" : "高级认知编辑", history ? "个人历史与世界事件独立。Experience 与 Impact 是结构化心理影响；文本不会被当作可执行命令。仅影响今后认知，不重新计算过去的世界。" : "包含目标、人格、全部记忆、决策依据、携带消息与任务。字段统一经过模拟核心校验。只修改这个角色的认知，不会改动世界事实。");
        var input = Named(new TextBox { Text = history ? _engine.ExportResidentHistory(id) : _engine.ExportResidentMind(id), AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MinHeight = 280, MaxHeight = 420 }, history ? "resident-history-json" : "resident-mind-json-input");
        panel.Children.Add(input);
        panel.Children.Add(Named(Button("校验并应用", () =>
        {
            try
            {
                BeginEdit(); if (history) _engine.EditResidentHistoryJson(id, input.Text ?? ""); else _engine.EditResidentMindJson(id, input.Text ?? "");
                CloseModal(); RefreshUi(true); SetStatus("角色记录已更新，将影响未来行为");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or JsonException) { SetStatus("未应用变更：" + FriendlyError(ex)); }
        }), history ? "resident-history-apply" : "resident-mind-apply"));
        OpenModal(panel);
    }

    private static void AddDatePreview(StackPanel panel, TextBox input, string label)
    {
        var preview = Paragraph("");
        void Update() => preview.Text = long.TryParse(input.Text, out var tick) && tick >= 0 ? $"{label}：{DateLabel(tick)}" : $"{label}：请输入非负日序";
        input.TextChanged += (_, _) => Update(); Update(); panel.Children.Add(preview);
    }

    private static TextBox Field(StackPanel panel, string label, object value, string id)
    {
        panel.Children.Add(Text(label, 12, Muted));
        var text = value is IFormattable format ? format.ToString(null, CultureInfo.InvariantCulture) : value.ToString();
        var box = Named(new TextBox { Text = text, HorizontalAlignment = HorizontalAlignment.Stretch }, id); panel.Children.Add(box); return box;
    }
    private static ComboBox EnumField<T>(StackPanel panel, string label, T value, Func<T, string> name, string id) where T : struct, Enum
    {
        panel.Children.Add(Text(label, 12, Muted));
        var picker = Named(new ComboBox { ItemsSource = Enum.GetValues<T>(), SelectedItem = value, HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<T>((item, _) => Text(name(item), 12)) }, id);
        panel.Children.Add(picker); return picker;
    }
    private static double Number(TextBox field)
    {
        if (!double.TryParse(field.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value)) throw new ArgumentException("请输入有效数值。");
        return value;
    }
    private static int Integer(TextBox field)
    {
        if (!int.TryParse(field.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) throw new ArgumentException("请输入有效整数。"); return value;
    }
    private static TextBox[] StockFields(StackPanel panel, ResourceStock stock, string prefix) =>
        [Field(panel, "粮食", stock.Food, prefix + "-food"), Field(panel, "木材", stock.Wood, prefix + "-wood"), Field(panel, "石材", stock.Stone, prefix + "-stone"), Field(panel, "矿产", stock.Ore, prefix + "-ore")];
    private static ResourceStock ReadStock(TextBox[] fields) => new() { Food = Number(fields[0]), Wood = Number(fields[1]), Stone = Number(fields[2]), Ore = Number(fields[3]) };
}

[JsonSerializable(typeof(AgentState))]
[JsonSerializable(typeof(List<ResidentHistoryEntry>))]
internal partial class ResidentUiJsonContext : JsonSerializerContext;
