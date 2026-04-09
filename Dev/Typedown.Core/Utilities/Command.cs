using System;
using System.Collections.ObjectModel;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Metadata;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using System.ComponentModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows.Input;

namespace Typedown.Core.Utilities
{
    public class Command<T> : ICommand, INotifyPropertyChanged
    {
        public event EventHandler? CanExecuteChanged;

        public event PropertyChangedEventHandler? PropertyChanged;

        private readonly Subject<T> executeSubject = new();

        private readonly BehaviorSubject<Func<object?, bool>> canExecuteSubject;

        public Command(bool canExecute = true)
        {
            canExecuteSubject = new(_ => canExecute);
            canExecuteSubject.DistinctUntilChanged().Subscribe(_ => CanExecuteChanged?.Invoke(this, EventArgs.Empty));
            CanExecuteChanged += (s, e) => PropertyChanged?.Invoke(this, new(nameof(IsExecutable)));
        }

        public void Execute(object? parameter)
        {
            if (canExecuteSubject.Value(parameter))
            {
                if (parameter == null)
                    executeSubject.OnNext(default!);
                else if (parameter is T typedParam)
                    executeSubject.OnNext(typedParam);
                else
                    executeSubject.OnNext((T)Convert.ChangeType(parameter, typeof(T)));
            }
        }

        public bool CanExecute(object? parameter) => canExecuteSubject.Value(parameter);

        public bool IsExecutable { get => CanExecute(null); set => SetCanExecute.OnNext(value); }

        public IObservable<T> OnExecute => executeSubject.AsObservable();

        public IObserver<bool> SetCanExecute => Observer.Create<bool>(b => canExecuteSubject.OnNext(_ => b));

        public IObserver<Func<object?, bool>> SetCanExecuteFunc => canExecuteSubject.AsObserver();
    }
}
