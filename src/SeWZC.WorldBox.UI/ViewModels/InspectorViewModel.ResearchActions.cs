using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.ViewModels.Details;

namespace SeWZC.WorldBox.UI.ViewModels;

internal sealed partial class InspectorViewModel
{
    internal void BuildResearchResidentActions(StackNode panel, Resident person)
    {
        panel.Children.Add(Named(LiveText(() =>
        {
            var current = _engine.GetResident(person.Id) ?? person;
            return "岗位功能：" + WorldEngine.ProfessionDescription(current.Profession)
                           + $"\n护甲剩余 {current.Armor:0.#}   个人结界 {current.PersonalWard:0.#}"
                           + (current.FrozenUntilTick > _engine.State.Tick
                               ? $"\n冻结剩余 {(current.FrozenUntilTick - _engine.State.Tick) / (double)SimulationTime.TicksPerDay:0.##} 日"
                               : "");
        }), "resident-research-role"));
        if (person.Health <= 0)
            return;
        if (person.Profession == Profession.Ranger)
        {
            panel.Children.Add(Named(Button("选择射击目标", () =>
            {
                var targets = _engine.State.Residents.Where(r => r.NationId != person.NationId && r.Health > 0)
                    .ToArray();
                if (targets.Length == 0)
                {
                    SetStatus("没有可选的外来居民");
                    return;
                }

                var modal = ModalPanel("游击射手射击", "需要随身弹药 1，目标在 4 格内且视线畅通，本人已收到交战军令。射击间隔至少 3 刻。");
                var target = ObjectField(modal, "目标", targets.Select(r => (r.Id, r.Name)), targets[0].Id,
                    "ranged-target");
                var requirements = Named(Paragraph(""), "ranged-requirements");
                modal.Children.Add(requirements);
                var apply = Named(Button("消耗弹药射击", () => RunEdit(() =>
                {
                    _engine.RangedAttack(person.Id, Integer(target));
                    CloseModal();
                }, "射击已执行")), "ranged-apply");

                void Refresh()
                {
                    var error = _engine.RangedAttackError(person.Id, Integer(target));
                    requirements.Text = error ?? "可以射击";
                    apply.IsEnabled = error is null;
                }

                target.SelectionChanged += (_, _) => Refresh();
                Refresh();
                modal.Children.Add(apply);
                OpenModal(modal);
            }), "resident-ranged"));
        }

        if (_engine.State.Society.Buildings.Any(b => b.Kind == BuildingKind.Waygate))
            panel.Children.Add(Named(Button("使用折跃门", _requests.Editors.ShowWaygateEditor), "resident-waygate"));
        if (person.Profession is Profession.Builder or Profession.Engineer or Profession.Firefighter)
        {
            panel.Children.Add(Named(Button("修复近处设施", () =>
            {
                var buildings = _engine.State.Society.Buildings.Where(b => b.SettlementId == person.SettlementId &&
                                                                           b.Health is > 0 and < 100
                                                                           && Math.Abs(b.X - person.X) +
                                                                           Math.Abs(b.Y - person.Y) <= 1).ToArray();
                if (buildings.Length == 0)
                {
                    SetStatus("1 格内没有受损的本地设施");
                    return;
                }

                RunEdit(() => _engine.RepairBuilding(person.Id, buildings.OrderBy(b => b.Health).First().Id),
                    "消耗随身石材修复了近处设施");
            }), "resident-repair"));
        }
    }
}
