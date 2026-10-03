using Avalonia.Controls;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    private readonly HashSet<ObservedObject> _watched = [];
    private bool _historyWatchedOnly;

    private bool InvolvesObject(WorldEvent item, ObservedObject target) => WorldStories.Involves(item, target)
        || target.Kind == ObservedObjectKind.Resident && _engine.GetResident(target.Id)?.History.Any(h => !h.PlayerEdited && h.EventId == item.Id) == true;

    private bool IsWatched(WorldEvent item) => _watched.Any(target => InvolvesObject(item, target));

    private CheckBox WatchControl(ObservedObjectKind kind, int id, string name)
    {
        var target = new ObservedObject(kind, id);
        var check = Named(new CheckBox { Content = Text("关注相关事件", 12), IsChecked = _watched.Contains(target) }, name);
        check.IsCheckedChanged += (_, _) =>
        {
            if (check.IsChecked == true) _watched.Add(target); else _watched.Remove(target);
            RefreshUi();
        };
        return check;
    }

    private string EvidenceLabel(int id)
    {
        var fact = _engine.State.Residents.Concat(_engine.State.ArchivedResidents).SelectMany(r => r.Agent.Memory.Concat(r.Agent.CarriedMessages))
            .Concat(_engine.State.Settlements.SelectMany(t => t.PublicKnowledge)).FirstOrDefault(f => f.Id == id);
        return fact is null ? "采用的消息已超出认知记录的保留范围" : $"采用的消息：{fact.Text}\n观察时间：{DateLabel(fact.ObservedTick)}\n原始来源：{ResidentName(fact.OriginResidentId)}";
    }

    private string WatchedName(ObservedObject target) => target.Kind switch
    {
        ObservedObjectKind.Nation => "国家：" + NationName(target.Id),
        ObservedObjectKind.Settlement => "聚落：" + TownName(target.Id),
        _ => "居民：" + ResidentName(target.Id)
    };

    private void OpenWatched(ObservedObject target)
    {
        if (target.Kind == ObservedObjectKind.Nation) OpenNation(target.Id);
        else if (target.Kind == ObservedObjectKind.Resident) OpenResident(target.Id);
        else if (_engine.State.Settlements.Any(t => t.Id == target.Id))
        {
            RememberLocation(); _inspectorSettlementId = target.Id; OpenInspector("infrastructure", false);
        }
        else SetStatus("这处聚落已不在当前世界中；其保留事件仍可在编年史查看。");
    }

    private void BuildWatchedInspector(StackPanel panel)
    {
        panel.Children.Add(Paragraph("关注国家、聚落和居民的关键变化。仅在当前世界会话保留；不会自动移动镜头。"));
        panel.Children.Add(LiveText(() => $"已关注 {_watched.Count} 个对象"));
        LiveRows(panel, () => _watched.OrderBy(t => t.Kind).ThenBy(t => t.Id), t => $"{t.Kind}:{t.Id}", WatchedName, OpenWatched);
        panel.Children.Add(Named(Button("清空关注", () => { _watched.Clear(); RefreshUi(); }), "watch-clear"));
        LiveRows(panel, () => WorldStories.Group(_engine.State.Events.Where(IsWatched)).Take(50), GroupKey, GroupLabel, g => FocusEvent(g.Latest));
    }

    private static string GroupKey(EventGroup group) => group.Entries[0].Id.ToString();
    private string GroupLabel(EventGroup group) => group.Count == 1 ? EventLabel(group.Latest)
        : $"同类事件 {group.Count} 次   {DateLabel(group.Entries[0].Tick)} 至 {DateLabel(group.Latest.Tick)}\n{EventLabel(group.Latest)}\n点击展开原始记录";

    private void BuildStoryInspector(StackPanel panel)
    {
        var person = _engine.GetResident(_selectedResidentId);
        if (person is null) { panel.Children.Add(Paragraph("该人物的档案已不在保留范围内。")); return; }
        panel.Children.Add(Text(person.Name + "的重要转折", 18, Mint));
        panel.Children.Add(WatchControl(ObservedObjectKind.Resident, person.Id, "story-watch"));
        panel.Children.Add(Paragraph("时间线来自实际经历和保留事件。玩家编辑会明确标记；个人记忆可能过时，不代表世界事实。"));
        LiveRows(panel, () => person.History.Select((entry, index) => (entry, index)).OrderByDescending(x => x.entry.Tick).ThenByDescending(x => x.index),
            x => x.index.ToString(), x =>
            {
                var entry = x.entry;
                var retained = _engine.State.Events.Any(e => e.Id == entry.EventId);
                return $"时间：{DateLabel(entry.Tick)}\n重要程度：{ImportanceName(entry.Importance)}\n记录来源：{(entry.PlayerEdited ? "玩家编辑" : "模拟记录")}\n{entry.Text}"
                    + (entry.SettlementId > 0 ? $"\n当时归属：{NationName(entry.NationId)} / {TownName(entry.SettlementId)}" : "")
                    + (entry.EvidenceFactId > 0 ? $"\n{EvidenceLabel(entry.EvidenceFactId)}" : "")
                    + (entry.EventId > 0 ? retained ? "\n查看关联世界事件" : "\n关联事件已超出保留范围" : "");
            }, x =>
            {
                if (_engine.State.Events.FirstOrDefault(e => e.Id == x.entry.EventId) is { } entry) FocusEvent(entry);
                else SetStatus("此经历没有仍在保留范围内的世界事件。");
            });
        panel.Children.Add(Text("实际决策与当时依据", 13, Mint));
        LiveRows(panel, () => person.Agent.Decisions.AsEnumerable().Reverse(), d => $"{d.Tick}:{d.Goal}:{d.EvidenceFactId}",
            d => $"决策时间：{DateLabel(d.Tick)}\n目标：{GoalName(d.Goal)}\n{d.Reason}\n消息观察时间：{DateLabel(d.KnowledgeObservedTick)}\n消息来源：{ResidentName(d.SourceResidentId)}");
        panel.Children.Add(Text("此人参与或直接相关的世界事件", 13, Mint));
        LiveRows(panel, () => WorldStories.Group(_engine.State.Events.Where(e => InvolvesObject(e, new(ObservedObjectKind.Resident, person.Id)))), GroupKey, GroupLabel, g => FocusEvent(g.Latest));
    }

    private void BuildMilitarySummary(StackPanel panel, Nation nation)
    {
        panel.Children.Add(Text("作战目标与已收战报", 13, Mint));
        panel.Children.Add(Named(LiveText(() =>
        {
            var record = nation.Military;
            if (record.CampaignEventId == 0) return "尚无作战目标";
            return $"作战目标：{WorldEngine.ObjectiveName(record.Objective)}\n目标聚落：{TownName(record.TargetSettlementId)}\n目标位置：{record.TargetX}, {record.TargetY}\n{record.Report}"
                + (record.LastReportEventId > 0 ? $"\n战报观察：{DateLabel(record.LastReportObservedTick)}\n战报送达：{DateLabel(record.LastReportReceivedTick)}" : "")
                + (record.RecoveryUntilTick > _engine.State.Tick ? $"\n恢复期剩余 {record.RecoveryUntilTick - _engine.State.Tick} 日，暂停自主进攻，仍可组织防御" : "\n当前不在恢复期");
        }), "nation-military"));
        panel.Children.Add(Text("前线实际状态（上帝视角）", 12, Mint));
        LiveRows(panel, () => _engine.State.Armies.Where(a => a.NationId == nation.Id), a => a.Id.ToString(),
            a => $"作战目标：{WorldEngine.ObjectiveName(a.Objective)}\n行动状态：{a.Status}\n目标聚落：{TownName(a.TargetSettlementId)}\n部队：{a.Soldiers}/{a.InitialSoldiers} 人\n实有军粮：{a.Supplies:F1}\n士气：{a.Morale:F0}\n{WorldEngine.OutcomeName(a.Outcome)}",
            a => _map.FocusTile(a.X, a.Y));
    }
}
