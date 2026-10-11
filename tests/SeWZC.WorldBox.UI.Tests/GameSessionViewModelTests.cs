using Avalonia;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;
using SeWZC.WorldBox.UI.ViewModels;
using SeWZC.WorldBox.UI.ViewModels.Details;

namespace SeWZC.WorldBox.UI.Tests;

/// <summary>各界面区域无需控件即可完成导航、取点与编辑协调。</summary>
public sealed class GameSessionViewModelTests
{
    /// <summary>跨详情返回恢复原视野、选择、折叠及滚动，不改变世界快照。</summary>
    [Fact]
    public void Back_restores_observation_and_detail_state_after_layout()
    {
        var (session, map) = CreateSession();
        var original = session.State.Engine.State;
        var pending = new List<Action>();
        session.LayoutRestorationRequested += pending.Add;
        session.Navigation.OpenTile(10, 10);
        session.Navigation.ScrollOffset = new Vector(0, 140);
        session.State.InspectorExpanded = true;
        session.Inspector._expandedDetails["sample"] = true;
        map.Viewport = new WorldMapControl.ViewportState(new Point(92, 70), 2);
        var selection = map.CaptureMapSelection();
        var viewport = map.Viewport;
        session.Navigation.OpenInspector("history");
        session.Navigation.ScrollOffset = new Vector(0, 40);
        session.Inspector._expandedDetails["sample"] = false;
        map.Viewport = new WorldMapControl.ViewportState(new Point(20, 40), .5);

        session.Navigation.GoBack();

        Assert.Equal("tile", session.State.InspectorMode);
        Assert.Equal(selection, map.CaptureMapSelection());
        Assert.Equal(viewport, map.Viewport);
        Assert.True(session.State.InspectorExpanded);
        Assert.True(session.Inspector._expandedDetails["sample"]);
        Assert.Equal(default, session.Navigation.ScrollOffset);
        Assert.Single(pending)();
        Assert.Equal(new Vector(0, 140), session.Navigation.ScrollOffset);
        Assert.Equal(original, session.State.Engine.State);
    }

    /// <summary>布局等待期间又跳转时，旧返回动作不会覆盖新详情的滚动位置。</summary>
    [Fact]
    public void Later_navigation_invalidates_queued_scroll_restoration()
    {
        var (session, _) = CreateSession();
        var pending = new List<Action>();
        session.LayoutRestorationRequested += pending.Add;
        session.Navigation.OpenTile(10, 10);
        session.Navigation.ScrollOffset = new Vector(0, 140);
        session.Navigation.OpenInspector("history");
        session.Navigation.GoBack();
        session.Navigation.OpenInspector("guide");
        session.Navigation.ScrollOffset = new Vector(0, 25);
        Assert.Single(pending)();
        Assert.Equal(new Vector(0, 25), session.Navigation.ScrollOffset);
        Assert.Equal("guide", session.State.InspectorMode);
    }

    /// <summary>连续定位共用最初的观察位置，返回保留原选中对象。</summary>
    [Fact]
    public void Repeated_location_returns_to_original_viewport_and_selection()
    {
        var (session, map) = CreateSession();
        map.SelectMapTile(3, 4);
        session.Navigation.SelectMapObject("tile", x: 3, y: 4);
        var originalView = map.Viewport;
        var originalSelection = map.CaptureMapSelection();
        var originalWorld = session.State.Engine.State;
        Assert.False(session.Navigation.FocusMapResident(999));
        Assert.False(session.State.CanReturnObservation);
        session.Navigation.FocusMapTile(10, 10);
        session.Navigation.FocusMapTile(20, 20);
        session.Navigation.ReturnToObservation();
        Assert.Equal(originalView, map.Viewport);
        Assert.Equal(originalSelection, map.CaptureMapSelection());
        Assert.Equal((3, 4), session.State.SelectedTile);
        Assert.False(session.State.CanReturnObservation);
        Assert.Equal(originalWorld, session.State.Engine.State);
    }

