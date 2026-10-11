using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;
using SeWZC.WorldBox.UI.ViewModels.Details;

namespace SeWZC.WorldBox.UI.ViewModels;

internal sealed partial class InspectorViewModel
{
    internal void BuildResidentInspector(StackNode panel)
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
        var belonging = new WrapNode { Orientation = Orientation.Horizontal };
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
            nation.ToolTip = nation.IsEnabled ? "查看所属国家" : "原属国家已不存在，保留历史归属";
            settlement.ToolTip = settlement.IsEnabled ? "查看所属聚落" : "原属聚落已不存在，保留历史归属";
        }

        _inspectorUpdates.Add(UpdateBelonging);
        UpdateBelonging();
        panel.Children.Add(LiveText(() =>
            Current().Health <= 0
                ? $"逝世时间：{DateLabel(Current().DeathTick)}\n死亡原因：{WorldEngine.DeathCauseName(Current().DeathCause)}"
                : ""));
        panel.Children.Add(LiveText(() =>
            $"生命 {Current().Health:0} / 100   体力 {ResidentNeedsRules.StaminaValue(Current()):0.#} / {ResidentNeedsRules.StaminaCapacity(Current()):0.#}   睡眠 {ResidentNeedsRules.SleepValue(Current()):0.#} / {ResidentNeedsRules.SleepCapacity(Current()):0.#}   饥饿 {Current().Hunger:0}%   口渴 {Current().Thirst:0}%"));
        panel.Children.Add(LiveText(() => _engine.GetResidentActionSummary(id)));
        panel.Children.Add(LiveText(() => _engine.GetResidentHome(id) is { } dwelling
            ? $"住所：住宅（{dwelling.X}，{dwelling.Y}），入住 {_engine.GetHousingOccupancy(dwelling.Id)} 人；{(Current().IsInsideHome ? "在家" : "外出")}" : "暂无空余住所"));
        panel.Children.Add(LiveText(() => Current().Agent.DailyPlan is { } plan
            ? $"今日预计返家：{DateLabel(plan.ReturnHomeTick)}" : ""));
        panel.Children.Add(LiveText(() =>
        {
            var agent = Current().Agent;
            if (agent.WorkplaceId != 0)
            {
                var workplace = _engine.State.Society.Buildings.FirstOrDefault(b => b.Id == agent.WorkplaceId);
                return workplace is null
                    ? "原登记工作地已移除，返乡后重新安排"
                    : $"登记工作地：{WorldEngine.BuildingName(workplace.Kind)}（{workplace.X}, {workplace.Y}）";
            }

            return agent.WorkAreaIndex >= 0
                ? $"固定采集范围：地块（{agent.WorkAreaIndex % _engine.State.Width}, {agent.WorkAreaIndex / _engine.State.Width}）及周围三格"
                : Current().Profession == Profession.Laborer
                    ? "暂无固定专业岗位，按眼前需求协助劳动"
                    : "";
        }));
        BuildResearchResidentActions(panel, resident);
        if (resident.Health > 0)
        {
            var quick = new WrapNode { Orientation = Orientation.Horizontal };
            quick.Children.Add(Named(Button("安排休息", () => _requests.Editors.QuickResidentGoal(id, AgentGoalKind.Rest)), "resident-rest"));
            quick.Children.Add(Named(Button("返回家园", () => _requests.Editors.QuickResidentGoal(id, AgentGoalKind.ReturnHome)),
                "resident-home"));
            quick.Children.Add(Named(Button("恢复自主", () => _requests.Editors.QuickResidentGoal(id, null)), "resident-autonomy"));
            quick.Children.Add(Named(
                Button("治疗",
                    () => RunEdit(() => _engine.EditResident(id, new ResidentEdit { Health = 100, SicknessTicks = 0 }),
                        "居民已得到治疗")), "resident-heal"));
            if (resident.Age >= ResidentNeedsRules.MinimumWorkAge)
            {
                var cast = Named(
                    Button("施放法术", () => _requests.Editors.ShowSpellSelectionEditor(SpellKind.Heal), "选择本人施法；窗口显示知识、训练和魔力要求"),
                    "resident-spell");
                _inspectorUpdates.Add(() => cast.IsEnabled = Current().Health > 0 && ResidentNeedsRules.CanWork(Current()));
                quick.Children.Add(cast);
            }

            panel.Children.Add(quick);
        }

        panel.Children.Add(WatchControl(ObservedObjectKind.Resident, id, "resident-watch"));
        panel.Children.Add(Named(Button("人物故事与重要转折", () => OpenInspector("story")), "resident-story"));
        var actions = new GridNode { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 5 };
        var locate = Named(Button("定位", () =>
        {
            if (FocusMapResident(id) && _isCompact)
                CloseInspector();
        }), "resident-locate");
        actions.Children.Add(locate);
        var follow = Named(new CheckNode { Content = Text("跟随", 12), IsChecked = _map.FollowSelectedResident },
            "resident-follow");
        var syncingFollow = false;
        follow.IsCheckedChanged += (_, _) =>
        {
            if (syncingFollow)
                return;
            if (follow.IsChecked == true)
            {
                if (FocusMapResident(id))
                    _map.SelectResident(id, true);
            }
            else
                _map.SelectResident(id);
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
        GridNode.SetColumn(follow, 1);
        actions.Children.Add(follow);
        var edit = Named(Button("编辑", () => _requests.Editors.ShowResidentEditor(id)), "resident-edit");
        GridNode.SetColumn(edit, 2);
        actions.Children.Add(edit);
        panel.Children.Add(actions);
        var body = FoldSection(panel, "身体、库存与魔法", "resident-body");
        body.Children.Add(LiveText(() =>
        {
            var r = Current();
            return
                $"{ActivityName(r.Activity)}\n生命 {r.Health:F1}\n饥饿 {r.Hunger:F1}\n口渴 {r.Thirst:F1}\n疫病 {r.SicknessTicks / (double)SimulationTime.TicksPerDay:0.##} 日\n体力 {ResidentNeedsRules.StaminaValue(r):F1} / {ResidentNeedsRules.StaminaCapacity(r):F1}\n睡眠 {ResidentNeedsRules.SleepValue(r):F1} / {ResidentNeedsRules.SleepCapacity(r):F1}\n社交需求 {r.Agent.SocialNeed:F1}\n特质：{r.Trait}\n随身库存：{StockLabel(r.Inventory)}\n魔力 {r.Mana:F1}\n天赋 {r.MagicTalent:F1}\n训练 {r.MagicTraining:F1}\n军队 {(r.ArmyId == 0 ? "无" : r.ArmyId.ToString())}\n家园 {TownName(r.SettlementId)}";
        }));
        var effects = FoldSection(panel, "当前加成与减益", "resident-effects");
        effects.Children.Add(LiveText(() => EffectLabel(_engine.GetResidentEffects(id))));
        var route = Named(new CheckNode { Content = "显示后续行动轨迹", IsChecked = _map.ShowResidentRoute }, "resident-route");
        route.IsCheckedChanged += (_, _) =>
        {
            _map.ShowResidentRoute = route.IsChecked == true;
            _map.InvalidateVisual();
        };
        panel.Children.Add(route);

        panel.Children.Add(Named(Button("编辑目标与人格", () => _requests.Editors.ShowGoalEditor(id)), "resident-goal-edit"));
        var secondary = FoldSection(panel, "性格、记忆与消息", "resident-cognition");
        secondary.Children.Add(Text("性格倾向", 12, Mint));
        secondary.Children.Add(LiveText(() =>
        {
            var p = Current().Agent.Personality;
            return $"勇气 {p.Courage:P0}\n勤勉 {p.Diligence:P0}\n社交 {p.Sociability:P0}\n抱负 {p.Ambition:P0}";
        }));
        secondary.Children.Add(Text("已知消息与记忆\n可能过时或有误", 12, Mint));
        secondary.Children.Add(Paragraph("下面是居民知道的内容，不等同于全世界的即时状态。改动只影响今后的认知与决策，不会重写已经发生的世界事件。"));
        secondary.Children.Add(Named(Button("添加一条记忆", () => _requests.Editors.ShowMemoryEditor(id, null)), "resident-memory-add"));
        LiveRows(secondary, () => Current().Agent.Memory.OrderByDescending(f => f.LearnedTick).Take(40),
            fact => fact.Id.ToString(), fact => FactLabel(fact), fact => _requests.Editors.ShowMemoryEditor(id, fact.Id));
        secondary.Children.Add(LiveText(() =>
            $"携带消息 {Current().Agent.CarriedMessages.Count} 条\n目的地 {TownName(Current().Agent.DestinationSettlementId)}"));
        secondary = FoldSection(panel, "决策记录", "resident-decisions");
        LiveRows(secondary, () => Current().Agent.Decisions.AsEnumerable().Reverse().Take(20),
            d => $"{d.Tick}:{d.Goal}:{d.EvidenceFactId}",
            d =>
                $"{DateLabel(d.Tick)}\n{GoalName(d.Goal)}\n{d.Reason}\n评估 {d.Score:F2}\n依据记忆 #{d.EvidenceFactId}\n消息观察时间 {DateLabel(d.KnowledgeObservedTick)}\n来源 {ResidentName(d.SourceResidentId)}");
        secondary = FoldSection(panel, "个人履历", "resident-history");
        secondary.Children.Add(Named(Button("添加个人经历", () => _requests.Editors.ShowHistoryEntryEditor(id, null)), "resident-history-add"));
        LiveRows(secondary, () => Current().History.Select((entry, index) => (entry, index)).Reverse().Take(40),
            item => item.index.ToString(),
            item =>
                $"{ImportanceName(item.entry.Importance)}\n{DateLabel(item.entry.Tick)}\n{item.entry.Text}\n经历类型 {ExperienceName(item.entry.Experience)}\n心理影响 {item.entry.Impact:+0.00;-0.00;0}{(item.entry.PlayerEdited ? "\n玩家编辑" : "")}",
            item => _requests.Editors.ShowHistoryEntryEditor(id, item.index));
    }

    internal string FactLabel(AgentFact fact)
    {
        var location = fact.Kind is AgentFactKind.Danger or AgentFactKind.WaterSource or AgentFactKind.FoundingSite
            ? $"\n位置 {fact.X}, {fact.Y}"
            : "";
        var learned = fact.LearnedTick != fact.ObservedTick ? "\n获知 " + DateLabel(fact.LearnedTick) : "";
        return
            $"{FactKindName(fact.Kind)}\n{fact.Text}\n来源 {ResidentName(fact.SourceResidentId)}\n观察 {DateLabel(fact.ObservedTick)}{learned}{location}\n可信度 {fact.Confidence:P0}";
    }

}
