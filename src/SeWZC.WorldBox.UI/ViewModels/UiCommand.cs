using System.Windows.Input;

namespace SeWZC.WorldBox.UI.ViewModels;

internal sealed class UiCommand(Action<object?> execute, Func<bool>? available = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return available?.Invoke() ?? true;
    }

    public void Execute(object? parameter)
    {
        if (CanExecute(parameter))
            execute(parameter);
    }

    internal void Refresh()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