    /// <summary>地图取点保留原表单草稿，返回不改坐标，点选只更新草稿。</summary>
    [Fact]
    public void Map_pick_restores_original_draft_and_pause_preference()
    {
        var (session, map) = CreateSession();
        session.State.IsPaused = false;
        session.Tools.SetCategory(ToolCategory.Life);
        session.Tools.SelectTool(MapTool.ForResidents(RaceKind.Elf));
        var tool = map.ActiveTool;
        var x = new NumberNode { Value = 8 };
        var y = new NumberNode { Value = 9 };
        var draft = new TextInputNode { Text = "未提交名称" };
        var content = Form();
        content.Children.Add(draft);
        session.Dialogs.OpenModal(content);
        var originalDialog = session.Dialogs.Content;
        var originalWorld = session.State.Engine.State;
        session.Interaction.StartMapPick(x, y, null);
        Assert.True(session.WorldTimeStopped);
        Assert.False(session.Dialogs.IsVisible);
        session.Interaction.ReturnToFormCommand.Execute(null);
        Assert.Same(originalDialog, session.Dialogs.Content);
        Assert.Equal("未提交名称", draft.Text);
        Assert.Equal(8m, x.Value);
        Assert.Equal(9m, y.Value);
        Assert.Equal(tool, map.ActiveTool);
        Assert.True(session.State.ToolsVisible);
        session.Interaction.StartMapPick(x, y, null);
        map.PickTile(12, 13);
        Assert.Equal(12m, x.Value);
        Assert.Equal(13m, y.Value);
        session.Dialogs.CloseModal();
        Assert.False(session.WorldTimeStopped);
        Assert.False(session.State.IsPaused);
        Assert.Equal(originalWorld, session.State.Engine.State);
    }

    /// <summary>提交准备过程中关闭并打开另一窗口时，取消原提交且只恢复原窗口锁定。</summary>
    [Fact]
    public async Task Closing_during_submission_keeps_new_form_independent()
    {
        var (session, _) = CreateSession();
        session.State.IsPaused = false;
        var disabled = new TextInputNode { IsEnabled = false };
        var form = Form();
        form.Children.Add(disabled);
        session.Dialogs.OpenModal(form);
        var originalDialog = session.Dialogs.Content!;
        var originalWorld = session.State.Engine.State;
        var observedLock = false;
        session.Edits.StateChanged += () =>
        {
            if (!originalDialog.IsSubmitting)
                return;
            observedLock = true;
            Assert.False(disabled.IsEnabled);
            session.Dialogs.CloseModal();
            session.Dialogs.OpenModal(Form());
        };
        var applied = false;
        var result = await session.SubmitEditAsync(() => applied = true);
        Assert.True(observedLock);
        Assert.False(result);
        Assert.False(applied);
        Assert.False(originalDialog.IsSubmitting);
        Assert.NotSame(originalDialog, session.Dialogs.Content);
        Assert.False(session.Dialogs.Content!.IsSubmitting);
        Assert.False(disabled.IsEnabled);
        Assert.False(session.State.IsPaused);
        Assert.Equal(originalWorld, session.State.Engine.State);
    }

    /// <summary>分类切换重置笔刷和分页，空槽不可执行，分页不会推进世界。</summary>
    [Fact]
    public void Tool_category_and_page_apply_their_own_defaults()
    {
        var (session, map) = CreateSession();
        var originalWorld = session.State.Engine.State;
        session.Tools.Categories[1].Command!.Execute(null);
        Assert.Equal((ToolCategory.Life, 1), map.Brush);
        Assert.False(session.Tools.Slots[4].IsEnabled);
        Assert.False(session.Tools.Slots[4].SelectCommand.CanExecute(null));
        session.Tools.Slots[0].SelectCommand.Execute(null);
        Assert.Equal(MapTool.ForResidents(RaceKind.Human), map.ActiveTool);
        session.Tools.BrushIndex = 2;
        session.Tools.Categories[0].Command!.Execute(null);
        session.Tools.NextCommand.Execute(null);
        Assert.Equal((ToolCategory.Terrain, 0), map.Brush);
        Assert.Equal("第 2 / 3 页", session.Tools.PageLabel);
        session.Tools.Categories[2].Command!.Execute(null);
        Assert.Equal("第 1 / 1 页", session.Tools.PageLabel);
        Assert.Equal((ToolCategory.Disaster, 0), map.Brush);
        Assert.Equal(originalWorld, session.State.Engine.State);
    }

    private static (GameSessionViewModel Session, GameMapStub Map) CreateSession()
    {
        var map = new GameMapStub();
        var state = new MainViewModel(WorldEngine.Create(42, 32, 32, false)) { IsPaused = true };
        return (new GameSessionViewModel(state, map, () => null), map);
    }

    private static StackNode Form()
    {
        var form = new StackNode();
        form.Children.Add(new TextNode { Text = "测试表单" });
        form.Children.Add(new ButtonNode { AutomationId = "test-apply" });
        return form;
    }
}
