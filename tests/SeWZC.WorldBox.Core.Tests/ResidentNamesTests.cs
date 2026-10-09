using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core.Tests;

/// <summary>批量命名避开在世居民与死亡档案中的已有名字。</summary>
public sealed class ResidentNamesTests
{
    /// <summary>候选名字重复时，批量投放仍保留原名并为新居民选择不同名字。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Spawned_names_avoid_existing_and_archived_names(bool archived)
    {
        var fixture = new WorldFixture();
        var original = fixture.Resident.Value;
        if (archived)
        {
            fixture.Resident.Replace(original with { Name = "在世居民" });
            fixture.Engine.ArchivedResidents.Add(new ResidentCursor(original with
            {
                Id = original.Id + 1000,
                Health = 0,
                DeathCause = DeathCause.OldAge,
                DeathTick = 0,
            }));
        }

        // 同一种族的命名候选每 32768 个稳定编号循环一次，强制遇到原名。
        var firstId = original.Id + 32768;
        fixture.Engine.NextId = firstId;
        fixture.Engine.SpawnResidents(16, 16, original.Race, 4);

        var spawned = fixture.Engine.State.Residents.Where(person => person.Id >= firstId).ToArray();
        Assert.Equal(4, spawned.Length);
        Assert.DoesNotContain(spawned, person => person.Name == original.Name);
        var allNames = fixture.Engine.State.Residents.Concat(fixture.Engine.State.ArchivedResidents)
            .Select(person => person.Name).ToArray();
        Assert.Equal(allNames.Length, allNames.Distinct(StringComparer.Ordinal).Count());
        if (archived)
            Assert.Equal(original.Name, Assert.Single(fixture.Engine.State.ArchivedResidents).Name);
        else
            Assert.Equal(original.Name, fixture.Resident.Name);
    }
}
