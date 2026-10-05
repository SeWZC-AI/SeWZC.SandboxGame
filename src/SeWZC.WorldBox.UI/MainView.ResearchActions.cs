using Avalonia.Controls;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    private void ShowResearchJobEditor(int townId, Profession job)
    {
        var panel = ModalPanel("分配" + WorldEngine.ProfessionName(job), WorldEngine.ProfessionDescription(job));
        var people = _engine.State.Residents.Where(r => r.SettlementId == townId && r.Health > 0 && r.Age >= 14 && r.ArmyId == 0 && r.Agent.DestinationSettlementId == 0).ToArray();
        if (people.Length == 0) { SetStatus("当地没有可分配的成年居民"); return; }
        var person = ObjectField(panel, "居民", people.Select(r => (r.Id, r.Name + "  " + WorldEngine.ProfessionName(r.Profession))), people[0].Id, "research-job-person");
        panel.Children.Add(Named(Button("分配岗位", () => RunEdit(() =>
        { _engine.AssignResearchProfession(Integer(person), job); CloseModal(); }, "岗位已分配，居民将自主寻找实际工作")), "research-job-apply"));
        OpenModal(panel);
    }

    private void ShowRailEditor(int townId)
    {
        var town = _engine.State.Settlements.First(t => t.Id == townId);
        var panel = ModalPanel("铺设铁路", "升级本国已有陆地道路，每格消耗石材 1、合金 0.5。居民沿实际连通的轨道移动。");
        var x = Field(panel, "目标 X", _selectedTile?.X ?? town.X, "rail-x");
        var y = Field(panel, "目标 Y", _selectedTile?.Y ?? town.Y, "rail-y");
        AddMapPicker(panel, x, y);
        var radius = Field(panel, "范围 0–4", 1, "rail-radius");
        panel.Children.Add(Named(Button("铺设铁路", () => RunEdit(() =>
        { _engine.BuildRail(townId, Integer(x), Integer(y), Integer(radius)); CloseModal(); }, "铁路已铺设，材料已扣除")), "rail-apply"));
        OpenModal(panel);
    }

    private void ShowWaygateEditor()
    {
        var people = _engine.State.Residents.Where(r => r.Health > 0 && r.Age >= 14 && r.ArmyId == 0).ToArray();
        var gates = _engine.State.Society.Buildings.Where(b => b.Kind == BuildingKind.Waygate).ToArray();
        if (people.Length == 0 || gates.Length == 0) { SetStatus("需要成年居民和两座同国折跃门"); return; }
        var panel = ModalPanel("使用折跃门", "本人须到源门 1 格内，两门相距至多 24 格。消耗个人魔力 30、随身魔晶 2，全部背包随本人抵达。");
        var person = ObjectField(panel, "居民", people.Select(r => (r.Id, r.Name)), people.Any(r => r.Id == _selectedResidentId) ? _selectedResidentId : people[0].Id, "waygate-person");
        var target = ObjectField(panel, "目标折跃门", gates.Select(b => (b.Id, TownName(b.SettlementId) + $"  {b.X},{b.Y}")), gates[0].Id, "waygate-target");
        var requirements = Named(Paragraph(""), "waygate-requirements"); panel.Children.Add(requirements);
        var apply = Named(Button("携带背包传送", () => RunEdit(() =>
        { _engine.TravelByWaygate(Integer(person), Integer(target)); CloseModal(); }, "居民已携背包抵达目标门")), "waygate-apply");
        panel.Children.Add(apply);
        void Refresh()
        {
            var error = _engine.WaygateTravelError(Integer(person), Integer(target));
            requirements.Text = DisplayFormat.Text(error ?? "人员、门与随身补给满足传送条件");
            apply.IsEnabled = error is null;
        }
        person.SelectionChanged += (_, _) => Refresh(); target.SelectionChanged += (_, _) => Refresh(); Refresh();
        OpenModal(panel);
    }

    private void BuildResearchResidentActions(StackPanel panel, Resident person)
    {
        panel.Children.Add(Named(LiveText(() =>
        {
            var current = _engine.GetResident(person.Id) ?? person;
            return "岗位功能：" + WorldEngine.ProfessionDescription(current.Profession)
                + $"\n护甲剩余 {current.Armor:0.#}   个人结界 {current.PersonalWard:0.#}"
                + (current.FrozenUntilTick > _engine.State.Tick ? $"\n冻结剩余 {current.FrozenUntilTick - _engine.State.Tick} 日" : "");
        }), "resident-research-role"));
        if (person.Health <= 0) return;
        if (person.Profession == Profession.Ranger)
            panel.Children.Add(Named(Button("选择射击目标", () =>
            {
                var targets = _engine.State.Residents.Where(r => r.NationId != person.NationId && r.Health > 0).ToArray();
                if (targets.Length == 0) { SetStatus("没有可选的外来居民"); return; }
                var modal = ModalPanel("游击射手射击", "需要随身弹药 1，目标在 4 格内且视线畅通，本人已收到交战军令。射击间隔至少 3 日。");
                var target = ObjectField(modal, "目标", targets.Select(r => (r.Id, r.Name)), targets[0].Id, "ranged-target");
                var requirements = Named(Paragraph(""), "ranged-requirements"); modal.Children.Add(requirements);
                var apply = Named(Button("消耗弹药射击", () => RunEdit(() =>
                { _engine.RangedAttack(person.Id, Integer(target)); CloseModal(); }, "射击已执行")), "ranged-apply");
                void Refresh()
                {
                    var error = _engine.RangedAttackError(person.Id, Integer(target));
                    requirements.Text = DisplayFormat.Text(error ?? "可以射击");
                    apply.IsEnabled = error is null;
                }
                target.SelectionChanged += (_, _) => Refresh(); Refresh(); modal.Children.Add(apply);
                OpenModal(modal);
            }), "resident-ranged"));
        if (_engine.State.Society.Buildings.Any(b => b.Kind == BuildingKind.Waygate))
            panel.Children.Add(Named(Button("使用折跃门", ShowWaygateEditor), "resident-waygate"));
        if (person.Profession is Profession.Engineer or Profession.Firefighter or Profession.Builder)
            panel.Children.Add(Named(Button("修复近处设施", () =>
            {
                var buildings = _engine.State.Society.Buildings.Where(b => b.SettlementId == person.SettlementId && b.Health is > 0 and < 100
                    && Math.Abs(b.X - person.X) + Math.Abs(b.Y - person.Y) <= 1).ToArray();
                if (buildings.Length == 0) { SetStatus("1 格内没有受损的本地设施"); return; }
                RunEdit(() => _engine.RepairBuilding(person.Id, buildings.OrderBy(b => b.Health).First().Id), "消耗随身石材修复了近处设施");
            }), "resident-repair"));
    }
}
