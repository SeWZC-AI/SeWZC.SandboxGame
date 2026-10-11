using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.ViewModels;

namespace SeWZC.WorldBox.UI.Tests;

/// <summary>主界面状态的停表、观察和通知行为。</summary>
public sealed class MainViewModelTests
{
    /// <summary>关闭操作窗口后保留玩家原来的暂停选择。</summary>
    [Fact]
    public void Closing_modal_preserves_explicit_pause()
    {
        var model = CreateModel();
        model.IsPaused = true;
        model.RefreshTime(true, false, false, false, false, false);
        model.PauseCommand.Execute(null);
        Assert.Contains("窗口操作", model.TimeState);

        model.RefreshTime(false, false, false, false, false, false);

        Assert.True(model.IsPaused);
        Assert.Equal("已暂停", model.TimeState);
        Assert.Equal("继续", model.PauseLabel);
    }

    /// <summary>暂停时选择速度不推进世界，也不自动恢复运行。</summary>
    [Fact]
    public void Choosing_speed_while_paused_does_not_resume_world()
    {
        var model = CreateModel();
        var initial = model.Engine.State;
        model.PauseCommand.Execute(null);

        model.SpeedCommand.Execute("5");

        Assert.Equal(5, model.Speed);
        Assert.True(model.IsPaused);
        Assert.Equal("已暂停", model.TimeState);
        Assert.Equal(initial, model.Engine.State);
    }

    /// <summary>取点时隐藏选择并阻止导航，返回后原选择仍可见。</summary>
    [Fact]
    public void Map_picker_preserves_selection_without_allowing_navigation()
    {
        var model = CreateModel();
        model.SelectionKind = "tile";
        model.SelectedTile = (1, 1);
        model.RefreshSelection();
        string? request = null;
        model.ViewRequested += value => request = value;

        model.RefreshTime(false, true, false, false, false, false);
        model.NavigateCommand.Execute("history");
        Assert.False(model.SelectionVisible);
        Assert.Null(request);
        Assert.False(model.PauseCommand.CanExecute(null));

        model.RefreshTime(false, false, false, false, false, false);
        Assert.True(model.SelectionVisible);
        Assert.Equal((1, 1), model.SelectedTile);
    }

    /// <summary>浏览选择和详情只更新展示，不消耗世界时间或随机状态。</summary>
    [Fact]
    public void Observing_and_refreshing_preserves_world_snapshot()
    {
        var model = CreateModel();
        var initial = model.Engine.State;
        model.SelectionKind = "tile";
        model.SelectedTile = (3, 4);

        model.RefreshSelection();
        model.InspectorVisible = true;
        model.RefreshWorld();
        model.InspectorVisible = false;
        model.RefreshSelection();

        Assert.True(model.SelectionVisible);
        Assert.Contains("地格（3, 4）", model.SelectionDescription);
        Assert.Equal(initial, model.Engine.State);
    }

    /// <summary>收起的通知在周期刷新时保持收起，新事件仍显示。</summary>
    [Fact]
    public void Dismissed_event_stays_hidden_until_another_event_arrives()
    {
        var model = CreateModel();
        var group = new EventGroup([new WorldEvent { Id = 1, Message = "施工完成" }]);
        model.ShowEvent(group);
        model.DismissEventCommand.Execute(null);

        model.ShowEvent(group);
        Assert.False(model.HasEvent);

        model.ShowEvent(new EventGroup([new WorldEvent { Id = 2, Message = "居民到达" }]));
        Assert.True(model.HasEvent);
        Assert.Equal("居民到达", model.EventText);
    }

    /// <summary>换世界后不把旧通知的收起状态应用到复用编号的新事件。</summary>
    [Fact]
    public void Replacing_world_clears_presentation_bookmarks()
    {
        var model = CreateModel();
        var group = new EventGroup([new WorldEvent { Id = 1, Message = "新事件" }]);
        model.ShowEvent(group);
        model.DismissEventCommand.Execute(null);
        model.CanReturnObservation = true;

        model.ResetWorldPresentation();
        model.ShowEvent(group);

        Assert.False(model.CanReturnObservation);
        Assert.True(model.HasEvent);
    }

    private static MainViewModel CreateModel()
    {
        return new MainViewModel(WorldEngine.Create(42, 32, 32, false));
    }

