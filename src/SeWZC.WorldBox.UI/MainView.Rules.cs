using Avalonia.Controls;
using Avalonia.Layout;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    private void BuildWorldRules(StackPanel panel)
    {
        var draft = _engine.State.Rules with { };
        var switches = new List<(CheckBox Box, Func<WorldRules, bool> Get, Action<WorldRules, bool> Set)>();
        var preset =
            Named(
                new ComboBox
                {
                    ItemsSource = new[] { "自定义 / 当前规则", "和平繁荣", "文明兴衰", "动荡世界" },
                    SelectedIndex = 0,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                }, "rules-preset");
        panel.Children.Add(preset);
        var disasters = Named(new CheckBox { Content = "允许自然灾害", IsChecked = _engine.State.NaturalDisasters },
            "rule-disasters");
        var magic = Named(new CheckBox { Content = "允许新的魔法发展", IsChecked = _engine.State.Society.MagicEnabled },
            "rule-magic");

        void Toggle(string title, string hint, string id, Func<WorldRules, bool> get, Action<WorldRules, bool> set)
        {
            var check = Named(new CheckBox { Content = title, IsChecked = get(draft), MinHeight = 36 }, id);
            ToolTip.SetTip(check, hint);
            switches.Add((check, get, set));
            panel.Children.Add(check);
        }

        panel.Children.Add(Text("人口与生存", 14, Mint));
        Toggle("自然生育", "有足够成年居民、住房与粮食时出现新生儿", "rule-births", r => r.Births, (r, v) => r.Births = v);
        Toggle("衰老", "关闭后停止自然增龄与衰老伤害", "rule-aging", r => r.Aging, (r, v) => r.Aging = v);
        Toggle("饥饿与粮食压力", "关闭后停止生存口粮消耗和饥饿伤害", "rule-hunger", r => r.Hunger, (r, v) => r.Hunger = v);
        Toggle("饮水与缺水压力", "居民须在河湖岸边打水，随身携带并运输入仓", "rule-thirst", r => r.Thirst, (r, v) => r.Thirst = v);
        Toggle("疾病传播与伤害", "关闭后现有疾病倒计时继续消退，不再传播或伤害", "rule-disease", r => r.Disease, (r, v) => r.Disease = v);
        panel.Children.Add(Text("文明发展", 14, Mint));
        Toggle("自主建设", "关闭后不自行立项；已开始施工继续", "rule-construction", r => r.Construction, (r, v) => r.Construction = v);
        Toggle("自主研究", "关闭后不自行启动新研究；已开始研究继续", "rule-research", r => r.Research, (r, v) => r.Research = v);
        Toggle("拓荒与扩张", "满足人口、资源与可见用地条件后，拓荒者带物资步行建村", "rule-expansion", r => r.Expansion, (r, v) => r.Expansion = v);
        Toggle("自主贸易", "停止新的贸易任务；在途货物继续送达", "rule-trade", r => r.Trade, (r, v) => r.Trade = v);
        panel.Children.Add(Text("外交与兴衰", 14, Mint));
        Toggle("自主结盟", "文明依据实际收到的接触与往来消息建立关系", "rule-alliances", r => r.Alliances, (r, v) => r.Alliances = v);
        Toggle("自主宣战", "关闭只阻止新宣战；已有战争仍需停战", "rule-wars", r => r.Wars, (r, v) => r.Wars = v);
        Toggle("自主停战", "战事持续或补给不足时宣布停战，命令须送达前线", "rule-peace", r => r.Peace, (r, v) => r.Peace = v);
        Toggle("居民迁徙", "困苦居民依据获知的粮情寻找新家园，实地抵达后转属", "rule-migration", r => r.Migration, (r, v) => r.Migration = v);
        Toggle("聚落分裂", "长期未解决的困苦报告积累动荡，非首都聚落可能独立", "rule-secession", r => r.Secession, (r, v) => r.Secession = v);

        ComboBox Select(string title, string[] choices, int selected, string id)
        {
            panel.Children.Add(Text(title, 12, Muted));
            var box = Named(
                new ComboBox
                {
                    ItemsSource = choices,
                    SelectedIndex = selected,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                }, id);
            panel.Children.Add(box);
            return box;
        }

        var conflict = Select("竞争与动荡强度", ["和平倾向", "低（留出恢复时间）", "标准", "高"], draft.Conflict, "rule-conflict");
        var rates = new[] { .5, 1d, 2d, 3d };
        var development = Select("建设与研究速率（不改变世界时间）", ["缓慢 0.5 倍", "标准 1 倍", "快速 2 倍", "迅速 3 倍"],
            Array.IndexOf(rates, draft.DevelopmentRate), "rule-development-rate");
        panel.Children.Add(Text("生态与劳动", 14, Mint));
        Toggle("自然资源再生", "关闭后地格资源不再自然恢复；已经伐尽的森林仍为空地", "rule-regeneration", r => r.ResourceRegeneration,
            (r, v) => r.ResourceRegeneration = v);
        Toggle("火势蔓延", "关闭后现有火焰继续燃烧，但不会引燃邻近森林", "rule-fire-spread", r => r.FireSpread, (r, v) => r.FireSpread = v);
        var gathering = Field(panel, "采集速率 0.25–3", draft.GatheringRate, "rule-gathering-rate", 3);
        gathering.Minimum = .25m;
        gathering.Increment = .25m;
        var combat = Field(panel, "战斗伤害倍率 0.25–3", draft.CombatDamageRate, "rule-combat-rate", 3);
        combat.Minimum = .25m;
        combat.Increment = .25m;
        panel.Children.Add(Text("环境与魔法", 14, Mint));
        panel.Children.Add(disasters);
        var frequency = Select("自然灾害频率", ["关闭", "低", "标准", "高"], draft.DisasterFrequency, "rule-disaster-frequency");
        var strength = Select("自然灾害范围", ["局部", "区域", "广泛"], draft.DisasterStrength - 1, "rule-disaster-strength");
        panel.Children.Add(magic);
        var magicRate = Select("魔法训练与恢复速率", ["缓慢 0.5 倍", "标准 1 倍", "快速 2 倍", "迅速 3 倍"],
            Array.IndexOf(rates, draft.MagicRate), "rule-magic-rate");
        panel.Children.Add(Paragraph("关闭新的魔法发展保留既有能力。已有施法者继续使用魔法；训练与恢复速率和时间倍率分别控制。"));
        preset.SelectionChanged += (_, _) =>
        {
            if (preset.SelectedIndex <= 0) return;
            draft = WorldRules.For((WorldPreset)(preset.SelectedIndex - 1));
            foreach (var entry in switches) entry.Box.IsChecked = entry.Get(draft);
            conflict.SelectedIndex = draft.Conflict;
            frequency.SelectedIndex = draft.DisasterFrequency;
            strength.SelectedIndex = draft.DisasterStrength - 1;
            development.SelectedIndex = Array.IndexOf(rates, draft.DevelopmentRate);
            magicRate.SelectedIndex = Array.IndexOf(rates, draft.MagicRate);
            disasters.IsChecked = draft.DisasterFrequency > 0;
            gathering.Value = (decimal)draft.GatheringRate;
            combat.Value = (decimal)draft.CombatDamageRate;
        };
        panel.Children.Add(Named(Button("应用世界规则", () => RunEdit(() =>
        {
            foreach (var entry in switches) entry.Set(draft, entry.Box.IsChecked == true);
            draft.GatheringRate = Number(gathering);
            draft.CombatDamageRate = Number(combat);
            draft.Conflict = Math.Max(0, conflict.SelectedIndex);
            draft.DisasterFrequency = Math.Max(0, frequency.SelectedIndex);
            draft.DisasterStrength = Math.Max(0, strength.SelectedIndex) + 1;
            draft.DevelopmentRate = rates[Math.Max(0, development.SelectedIndex)];
            draft.MagicRate = rates[Math.Max(0, magicRate.SelectedIndex)];
            _engine.ConfigureWorld(draft, disasters.IsChecked == true, magic.IsChecked == true);
            CloseModal();
        }, "世界规则已应用并随存档保存，点击继续观察")), "world-rules-apply"));
    }
}
