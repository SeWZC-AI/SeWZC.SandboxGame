using Avalonia.Layout;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.ViewModels.Details;

namespace SeWZC.WorldBox.UI.ViewModels;

internal sealed partial class WorldEditorViewModel
{
    internal void ShowGovernanceEditor(int nationId)
    {
        var nation = _engine.State.Nations.FirstOrDefault(n => n.Id == nationId);
        if (nation is null)
            return;
        var institution = _engine.State.Society.Institutions.First(i => i.NationId == nationId);
        var panel = ModalPanel("文化、制度与政策", "制度改变居民与代表意见的权重。玩家指定政策会持续覆盖自治；恢复自治后由实际收到的报告继续决策。国家文化不会瞬间改写每位居民的文化。");
        var cultures = _engine.State.Society.Cultures.ToArray();
        panel.Children.Add(Text("国家文化", 12, Muted));
        var culture =
            Named(
                new ChoiceNode
                {
                    ItemsSource = cultures.Select(c => c.Name).ToArray(),
                    SelectedIndex = Array.FindIndex(cultures, c => c.Id == nation.CultureId),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                }, "nation-culture");
        panel.Children.Add(culture);
        var government = EnumField(panel, "政治制度", institution.Kind, InstitutionName, "nation-institution");
        var policy = EnumField(panel, "政策方向", institution.PlayerPolicy ?? PolicyKind.Balanced, WorldEngine.PolicyName,
            "nation-policy");
        var autonomous =
            Named(new CheckNode { Content = Text("由居民与代表自治决定政策", 12), IsChecked = institution.PlayerPolicy is null },
                "nation-policy-autonomy");
        panel.Children.Add(autonomous);
        policy.IsEnabled = autonomous.IsChecked != true;
        autonomous.IsCheckedChanged += (_, _) => policy.IsEnabled = autonomous.IsChecked != true;
        panel.Children.Add(Named(Button("应用制度与政策", () =>
        {
            if (culture.SelectedIndex < 0)
            {
                SetStatus("请选择有效文化。");
                return;
            }

            RunEdit(() =>
            {
                _engine.SetNationCulture(nationId, cultures[culture.SelectedIndex].Id);
                _engine.SetInstitution(nationId, (InstitutionKind)government.SelectedItem!);
                if (autonomous.IsChecked == true)
                    _engine.SetPolicyAutonomy(nationId);
                else
                    _engine.SetPolicy(nationId, (PolicyKind)policy.SelectedItem!);
                CloseModal();
            }, "文化、制度与政策已更新");
        }), "nation-governance-apply"));
        OpenModal(panel);
    }

    internal void ShowCultureEditor(int cultureId)
    {
        var culture = _engine.State.Society.Cultures.FirstOrDefault(c => c.Id == cultureId);
        if (culture is null)
            return;
        var panel = ModalPanel("编辑文化价值", "文化独立于种族和国家。数值影响合作、创新与自然偏好，居民通过接触积累文化影响；修改会影响拥有该文化的居民未来行为。");
        var name = Field(panel, "名称", culture.Name, "culture-name");
        var cooperation = Field(panel, "合作倾向 0–1", culture.Cooperation, "culture-cooperation");
        var innovation = Field(panel, "创新倾向 0–1", culture.Innovation, "culture-innovation");
        var nature = Field(panel, "自然亲和 0–1", culture.NatureAffinity, "culture-nature");
        panel.Children.Add(Named(Button("应用文化价值", () =>
        {
            try
            {
                var a = Number(cooperation);
                var b = Number(innovation);
                var c = Number(nature);
                if (a is < 0 or > 1 || b is < 0 or > 1 || c is < 0 or > 1 || string.IsNullOrWhiteSpace(name.Text) ||
                    name.Text.Length > 60 ||
                    name.Text.Any(char.IsControl))
                    throw new ArgumentException("请输入名称及 0–1 的文化数值。");
                RunEdit(() =>
                {
                    _engine.RenameCulture(cultureId, name.Text);
                    _engine.SetCultureValues(cultureId, a, b, c);
                    CloseModal();
                }, "文化价值已更新");
            }
            catch (ArgumentException ex)
            {
                SetStatus(FriendlyError(ex));
            }
        }), "culture-apply"));
        OpenModal(panel);
    }

    internal void ShowBuildingEditor(int townId)
    {
        ShowBuildingSelectionEditor(townId, BuildingKind.Farm);
    }

