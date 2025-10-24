using System;
using System.Windows.Input;

namespace Kiosk.Commands
{
    public interface IRelayCommand : ICommand
    {
        void RaiseCanExecuteChanged();
    }

    public interface IRelayCommand<T> : ICommand
    {
        void RaiseCanExecuteChanged();
    }

    public class RelayCommand : IRelayCommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter)
            => _canExecute == null || _canExecute();

        public void Execute(object? parameter)
            => _execute();

        public void RaiseCanExecuteChanged()
            => CommandManager.InvalidateRequerySuggested();
    }

    public class RelayCommand<T> : IRelayCommand<T>
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool>? _canExecute;

        public RelayCommand(Action<T> execute, Func<T, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter)
        {
            if (parameter is null)
            {
                var t = typeof(T);
                if (!t.IsValueType || Nullable.GetUnderlyingType(t) != null)
                    return _canExecute == null || _canExecute((T?)parameter!);
                return false; // 값형 non-nullable 인데 null이면 실행 불가
            }

            if (parameter is T typed)
                return _canExecute == null || _canExecute(typed);

            return false;
        }

        public void Execute(object? parameter)
        {
            if (parameter is null)
            {
                var t = typeof(T);
                if (!t.IsValueType || Nullable.GetUnderlyingType(t) != null)
                {
                    _execute((T?)parameter!);
                    return;
                }
            }
            else if (parameter is T typed)
            {
                _execute(typed);
            }
        }

        public void RaiseCanExecuteChanged()
            => CommandManager.InvalidateRequerySuggested();
    }
}