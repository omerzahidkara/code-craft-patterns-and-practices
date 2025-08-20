using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

public sealed class MainViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    private int _count;
    public int Count
    {
        get => _count;
        private set
        {
            var clamped = Math.Max(Min, Math.Min(Max, value)); // sınır koruması
            if (_count == clamped) return;
            _count = clamped;
            OnPropertyChanged();
            IncrementCommand.RaiseCanExecuteChanged();
            DecrementCommand.RaiseCanExecuteChanged();
        }
    }

    public int Min { get; } = 0;
    public int Max { get; } = 10;

    public RelayCommand IncrementCommand { get; }
    public RelayCommand DecrementCommand { get; }

    public MainViewModel()
    {
        IncrementCommand = new RelayCommand(
            execute => Count = Math.Min(Count + 1, Max), // UI emniyet sınır koruması
            canExecute => Count < Max
        );
        DecrementCommand = new RelayCommand(
            execute => Count = Math.Max(Count - 1, Min),
            canExecute => Count > Min
        );

        Count = 0; // başlangıç
    }
}

public sealed class RelayCommand : ICommand
{
    private readonly Predicate<object> _canExecute;
    private readonly Action<object> _execute;
    public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute ?? (_ => true);
    }
    public bool CanExecute(object parameter) => _canExecute(parameter);
    public void Execute(object parameter) => _execute(parameter);
    public event EventHandler CanExecuteChanged;
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
