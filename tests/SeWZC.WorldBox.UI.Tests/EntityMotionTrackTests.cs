using Avalonia;
using SeWZC.WorldBox.UI.Controls;

namespace SeWZC.WorldBox.UI.Tests;

/// <summary>实体显示轨迹的插值与立即定位检查。</summary>
public sealed class EntityMotionTrackTests
{
    /// <summary>未提交移动时保持初始位置。</summary>
    [Fact]
    public void New_track_stays_at_its_initial_position()
    {
        var track = new EntityMotionTrack(new Point(2, 3));

        Assert.Equal(new Point(2, 3), track.Position(100));
        Assert.False(track.IsMoving(100));
    }

    /// <summary>插值受起止位置约束。</summary>
    [Theory]
    [InlineData(8, 2, 4)]
    [InlineData(10, 2, 4)]
    [InlineData(11, 4, 5)]
    [InlineData(12, 6, 6)]
    [InlineData(14, 10, 8)]
    [InlineData(20, 10, 8)]
    public void Position_interpolates_and_clamps_to_the_segment(double time, double x, double y)
    {
        var track = new EntityMotionTrack(new Point(2, 4));
        track.Update(new Point(2, 4), new Point(10, 8), 10, 4, false);

        Assert.Equal(new Point(x, y), track.Position(time));
    }

    /// <summary>抵达终点时结束移动。</summary>
    [Theory]
    [InlineData(13.999, true)]
    [InlineData(14, false)]
    [InlineData(15, false)]
    public void IsMoving_stops_at_the_end_of_the_segment(double time, bool expected)
    {
        var track = new EntityMotionTrack(default);
        track.Update(default, new Point(4, 0), 10, 4, false);

        Assert.Equal(expected, track.IsMoving(time));
    }

    /// <summary>重复快照不重置正在显示的轨迹。</summary>
    [Fact]
    public void Repeated_snapshot_does_not_restart_the_segment()
    {
        var track = new EntityMotionTrack(default);
        track.Update(default, new Point(8, 0), 10, 4, false);

        track.Update(new Point(99, 99), new Point(8, 0), 10, 40, false);

        Assert.Equal(new Point(4, 0), track.Position(12));
    }

    /// <summary>立即定位覆盖同一快照的插值。</summary>
    [Fact]
    public void Snap_immediately_shows_the_target()
    {
        var track = new EntityMotionTrack(default);
        track.Update(default, new Point(8, 0), 10, 4, false);

        track.Update(default, new Point(8, 0), 10, 4, true);

        Assert.Equal(new Point(8, 0), track.Position(10));
        Assert.False(track.IsMoving(10));
    }

    /// <summary>无效或零日数按一日显示。</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Nonpositive_duration_uses_one_tick(int duration)
    {
        var track = new EntityMotionTrack(default);
        track.Update(default, new Point(8, 0), 10, duration, false);

        Assert.Equal(new Point(4, 0), track.Position(10.5));
        Assert.False(track.IsMoving(11));
    }

    /// <summary>后续移动即使终点相同，也采用新的区段。</summary>
    [Fact]
    public void New_start_tick_replaces_the_previous_segment()
    {
        var track = new EntityMotionTrack(default);
        track.Update(default, new Point(8, 0), 10, 4, false);

        track.Update(new Point(4, 0), new Point(8, 0), 14, 4, false);

        Assert.Equal(new Point(6, 0), track.Position(16));
    }
}
