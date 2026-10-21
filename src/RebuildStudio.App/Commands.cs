using System.Windows.Input;

namespace RebuildStudio.App;

public sealed class AsyncRelayCommand : ICommand
{
    readonly Func<Task> _execute;
    readonly Func<bool>? _canExecute;
    bool _running;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => !_running && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        _running = true;
        CommandManager.InvalidateRequerySuggested();
        try { await _execute(); }
        catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message, "Erro inesperado"); }
        finally
        {
            _running = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
