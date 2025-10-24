using System.Windows.Input;

namespace Kiosk.Commands
{
    public interface IAsyncCommand : ICommand
    {
        Task ExecuteAsync(object? parameter = null);
        void NotifyCanExecuteChanged();
    }

    public sealed class AsyncRelayCommand : IAsyncCommand
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool>? _canExecute;
        private bool _running;

        public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? p) => !_running && (_canExecute?.Invoke() ?? true);
        public async void Execute(object? p) => await ExecuteAsync();

        public async Task ExecuteAsync(object? p = null)
        {
            if (!CanExecute(p)) return;
            try
            {
                _running = true;
                NotifyCanExecuteChanged();
                await _execute();
            }
            finally
            {
                _running = false;
                NotifyCanExecuteChanged();
            }
        }

        public event EventHandler? CanExecuteChanged;
        public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public sealed class AsyncRelayCommand<T> : IAsyncCommand
    {
        private readonly Func<T?, Task> _execute;
        private readonly Func<T?, bool>? _canExecute;
        private bool _running;

        public AsyncRelayCommand(Func<T?, Task> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? p) => !_running && (_canExecute?.Invoke((T?)p) ?? true);
        public async void Execute(object? p) => await ExecuteAsync(p);

        public async Task ExecuteAsync(object? p = null)
        {
            if (!CanExecute(p)) return;
            try
            {
                _running = true;
                NotifyCanExecuteChanged();
                await _execute((T?)p);
            }
            finally
            {
                _running = false;
                NotifyCanExecuteChanged();
            }
        }

        public event EventHandler? CanExecuteChanged;
        public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}