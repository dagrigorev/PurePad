namespace PurePad.Commands;

/// <summary>
/// A user-triggerable action (Command pattern). Menu items are bound to commands rather
/// than to event handlers, which lets the same action be reused and lets the menu reflect
/// availability through <see cref="CanExecute"/> (e.g. greying out Undo or Cut).
/// </summary>
public interface IApplicationCommand
{
    /// <summary>Whether the command can run right now; drives menu-item enabled state.</summary>
    bool CanExecute();

    /// <summary>Perform the action.</summary>
    void Execute();
}
