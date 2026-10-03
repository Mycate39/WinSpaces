using System.Windows.Input;

namespace WinSpaces.ViewModels;

/// <summary>Commande simple (avec paramètre optionnel) pour les vues WPF.</summary>
public sealed class DelegateCommand : ICommand
{
    private readonly Action<object?> _execute;

    public DelegateCommand(Action execute) : this(_ => execute()) { }

    public DelegateCommand(Action<object?> execute)
        => _execute = execute ?? throw new ArgumentNullException(nameof(execute));

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _execute(parameter);
}