    internal void ShowBuildingSelectionEditor(int townId, BuildingKind initialKind)
    {
        var town = _engine.State.Settlements.FirstOrDefault(t => t.Id == townId);
        if (town is null)
            return;
        var panel = ModalPanel("建造设施", "选择设施和目标地块。安排施工会扣除当地材料，等待居民到场完成。");
        var type = EnumField(panel, "设施类型", initialKind, WorldEngine.BuildingName, "building-kind");
        var bridgeOptions = Named(new StackNode { Spacing = 10 }, "building-bridge-options");
        panel.Children.Add(bridgeOptions);
        var direction = EnumField(bridgeOptions, "桥梁方向", BridgeDirection.Horizontal, WorldEngine.BridgeDirectionName,
            "building-bridge-direction");
        var level = ObjectField(bridgeOptions, "桥梁等级",
            [(1, "1 级：离岸 2 格"), (2, "2 级：离岸 4 格"), (3, "3 级：离岸 6 格")], 1, "building-bridge-level");
        var cost = Named(Paragraph(""), "building-requirements");
        panel.Children.Add(cost);

        void RefreshCost()
        {
            bridgeOptions.IsVisible = type.SelectedItem is BuildingKind.Bridge;
            if (type.SelectedItem is BuildingKind kind)
            {
                cost.Text = "施工材料：" + StockLabel(WorldEngine.FacilityCost(kind,
                                        kind == BuildingKind.Bridge ? Integer(level) : 1))
                                    + (WorldEngine.IsWaterfrontBuilding(kind) ? "须建在紧邻自然陆岸的水域。" : "")
                                    + "\n" + (ProductionRules.For(kind) is { } a
                                        ? "运营需要：" + a.Research.Name + "及其前置\n"
                                        : "")
                                    + (kind is BuildingKind.Waystation or BuildingKind.MountainPass
                                            or BuildingKind.Bridge
                                            or BuildingKind.Dock ? "建设知识：驿路运输。\n"
                                        : kind == BuildingKind.ArcaneSanctum ? "建设知识：奥术基础，且须开启魔法发展。\n" : "")
                                    + (WorldEngine.BuildingRace(kind) is { } race
                                        ? $"种族条件：本聚落须有成年{RaceName(race)}，由同族成年人运营。\n"
                                        : "")
                                    + WorldEngine.BuildingDescription(kind);
            }
        }

        type.SelectionChanged += (_, _) => RefreshCost();
        level.SelectionChanged += (_, _) => RefreshCost();
        RefreshCost();
        panel.Children.Add(Paragraph($"{town.Name}库存：{StockLabel(town.Resources)}"));
        var x = Field(panel, "目标 X", _selectedTile?.X ?? town.X + 1, "building-x");
        var y = Field(panel, "目标 Y", _selectedTile?.Y ?? town.Y, "building-y");
        AddMapPicker(panel, x, y);
        var gift = Named(new CheckNode { Content = Text("直接赐予完工（不扣施工材料，仍需运营条件）", 12), IsChecked = false },
            "building-gift");
        panel.Children.Add(gift);
        var placement = Named(Paragraph(""), "building-placement-status");
        panel.Children.Add(placement);

        void RefreshPlacement()
        {
            try
            {
                var kind = (BuildingKind)type.SelectedItem!;
                var error = _engine.FacilityPlacementError(townId, kind, Integer(x), Integer(y), gift.IsChecked == true,
                    kind == BuildingKind.Bridge ? (BridgeDirection?)direction.SelectedItem : null,
                    kind == BuildingKind.Bridge ? Integer(level) : 1);
                placement.Text = error is not null ? "暂不能建造：" + error
                    : gift.IsChecked == true ? "可直接赐予完工；运营仍需满足设施条件" : "可安排施工；提交后扣除材料，等待居民到场";
            }
            catch (ArgumentException)
            {
                placement.Text = "请填写有效的整数坐标与桥梁等级。";
            }
        }

        type.SelectionChanged += (_, _) => RefreshPlacement();
        direction.SelectionChanged += (_, _) => RefreshPlacement();
        level.SelectionChanged += (_, _) => RefreshPlacement();
        x.ValueChanged += (_, _) => RefreshPlacement();
        y.ValueChanged += (_, _) => RefreshPlacement();
        gift.IsCheckedChanged += (_, _) => RefreshPlacement();
        RefreshPlacement();
        panel.Children.Add(Named(Button("建造设施", () =>
        {
            try
            {
                var xx = Integer(x);
                var yy = Integer(y);
                RunEdit(() =>
                {
                    if (gift.IsChecked == true)
                    {
                        _engine.GrantFacility(townId, (BuildingKind)type.SelectedItem!, xx, yy,
                            (BuildingKind)type.SelectedItem! == BuildingKind.Bridge
                                ? (BridgeDirection?)direction.SelectedItem
                                : null, (BuildingKind)type.SelectedItem! == BuildingKind.Bridge ? Integer(level) : 1);
                    }
                    else
                    {
                        _engine.BuildFacility(townId, (BuildingKind)type.SelectedItem!, xx, yy,
                            (BuildingKind)type.SelectedItem! == BuildingKind.Bridge
                                ? (BridgeDirection?)direction.SelectedItem
                                : null, (BuildingKind)type.SelectedItem! == BuildingKind.Bridge ? Integer(level) : 1);
                    }

                    CloseModal();
                }, gift.IsChecked == true ? "设施已赐予；效果按各建筑的生效条件提供" : "设施已立项，继续模拟后居民会施工");
            }
            catch (ArgumentException ex)
            {
                SetStatus(FriendlyError(ex));
            }
        }), "building-apply"));
        OpenModal(panel);
    }

