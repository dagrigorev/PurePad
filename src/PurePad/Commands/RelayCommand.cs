namespace PurePad.Commands;

/// <summary>
/// A reusable <see cref="IApplicationCommand"/> that delegates to supplied callbacks.
/// This spares the app a bespoke class per menu action while still giving every action a
/// first-class command object with its own availability rule.
/// </summary>
public sealed class RelayCommand : IApplicationCommand
{
    private readonly Action _execute;
    private readonly Func<bool> _canExecute;

    /// <param name="execute">The action to perform.</param>
    /// <param name="canExecute">Availability predicate; defaults to always-available.</param>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute ?? (static () => true);
    }

    public bool CanExecute() => _canExecute();

    public void Execute()
    {
        if (_canExecute())
        {
            _execute();
        }
    }
}
