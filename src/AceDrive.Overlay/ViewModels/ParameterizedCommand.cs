using System.Windows.Input;

namespace AceDrive.Overlay.ViewModels;

/// <summary>RelayCommand variant carrying a typed parameter (e.g. which brand was picked).</summary>
public sealed class ParameterizedCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Predicate<T?>? _canExecute;

    public ParameterizedCommand(Action<T?> execute, Predicate<T?>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) =>
        parameter is null ? _canExecute?.Invoke(default) ?? true
        : parameter is T value && (_canExecute?.Invoke(value) ?? true);

    public void Execute(object? parameter) => _execute((T?)parameter);

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
