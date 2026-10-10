using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core.Tests;

/// <summary>居民纯转换与实体集合快照的独立边界。</summary>
public sealed class ResidentSnapshotTests
{
    /// <summary>候选转换不改变引用，显式替换后读取不再触发其他提交。</summary>
    [Fact]
    public void Reading_a_resident_does_not_commit_changes()
    {
        var initial = new Resident { Id = 1, Health = 100 };
        var list = new EntityStore<Resident>(ImmutableVector<Resident>.Create([initial]));
        var changes = 0;
        list.Changed = (_, _, _) => changes++;
        var person = list[0];
        var candidate = person.Value.WithHealth(80);
        Assert.Same(initial, person.Value);
        Assert.Equal(0, changes);

        person.Replace(candidate);
        Assert.Same(candidate, person.Value);
        Assert.Same(candidate, person.Value);
        Assert.Equal(1, changes);
        Assert.Equal(100, initial.Health);
    }

    /// <summary>批次捕获后的快照不受后续纯转换与完整替换影响。</summary>
    [Fact]
    public void Replacing_a_resident_preserves_captured_values()
    {
        var initial = new Resident { Id = 1, Health = 100 };
        var list = new EntityStore<Resident>(ImmutableVector<Resident>.Create([initial]));
        using var updates = list.BeginUpdates();
        var person = list[0];
        person.Replace(person.Value.WithHealth(80));
        var retained = list.CaptureSnapshot();
        var replacement = initial with { Health = 60 };

        person.Replace(replacement);

        Assert.Same(replacement, list.CaptureSnapshot()[0]);
        Assert.Equal(80, retained[0].Health);
        Assert.Equal(100, initial.Health);
    }

    /// <summary>居民转入归档后，新集合提交后续转换，原集合快照保持独立。</summary>
    [Fact]
    public void Rebinding_residents_preserves_new_collection_updates()
    {
        var initial = new Resident { Id = 1, Health = 100 };
        var source = new EntityStore<Resident>(ImmutableVector<Resident>.Create([initial]));
        var archive = new EntityStore<Resident>(ImmutableVector<Resident>.Create([]));
        var person = source[0];
        using var sourceUpdates = source.BeginUpdates();
        using var archiveUpdates = archive.BeginUpdates();
        person.Replace(person.Value.WithHealth(80));
        source.Remove(person);
        archive.Add(person);
        person.Replace(person.Value.WithHealth(60));

        Assert.Empty(source.CaptureSnapshot());
        Assert.Equal(60, archive.CaptureSnapshot()[0].Health);
        Assert.Equal(100, initial.Health);
    }

    /// <summary>恢复原实体时，之前取得的候选仍保留其值。</summary>
    [Fact]
    public void Replacing_with_the_original_value_preserves_previous_candidates()
    {
        var initial = new Resident { Id = 1, Health = 100 };
        var list = new EntityStore<Resident>(ImmutableVector<Resident>.Create([initial]));
        var person = list[0];
        person.Replace(person.Value.WithHealth(80));
        var retained = person.Value;

        person.Replace(initial);

        Assert.Same(initial, person.Value);
        Assert.Same(initial, list.CaptureSnapshot()[0]);
        Assert.Equal(80, retained.Health);
    }
}