    internal void ShowSpellSelectionEditor(SpellKind initialSpell, int? townId = null)
    {
        var selectedPerson = _inspectorMode == "resident" && townId is null
            ? _engine.GetResident(_selectedResidentId)
            : null;
        var localTown = townId ?? selectedPerson?.SettlementId ?? _requests.Inspector._inspectorSettlementId;
        var casters = _engine.State.Residents
            .Where(r => r.Health > 0 && r.Age >= 14 && (localTown <= 0 || r.SettlementId == localTown))
            .OrderBy(r => _engine.SpellUnlockError(r.Id, initialSpell) is not null)
            .ThenBy(r => r.MagicTalent < 25 || r.MagicTraining < 8).ThenByDescending(r => r.MagicTraining)
            .ThenBy(r => r.Id).ToArray();
        if (casters.Length == 0)
        {
            OpenModal(ModalPanel("施放魔法", "当前聚落没有存活的成年居民可供施法。可在居民档案查看天赋、训练与魔力。"));
            return;
        }

        var panel = ModalPanel("施放魔法", "选择当地施法者和目标。需要成年、天赋至少 25、训练至少 8；目标须在施法者 4 格内，具体法术还需满足当地研究、魔力和目标条件。");
        var caster = Named(new ChoiceNode
        {
            ItemsSource = casters.Select(r =>
                    $"{r.Name}   {TownName(r.SettlementId)}\n天赋 {r.MagicTalent:F0}   训练 {r.MagicTraining:F0}   魔力 {r.Mana:F0}")
                .ToArray(),
            SelectedIndex = selectedPerson is null
                ? 0
                : Math.Max(0, Array.FindIndex(casters, r => r.Id == selectedPerson.Id)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        }, "spell-caster");
        panel.Children.Add(caster);
        var spell = EnumField(panel, "法术", initialSpell, SpellName, "spell-kind");
        var initial = casters[caster.SelectedIndex];
        var x = Field(panel, "目标 X", _selectedTile?.X ?? initial.X, "spell-x");
        var y = Field(panel, "目标 Y", _selectedTile?.Y ?? initial.Y, "spell-y");
        var targetChosen = _selectedTile.HasValue;
        var movingTarget = false;
        AddMapPicker(panel, x, y, () => targetChosen = true);
        var requirements = Named(Paragraph(""), "spell-requirements");
        panel.Children.Add(requirements);

        string? Blocker()
        {
            var person = casters[caster.SelectedIndex];
            if (person.MagicTalent < 25 || person.MagicTraining < 8)
                return "需要魔法天赋至少 25、训练至少 8";
            if (_engine.SpellUnlockError(person.Id, (SpellKind)spell.SelectedItem!) is { } knowledge)
                return knowledge;
            if (x.Value is not { } xx || y.Value is not { } yy || xx != decimal.Truncate(xx) ||
                yy != decimal.Truncate(yy))
                return "请选择有效的整数目标坐标";
            if (Math.Abs(person.X - Integer(x)) + Math.Abs(person.Y - Integer(y)) > 4)
                return "目标须位于施法者 4 格以内";
            return null;
        }

        var apply = Named(Button("施放法术", () =>
        {
            try
            {
                var xx = Integer(x);
                var yy = Integer(y);
                RunEdit(() =>
                {
                    _engine.CastSpell(casters[caster.SelectedIndex].Id, (SpellKind)spell.SelectedItem!, xx, yy);
                    CloseModal();
                }, "法术已生效，消耗已从施法者魔力扣除");
            }
            catch (ArgumentException ex)
            {
                SetStatus(FriendlyError(ex));
            }
        }), "spell-apply");

        void RefreshRequirements()
        {
            var blocker = Blocker();
            requirements.Text = $"基础魔力：{WorldEngine.SpellManaCost((SpellKind)spell.SelectedItem!):0}\n"
                                + (blocker is null ? "训练、当地知识与目标距离满足；施放时检查魔力和实际目标" : "暂不能施放：" + blocker);
            apply.IsEnabled = blocker is null;
        }

        void TargetChanged()
        {
            if (!movingTarget)
                targetChosen = true;
            RefreshRequirements();
        }

        x.ValueChanged += (_, _) => TargetChanged();
        y.ValueChanged += (_, _) => TargetChanged();
        caster.SelectionChanged += (_, _) =>
        {
            if (!targetChosen)
            {
                movingTarget = true;
                x.Value = casters[caster.SelectedIndex].X;
                y.Value = casters[caster.SelectedIndex].Y;
                movingTarget = false;
            }

            RefreshRequirements();
        };
        spell.SelectionChanged += (_, _) => RefreshRequirements();
        RefreshRequirements();
        panel.Children.Add(apply);
        OpenModal(panel);
    }
}