    /// <summary>进食和昏迷会中断劳动，摘要显示实际行动而非保留的任务目标。</summary>
    [Theory]
    [InlineData(ResidentActivity.Eating, "吃随身口粮")]
    [InlineData(ResidentActivity.Unconscious, "强制昏迷")]
    [InlineData(ResidentActivity.Working, "当前劳作：正在前往")]
    public void Resident_brief_reports_current_action_without_changing_world(ResidentActivity activity, string expected)
    {
        var model = CreateModel();
        model.Engine.PaintTerrain(16, 16, TerrainType.Grass, 0);
        model.Engine.SpawnResidents(16, 16, RaceKind.Human, 1);
        var state = model.Engine.State;
        var resident = state.Residents.Single();
        model.Engine = WorldEngine.FromSnapshot(state with
        {
            Residents = state.Residents.SetItem(0,
                resident with
                {
                    Activity = activity,
                    Agent = resident.Agent with
                    {
                        Sleep = activity == ResidentActivity.Unconscious
                            ? 0
                            : ResidentNeedsRules.MaximumPercent,
                        Goal = new AgentGoal
                        {
                            Kind = AgentGoalKind.Work, TargetX = resident.X, TargetY = resident.Y,
                        },
                    },
                }),
        });
        var world = model.Engine.ExportJson();
        model.SelectionKind = "resident";
        model.SelectedResidentId = resident.Id;

        model.RefreshSelection();

        Assert.Contains(expected, model.SelectionDescription);
        Assert.DoesNotContain("当前任务：", model.SelectionDescription);
        Assert.Equal(world, model.Engine.ExportJson());
    }

    /// <summary>窄屏详情和工具隐藏通知，关闭后恢复未收起的原通知。</summary>
    [Fact]
    public void Compact_inspector_hides_notice_without_dismissing_it()
    {
        var model = CreateModel();
        model.ShowEvent(new EventGroup([new WorldEvent { Id = 1, Message = "施工完成" }]));
        model.IsCompactHud = true;

        model.InspectorVisible = true;
        Assert.True(model.HasEvent);
        Assert.False(model.EventVisible);

        model.InspectorVisible = false;
        Assert.True(model.EventVisible);

        model.ToolsVisible = true;
        Assert.False(model.EventVisible);
        model.IsCompactHud = false;
        Assert.True(model.EventVisible);
        model.IsCompactHud = true;
        model.ToolsVisible = false;
        Assert.True(model.EventVisible);
    }

    /// <summary>放置确认或表单选点使用提示区，结束后恢复原通知。</summary>
    [Fact]
    public void Placement_and_map_picker_hide_notice_without_dismissing_it()
    {
        var model = CreateModel();
        model.ShowEvent(new EventGroup([new WorldEvent { Id = 1, Message = "施工完成" }]));

        model.PlacementPending = true;
        Assert.True(model.HasEvent);
        Assert.False(model.EventVisible);
        model.PlacementPending = false;
        Assert.True(model.EventVisible);

        var changes = new List<string?>();
        model.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        model.RefreshTime(false, true, false, false, false, false);
        Assert.False(model.EventVisible);
        Assert.Contains(nameof(MainViewModel.EventVisible), changes);
        changes.Clear();
        model.RefreshTime(false, false, false, false, false, false);
        Assert.True(model.EventVisible);
        Assert.Contains(nameof(MainViewModel.EventVisible), changes);
    }

    /// <summary>短窗口保留操作空间，增加高度后仍能查看原通知。</summary>
    [Fact]
    public void Short_window_hides_notice_without_dismissing_it()
    {
        var model = CreateModel();
        model.ShowEvent(new EventGroup([new WorldEvent { Id = 1, Message = "施工完成" }]));
        model.SelectionKind = "tile";
        model.SelectedTile = (3, 4);
        model.RefreshSelection();
        model.IsShortHud = true;

        Assert.True(model.SelectionVisible);
        Assert.False(model.SelectionDescriptionVisible);
        Assert.Contains("地格（3, 4）", model.SelectionDescription);
        Assert.True(model.HasEvent);
        Assert.False(model.EventVisible);

        model.IsShortHud = false;
        Assert.True(model.SelectionDescriptionVisible);
        Assert.True(model.EventVisible);
        model.IsShortHud = true;
        model.SelectionKind = null;
        model.RefreshSelection();
        Assert.False(model.EventVisible);
        model.IsShortHud = false;
        Assert.True(model.EventVisible);
    }
}
