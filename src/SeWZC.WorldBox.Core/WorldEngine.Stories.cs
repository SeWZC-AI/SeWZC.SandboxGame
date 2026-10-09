using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private void RecordLife(ResidentCursor person, string text, WorldEvent? entry = null,
        PersonalExperienceKind experience = PersonalExperienceKind.Neutral,
        EventImportance importance = EventImportance.Notable)
    {
        var history = person.History.Add(new ResidentHistoryEntry
        {
            Tick = Current.Tick,
            Text = text,
            Importance = importance,
            EventId = entry?.Id ?? 0,
            EvidenceFactId = entry?.EvidenceFactId ?? 0,
            SettlementId = person.SettlementId,
            NationId = person.NationId,
            Experience = experience,
        });
        while (history.Count > 24)
            history = history.RemoveAt(0);
        person.Replace(person.Value with { History = history });
    }

    private void ObserveProjects()
    {
        foreach (var building in Current.Buildings)
            if (!building.Value.IsCompleted)
            {
                building.Replace(building.Value with
                {
                    Observation = building.Value.Observation.Observe(Current.Tick, Current.Rules.DevelopmentRate,
                    building.Value.ConstructionProgress)
                });
            }

        var tick = Current.Tick;
        var rate = Current.Rules.DevelopmentRate;
        var research = Current.Society.Research.Map(project => project.Observe(tick, rate));
        if (!ReferenceEquals(research, Current.Society.Research))
            Current.Society = Current.Society with { Research = research };
    }

    /// <summary>依据近期稳定的实际工作速率估算项目剩余日数；依据不足时给出原因。</summary>
    /// <param name="observation">项目实际进度的采样记录。</param>
    /// <param name="progress">项目已经累计的工作量。</param>
    /// <param name="required">项目完成所需的总工作量。</param>
    public CompletionEstimate GetCompletionEstimate(ProjectObservation observation, double progress, double required)
    {
        if (progress >= required)
            return new CompletionEstimate(0, "已完成");
        var samples = observation.Samples;
        if (observation.DevelopmentRate != Current.Rules.DevelopmentRate || samples.Length < 4)
            return new CompletionEstimate(null, "暂无法估算：等待足够的实际工作记录");
        var last = samples[^1];
        if (Current.Tick - last.Tick > 8 || samples[^1].Progress <= samples[^2].Progress)
            return new CompletionEstimate(null, "暂停估算：近期未取得实际进度");
        var rates = samples.Zip(samples.Skip(1), (a, b) => (b.Progress - a.Progress) / (b.Tick - a.Tick)).ToArray();
        var mean = rates.Average();
        if (mean <= 0 || rates.Any(r => r < mean * .5 || r > mean * 1.5))
            return new CompletionEstimate(null, "暂无法估算：近期工作速率不稳定");
        var duration = Math.Ceiling((required - progress) / mean);
        if (!double.IsFinite(duration) || duration > 100_000)
            return new CompletionEstimate(null, "暂无法估算：有效工作速率过低");
        var remaining = (long)duration;
        return new CompletionEstimate(remaining,
            $"按近期实际速率，预计还需约 {remaining / (double)SimulationTime.TicksPerDay:0.##} 日；人员与供给变化会影响结果");
    }

    /// <summary>估算聚落当前建设或研究项目的剩余日数。</summary>
    /// <param name="settlementId">聚落 ID。</param>
    public CompletionEstimate GetDevelopmentEstimate(int settlementId)
    {
        _ = RequireTown(settlementId);
        var building = Current.Buildings.FirstOrDefault(b => b.Value.SettlementId == settlementId && !b.Value.IsCompleted);
        if (building is not null)
        {
            return GetCompletionEstimate(building.Value.Observation, building.Value.ConstructionProgress,
                building.Value.ConstructionRequired);
        }

        var research = Current.Society.Research.First(r => r.SettlementId == settlementId);
        return research.ActiveProject is not null
            ? GetCompletionEstimate(research.Observation, research.Progress, research.RequiredProgress)
            : new CompletionEstimate(null, "尚无进行中的建设或研究");
    }

    private static void ValidateStoryReferences(Resident person, int nextId)
    {
        bool Valid(int id)
        {
            return id >= 0 && id < nextId;
        }

        CheckV2(Valid(person.Agent.Goal.EvidenceFactId) && Valid(person.Agent.Goal.CauseEventId), "目标证据引用无效。");
        if (person.Agent.DaytimeGoal is { } daytime)
            CheckV2(Valid(daytime.EvidenceFactId) && Valid(daytime.CauseEventId), "暂存白天目标的证据引用无效。");
        foreach (var fact in person.Agent.Memory.Concat(person.Agent.CarriedMessages))
            CheckV2(Valid(fact.EventId) && Valid(fact.CampaignEventId), "消息事件引用无效。");
        foreach (var entry in person.History)
            CheckV2(
                Valid(entry.EventId) && Valid(entry.SettlementId) && Valid(entry.NationId) &&
                Valid(entry.EvidenceFactId), "人物经历引用无效。");
    }

    private static void ValidateStories(WorldState state)
    {
        bool Reference(int id)
        {
            return id >= 0 && id < state.NextId;
        }

        bool Time(long tick)
        {
            return tick >= 0 && tick <= state.Tick;
        }

        void Project(ProjectObservation? observation, double progress)
        {
            CheckV2(observation is not null && Reference(observation.StartEventId) &&
                    Number(observation.DevelopmentRate, .5, 3)
                    && !observation.Contributors.IsDefault && observation.Contributors.Length <= 32 &&
                    observation.Contributors.All(id => id > 0 && Reference(id)) &&
                    observation.Contributors.Distinct().Count() == observation.Contributors.Length
                    && !observation.Samples.IsDefault && observation.Samples.Length <= 7, "项目观察记录无效。");
            long previousTick = -1;
            double previousProgress = 0;
            foreach (var sample in observation.Samples)
            {
                CheckV2(sample is not null && Time(sample.Tick) && sample.Tick > previousTick
                        && Number(sample.Progress, previousProgress, progress), "项目进度样本无效。");
                previousTick = sample.Tick;
                previousProgress = sample.Progress;
            }
        }

        foreach (var building in state.Society.Buildings)
            Project(building.Observation, building.ConstructionProgress);
        foreach (var research in state.Society.Research)
        {
            Project(research.Observation, research.Progress);
            CheckV2(Reference(research.LastCompletionEventId), "研究事件编号无效。");
        }

        var eventIds = new HashSet<int>();
        CheckV2(state.Events.Select(e => e.Id).Distinct().Count() == state.Events.Count, "事件编号重复。");
        var events = state.Events.ToDictionary(e => e.Id);
        foreach (var entry in state.Events)
        {
            CheckV2(entry.Id > 0 && Reference(entry.Id) && eventIds.Add(entry.Id) && Enum.IsDefined(entry.Action)
                    && Reference(entry.NationId) && Reference(entry.SecondNationId) && Reference(entry.ResidentId)
                    && Reference(entry.SettlementId) && Reference(entry.SecondSettlementId) &&
                    Reference(entry.EvidenceFactId)
                    && Reference(entry.CauseEventId) && entry.AdditionalCauseEventIds is not null &&
                    entry.AdditionalCauseEventIds.Count <= 4
                    && entry.AdditionalCauseEventIds.All(id => id > 0 && Reference(id) && id < entry.Id)
                    && entry.AdditionalCauseEventIds.Distinct().Count() == entry.AdditionalCauseEventIds.Count,
                "事件关联无效。");
            foreach (var id in WorldStories.Causes(entry))
                CheckV2(
                    Reference(id) && id < entry.Id &&
                    (!events.TryGetValue(id, out var cause) || cause.Tick <= entry.Tick), "事件前因须早于结果。");
        }

        foreach (var nation in state.Nations)
        {
            var record = nation.Military;
            CheckV2(record is not null && Reference(record.CampaignEventId) && Reference(record.EnemyNationId)
                    && Reference(record.TargetSettlementId) && Reference(record.LastMobilizedOrderId) &&
                    Reference(record.LastReportEventId)
                    && Enum.IsDefined(record.Objective) && Enum.IsDefined(record.ReportedOutcome) &&
                    BoundedText(record.Report, 400)
                    && Coordinates(record.TargetX, record.TargetY, state.Width, state.Height) &&
                    Time(record.StartedTick)
                    && Time(record.LastReportObservedTick) && Time(record.LastReportReceivedTick)
                    && record.LastReportObservedTick <= record.LastReportReceivedTick
                    && record.RecoveryUntilTick >= 0 && record.RecoveryUntilTick <= state.Tick + 100_000, "国家军事记录无效。");
        }

        foreach (var army in state.Armies)
            CheckV2(Reference(army.CampaignEventId) && Reference(army.LastEventId) && Reference(army.TargetSettlementId)
                    && Enum.IsDefined(army.Objective) && Enum.IsDefined(army.Outcome) && Time(army.StartedTick)
                    && army.InitialSoldiers is >= 0 and <= MaxPopulation && army.BlockedTicks is >= 0 and <= 100_000,
                "作战目标或进程无效。");
        foreach (var report in state.Society.Reports)
            CheckV2(Reference(report.EventId), "制度报告事件引用无效。");
        foreach (var person in state.Residents.Concat(state.ArchivedResidents))
            ValidateStoryReferences(person, state.NextId);
        foreach (var person in state.Residents.Concat(state.ArchivedResidents))
        foreach (var entry in person.History)
            CheckV2(
                Reference(entry.EventId) && Reference(entry.EvidenceFactId) && Reference(entry.SettlementId) &&
                Reference(entry.NationId), "人物经历关联无效。");
        foreach (var fact in state.Residents.Concat(state.ArchivedResidents)
                     .SelectMany(r => r.Agent.Memory.Concat(r.Agent.CarriedMessages))
                     .Concat(state.Settlements.SelectMany(t => t.PublicKnowledge))
                     .Concat(state.PendingMessages.SelectMany(m => m.Facts)))
            CheckV2(Reference(fact.EventId) && Reference(fact.CampaignEventId), "消息事件编号无效。");
    }
}
