using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    private void BuildResidentInspector(StackPanel panel)
    {
        var id = _selectedResidentId;
        if (_engine.GetResident(id) is not { } resident)
        {
            panel.Children.Add(Paragraph("这位居民的记录已不在当前世界中。"));
            return;
        }

        Resident Current()
        {
            return _engine.GetResident(id) ?? resident;
        }

        panel.Children.Add(LiveText(() => $"{Current().Name}   {ProfessionName(Current().Profession)}", 18, Mint));
        panel.Children.Add(LiveText(() =>
            $"{RaceName(Current().Race)}   年龄 {Current().Age:F1} / 预期寿命 {WorldEngine.Lifespan(Current().Race)} 岁"));
        var belonging = new WrapPanel { Orientation = Orientation.Horizontal };
        var nation = Named(Button("查看国家", () => OpenNation(Current().NationId)), "resident-nation");
        var settlement = Named(Button("查看所属聚落", () => OpenSettlement(Current().SettlementId)),
            "resident-settlement-link");
        belonging.Children.Add(nation);
        belonging.Children.Add(settlement);
        panel.Children.Add(belonging);

        void UpdateBelonging()
        {
            var r = Current();
            nation.Content = "国家：" + NationName(r.NationId);
            nation.IsEnabled = _engine.State.Nations.Any(n => n.Id == r.NationId);
            settlement.Content = "聚落：" + TownName(r.SettlementId);
            settlement.IsEnabled = _engine.State.Settlements.Any(t => t.Id == r.SettlementId);
            ToolTip.SetTip(nation, nation.IsEnabled ? "查看所属国家" : "原属国家已不存在，保留历史归属");
            ToolTip.SetTip(settlement, settlement.IsEnabled ? "查看所属聚落" : "原属聚落已不存在，保留历史归属");
        }

        _inspectorUpdates.Add(UpdateBelonging);
        UpdateBelonging();
        panel.Children.Add(LiveText(() =>
            Current().Health <= 0
                ? $"逝世时间：{DateLabel(Current().DeathTick)}\n死亡原因：{WorldEngine.DeathCauseName(Current().DeathCause)}"
                : ""));
        panel.Children.Add(LiveText(() =>
            $"生命 {Current().Health:0} / 100   体力 {100 - Current().Agent.Fatigue:0} / 100   饥饿 {Current().Hunger:0}%   口渴 {Current().Thirst:0}%"));
        panel.Children.Add(LiveText(() => _engine.GetResidentActionSummary(id)));
        panel.Children.Add(LiveText(() =>
        {
            var agent = Current().Agent;
            if (agent.WorkplaceId != 0)
            {
                var workplace = _engine.State.Society.Buildings.FirstOrDefault(b => b.Id == agent.WorkplaceId);
                return workplace is null ? "原登记工作地已移除，返乡后重新安排"
                    : $"登记工作地：{WorldEngine.BuildingName(workplace.Kind)}（{workplace.X}, {workplace.Y}）";
            }
            return agent.WorkAreaIndex >= 0
                ? $"固定采集范围：地块（{agent.WorkAreaIndex % _engine.State.Width}, {agent.WorkAreaIndex / _engine.State.Width}）及周围三格"
                : Current().Profession == Profession.Laborer ? "暂无固定专业岗位，按眼前需求协助劳动" : "";
        }));
        BuildResearchResidentActions(panel, resident);
        if (resident.Health > 0)
        {
            var quick = new WrapPanel { Orientation = Orientation.Horizontal };
            quick.Children.Add(Named(Button("安排休息", () => QuickResidentGoal(id, AgentGoalKind.Rest)), "resident-rest"));
            quick.Children.Add(Named(Button("返回家园", () => QuickResidentGoal(id, AgentGoalKind.ReturnHome)),
                "resident-home"));
            quick.Children.Add(Named(Button("恢复自主", () => QuickResidentGoal(id, null)), "resident-autonomy"));
            quick.Children.Add(Named(
                Button("治疗",
                    () => RunEdit(() => _engine.EditResident(id, new ResidentEdit { Health = 100, SicknessTicks = 0 }),
                        "居民已得到治疗")), "resident-heal"));
            if (resident.Age >= 14)
            {
                var cast = Named(
                    Button("施放法术", () => ShowSpellSelectionEditor(SpellKind.Heal), "选择本人施法；窗口显示知识、训练和魔力要求"),
                    "resident-spell");
                _inspectorUpdates.Add(() => cast.IsEnabled = Current().Health > 0 && Current().Age >= 14);
                quick.Children.Add(cast);
            }

            panel.Children.Add(quick);
        }

        panel.Children.Add(WatchControl(ObservedObjectKind.Resident, id, "resident-watch"));
        panel.Children.Add(Named(Button("人物故事与重要转折", () => OpenInspector("story")), "resident-story"));
        var actions = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 5 };
        var locate = Named(Button("定位", () =>
        {
            _map.FocusResident(id);
            if (_isCompact)
                CloseInspector();
        }), "resident-locate");
        actions.Children.Add(locate);
        var follow = Named(new CheckBox { Content = Text("跟随", 12), IsChecked = _map.FollowSelectedResident },
            "resident-follow");
        var syncingFollow = false;
        follow.IsCheckedChanged += (_, _) =>
        {
            if (syncingFollow)
                return;
            _map.SelectResident(id, follow.IsChecked == true);
            if (follow.IsChecked == true)
                _map.FocusResident(id);
        };
        _inspectorUpdates.Add(() =>
        {
            var actual = _map.FollowSelectedResident;
            if (follow.IsChecked == actual)
                return;
            syncingFollow = true;
            follow.IsChecked = actual;
            syncingFollow = false;
        });
        Grid.SetColumn(follow, 1);
        actions.Children.Add(follow);
        var edit = Named(Button("编辑", () => ShowResidentEditor(id)), "resident-edit");
        Grid.SetColumn(edit, 2);
        actions.Children.Add(edit);
        panel.Children.Add(actions);
        var body = FoldSection(panel, "身体、库存与魔法", "resident-body");
        body.Children.Add(LiveText(() =>
        {
            var r = Current();
            return
                $"{ActivityName(r.Activity)}\n生命 {r.Health:F1}\n饥饿 {r.Hunger:F1}\n口渴 {r.Thirst:F1}\n疫病 {r.SicknessTicks} 日\n疲劳 {r.Agent.Fatigue:F1}\n社交需求 {r.Agent.SocialNeed:F1}\n特质：{r.Trait}\n随身库存：{StockLabel(r.Inventory)}\n魔力 {r.Mana:F1}\n天赋 {r.MagicTalent:F1}\n训练 {r.MagicTraining:F1}\n军队 {(r.ArmyId == 0 ? "无" : r.ArmyId.ToString())}\n家园 {TownName(r.SettlementId)}";
        }));
        var effects = FoldSection(panel, "当前加成与减益", "resident-effects");
        effects.Children.Add(LiveText(() => EffectLabel(_engine.GetResidentEffects(id))));
        var route = Named(new CheckBox { Content = "显示后续行动轨迹", IsChecked = _map.ShowResidentRoute }, "resident-route");
        route.IsCheckedChanged += (_, _) =>
        {
            _map.ShowResidentRoute = route.IsChecked == true;
            _map.InvalidateVisual();
        };
        panel.Children.Add(route);

        panel.Children.Add(Named(Button("编辑目标与人格", () => ShowGoalEditor(id)), "resident-goal-edit"));
        var secondary = FoldSection(panel, "性格、记忆与消息", "resident-cognition");
        secondary.Children.Add(Text("性格倾向", 12, Mint));
        secondary.Children.Add(LiveText(() =>
        {
            var p = Current().Agent.Personality;
            return $"勇气 {p.Courage:P0}\n勤勉 {p.Diligence:P0}\n社交 {p.Sociability:P0}\n抱负 {p.Ambition:P0}";
        }));
        secondary.Children.Add(Text("已知消息与记忆\n可能过时或有误", 12, Mint));
        secondary.Children.Add(Paragraph("下面是居民知道的内容，不等同于全世界的即时状态。改动只影响今后的认知与决策，不会重写已经发生的世界事件。"));
        secondary.Children.Add(Named(Button("添加一条记忆", () => ShowMemoryEditor(id, null)), "resident-memory-add"));
        LiveRows(secondary, () => Current().Agent.Memory.OrderByDescending(f => f.LearnedTick).Take(40),
            fact => fact.Id.ToString(), fact => FactLabel(fact), fact => ShowMemoryEditor(id, fact.Id));
        secondary.Children.Add(LiveText(() =>
            $"携带消息 {Current().Agent.CarriedMessages.Count} 条\n目的地 {TownName(Current().Agent.DestinationSettlementId)}"));
        secondary = FoldSection(panel, "决策记录", "resident-decisions");
        LiveRows(secondary, () => Current().Agent.Decisions.AsEnumerable().Reverse().Take(20),
            d => $"{d.Tick}:{d.Goal}:{d.EvidenceFactId}",
            d =>
                $"{DateLabel(d.Tick)}\n{GoalName(d.Goal)}\n{d.Reason}\n评估 {d.Score:F2}\n依据记忆 #{d.EvidenceFactId}\n消息观察时间 {DateLabel(d.KnowledgeObservedTick)}\n来源 {ResidentName(d.SourceResidentId)}");
        secondary = FoldSection(panel, "个人履历", "resident-history");
        secondary.Children.Add(Named(Button("添加个人经历", () => ShowHistoryEntryEditor(id, null)), "resident-history-add"));
        LiveRows(secondary, () => Current().History.Select((entry, index) => (entry, index)).Reverse().Take(40),
            item => item.index.ToString(),
            item =>
                $"{ImportanceName(item.entry.Importance)}\n{DateLabel(item.entry.Tick)}\n{item.entry.Text}\n经历类型 {ExperienceName(item.entry.Experience)}\n心理影响 {item.entry.Impact:+0.00;-0.00;0}{(item.entry.PlayerEdited ? "\n玩家编辑" : "")}",
            item => ShowHistoryEntryEditor(id, item.index));
    }

    private string FactLabel(AgentFact fact)
    {
        var location = fact.Kind is AgentFactKind.Danger or AgentFactKind.WaterSource or AgentFactKind.FoundingSite
            ? $"\n位置 {fact.X}, {fact.Y}"
            : "";
        var learned = fact.LearnedTick != fact.ObservedTick ? "\n获知 " + DateLabel(fact.LearnedTick) : "";
        return
            $"{FactKindName(fact.Kind)}\n{fact.Text}\n来源 {ResidentName(fact.SourceResidentId)}\n观察 {DateLabel(fact.ObservedTick)}{learned}{location}\n可信度 {fact.Confidence:P0}";
    }

    private static string FactKindName(AgentFactKind kind)
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

    private void ShowResidentEditor(int id)
    {
        var resident = _engine.GetResident(id);
        if (resident is null)
            return;
        var archived = _engine.State.ArchivedResidents.Any(person => person.Id == id);
        var panel = ModalPanel("编辑居民档案", "打开期间时间暂时停止，取消会恢复原状态；应用修改后保持暂停。迁居会改变国家归属，当前任务请在目标编辑中调整。");
        var identity = new StackPanel { Spacing = 10 };
        var condition = new StackPanel { Spacing = 10 };
        var belonging = new StackPanel { Spacing = 10 };
        var magic = new StackPanel { Spacing = 10 };
        var possessions = new StackPanel { Spacing = 10 };
        var tabs = Named(
            new TabControl
            {
                ItemsSource = new[]
                {
                    Named(new TabItem { Header = Text("身份", 12), Content = identity }, "resident-tab-identity"), Named(
                        new TabItem { Header = Text("生理", 12), Content = condition },
                        "resident-tab-condition"),
                    Named(new TabItem { Header = Text("归属", 12), Content = belonging },
                        "resident-tab-belonging"),
                    Named(new TabItem { Header = Text("魔法", 12), Content = magic }, "resident-tab-magic"), Named(
                        new TabItem { Header = Text("物品", 12), Content = possessions },
                        "resident-tab-possessions"),
                },
                SelectedIndex = 0,
            }, "resident-editor-tabs");
        panel.Children.Add(tabs);
        var name = Field(identity, "姓名", resident.Name, "resident-name");
        var trait = ObjectField(identity, "性格预设（选择后同步调整对应倾向）",
            new[] { (0, "保持当前性格"), (1, "勤劳"), (2, "勇敢"), (3, "好奇"), (4, "温和") }, 0, "resident-trait");
        var race = EnumField(identity, "种族", resident.Race, RaceName, "resident-race");
        var profession = EnumField(identity, "职业", resident.Profession, ProfessionName, "resident-profession");
        var culture = ObjectField(belonging, "文化", _engine.State.Society.Cultures.Select(c => (c.Id, c.Name)),
            resident.CultureId, "resident-culture", historical: archived);
        var home = ObjectField(belonging, "居住聚落", _engine.State.Settlements.Select(t => (t.Id, t.Name)),
            resident.SettlementId, "resident-settlement", historical: archived);
        belonging.Children.Add(Paragraph("迁居会同步调整国家归属；文化认同保留你的选择。"));
        var age = Field(condition, "年龄", resident.Age, "resident-age");
        var health = Field(condition, "生命 0–100", resident.Health, "resident-health");
        var hunger = Field(condition, "饥饿 0–100", resident.Hunger, "resident-hunger");
        var thirst = Field(condition, "口渴 0–100", resident.Thirst, "resident-thirst");
        var sickness = Field(condition, "疫病剩余日数", resident.SicknessTicks, "resident-sickness");
        var x = Field(belonging, "位置 X", resident.X, "resident-x");
        var y = Field(belonging, "位置 Y", resident.Y, "resident-y");
        var army = ObjectField(belonging, "军队",
            _engine.State.Armies.Where(a => a.NationId == resident.NationId)
                .Select(a => (a.Id, NationName(a.NationId) + "\n" + a.Status)), resident.ArmyId, "resident-army", true,
            archived);
        var mana = Field(magic, "魔力", resident.Mana, "resident-mana");
        var talent = Field(magic, "魔法天赋", resident.MagicTalent, "resident-magic-talent");
        var training = Field(magic, "魔法训练", resident.MagicTraining, "resident-magic-training");
        possessions.Children.Add(Text("随身库存", 13, Mint));
        var inventory = StockFields(possessions, resident.Inventory, "resident-inventory");
        panel.Children.Add(Named(Button("应用档案变更", async () =>
        {
            if (!CanSubmitEdit())
                return;
            try
            {
                var patch = new ResidentEdit
                {
                    Name = name.Text ?? "",
                    Trait = Integer(trait) == 0 ? null : new[] { "", "勤劳", "勇敢", "好奇", "温和" }[Integer(trait)],
                    Race = (RaceKind)race.SelectedItem!,
                    Profession = (Profession)profession.SelectedItem!,
                    CultureId = Integer(culture),
                    SettlementId = Integer(home) == resident.SettlementId ? null : Integer(home),
                    Age = Number(age),
                    Health = Number(health),
                    Hunger = Number(hunger),
                    Thirst = Number(thirst),
                    SicknessTicks = Integer(sickness),
                    X = Integer(x),
                    Y = Integer(y),
                    ArmyId = Integer(army) == resident.ArmyId ? null : Integer(army),
                    Mana = Number(mana),
                    MagicTalent = Number(talent),
                    MagicTraining = Number(training),
                    Inventory = ReadStock(inventory),
                };
                await SubmitEditAsync(() =>
                {
                    _engine.EditResident(id, patch);
                    CloseModal();
                    _map.RefreshWorld();
                    RefreshUi(true);
                    SetStatus("居民档案已更新\n可撤销");
                });
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                SetStatus("未应用变更：" + FriendlyError(ex));
            }
        }), "resident-apply"));
        OpenModal(panel);
    }

    private AgentState CreateMindDraft(int id)
    {
        return _engine.GetResident(id)?.Agent ?? throw new ArgumentException("居民不存在。");
    }

    private void ShowGoalEditor(int id)
    {
        var resident = _engine.GetResident(id);
        if (resident is null)
            return;
        var panel = ModalPanel("目标、性格与需求", "目标在未来的行动中执行。只修改人格或需求会保留原目标；历史记录中的对象可以保留，自主思考仍会考虑危险与基本需求。");
        var mind = CreateMindDraft(id);
        var originalGoal = mind.Goal;
        var goal = EnumField(panel, "当前目标", mind.Goal.Kind, GoalName, "resident-goal");
        panel.Children.Add(Paragraph("选择目标、地点、对象和持续时间决定实际行动。"));
        var x = Field(panel, "目标 X", mind.Goal.TargetX, "resident-goal-x");
        var y = Field(panel, "目标 Y", mind.Goal.TargetY, "resident-goal-y");
        x.Maximum = _engine.State.Width - 1;
        y.Maximum = _engine.State.Height - 1;
        AddMapPicker(panel, x, y);
        var town = ObjectField(panel, "目标聚落", _engine.State.Settlements.Select(t => (t.Id, t.Name)),
            mind.Goal.TargetSettlementId, "resident-goal-town", true, true);
        var entityLabel = Text("目标对象", 12, Muted);
        panel.Children.Add(entityLabel);
        var entity = Named(new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, MinHeight = 36 },
            "resident-goal-entity");
        panel.Children.Add(entity);
        var updatingEntities = false;

        void UpdateEntities()
        {
            var kind = (AgentGoalKind)goal.SelectedItem!;
            var facilities = kind is AgentGoalKind.Work or AgentGoalKind.Study or AgentGoalKind.TrainMagic;
            var sourceGoal = kind is AgentGoalKind.FetchWater or AgentGoalKind.Hunt or AgentGoalKind.Fish;
            entityLabel.Text = sourceGoal ? "附近源地块" : facilities ? "目标设施（本聚落）" : "目标居民";
            var choices = new List<EntityChoice> { new(0, "无 / 按目标地点行动") };
            if (facilities)
            {
                choices.AddRange(_engine.State.Society.Buildings.Where(building =>
                        building.SettlementId == resident.SettlementId
                        && (kind == AgentGoalKind.Work || (kind == AgentGoalKind.Study &&
                                                           building.Kind == BuildingKind.Academy)
                                                       || (kind == AgentGoalKind.TrainMagic &&
                                                           building.Kind is BuildingKind.ArcaneSanctum
                                                               or BuildingKind.SacredGrove)))
                    .Select(building => new EntityChoice(building.Id, BuildingLabel(building))));
            }
            else if (sourceGoal)
            {
                for (var yy = Math.Max(0, resident.Y - 6);
                     yy <= Math.Min(_engine.State.Height - 1, resident.Y + 6);
                     yy++)
                    for (var xx = Math.Max(0, resident.X - 6);
                         xx <= Math.Min(_engine.State.Width - 1, resident.X + 6);
                         xx++)
                    {
                        var index = yy * _engine.State.Width + xx;
                        var tile = _engine.State.Tiles[index];
                        if (kind == AgentGoalKind.FetchWater
                                ? WorldEngine.IsWaterSource(tile)
                                : kind == AgentGoalKind.Fish
                                    ? WorldEngine.IsWaterTerrain(tile.Terrain) && AnimalRules.Species.Any(s =>
                                        AnimalRules.For(s).Aquatic && AnimalRules.For(s).Diet == AnimalDiet.Herbivore &&
                                        tile.AnimalPopulation(s) > 0)
                                    : tile.WildlifeMask != 0 && RaceTerrainRules.CanWalk(tile, resident.Race))
                            choices.Add(new EntityChoice(index + 1, $"{TerrainName(tile.Terrain)} {xx}, {yy}"));
                    }
            }
            else
                choices.AddRange(_engine.State.Residents.Select(person => new EntityChoice(person.Id, person.Name)));

            if (kind == originalGoal.Kind && choices.All(choice => choice.Id != originalGoal.TargetEntityId))
            {
                choices.Add(
                    new EntityChoice(originalGoal.TargetEntityId, $"保留原目标 #{originalGoal.TargetEntityId}（历史引用）"));
            }

            var selected = entity.SelectedItem is EntityChoice prior ? prior.Id : originalGoal.TargetEntityId;
            updatingEntities = true;
            entity.ItemsSource = choices;
            entity.SelectedItem = choices.FirstOrDefault(choice => choice.Id == selected) ?? choices[0];
            updatingEntities = false;
        }

        UpdateEntities();
        goal.SelectionChanged += (_, _) => UpdateEntities();
        entity.SelectionChanged += (_, _) =>
        {
            if (updatingEntities || entity.SelectedItem is not EntityChoice choice)
                return;
            if ((AgentGoalKind)goal.SelectedItem! is AgentGoalKind.Work or AgentGoalKind.Study
                or AgentGoalKind.TrainMagic)
            {
                if (_engine.State.Society.Buildings.FirstOrDefault(item => item.Id == choice.Id) is { } building)
                {
                    x.Value = building.X;
                    y.Value = building.Y;
                }
            }
            else if ((AgentGoalKind)goal.SelectedItem! is AgentGoalKind.FetchWater or AgentGoalKind.Hunt
                     or AgentGoalKind.Fish)
            {
                if (choice.Id <= 0 || choice.Id > _engine.State.Tiles.Count)
                    return;
                var sourceX = (choice.Id - 1) % _engine.State.Width;
                var sourceY = (choice.Id - 1) / _engine.State.Width;
                if ((AgentGoalKind)goal.SelectedItem! == AgentGoalKind.Hunt ||
                    ((AgentGoalKind)goal.SelectedItem! == AgentGoalKind.FetchWater &&
                     _engine.State.Tiles[choice.Id - 1].IsWalkable))
                {
                    x.Value = sourceX;
                    y.Value = sourceY;
                }
                else
                {
                    var bank = new[]
                        {
                            (X: sourceX - 1, Y: sourceY), (X: sourceX + 1, Y: sourceY),
                            (X: sourceX, Y: sourceY - 1), (X: sourceX, Y: sourceY + 1),
                        }
                        .Where(p => p.X >= 0 && p.Y >= 0 && p.X < _engine.State.Width && p.Y < _engine.State.Height &&
                                    _engine.State.Tiles[p.Y * _engine.State.Width + p.X].IsWalkable)
                        .OrderBy(p => Math.Abs(p.X - resident.X) + Math.Abs(p.Y - resident.Y))
                        .Select(p => ((int X, int Y)?)p).FirstOrDefault();
                    if (bank is { } p)
                    {
                        x.Value = p.X;
                        y.Value = p.Y;
                    }
                }
            }
            else if (_engine.State.Residents.FirstOrDefault(person => person.Id == choice.Id) is { } target)
            {
                x.Value = target.X;
                y.Value = target.Y;
            }
        };
        town.SelectionChanged += (_, _) =>
        {
            if (town.SelectedItem is EntityChoice choice &&
                _engine.State.Settlements.FirstOrDefault(t => t.Id == choice.Id) is { } destination)
            {
                x.Value = destination.X;
                y.Value = destination.Y;
            }
        };
        panel.Children.Add(Paragraph("选择聚落会同步填写目标地点；目标改变未来行动，紧急生存需求仍可打断。"));
        var initialDuration = Math.Max(24, mind.Goal.ReviewTick - _engine.State.Tick);
        var duration = Field(panel, "目标保持日数", initialDuration, "resident-goal-duration");
        var fatigue = Field(panel, "疲劳", mind.Fatigue, "resident-fatigue");
        var social = Field(panel, "社交需求", mind.SocialNeed, "resident-social-need");
        var courage = Field(panel, "勇气 0–1", mind.Personality.Courage, "resident-courage");
        var diligence = Field(panel, "勤勉 0–1", mind.Personality.Diligence, "resident-diligence");
        var sociability = Field(panel, "社交 0–1", mind.Personality.Sociability, "resident-sociability");
        var ambition = Field(panel, "抱负 0–1", mind.Personality.Ambition, "resident-ambition");
        panel.Children.Add(Named(Button("应用目标与人格", async () =>
        {
            if (!CanSubmitEdit())
                return;
            try
            {
                var kind = (AgentGoalKind)goal.SelectedItem!;
                var targetX = Integer(x);
                var targetY = Integer(y);
                var targetTown = Integer(town);
                var targetEntity = Integer(entity);
                var keepDays = Integer(duration);
                var goalReason = originalGoal.Reason;
                var goalChanged = kind != originalGoal.Kind || targetX != originalGoal.TargetX ||
                                  targetY != originalGoal.TargetY
                                  || targetTown != originalGoal.TargetSettlementId ||
                                  targetEntity != originalGoal.TargetEntityId
                                  || goalReason != originalGoal.Reason || keepDays != initialDuration;
                mind = mind with
                {
                    Goal = goalChanged
                    ? new AgentGoal
                    {
                        Kind = kind,
                        TargetX = targetX,
                        TargetY = targetY,
                        TargetSettlementId = targetTown,
                        TargetEntityId = targetEntity,
                        Reason = "玩家指定：" + GoalName(kind),
                        PlayerDirected = true,
                        StartedTick = _engine.State.Tick,
                        ReviewTick = _engine.State.Tick + keepDays,
                    }
                    : originalGoal
                };
                mind = mind with { Fatigue = Number(fatigue) };
                mind = mind with { SocialNeed = Number(social) };
                mind = mind with
                {
                    Personality = mind.Personality with
                    {
                        Courage = Number(courage),
                        Diligence = Number(diligence),
                        Sociability = Number(sociability),
                        Ambition = Number(ambition),
                    }
                };
                await SubmitEditAsync(() =>
                {
                    _engine.EditResident(id, new ResidentEdit { Agent = mind });
                    CloseModal();
                    _map.RefreshWorld();
                    RefreshUi(true);
                    SetStatus("目标与人格已更新，将影响接下来的行动");
                });
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                SetStatus("未应用变更：" + FriendlyError(ex));
            }
        }), "resident-goal-apply"));
        OpenModal(panel);
    }

    private void ShowMemoryEditor(int id, int? factId)
    {
        var mind = CreateMindDraft(id);
        var fact = factId.HasValue ? mind.Memory.FirstOrDefault(f => f.Id == factId) : null;
        var adding = fact is null;
        fact ??= new AgentFact
        {
            Id = 0,
            Kind = AgentFactKind.FoodSupply,
            SubjectId = _engine.GetResident(id)?.SettlementId ?? 0,
            ObservedTick = _engine.State.Tick,
            LearnedTick = _engine.State.Tick,
            OriginResidentId = id,
            SourceResidentId = id,
            OriginProfession = _engine.GetResident(id)?.Profession ?? Profession.Child,
            X = _engine.GetResident(id)?.X ?? 0,
            Y = _engine.GetResident(id)?.Y ?? 0,
        };
        var panel = ModalPanel(adding ? "添加记忆" : "编辑记忆", "这是角色的认知记录，允许它与实际世界不同。修改将影响以后的决策与传播，不回算已经发生的战争、死亡或资源变化。");
        var kind = EnumField(panel, "记忆类型", fact.Kind, FactKindName, "memory-kind");
        kind.ItemsSource = Enum.GetValues<AgentFactKind>()
            .Where(k => k != AgentFactKind.Personal || fact.Kind == AgentFactKind.Personal).ToArray();
        kind.SelectedItem = fact.Kind;
        panel.Children.Add(Paragraph("记忆由类型、对象、数值和可信度决定实际作用；现有文字保留为记录。"));
        var meaning = Paragraph("");
        panel.Children.Add(meaning);
        var value = Field(panel, "数值", fact.Value, "memory-value");
        value.Minimum = -1_000_000_000;
        value.Maximum = 1_000_000_000;
        value.Value = (decimal)fact.Value;
        var choice = Named(new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch }, "memory-value-choice");
        panel.Children.Add(choice);
        var addressed = ObjectField(panel, "外交声明／军令的接收国家", _engine.State.Nations.Select(n => (n.Id, n.Name)),
            fact.TargetNationId, "memory-addressed", true);
        var confidence = Field(panel, "置信度 0–1", fact.Confidence, "memory-confidence");
        var subject = ObjectField(panel, "消息涉及的对象", _engine.State.Settlements.Select(t => (t.Id, t.Name)),
            fact.SubjectId, "memory-subject", true);

        void UpdateSubject()
        {
            var selected = (subject.SelectedItem as EntityChoice)?.Id ?? fact.SubjectId;
            var selectedKind = (AgentFactKind)kind.SelectedItem!;
            var entries = selectedKind is AgentFactKind.WarOrder or AgentFactKind.PeaceOrder
                or AgentFactKind.TradeExchange or AgentFactKind.DiplomaticNotice
                ? _engine.State.Nations.Select(n => new EntityChoice(n.Id, n.Name))
                : selectedKind == AgentFactKind.Personal
                    ? _engine.State.Residents.Select(p => new EntityChoice(p.Id, p.Name))
                    : selectedKind == AgentFactKind.Danger
                        ? _engine.State.Armies.Select(a => new EntityChoice(a.Id, NationName(a.NationId) + "军队"))
                        : _engine.State.Settlements.Select(t => new EntityChoice(t.Id, t.Name));
            var list = new[] { new EntityChoice(0, "无 / 地点") }.Concat(entries).ToArray();
            subject.ItemsSource = list;
            subject.SelectedItem = list.FirstOrDefault(i => i.Id == selected) ?? list[0];
            var options = selectedKind switch
            {
                AgentFactKind.SettlementLocation => _engine.State.Nations.Select(n => new EntityChoice(n.Id, n.Name)),
                AgentFactKind.Policy => Enum.GetValues<PolicyKind>()
                    .Select(p => new EntityChoice((int)p, WorldEngine.PolicyName(p))),
                AgentFactKind.WarOrder or AgentFactKind.PeaceOrder => new[] { new EntityChoice(0, "仅使用地点") }.Concat(
                    _engine.State.Settlements.Select(t => new EntityChoice(t.Id, t.Name))),
                AgentFactKind.Culture => _engine.State.Society.Cultures.Select(c => new EntityChoice(c.Id, c.Name)),
                AgentFactKind.Research => Advancement.All.OrderBy(research => research.Id)
                    .Select(r => new EntityChoice(r.Id, r.Name)),
                AgentFactKind.TradeExchange => new[] { new EntityChoice(1, "实际完成交易") },
                AgentFactKind.DiplomaticNotice => new[]
                {
                    new EntityChoice(0, "停战声明"), new EntityChoice(1, "结盟提议"), new EntityChoice(2, "宣战声明"),
                },
                _ => Array.Empty<EntityChoice>(),
            };
            var values = options.ToArray();
            choice.ItemsSource = values;
            choice.SelectedItem = values.FirstOrDefault(v => v.Id == fact.Value) ?? values.FirstOrDefault();
            choice.IsVisible = values.Length > 0;
            value.IsVisible = !choice.IsVisible;
            addressed.IsVisible =
                selectedKind is AgentFactKind.WarOrder or AgentFactKind.PeaceOrder or AgentFactKind.DiplomaticNotice;
            meaning.Text = selectedKind switch
            {
                AgentFactKind.FoodSupply => "相信该聚落有多少份粮食；影响采集、贸易和迁徙选择。",
                AgentFactKind.Danger => "大于零表示危险；位置、来源、时效与可信度共同影响避险。",
                AgentFactKind.SettlementLocation => "相信该聚落属于哪个国家；地点和消息时效影响探索与外交。",
                AgentFactKind.ReliefRequest => "困苦程度 0–100；送达机构后影响救济政策与地方不满。",
                AgentFactKind.Policy => "送达的政策方向；仍受当地自治与玩家覆盖约束。",
                AgentFactKind.WarOrder or AgentFactKind.PeaceOrder => "选择目标聚落与接收国；士兵依据实际收到的命令行动。",
                AgentFactKind.Culture => "接触到的文化；需要持续交流才会改变认同。",
                AgentFactKind.Research => "相信当地掌握的研究；实际递送后可能传播该成果。",
                AgentFactKind.DiplomaticNotice => "选择声明类型与接收国；结盟还须存在对应提议。",
                _ => "记录的数值。说明文字仅作备注，不会自动执行。",
            };
        }

        kind.SelectionChanged += (_, _) => UpdateSubject();
        UpdateSubject();
        var x = Field(panel, "地点 X", fact.X, "memory-x");
        var y = Field(panel, "地点 Y", fact.Y, "memory-y");
        var observed = Field(panel, "观察日序（0 起）", fact.ObservedTick, "memory-observed");
        var learned = Field(panel, "获知日序（0 起）", fact.LearnedTick, "memory-learned");
        AddDatePreview(panel, observed, "观察时间");
        AddDatePreview(panel, learned, "获知时间");
        var origin = ObjectField(panel, "最初观察者",
            _engine.State.Residents.Concat(_engine.State.ArchivedResidents).Select(p => (p.Id, p.Name)),
            fact.OriginResidentId, "memory-origin", true);
        var source = ObjectField(panel, "消息来源",
            _engine.State.Residents.Concat(_engine.State.ArchivedResidents).Select(p => (p.Id, p.Name)),
            fact.SourceResidentId, "memory-source", true);
        var hops = Field(panel, "转述次数", fact.Hops, "memory-hops");
        hops.Maximum = 1000;
        x.Minimum = -1;
        y.Minimum = -1;
        AddMapPicker(panel, x, y);
        panel.Children.Add(Named(Button("保存记忆", async () =>
        {
            if (!CanSubmitEdit())
                return;
            try
            {
                var revisedFact = fact with
                {
                    Kind = (AgentFactKind)kind.SelectedItem!,
                    Text = adding ? FactKindName((AgentFactKind)kind.SelectedItem!) + "（玩家设置）" : fact.Text,
                    Value = choice.IsVisible ? Integer(choice) : Number(value),
                    TargetNationId = addressed.IsVisible ? Integer(addressed) : 0,
                    Confidence = Number(confidence),
                    SubjectId = Integer(subject),
                    X = Integer(x),
                    Y = Integer(y),
                    ObservedTick = Integer(observed),
                    LearnedTick = Integer(learned),
                    OriginResidentId = Integer(origin),
                    SourceResidentId = Integer(source),
                    Hops = Integer(hops),
                };
                if (adding)
                    mind = mind with { Memory = mind.Memory.Add(revisedFact) };
                else
                    mind = mind with { Memory = mind.Memory.SetItem(mind.Memory.IndexOf(fact), revisedFact) };
                fact = revisedFact;
                if (!await SubmitEditAsync(() =>
                    {
                        _engine.EditResident(id, new ResidentEdit { Agent = mind });
                        CloseModal();
                        _map.RefreshWorld();
                        RefreshUi(true);
                        SetStatus("记忆已更新，世界历史保持原样");
                    }) && adding)
                    mind = mind with { Memory = mind.Memory.Remove(fact) };
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                if (adding)
                    mind = mind with { Memory = mind.Memory.Remove(fact) };
                SetStatus("未应用变更：" + FriendlyError(ex));
            }
        }), "memory-apply"));
        if (!adding)
        {
            panel.Children.Add(Named(Button("删除这条记忆", async () =>
            {
                if (!CanSubmitEdit())
                    return;
                var previousIndex = mind.Memory.IndexOf(fact);
                mind = mind with { Memory = mind.Memory.Remove(fact) };
                if (!await SubmitEditAsync(() =>
                    {
                        _engine.EditResident(id, new ResidentEdit { Agent = mind });
                        CloseModal();
                        _map.RefreshWorld();
                        RefreshUi(true);
                        SetStatus("这条记忆已移除");
                    }) && previousIndex >= 0)
                    mind = mind with { Memory = mind.Memory.Insert(previousIndex, fact) };
            }), "memory-delete"));
        }

        OpenModal(panel);
    }

    private static string ExperienceName(PersonalExperienceKind experience)
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

    private void ShowHistoryEntryEditor(int id, int? index)
    {
        var history = JsonSerializer.Deserialize(_engine.ExportResidentHistory(id),
            ResidentUiJsonContext.Default.ListResidentHistoryEntry) ?? [];
        var adding = !index.HasValue || index < 0 || index >= history.Count;
        var entry = adding
            ? new ResidentHistoryEntry
            {
                Tick = _engine.State.Tick,
                PlayerEdited = true,
                Experience = PersonalExperienceKind.Learning,
                Impact = .25,
            }
            : history[index!.Value];
        var panel = ModalPanel(adding ? "添加个人经历" : "编辑个人经历",
            "这份个人履历独立于世界编年史。经历类型与影响数值改变今后的性格倾向；文字用于记录，不会被自动理解成新的世界事实。过去的资源、死亡、战争不回算。");
        if (!adding)
            panel.Children.Add(Paragraph(entry.Text));
        panel.Children.Add(Paragraph("选择经历类型和强度，会直接调整今后的性格倾向。"));
        var tick = Field(panel, "发生日序（0 起）", entry.Tick, "history-entry-tick");
        AddDatePreview(panel, tick, "发生时间");
        var importance = EnumField(panel, "重要程度", entry.Importance, ImportanceName, "history-entry-importance");
        var experience = EnumField(panel, "经历类型", entry.Experience, ExperienceName, "history-entry-experience");
        var impact = Field(panel, "影响强度 −1 至 1", entry.Impact, "history-entry-impact");
        var strength = ObjectField(panel, "强度预设",
            new[] { (0, "保持当前强度"), (1, "轻微（0.25）"), (2, "明显（0.5）"), (3, "重大（1）") }, 0, "history-entry-strength");
        strength.SelectionChanged += (_, _) =>
        {
            var value = Integer(strength);
            if (value > 0)
                impact.Value = new[] { 0m, .25m, .5m, 1m }[value];
        };
        var effects = Paragraph("");
        panel.Children.Add(effects);

        void UpdateEffect()
        {
            var strength = (double)(impact.Value ?? 0);
            effects.Text = $"当前强度对应 {Math.Abs(strength) * 10:0.#} 个百分点的性格变化；范围限制为 0–100%。";
        }

        experience.SelectionChanged += (_, _) => UpdateEffect();
        impact.ValueChanged += (_, _) => UpdateEffect();
        UpdateEffect();
        panel.Children.Add(Paragraph(
            "强度为 1 时：艰难使勇气减少 10 个百分点，成就使勇气增加 10 个百分点；善意使社交增加 10 个百分点，背叛使社交减少 10 个百分点；学习使勤勉增加 10 个百分点。负强度反向作用，中性经历不改变性格。"));
        panel.Children.Add(Named(Button("保存个人经历", async () =>
        {
            if (!CanSubmitEdit())
                return;
            try
            {
                var revisedEntry = entry with
                {
                    Text = adding
                        ? ExperienceName((PersonalExperienceKind)experience.SelectedItem!) + "（玩家设置）"
                        : entry.Text,
                    Tick = Integer(tick),
                    Importance = (EventImportance)importance.SelectedItem!,
                    Experience = (PersonalExperienceKind)experience.SelectedItem!,
                    Impact = Number(impact),
                    PlayerEdited = true,
                };
                if (adding)
                    history.Add(revisedEntry);
                else
                    history[history.IndexOf(entry)] = revisedEntry;
                entry = revisedEntry;
                if (!await SubmitEditAsync(() =>
                    {
                        _engine.EditResident(id, new ResidentEdit { History = history });
                        CloseModal();
                        RefreshUi(true);
                        SetStatus("个人经历已更新，将影响今后的性格与行为");
                    }) && adding)
                    history.Remove(entry);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                if (adding)
                    history.Remove(entry);
                SetStatus("未应用变更：" + FriendlyError(ex));
            }
        }), "history-entry-apply"));
        if (!adding)
        {
            panel.Children.Add(Named(Button("删除这条个人经历", async () =>
            {
                if (!CanSubmitEdit())
                    return;
                var previousIndex = history.IndexOf(entry);
                history.Remove(entry);
                if (!await SubmitEditAsync(() =>
                    {
                        _engine.EditResident(id, new ResidentEdit { History = history });
                        CloseModal();
                        RefreshUi(true);
                        SetStatus("个人经历已移除，世界历史未改变");
                    }) && previousIndex >= 0)
                    history.Insert(previousIndex, entry);
            }), "history-entry-delete"));
        }

        OpenModal(panel);
    }

    private void ShowResidentJsonEditor(int id, bool history)
    {
        var panel = ModalPanel(history ? "编辑个人经历与历史" : "高级认知编辑",
            history
                ? "个人历史与世界事件独立。Experience 与 Impact 是结构化心理影响；文本不会被当作可执行命令。仅影响今后认知，不重新计算过去的世界。"
                : "包含目标、人格、全部记忆、决策依据、携带消息与任务。字段统一经过模拟核心校验。只修改这个角色的认知，不会改动世界事实。");
        var input = Named(
            new TextBox
            {
                Text = history ? _engine.ExportResidentHistory(id) : _engine.ExportResidentMind(id),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 280,
                MaxHeight = 420,
            }, history ? "resident-history-json" : "resident-mind-json-input");
        panel.Children.Add(input);
        panel.Children.Add(Named(Button("校验并应用", async () =>
        {
            if (!CanSubmitEdit())
                return;
            try
            {
                var json = input.Text ?? "";
                await SubmitEditAsync(() =>
                {
                    if (history)
                        _engine.EditResidentHistoryJson(id, json);
                    else
                        _engine.EditResidentMindJson(id, json);
                    CloseModal();
                    RefreshUi(true);
                    SetStatus("角色记录已更新，将影响未来行为");
                });
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or JsonException)
            {
                SetStatus("未应用变更：" + FriendlyError(ex));
            }
        }), history ? "resident-history-apply" : "resident-mind-apply"));
        OpenModal(panel);
    }

    private static void AddDatePreview(StackPanel panel, TextBox input, string label)
    {
        var preview = Paragraph("");

        void Update()
        {
            preview.Text = long.TryParse(input.Text, out var tick) && tick >= 0
                ? $"{label}：{DateLabel(tick)}"
                : $"{label}：请输入非负日序";
        }

        input.TextChanged += (_, _) => Update();
        Update();
        panel.Children.Add(preview);
    }

    private static TextBox Field(StackPanel panel, string label, object value, string id)
    {
        panel.Children.Add(Text(label, 12, Muted));
        var text = value is IFormattable format
            ? format.ToString(null, CultureInfo.InvariantCulture)
            : value.ToString();
        var box = Named(new TextBox { Text = text, HorizontalAlignment = HorizontalAlignment.Stretch }, id);
        panel.Children.Add(box);
        return box;
    }

    private static ComboBox EnumField<T>(StackPanel panel, string label, T value, Func<T, string> name, string id)
        where T : struct, Enum
    {
        panel.Children.Add(Text(label, 12, Muted));
        var picker = Named(new ComboBox
        {
            ItemsSource = Enum.GetValues<T>(),
            SelectedItem = value,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemTemplate = new FuncDataTemplate<T>((item, _) => Text(name(item), 12)),
        }, id);
        panel.Children.Add(picker);
        return picker;
    }

    private static double Number(TextBox field)
    {
        if (!double.TryParse(field.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
            !double.IsFinite(value))
            throw new ArgumentException("请输入有效数值。");
        return value;
    }

    private static int Integer(TextBox field)
    {
        if (!int.TryParse(field.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            throw new ArgumentException("请输入有效整数。");
        return value;
    }

    private NumericUpDown[] StockFields(StackPanel panel, ResourceStock stock, string prefix)
    {
        return ResourceStock.Kinds.Select(kind => Field(panel, ResourceStock.Name(kind), stock.Get(kind),
            prefix + "-" + kind.ToString().ToLowerInvariant())).ToArray();
    }

    private static ResourceStock ReadStock(NumericUpDown[] fields)
    {
        var stock = new ResourceStock();
        for (var i = 0; i < fields.Length; i++)
            stock = stock.WithAmount(ResourceStock.Kinds[i], Number(fields[i]));
        return stock;
    }
}

[JsonSerializable(typeof(AgentState))]
[JsonSerializable(typeof(List<ResidentHistoryEntry>))]
internal partial class ResidentUiJsonContext : JsonSerializerContext;
