using System.Windows.Input;


/// <summary>
/// Async/await ile uyumlu ICommand uygulaması. Çalışma sırasında yeniden girişe izin vermez.
/// </summary>

namespace WpfPractise2
{
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _executeAsync;
        private readonly Func<bool>? _canExecute;
        private bool _isExecuting;

        public event EventHandler? CanExecuteChanged;

        public AsyncRelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
            => !_isExecuting && (_canExecute?.Invoke() ?? true);// Çalışırken yeniden çağrı engellenir
                                                                // Dış koşullar uygunsa çalıştırılabilir
        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter)) return;
            _isExecuting = true;
            RaiseCanExecuteChanged();
            try { await _executeAsync(); }
            finally { _isExecuting = false; RaiseCanExecuteChanged(); }
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
