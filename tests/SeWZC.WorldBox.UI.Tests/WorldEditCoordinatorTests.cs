using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Services;
using SeWZC.WorldBox.UI.ViewModels;

namespace SeWZC.WorldBox.UI.Tests;

/// <summary>编辑服务的恢复点、失败和界面会话边界。</summary>
public sealed class WorldEditCoordinatorTests
{
    /// <summary>应用前校验失败时保留原世界和原来的运行选择。</summary>
    [Fact(DisplayName = "应用前校验失败时保留原世界和原来的运行选择")]
    public async Task Rejected_command_does_not_publish_a_checkpoint_or_pause()
    {
        var state = CreateState();
        var original = state.Engine.State;
        var coordinator = CreateCoordinator(state);
        string? feedback = null;
        coordinator.Feedback += text => feedback = text;

        var result = await coordinator.SubmitAsync(() => throw new InvalidOperationException("Invalid draft"));

        Assert.False(result);
        Assert.Equal(original, state.Engine.State);
        Assert.Null(coordinator.Checkpoint);
        Assert.False(state.IsPaused);
        Assert.StartsWith("未应用变更：", feedback);
        Assert.False(coordinator.CaptureActive);
    }

    /// <summary>复合命令在部分改变世界后失败，仍可恢复编辑前的快照。</summary>
    [Fact(DisplayName = "复合命令在部分改变世界后失败，仍可恢复编辑前的快照")]
    public async Task Partial_failure_preserves_original_world_for_undo()
    {
        var state = CreateState();
        var original = state.Engine.State;
        var coordinator = CreateCoordinator(state);
        string? feedback = null;
        coordinator.Feedback += text => feedback = text;

        var result = await coordinator.SubmitAsync(() =>
        {
            state.Engine.PaintTerrain(10, 10, TerrainType.Mountain, 0);
            throw new InvalidOperationException("Second change failed");
        });

        Assert.False(result);
        Assert.NotEqual(original, state.Engine.State);
        Assert.Equal(original, coordinator.Checkpoint);
        Assert.True(state.IsPaused);
        Assert.StartsWith("变更未完成，可撤销：", feedback);
        Assert.Equal(original, WorldEngine.FromSnapshot(coordinator.Checkpoint!.Value).State);
    }

    /// <summary>锁定输入期间关闭窗口、跳转详情或替换世界时，原命令不得提交。</summary>
    [Theory(DisplayName = "锁定输入期间关闭窗口、跳转详情或替换世界时，原命令不得提交")]
    [InlineData("close")]
    [InlineData("navigate")]
    [InlineData("world")]
    [InlineData("cancel")]
    public async Task Invalidated_session_restores_inputs_without_executing_command(string action)
    {
        var state = CreateState();
        var session = new EditSession(1, action != "navigate", 1, false);
        var storage = new WorldSaveCoordinator(() => state.Engine, () => null, () => 0);
        var coordinator = new WorldEditCoordinator(state, storage, () => session);
        var executed = false;
        var restored = false;
        var result = await coordinator.SubmitAsync(() => executed = true, lockInputs: _ =>
        {
            switch (action)
            {
                case "close":
                    session = session with { Generation = 2, Modal = false };
                    break;
                case "navigate":
                    session = session with { Navigation = 2 };
                    break;
                case "world":
                    state.Engine = WorldEngine.Create(43, 32, 32, false);
                    break;
                case "cancel":
                    coordinator.CancelPending();
                    break;
            }

            return () => restored = true;
        });

        Assert.False(result);
        Assert.False(executed);
        Assert.True(restored);
        Assert.Null(coordinator.Checkpoint);
        Assert.False(state.IsPaused);
        Assert.True(coordinator.CanSubmit);
    }

    /// <summary>地图工具在准备期间失效时不得留下恢复点或暂停选择。</summary>
    [Fact(DisplayName = "地图工具在准备期间失效时不得留下恢复点或暂停选择")]
    public async Task Invalidated_map_tool_cancels_capture()
    {
        var state = CreateState();
        var coordinator = CreateCoordinator(state);
        var current = true;
        coordinator.StateChanged += () => current = false;

        await Assert.ThrowsAsync<OperationCanceledException>(() => coordinator.PrepareMapEditAsync(() => current));

        Assert.Null(coordinator.Checkpoint);
        Assert.False(state.IsPaused);
        Assert.False(coordinator.CaptureActive);
    }

    private static MainViewModel CreateState()
    {
        return new MainViewModel(WorldEngine.Create(42, 32, 32, false));
    }

    private static WorldEditCoordinator CreateCoordinator(MainViewModel state)
    {
        return new WorldEditCoordinator(state,
            new WorldSaveCoordinator(() => state.Engine, () => null, () => 0),
            () => new EditSession(1, true, 1, false));
    }
}
