using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;

namespace SeWZC.WorldBox.UI.Tests;

/// <summary>地图分区查询在移动、编辑和换世界后的可见对象检查。</summary>
public sealed class MapResidentIndexTests
{
    /// <summary>跨分区的移动区段在起点、中间和终点均可查询，且不重复返回居民。</summary>
    [Theory]
    [InlineData(14, 15)]
    [InlineData(16, 16)]
    [InlineData(17, 19)]
    [InlineData(14, 19)]
    public void Moving_resident_is_found_along_the_segment(int left, int right)
    {
        var index = new MapResidentIndex();
        var resident = new Resident
        {
            Id = 1,
            FromX = 14,
            X = 19,
            FromY = 8,
            Y = 8,
        };
        var results = new List<Resident>();

        index.Query([resident], left, 8, right, 8, results);

        Assert.Same(resident, Assert.Single(results));
    }

    /// <summary>只返回与查询区域相交的居民，不包含同一分区内的远处对象。</summary>
    [Fact]
    public void Query_excludes_residents_outside_the_viewport()
    {
        var resident = new Resident
        {
            Id = 1,
            FromX = 4,
            X = 4,
            FromY = 4,
            Y = 4,
        };
        var results = new List<Resident>();

        new MapResidentIndex().Query([resident], 5, 5, 6, 6, results);

        Assert.Empty(results);
    }

    /// <summary>同日编辑产生新快照时立即替换旧的地点和对象引用。</summary>
    [Fact]
    public void New_snapshot_replaces_cached_positions()
    {
        var index = new MapResidentIndex();
        var resident = new Resident
        {
            Id = 1,
            FromX = 4,
            X = 4,
            FromY = 4,
            Y = 4,
        };
        var moved = resident with { FromX = 32, X = 32, FromY = 32, Y = 32 };
        var results = new List<Resident>();
        index.Query([resident], 4, 4, 4, 4, results);

        index.Query([moved], 4, 4, 4, 4, results);
        Assert.Empty(results);
        index.Query([moved], 32, 32, 32, 32, results);
        Assert.Same(moved, Assert.Single(results));
    }

    /// <summary>清理后不会将旧世界居民带入新世界。</summary>
    [Fact]
    public void Clearing_index_removes_previous_world()
    {
        var index = new MapResidentIndex();
        var results = new List<Resident>();
        index.Query([new Resident { Id = 1 }], 0, 0, 1, 1, results);

        index.Clear();
        index.Query([], 0, 0, 1, 1, results);

        Assert.Empty(results);
    }
}
