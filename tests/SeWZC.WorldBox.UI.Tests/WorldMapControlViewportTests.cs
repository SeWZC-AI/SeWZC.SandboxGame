using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;

namespace SeWZC.WorldBox.UI.Tests;

/// <summary>地图观察视野的恢复与模拟隔离。</summary>
public sealed class WorldMapControlViewportTests
{
    /// <summary>定位改变镜头，返回时同时恢复原中心和缩放。</summary>
    [Fact(DisplayName = "定位改变镜头，返回时同时恢复原中心和缩放")]
    public void Restoring_viewport_returns_to_the_previous_observation()
    {
        var map = new WorldMapControl { Engine = WorldEngine.Create(42, 32, 32, false) };
        var initial = map.CaptureViewport();
        map.FocusTile(16, 16);
        Assert.NotEqual(initial, map.CaptureViewport());

        map.RestoreViewport(initial);

        Assert.Equal(initial, map.CaptureViewport());
    }

    /// <summary>镜头恢复不改变当前选择或模拟快照。</summary>
    [Fact(DisplayName = "镜头恢复不改变当前选择或模拟快照")]
    public void Restoring_camera_does_not_change_selection_or_world()
    {
        var engine = WorldEngine.Create(42, 32, 32, false);
        var map = new WorldMapControl { Engine = engine };
        var world = engine.State;
        var initial = map.CaptureViewport();
        map.SelectMapTile(3, 4);
        var selection = map.CaptureMapSelection();
        map.ZoomIn();

        map.RestoreViewport(initial);

        Assert.Equal(selection, map.CaptureMapSelection());
        Assert.Equal(world, engine.State);
    }

    /// <summary>定位后更换建设归属，返回观察时保持当前工具实际使用的归属。</summary>
    [Fact(DisplayName = "定位后更换建设归属，返回观察时保持当前工具实际使用的归属")]
    public void Returning_to_observation_preserves_current_construction_context()
    {
        var map = new WorldMapControl { Engine = WorldEngine.Create(42, 32, 32, false) };
        map.SelectMapTile(3, 4);
        var observation = map.CaptureMapSelection();
        map.FocusTile(16, 16);
        map.SelectMapTile(16, 16);
        map.SelectedNationId = 7;
        map.SelectedSettlementId = 9;

        map.RestoreObservationSelection(observation);

        var restored = map.CaptureMapSelection();
        Assert.Equal(observation.Tile, restored.Tile);
        Assert.Equal(7, restored.NationId);
        Assert.Equal(9, restored.SettlementId);
        Assert.Equal(MapTool.Inspect, map.ActiveTool);
    }

    /// <summary>表单地图选点取消仍可恢复完整的工具归属。</summary>
    [Fact(DisplayName = "表单地图选点取消仍可恢复完整的工具归属")]
    public void Restoring_full_map_selection_restores_tool_context()
    {
        var map = new WorldMapControl { Engine = WorldEngine.Create(42, 32, 32, false) };
        map.SelectedNationId = 7;
        map.SelectedSettlementId = 9;
        map.SelectMapTile(3, 4);
        var selection = map.CaptureMapSelection();
        map.SelectedNationId = 0;
        map.SelectedSettlementId = 0;

        map.RestoreMapSelection(selection);

        Assert.Equal(selection, map.CaptureMapSelection());
    }

    /// <summary>定位地格只移动镜头，地图高亮仍与原对象摘要对应。</summary>
    [Fact(DisplayName = "定位地格只移动镜头，地图高亮仍与原对象摘要对应")]
    public void Focusing_location_preserves_selection_and_tool_context()
    {
        var engine = WorldEngine.Create(42, 32, 32, false);
        var map = new WorldMapControl { Engine = engine };
        map.SelectMapTile(3, 4);
        map.SelectedNationId = 7;
        map.SelectedSettlementId = 9;
        var selection = map.CaptureMapSelection();
        var world = engine.State;

        map.FocusTile(16, 16);

        Assert.Equal(selection, map.CaptureMapSelection());
        Assert.Equal(world, engine.State);
    }

    /// <summary>归档居民没有地图轨迹，定位失败时保留原镜头、选择和世界。</summary>
    [Fact(DisplayName = "归档居民没有地图轨迹，定位失败时保留原镜头、选择和世界")]
    public void Focusing_archived_resident_reports_failure_without_changing_observation()
    {
        var engine = WorldEngine.Create(42, 32, 32, false);
        engine.PaintTerrain(16, 16, TerrainType.Grass, 0);
        engine.SpawnResidents(16, 16, RaceKind.Human, 1);
        var state = engine.State;
        var resident = state.Residents.Single();
        engine = WorldEngine.FromSnapshot(state with
        {
            Residents = state.Residents.RemoveAt(0),
            ArchivedResidents = state.ArchivedResidents.Add(resident with { Health = 0 }),
        });
        var map = new WorldMapControl { Engine = engine };
        map.SelectMapTile(3, 4);
        var viewport = map.CaptureViewport();
        var selection = map.CaptureMapSelection();
        var world = engine.ExportJson();

        Assert.False(map.FocusResident(resident.Id));

        Assert.Equal(viewport, map.CaptureViewport());
        Assert.Equal(selection, map.CaptureMapSelection());
        Assert.Equal(world, engine.ExportJson());
    }

    /// <summary>定位在地图上的居民同步选择目标，不把其他居民的跟随状态带到目标。</summary>
    [Fact(DisplayName = "定位在地图上的居民同步选择目标，不把其他居民的跟随状态带到目标")]
    public void Focusing_another_resident_selects_target_without_inheriting_follow()
    {
        var engine = WorldEngine.Create(42, 32, 32, false);
        engine.PaintTerrain(16, 16, TerrainType.Grass, 0);
        engine.SpawnResidents(16, 16, RaceKind.Human, 2);
        var map = new WorldMapControl { Engine = engine };
        map.SelectResident(engine.State.Residents[0].Id, true);
        var target = engine.State.Residents[1].Id;
        var world = engine.ExportJson();

        Assert.True(map.FocusResident(target));

        Assert.Equal(target, map.SelectedResidentId);
        Assert.False(map.FollowSelectedResident);
        Assert.Equal(world, engine.ExportJson());
    }
}
