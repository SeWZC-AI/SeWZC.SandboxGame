using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI.ViewModels;

internal sealed partial class WorldEditorViewModel
{
    internal void ShowResearchJobEditor(int townId, Profession job)
    {
        var panel = ModalPanel("分配" + WorldEngine.ProfessionName(job), WorldEngine.ProfessionDescription(job));
        var people = _engine.State.Residents.Where(r =>
            r.SettlementId == townId && r.Health > 0 && r.Age >= 14 && r.ArmyId == 0 &&
            r.Agent.DestinationSettlementId == 0).ToArray();
        if (people.Length == 0)
        {
            SetStatus("当地没有可分配的成年居民");
            return;
        }

        var person = ObjectField(panel, "居民",
            people.Select(r => (r.Id, r.Name + "  " + WorldEngine.ProfessionName(r.Profession))), people[0].Id,
            "research-job-person");
        panel.Children.Add(Named(Button("分配岗位", () => RunEdit(() =>
        {
            _engine.AssignResearchProfession(Integer(person), job);
            CloseModal();
        }, "岗位已分配，居民将自主寻找实际工作")), "research-job-apply"));
        OpenModal(panel);
    }

    internal void ShowRailEditor(int townId)
    {
        var town = _engine.State.Settlements.First(t => t.Id == townId);
        var panel = ModalPanel("铺设铁路", "升级本国已有陆地道路，每格消耗石材 1、合金 0.5。居民沿实际连通的轨道移动。");
        var x = Field(panel, "目标 X", _selectedTile?.X ?? town.X, "rail-x");
        var y = Field(panel, "目标 Y", _selectedTile?.Y ?? town.Y, "rail-y");
        AddMapPicker(panel, x, y);
        var radius = Field(panel, "范围 0–4", 1, "rail-radius");
        panel.Children.Add(Named(Button("铺设铁路", () => RunEdit(() =>
        {
            _engine.BuildRail(townId, Integer(x), Integer(y), Integer(radius));
            CloseModal();
        }, "铁路已铺设，材料已扣除")), "rail-apply"));
        OpenModal(panel);
    }

    internal void ShowWaygateEditor()
    {
        var people = _engine.State.Residents.Where(r => r.Health > 0 && r.Age >= 14 && r.ArmyId == 0).ToArray();
        var gates = _engine.State.Society.Buildings.Where(b => b.Kind == BuildingKind.Waygate).ToArray();
        if (people.Length == 0 || gates.Length == 0)
        {
            SetStatus("需要成年居民和两座同国折跃门");
            return;
        }

        var panel = ModalPanel("使用折跃门", "本人须到源门 1 格内，两门相距至多 24 格。消耗个人魔力 30、随身魔晶 2，全部背包随本人抵达。");
        var person = ObjectField(panel, "居民", people.Select(r => (r.Id, r.Name)),
            people.Any(r => r.Id == _selectedResidentId) ? _selectedResidentId : people[0].Id, "waygate-person");
        var target = ObjectField(panel, "目标折跃门", gates.Select(b => (b.Id, TownName(b.SettlementId) + $"  {b.X},{b.Y}")),
            gates[0].Id, "waygate-target");
        var requirements = Named(Paragraph(""), "waygate-requirements");
        panel.Children.Add(requirements);
        var apply = Named(Button("携带背包传送", () => RunEdit(() =>
        {
            _engine.TravelByWaygate(Integer(person), Integer(target));
            CloseModal();
        }, "居民已携背包抵达目标门")), "waygate-apply");
        panel.Children.Add(apply);

        void Refresh()
        {
            var error = _engine.WaygateTravelError(Integer(person), Integer(target));
            requirements.Text = error ?? "人员、门与随身补给满足传送条件";
            apply.IsEnabled = error is null;
        }

        person.SelectionChanged += (_, _) => Refresh();
        target.SelectionChanged += (_, _) => Refresh();
        Refresh();
        OpenModal(panel);
    }
}
