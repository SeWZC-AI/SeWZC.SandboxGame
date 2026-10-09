using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using SeWZC.WorldBox.Core;

/// <summary>默认世界的长程行为检查；在不同日内时刻校验状态并记录实际发展。</summary>
internal static class SimulationAudit
{
    internal static void Run(string path, WorldEngine engine, int years)
    {
        var seed = engine.State.Seed;
        var endTick = (long)years * SimulationTime.TicksPerYear;
        var checks = Checkpoints(years).Append(0).Append(endTick).Distinct().Order().ToArray();
        var rows = new List<object>();
        var annual = new List<object>();
        var notes = new List<string>();
        var started = Stopwatch.StartNew();
        var initialPopulation = engine.State.Population;
        var maxPopulation = initialPopulation;
        var slept = new HashSet<int>();
        var worked = new HashSet<int>();
        var deaths = new Dictionary<DeathCause, int>();
        var recordedDeaths = new HashSet<int>();
        var archived = engine.State.ArchivedResidents;
        var hourlyActivity = new long[SimulationTime.TicksPerDay, Enum.GetValues<ResidentActivity>().Length];
        var maxHungry = 0;
        var maxThirsty = 0;
        var yearsWithoutChildren = 0;
        var activityKinds = Enum.GetValues<ResidentActivity>();
        var assemblyHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(WorldEngine).Assembly.Location)));
        Console.WriteLine($"默认规则检查：{engine.State.Width}×{engine.State.Height}，初始 {initialPopulation} 人，种子 {seed}，{years} 年 / {endTick} tick");
        foreach (var target in checks)
        {
            while (engine.State.Tick < target)
            {
                engine.Step();
                var state = engine.State;
                maxPopulation = Math.Max(maxPopulation, state.Population);
                var hungryNow = 0;
                var thirstyNow = 0;
                var timeOfDay = SimulationTime.TimeOfDay(state.Tick);
                var nightNow = timeOfDay < SimulationTime.WakeTick || timeOfDay >= SimulationTime.SleepTick;
                foreach (var person in state.Residents)
                {
                    if (person.Hunger > 60) hungryNow++;
                    if (person.Thirst > 80) thirstyNow++;
                    hourlyActivity[timeOfDay, (int)person.Activity]++;
                    if (nightNow && person.Activity == ResidentActivity.Working && !person.Agent.Goal.PlayerDirected
                        && person.ArmyId == 0 && person.Hunger <= 60 && person.Thirst <= 80
                        && state.Tiles[person.Y * state.Width + person.X].FireTicks == 0)
                        throw new InvalidOperationException($"tick {state.Tick}：居民 {person.Id} 在夜间进行普通劳动。");
                    if (state.Tick > SimulationTime.TicksPerMonth) continue;
                    if (person.Activity == ResidentActivity.Sleeping) slept.Add(person.Id);
                    if (person.Activity == ResidentActivity.Working) worked.Add(person.Id);
                }
                maxHungry = Math.Max(maxHungry, hungryNow);
                maxThirsty = Math.Max(maxThirsty, thirstyNow);
                if (!ReferenceEquals(archived, state.ArchivedResidents))
                {
                    archived = state.ArchivedResidents;
                    foreach (var person in archived)
                        if (recordedDeaths.Add(person.Id))
                            deaths[person.DeathCause] = deaths.GetValueOrDefault(person.DeathCause) + 1;
                }
                if (state.Tick % SimulationTime.TicksPerYear == 0)
                {
                    var children = state.Residents.Count(person => person.Age < 18);
                    yearsWithoutChildren = children == 0 ? yearsWithoutChildren + 1 : 0;
                    if (yearsWithoutChildren == 10)
                        notes.Add($"tick {state.Tick}：连续十年没有儿童，需检查家庭供给和繁育条件。");
                    annual.Add(new
                    {
                        state.Tick, ElapsedYears = state.Tick / SimulationTime.TicksPerYear, state.Population,
                        Children = children, Hungry = hungryNow, Thirsty = thirstyNow,
                        MeanHealth = state.Population == 0 ? 0 : state.Residents.Average(person => person.Health),
                        Settlements = state.Settlements.Select(town => new
                        {
                            town.Id, town.Population, town.Resources, town.DevelopmentGoal, town.DevelopmentBlocker,
                            Research = state.Society.Research.First(r => r.SettlementId == town.Id).Completed.Select(r => r.Id).ToArray(),
                        }).ToArray(),
                    });
                }
            }

            var snapshot = engine.State;
            var json = engine.ExportJson();
            File.WriteAllText(Path.ChangeExtension(path, ".world.json"), json);
            // 完整核心校验包括关系、有限数值、路线、期限、日额度和途中状态。
            _ = WorldEngine.FromSnapshot(snapshot);
            var time = SimulationTime.TimeOfDay(snapshot.Tick);
            var night = time < SimulationTime.WakeTick || time >= SimulationTime.SleepTick;
            var inappropriateWork = snapshot.Residents.Where(person => night
                && person.Activity == ResidentActivity.Working && !person.Agent.Goal.PlayerDirected
                && person.ArmyId == 0 && person.Hunger <= 60 && person.Thirst <= 80
                && snapshot.Tiles[person.Y * snapshot.Width + person.X].FireTicks == 0).Select(p => p.Id).ToArray();
            if (inappropriateWork.Length > 0)
                throw new InvalidOperationException($"tick {snapshot.Tick}：夜间仍有普通劳动居民 {string.Join(',', inappropriateWork)}。");

            var restored = WorldEngine.ImportJson(json);
            var branch = WorldEngine.FromSnapshot(snapshot);
            // 检查读取不修改原世界，且不同日内时刻保存能续演跨过下一次晨起或入睡。
            var continuation = SimulationTime.TicksPerDay + (rows.Count * 7 + 1) % SimulationTime.TicksPerDay;
            branch.Step(continuation);
            restored.Step(continuation);
            if (branch.ExportJson() != restored.ExportJson() || engine.ExportJson() != json)
                throw new InvalidOperationException($"tick {snapshot.Tick}：保存恢复、续演或只读检查改变了世界。");

            if (snapshot.Population == 0)
                notes.Add($"tick {snapshot.Tick}：世界人口为零，需检查死亡原因与供给。");
            var hungry = snapshot.Residents.Count(p => p.Hunger > 60);
            var thirsty = snapshot.Residents.Count(p => p.Thirst > 80);
            if (hungry * 2 > snapshot.Population && snapshot.Population > 0)
                notes.Add($"tick {snapshot.Tick}：超过半数居民严重饥饿。");
            if (thirsty * 2 > snapshot.Population && snapshot.Population > 0)
                notes.Add($"tick {snapshot.Tick}：超过半数居民严重缺水。");
            var row = new
            {
                snapshot.Tick, snapshot.Year, snapshot.Month, snapshot.Day, TimeOfDay = time,
                ElapsedYears = snapshot.Tick / (double)SimulationTime.TicksPerYear,
                snapshot.Population, Hungry = hungry, Thirsty = thirsty,
                Sick = snapshot.Residents.Count(p => p.SicknessTicks > 0),
                MeanHealth = snapshot.Population == 0 ? 0 : snapshot.Residents.Average(p => p.Health),
                Activities = snapshot.Residents.GroupBy(p => p.Activity).ToDictionary(g => g.Key.ToString(), g => g.Count()),
                SleepAtHome = snapshot.Residents.Count(p => p.Activity == ResidentActivity.Sleeping
                    && snapshot.Settlements.Any(t => t.Id == p.SettlementId && Math.Abs(t.X - p.X) + Math.Abs(t.Y - p.Y) <= 1)),
                Deaths = deaths.ToDictionary(entry => entry.Key.ToString(), entry => entry.Value),
                Goals = snapshot.Residents.GroupBy(p => (p.Agent.DaytimeGoal ?? p.Agent.Goal).Kind)
                    .ToDictionary(group => group.Key.ToString(), group => group.Count()),
                CompletedBuildings = snapshot.Society.Buildings.Count(b => b.IsCompleted),
                ProductionBatches = snapshot.Society.Buildings.Sum(b => (long)b.ProductionBatches),
                Armies = snapshot.Armies.Count,
                Settlements = snapshot.Settlements.Select(t => new
                {
                    t.Id, t.Name, t.Population, t.Tier, t.Resources,
                    Professions = snapshot.Residents.Where(p => p.SettlementId == t.Id).GroupBy(p => p.Profession)
                        .ToDictionary(group => group.Key.ToString(), group => group.Count()),
                    Hungry = snapshot.Residents.Count(p => p.SettlementId == t.Id && p.Hunger > 60),
                    Thirsty = snapshot.Residents.Count(p => p.SettlementId == t.Id && p.Thirst > 80),
                    Research = snapshot.Society.Research.Where(r => r.SettlementId == t.Id)
                        .Select(r => new { Active = r.ActiveProject?.Id, r.Progress, r.RequiredProgress,
                            Completed = r.Completed.Select(a => a.Id).ToArray() }).ToArray(),
                    Buildings = snapshot.Society.Buildings.Where(b => b.SettlementId == t.Id)
                        .Select(b => new { b.Kind, b.IsCompleted, b.ConstructionProgress, b.ConstructionRequired,
                            b.Health, b.LastWorkedTick, b.ProductionBatches }).ToArray(),
                }).ToArray(),
                Events = snapshot.Events.TakeLast(20).Select(e => new { e.Tick, e.Kind, e.Message }).ToArray(),
                SaveBytes = System.Text.Encoding.UTF8.GetByteCount(json),
                StateSha256 = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json))),
                SaveContinuationVerified = true,
            };
            rows.Add(row);
            Console.WriteLine($"检查 tick {snapshot.Tick} / {snapshot.Year}年{snapshot.Month}月{snapshot.Day}日 {time}：人口 {snapshot.Population}，睡眠 {snapshot.Residents.Count(p => p.Activity == ResidentActivity.Sleeping)}，饥渴 {hungry}/{thirsty}，建筑 {row.CompletedBuildings}，{started.Elapsed.TotalSeconds:F1}s");
            WriteResult();
        }
        File.WriteAllText(Path.ChangeExtension(path, ".world.json"), engine.ExportJson());

        void WriteResult()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                Seed = seed, Width = engine.State.Width, Height = engine.State.Height,
                InitialPopulation = initialPopulation, MaxPopulation = maxPopulation,
                Years = years, EndTick = endTick, ReachedTick = engine.State.Tick,
                Rules = engine.State.Rules, engine.State.NaturalDisasters,
                SimulationTime.MonthsPerYear, SimulationTime.DaysPerMonth, SimulationTime.TicksPerDay,
                FirstMonthSleepingResidents = slept.Count, FirstMonthWorkingResidents = worked.Count,
                MaxHungry = maxHungry, MaxThirsty = maxThirsty,
                HourlyActivities = Enumerable.Range(0, SimulationTime.TicksPerDay).Select(hour => new
                {
                    Hour = hour, Activities = activityKinds.ToDictionary(activity => activity.ToString(),
                        activity => hourlyActivity[hour, (int)activity]),
                }).ToArray(),
                ElapsedSeconds = started.Elapsed.TotalSeconds,
                Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                CoreAssemblySha256 = assemblyHash,
                engine.State.FormatVersion, engine.State.SimulationVersion,
                Checkpoints = rows, AnnualResults = annual, Findings = notes,
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static IEnumerable<long> Checkpoints(int years)
    {
        var ordinal = 0;
        long Point(long boundary) => boundary - SimulationTime.TicksPerDay
            + (ordinal++ * 7 + SimulationTime.WakeTick + 1) % SimulationTime.TicksPerDay;
        for (var day = 1; day <= SimulationTime.DaysPerMonth; day++)
            yield return Point((long)day * SimulationTime.TicksPerDay);
        for (var month = 1; month <= SimulationTime.MonthsPerYear; month++)
            yield return Point((long)month * SimulationTime.TicksPerMonth);
        for (var year = 1; year <= Math.Min(10, years); year++)
            yield return Point((long)year * SimulationTime.TicksPerYear);
        for (var year = 10; year <= years; year += 10)
            yield return Point((long)year * SimulationTime.TicksPerYear);
    }
}
