using Avalonia;
using Avalonia.Media;
using SeWZC.WorldBox.UI.ViewModels.Details;
using static SeWZC.WorldBox.UI.ViewModels.WorldPresentationModel;

namespace SeWZC.WorldBox.UI.ViewModels;

/// <summary>弹窗生命周期、反馈与提交期间的表单状态。</summary>
internal sealed class DialogHostViewModel : ViewModelBase
{
    private readonly GameSessionViewModel _session;
    private ModalViewModel? _content;
    private bool _visible;

    /// <summary>连接窗口使用的会话状态。</summary>
    /// <param name="session">当前游戏会话。</param>
    internal DialogHostViewModel(GameSessionViewModel session)
    {
        _session = session;
    }

    public bool IsVisible
    {
        get => _visible;
        internal set => Set(ref _visible, value);
    }

    public ModalViewModel? Content
    {
        get => _content;
        private set => Set(ref _content, value);
    }

    internal int Generation { get; private set; }

    internal void OpenModal(StackNode content)
    {
        _session.Edits.CancelPending();
        _session.Interaction.CancelMapPick(true);
        Generation++;
        var feedback = Paragraph("");
        feedback.Foreground = Brush.Parse("#E7BD87");
        VisualNode? header = null;
        if (content.Children.Count > 0)
        {
            header = content.Children[0];
            content.Children.RemoveAt(0);
        }

        var primary = content.Children.OfType<ButtonNode>().LastOrDefault(button =>
            button.AutomationId.EndsWith("-apply", StringComparison.Ordinal));
        if (primary is not null)
            content.Children.Remove(primary);
        Content = new ModalViewModel(header, content, primary, feedback, CloseModal);
        UpdateBounds();
        IsVisible = true;
        _session.Map.IsSimulationPaused = true;
        _session.RefreshUi();
    }

    internal void UpdateBounds()
    {
        if (Content is not { } dialog)
            return;
        var layout = _session.Layout;
        var available = Math.Max(180, layout.Height - (layout.IsCompact ? 64 : 28));
        dialog.Width = Math.Min(520, Math.Max(260, layout.Width - 28));
        dialog.MaxHeight = available;
        dialog.Height = dialog.HasPrimary ? Math.Min(layout.IsCompact ? 560 : 700, available * .9) : double.NaN;
        dialog.Padding = new Thickness(layout.IsCompact ? 8 : 12);
    }

    internal void CloseModal()
    {
        _session.Edits.CancelPending();
        _session.Interaction.CancelMapPick(true);
        Generation++;
        IsVisible = false;
        Content = null;
        _session.ResetTimeAnchor();
        _session.Map.IsSimulationPaused = _session.WorldTimeStopped;
        _session.RefreshUi();
        _session.RequestFocus();
    }

    internal void SetStatus(string text)
    {
        if (IsVisible && Content is { } dialog)
            dialog.Feedback.Text = text;
    }

    internal Action LockSubmissionInputs(bool modal)
    {
        var original = modal ? Content : null;
        if (original is not null)
            original.IsSubmitting = true;
        return () =>
        {
            if (original is not null)
                original.IsSubmitting = false;
        };
    }
}
