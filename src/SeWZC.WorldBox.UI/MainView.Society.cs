using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    private int _inspectorSettlementId;
    private string CultureName(int id) => _engine.State.Society.Cultures.FirstOrDefault(c => c.Id == id)?.Name ?? $"文化 #{id}";
    private static string InstitutionName(InstitutionKind value) => value switch { InstitutionKind.Council => "居民议事会", InstitutionKind.Monarchy => "君主制", _ => "行会议会" };
    private static string SpellName(SpellKind value) => value switch { SpellKind.Heal => "治疗", SpellKind.HarvestBlessing => "丰饶祝福", SpellKind.Shield => "守护结界", _ => "战斗火花" };

    private void BuildNationInspector(StackPanel panel)
    {
        var nation = _engine.State.Nations.FirstOrDefault(n => n.Id == _selectedNationId);
        if (nation is null) { panel.Children.Add(Paragraph("这个国家已不在当前世界中。其历史仍可在编年史查询。")); return; }
        panel.Children.Add(LiveText(() => nation.Name, 18, Mint));
        panel.Children.Add(LiveText(() => $"实际国家状态\n人口 {nation.Population} · 领土 {nation.Territory}\n{StockLabel(nation.Resources)}\n国家文化：{CultureName(nation.CultureId)}\n首都：{TownName(nation.CapitalId)} · 代表：{ResidentName(nation.RepresentativeId)}"));
        panel.Children.Add(Button("编辑这个国家", () => ShowNationEditor(nation.Id)));
        panel.Children.Add(Named(Button("文化、制度与政策", () => ShowGovernanceEditor(nation.Id)), "nation-governance"));
        panel.Children.Add(Named(Button("编辑国家文化价值", () => ShowCultureEditor(nation.CultureId)), "nation-culture-edit"));
        panel.Children.Add(Text("国家决策及其已知议题", 12, Mint));
        panel.Children.Add(LiveText(() =>
        {
            var institution = _engine.State.Society.Institutions.FirstOrDefault(i => i.NationId == nation.Id);
            return $"{(institution is null ? "尚未建立制度" : InstitutionName(institution.Kind))}\n{institution?.LastDecision}\n政策：{(institution?.PlayerPolicy is { } policy ? WorldEngine.PolicyName(policy) + "（玩家指定）" : "居民与代表协商自治")}\n{nation.Decision}";
        }));
        panel.Children.Add(Paragraph("决策取决于代表与信使实际送达的议题。下方聚落库存是上帝视角事实，不代表首都已知这些变化。"));
        LiveRows(panel, () => _engine.State.Settlements.Where(t => t.NationId == nation.Id).OrderBy(t => t.Id), t => t.Id.ToString(), t =>
        {
            var policy = _engine.State.Society.Policies.FirstOrDefault(p => p.SettlementId == t.Id);
            return $"{t.Name} #{t.Id} · {t.Population} 人\n{StockLabel(t.Resources)}\n{(policy is null ? "尚无政策" : WorldEngine.PolicyName(policy.Kind))}\n{policy?.Reason}";
        }, t => { _inspectorSettlementId = t.Id; OpenInspector("infrastructure"); });
        panel.Children.Add(Text("居民构成与文化传播", 12, Mint));
        panel.Children.Add(LiveText(() => string.Join("\n", _engine.State.Residents.Where(r => r.NationId == nation.Id).GroupBy(r => r.CultureId).Select(g => $"{CultureName(g.Key)}：{g.Count()} 人"))));
        panel.Children.Add(Button("前往首都", () => { var town = _engine.State.Settlements.FirstOrDefault(t => t.Id == nation.CapitalId); if (town is not null) _map.FocusTile(town.X, town.Y); _mobilePanel = false; ApplyLayout(); }));
    }

    private void ShowGovernanceEditor(int nationId)
    {
        var nation = _engine.State.Nations.FirstOrDefault(n => n.Id == nationId); if (nation is null) return;
        var institution = _engine.State.Society.Institutions.First(i => i.NationId == nationId);
        _paused = true; _map.IsSimulationPaused = true;
        var panel = ModalPanel("文化、制度与政策", "制度改变居民与代表意见的权重。玩家指定政策会持续覆盖自治；恢复自治后由实际收到的报告继续决策。国家文化不会瞬间改写每位居民的文化。");
        var cultures = _engine.State.Society.Cultures.ToArray();
        panel.Children.Add(Text("国家文化", 12, Muted));
        var culture = Named(new ComboBox { ItemsSource = cultures.Select(c => c.Name).ToArray(), SelectedIndex = Array.FindIndex(cultures, c => c.Id == nation.CultureId), HorizontalAlignment = HorizontalAlignment.Stretch }, "nation-culture"); panel.Children.Add(culture);
        var government = EnumField(panel, "政治制度", institution.Kind, InstitutionName, "nation-institution");
        var policy = EnumField(panel, "政策方向", institution.PlayerPolicy ?? PolicyKind.Balanced, WorldEngine.PolicyName, "nation-policy");
        var autonomous = Named(new CheckBox { Content = Text("由居民与代表自治决定政策", 12), IsChecked = institution.PlayerPolicy is null }, "nation-policy-autonomy"); panel.Children.Add(autonomous);
        policy.IsEnabled = autonomous.IsChecked != true; autonomous.IsCheckedChanged += (_, _) => policy.IsEnabled = autonomous.IsChecked != true;
        panel.Children.Add(Named(Button("应用制度与政策", () =>
        {
            if (culture.SelectedIndex < 0) { SetStatus("请选择有效文化。"); return; }
            RunEdit(() => { _engine.SetNationCulture(nationId, cultures[culture.SelectedIndex].Id); _engine.SetInstitution(nationId, (InstitutionKind)government.SelectedItem!); if (autonomous.IsChecked == true) _engine.SetPolicyAutonomy(nationId); else _engine.SetPolicy(nationId, (PolicyKind)policy.SelectedItem!); CloseModal(); }, "文化、制度与政策已更新");
        }), "nation-governance-apply"));
        OpenModal(panel);
    }
    private void ShowCultureEditor(int cultureId)
    {
        var culture = _engine.State.Society.Cultures.FirstOrDefault(c => c.Id == cultureId); if (culture is null) return;
        _paused = true; _map.IsSimulationPaused = true;
        var panel = ModalPanel("编辑文化价值", "文化独立于种族和国家。数值影响合作、创新与自然偏好，居民通过接触积累文化影响；修改会影响拥有该文化的居民未来行为。");
        var name = Field(panel, "名称", culture.Name, "culture-name");
        var cooperation = Field(panel, "合作倾向 0–1", culture.Cooperation, "culture-cooperation");
        var innovation = Field(panel, "创新倾向 0–1", culture.Innovation, "culture-innovation");
        var nature = Field(panel, "自然亲和 0–1", culture.NatureAffinity, "culture-nature");
        panel.Children.Add(Named(Button("应用文化价值", () =>
        {
            try
            {
                var a = Number(cooperation); var b = Number(innovation); var c = Number(nature);
                if (a is < 0 or > 1 || b is < 0 or > 1 || c is < 0 or > 1 || string.IsNullOrWhiteSpace(name.Text) || name.Text.Length > 60 || name.Text.Any(char.IsControl)) throw new ArgumentException("请输入名称及 0–1 的文化数值。");
                RunEdit(() => { _engine.RenameCulture(cultureId, name.Text); _engine.SetCultureValues(cultureId, a, b, c); CloseModal(); }, "文化价值已更新");
            }
            catch (ArgumentException ex) { SetStatus(FriendlyError(ex)); }
        }), "culture-apply"));
        OpenModal(panel);
    }

    private void BuildWorldRules(StackPanel panel)
    {
        panel.Children.Add(Paragraph("关闭发展路径会阻止新的相关研究与设施；已有能力与成果继续存在。存档会保存这些世界规则。"));
        var disasters = Named(new CheckBox { Content = Text("允许自然灾害", 12), IsChecked = _engine.State.NaturalDisasters }, "rule-disasters");
        var magic = Named(new CheckBox { Content = Text("允许新的魔法发展", 12), IsChecked = _engine.State.Society.MagicEnabled }, "rule-magic");
        panel.Children.Add(disasters); panel.Children.Add(magic);
        panel.Children.Add(Named(Button("应用世界规则", () => RunEdit(() => _engine.SetWorldRules(disasters.IsChecked == true, magic.IsChecked == true), "世界规则已更新")), "world-rules-apply"));
        panel.Children.Add(Text("中魔世界 · 有代价的局部能力", 12, Mint));
        panel.Children.Add(Paragraph("法术需要成年施法者、天赋、训练与魔力，目标必须位于附近。精灵治疗、矮人护盾、兽人战斗法术具有不同消耗；它们实际影响健康、农业或战斗。"));
        panel.Children.Add(Named(Button("选择居民施法", ShowSpellEditor), "spell-open"));
        panel.Children.Add(Text("已有文化", 12, Mint));
        LiveRows(panel, () => _engine.State.Society.Cultures.OrderBy(c => c.Id), c => c.Id.ToString(), c => $"{c.Name} #{c.Id}\n合作 {c.Cooperation:P0} · 创新 {c.Innovation:P0} · 自然 {c.NatureAffinity:P0}", c => ShowCultureEditor(c.Id));
    }

    private void BuildInfrastructureInspector(StackPanel panel, bool communications)
    {
        var towns = _engine.State.Settlements.OrderBy(t => t.Id).ToArray();
        if (towns.Length == 0) { panel.Children.Add(Paragraph("先投放居民形成聚落，才能建设设施与运输网络。")); return; }
        var selected = Array.FindIndex(towns, t => t.Id == _inspectorSettlementId); if (selected < 0) selected = 0;
        var town = towns[selected]; _inspectorSettlementId = town.Id;
        var picker = Named(new ComboBox { ItemsSource = towns.Select(t => $"{t.Name} · {NationName(t.NationId)}").ToArray(), SelectedIndex = selected, HorizontalAlignment = HorizontalAlignment.Stretch }, "infrastructure-town");
        picker.SelectionChanged += (_, _) => { if (picker.SelectedIndex < 0) return; _inspectorSettlementId = towns[picker.SelectedIndex].Id; InvalidateInspector(); RefreshInspector(true); }; panel.Children.Add(picker);
        panel.Children.Add(LiveText(() => $"{town.Name} #{town.Id}\n实际库存：{StockLabel(town.Resources)}\n居民 {town.Population} · 代表 {ResidentName(town.RepresentativeId)}"));
        panel.Children.Add(Named(Button("定位聚落并开始建设", () => { _map.SelectedSettlementId = town.Id; SetCategory("build"); _map.FocusTile(town.X, town.Y); _mobilePanel = false; ApplyLayout(); }), "infrastructure-build"));
        if (!communications)
        {
            panel.Children.Add(Text("研究 · 当地掌握的知识", 12, Mint));
            panel.Children.Add(LiveText(() =>
            {
                var research = _engine.State.Society.Research.FirstOrDefault(r => r.SettlementId == town.Id);
                return research is null ? "尚无研究记录" : $"已掌握：{string.Join("、", research.Completed.Select(WorldEngine.ResearchName))}\n{(research.ActiveProject is { } project ? $"正在研究 {WorldEngine.ResearchName(project)} · {research.Progress:F1}/{research.RequiredProgress:F0}" : "暂无研究项目")}";
            }));
            var researchPicker = EnumField(panel, "选择研究", ResearchKind.Agriculture, WorldEngine.ResearchName, "research-kind");
            var cost = Paragraph(StockLabel(WorldEngine.GetResearchCost(ResearchKind.Agriculture))); panel.Children.Add(cost);
            researchPicker.SelectionChanged += (_, _) => { if (researchPicker.SelectedItem is ResearchKind kind) cost.Text = "投入材料：" + StockLabel(WorldEngine.GetResearchCost(kind)); };
            panel.Children.Add(Named(Button("投入研究", () => RunEdit(() => _engine.StartResearch(town.Id, (ResearchKind)researchPicker.SelectedItem!), "研究已立项，需居民到学舍工作后推进")), "research-start"));
            panel.Children.Add(Paragraph("研究需要已建成学舍与到场人员。信号网络先需要驿路运输；新奥术研究受世界魔法规则限制。"));
            panel.Children.Add(Text("设施 · 施工与工作人员", 12, Mint));
            LiveRows(panel, () => _engine.State.Society.Buildings.Where(b => b.SettlementId == town.Id).OrderBy(b => b.Id), b => b.Id.ToString(), b => $"{WorldEngine.BuildingName(b.Kind)} #{b.Id} · {b.X},{b.Y}\n{(b.IsCompleted ? "已建成" : $"施工 {b.ConstructionProgress:F1}/{b.ConstructionRequired:F0}")} · 健康 {b.Health:F0}\n工作岗位 {b.Workers.Count}/{b.WorkSlots} · 最近工作 {DateLabel(b.LastWorkedTick)}", b => _map.FocusTile(b.X, b.Y));
            panel.Children.Add(Named(Button("查看设施成本与建造", () => ShowBuildingEditor(town.Id)), "building-open"));
        }
        panel.Children.Add(Text("实体运输与传信", 12, Mint));
        LiveRows(panel, () => _engine.State.Residents.Where(r => (r.SettlementId == town.Id || r.Agent.DestinationSettlementId == town.Id) && (r.Agent.Goal.Kind is AgentGoalKind.Trade or AgentGoalKind.DeliverMessage || r.Profession is Profession.Trader or Profession.Messenger)).OrderBy(r => r.Id).Take(30), r => r.Id.ToString(), r => $"{r.Name} · {GoalName(r.Agent.Goal.Kind)}\n{r.X},{r.Y} → {TownName(r.Agent.DestinationSettlementId)}\n携带：{StockLabel(r.Inventory)} · 消息 {r.Agent.CarriedMessages.Count} 条\n{r.Agent.Goal.Reason}", r => OpenResident(r.Id));
        panel.Children.Add(Text("通信覆盖与连通 · 当前实际状态", 12, Mint));
        panel.Children.Add(Paragraph("同国在运作的信号塔通过视线连通；聚落接入距离 12 格，塔间 24 格，山脉阻挡。设施需要工作人员、足够健康且未着火。道路与驿站改变实际信使行程。"));
        LiveRows(panel, () => _engine.State.Settlements.Where(t => t.NationId == town.NationId && t.Id != town.Id).OrderBy(t => t.Id), t => t.Id.ToString(), t => _engine.CanRelayInformation(town.Id, t.Id, out var ticks) ? $"{town.Name} ↔ {t.Name}\n信号连通 · 预计 {ticks} 日" : $"{town.Name} ↔ {t.Name}\n信号未连通 · 依赖居民实际携带消息", t => _map.FocusTile(t.X, t.Y));
        panel.Children.Add(LiveText(() => $"等待投递消息 {_engine.State.PendingMessages.Count} 条\n聚落公开知识 {town.PublicKnowledge.Count} 条 · 已递送报告 {_engine.State.Society.Reports.Count(r => r.RecipientSettlementId == town.Id)} 条"));
        if (communications)
        {
            panel.Children.Add(Text("聚落已知消息 · 与实际世界可能不同步", 12, Mint));
            LiveRows(panel, () => town.PublicKnowledge.OrderByDescending(f => f.LearnedTick).Take(30), f => f.Id.ToString(), FactLabel);
            panel.Children.Add(Text("实际递送的公民报告", 12, Mint));
            LiveRows(panel, () => _engine.State.Society.Reports.Where(r => r.RecipientSettlementId == town.Id).OrderByDescending(r => r.ReceivedTick).Take(20),
                r => $"{r.FactId}:{r.OriginResidentId}:{r.RepresentativeId}:{r.ReceivedTick}",
                r => $"{FactKindName(r.Topic)} · 主题 #{r.SubjectId} · 数值 {r.Value:F2}\n原始提出者：{ResidentName(r.OriginResidentId)}（{ProfessionName(r.ReportedProfession)}）\n递送代表：{ResidentName(r.RepresentativeId)}\n观察：{DateLabel(r.ObservedTick)}\n递送：{DateLabel(r.ReceivedTick)} · 可信度 {r.Confidence:P0}\n消息年龄 {Math.Max(0, _engine.State.Tick - r.ObservedTick)} 日");
        }
        panel.Children.Add(Button(communications ? "查看建设与研究" : "查看聚落通信与已知消息", () => OpenInspector(communications ? "infrastructure" : "communication")));
    }

    private void ShowBuildingEditor(int townId)
    {
        var town = _engine.State.Settlements.FirstOrDefault(t => t.Id == townId); if (town is null) return;
        var panel = ModalPanel("建造设施", "设施必须位于聚落 8 格内的可通行土地。材料从该聚落库存扣除，居民实际到场施工。道路可在聚落 24 格内绘制。");
        var type = EnumField(panel, "设施类型", BuildingKind.Farm, WorldEngine.BuildingName, "building-kind");
        var cost = Paragraph("材料：" + StockLabel(WorldEngine.GetBuildingCost(BuildingKind.Farm))); panel.Children.Add(cost);
        type.SelectionChanged += (_, _) => { if (type.SelectedItem is BuildingKind kind) cost.Text = "材料：" + StockLabel(WorldEngine.GetBuildingCost(kind)); };
        panel.Children.Add(Paragraph($"{town.Name}库存：{StockLabel(town.Resources)}\n驿站需要驿路运输；信号塔需要信号网络；奥术研习所需要奥术基础及开放魔法发展。"));
        var x = Field(panel, "目标 X", _selectedTile?.X ?? town.X + 1, "building-x"); var y = Field(panel, "目标 Y", _selectedTile?.Y ?? town.Y, "building-y");
        panel.Children.Add(Named(Button("建造设施", () =>
        {
            try { var xx = Integer(x); var yy = Integer(y); RunEdit(() => { _engine.BuildFacility(townId, (BuildingKind)type.SelectedItem!, xx, yy); CloseModal(); }, "设施已立项，继续模拟后居民会施工"); }
            catch (ArgumentException ex) { SetStatus(FriendlyError(ex)); }
        }), "building-apply"));
        OpenModal(panel);
    }
    private void ShowSpellEditor()
    {
        var casters = _engine.State.Residents.Where(r => r.Age >= 14).OrderByDescending(r => r.MagicTraining).ThenBy(r => r.Id).ToArray();
        if (casters.Length == 0) { SetStatus("当前世界没有成年居民。"); return; }
        _paused = true; _map.IsSimulationPaused = true;
        var panel = ModalPanel("施放魔法", "需要天赋至少 25、训练至少 8，目标在施法者 4 格内。治疗寻找本国伤病居民；丰饶与护盾作用于本国聚落；战斗火花只对交战敌人生效。");
        var caster = Named(new ComboBox { ItemsSource = casters.Select(r => $"{r.Name} · 天赋{r.MagicTalent:F0} 训练{r.MagicTraining:F0} 魔力{r.Mana:F0}").ToArray(), SelectedIndex = Math.Max(0, Array.FindIndex(casters, r => r.Id == _selectedResidentId)), HorizontalAlignment = HorizontalAlignment.Stretch }, "spell-caster"); panel.Children.Add(caster);
        var spell = EnumField(panel, "法术", SpellKind.Heal, SpellName, "spell-kind");
        var initial = casters[caster.SelectedIndex];
        var x = Field(panel, "目标 X", _selectedTile?.X ?? initial.X, "spell-x"); var y = Field(panel, "目标 Y", _selectedTile?.Y ?? initial.Y, "spell-y");
        var cost = Paragraph("基础魔力：治疗 16 · 丰饶 25 · 护盾 22 · 火花 20；种族适性可降低消耗。"); panel.Children.Add(cost);
        panel.Children.Add(Named(Button("施放法术", () =>
        {
            try { var xx = Integer(x); var yy = Integer(y); RunEdit(() => { _engine.CastSpell(casters[caster.SelectedIndex].Id, (SpellKind)spell.SelectedItem!, xx, yy); CloseModal(); }, "法术已生效，消耗已从施法者魔力扣除"); }
            catch (ArgumentException ex) { SetStatus(FriendlyError(ex)); }
        }), "spell-apply"));
        OpenModal(panel);
    }
}
