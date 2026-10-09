using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core.Tests;

/// <summary>居民实体读取与集合提交的独立边界。</summary>
public sealed class ResidentSnapshotTests
{
    /// <summary>读取当前身体只推导快照，未修改的读取复用结果，集合在显式捕获时才提交。</summary>
    [Fact]
    public void Reading_a_resident_preserves_published_state_until_capture()
    {
        var initial = new Resident { Id = 1, Health = 100 };
        var published = ImmutableVector<Resident>.Create([initial]);
        var list = new EntityListCursor<Resident, ResidentCursor>(published, next => published = next,
            value => new ResidentCursor(value));
        var person = list[0];

        person.Health = 80;
        var middle = person.Value;
        Assert.Same(middle, person.Value);
        Assert.Equal(80, middle.Health);
        Assert.Same(initial, published[0]);
        Assert.Same(initial, ((StateReference<Resident>)person).Value);
        person.Health = 60;
        var current = person.Value;
        Assert.Equal(80, middle.Health);
        Assert.Equal(60, current.Health);
        Assert.Same(initial, published[0]);

        Assert.Same(current, list.CaptureSnapshot()[0]);
        Assert.Same(current, published[0]);
    }

    /// <summary>完整替换直接提交候选实体，不发布将被覆盖的身体草稿。</summary>
    [Fact]
    public void Replacing_a_resident_does_not_publish_intermediate_drafts()
    {
        var initial = new Resident { Id = 1, Health = 100 };
        var publications = new List<ImmutableVector<Resident>>();
        var list = new EntityListCursor<Resident, ResidentCursor>(ImmutableVector<Resident>.Create([initial]),
            publications.Add, value => new ResidentCursor(value));
        var person = list[0];
        person.Health = 80;
        var retained = person.Value;
        var replacement = initial with { Health = 60 };

        person.Replace(replacement);

        Assert.Same(replacement, Assert.Single(publications)[0]);
        Assert.Same(replacement, list.CaptureSnapshot()[0]);
        Assert.Single(publications);
        Assert.Equal(80, retained.Health);
    }

    /// <summary>日内转入归档的居民由新集合提交，旧集合冻结不能清掉新集合的待提交登记。</summary>
    [Fact]
    public void Rebinding_pending_residents_preserves_new_collection_updates()
    {
        var initial = new Resident { Id = 1, Health = 100 };
        var source = new EntityListCursor<Resident, ResidentCursor>(ImmutableVector<Resident>.Create([initial]),
            _ => { }, value => new ResidentCursor(value));
        var archive = new EntityListCursor<Resident, ResidentCursor>(ImmutableVector<Resident>.Create([]),
            _ => { }, value => new ResidentCursor(value));
        var person = source[0];
        using var sourceUpdates = source.BeginUpdates();
        using var archiveUpdates = archive.BeginUpdates();
        person.Health = 80;
        source.Remove(person);
        archive.Add(person);
        person.Health = 60;

        Assert.Empty(source.CaptureSnapshot());
        Assert.Equal(60, archive.CaptureSnapshot()[0].Health);
        Assert.Equal(100, initial.Health);
    }

    /// <summary>恢复原实体会丢弃尚未提交的身体草稿。</summary>
    [Fact]
    public void Replacing_with_the_committed_value_discards_pending_drafts()
    {
        var initial = new Resident { Id = 1, Health = 100 };
        var list = new EntityListCursor<Resident, ResidentCursor>(ImmutableVector<Resident>.Create([initial]),
            _ => { }, value => new ResidentCursor(value));
        var person = list[0];
        person.Health = 80;
        var retained = person.Value;

        person.Replace(initial);

        Assert.Same(initial, person.Value);
        Assert.Same(initial, list.CaptureSnapshot()[0]);
        Assert.Equal(80, retained.Health);
    }
}
