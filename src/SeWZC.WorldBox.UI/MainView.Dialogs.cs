using static SeWZC.WorldBox.UI.ViewModels.WorldPresentationModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SeWZC.WorldBox.UI;

public sealed partial class MainView
{
    private void OpenModal(ViewModels.Details.StackNode content)
    {
        CancelPendingEdit();
        CancelMapPick(true);
        _modalGeneration++;
        _modalFeedback = Paragraph("");
        _modalFeedback.Foreground = Brush.Parse("#E7BD87");
        ViewModels.Details.VisualNode? header = null;
        if (content.Children.Count > 0)
        {
            header = content.Children[0];
            content.Children.RemoveAt(0);
        }
        var primary = content.Children.OfType<ViewModels.Details.ButtonNode>().LastOrDefault(button =>
            button.AutomationId.EndsWith("-apply", StringComparison.Ordinal));
        if (primary is not null)
            content.Children.Remove(primary);
        _modal.Child = new Views.ModalDialog
        {
            DataContext = new ViewModels.ModalViewModel(header, content, primary, _modalFeedback, CloseModal),
        };
        _modalHasPrimary = primary is not null;
        UpdateModalBounds();
        _modal.IsVisible = true;
        _map.IsSimulationPaused = true;
        RefreshUi();
    }

    private void UpdateModalBounds()
    {
        if (_modal.Child is not Border dialog)
            return;
        var available = Math.Max(180, Bounds.Height - (_isCompact ? 64 : 28));
        dialog.Width = Math.Min(520, Math.Max(260, Bounds.Width - 28));
        dialog.MaxHeight = available;
        dialog.Height = _modalHasPrimary ? Math.Min(_isCompact ? 560 : 700, available * .9) : double.NaN;
        dialog.Padding = new Thickness(_isCompact ? 8 : 12);
    }

    private void CloseModal()
    {
        CancelPendingEdit();
        CancelMapPick(true);
        _modalGeneration++;
        _modal.IsVisible = false;
        _modal.Child = null;
        _modalFeedback = null;
        _previousTime = _clock.Elapsed.TotalSeconds;
        _map.IsSimulationPaused = WorldTimeStopped;
        RefreshUi();
        Focus();
    }

    private void SetStatus(string text)
    {
        _viewModel.Feedback = text;
        if (_modal.IsVisible && _modalFeedback is not null)
            _modalFeedback.Text = text;
    }
}
