using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows.Input;

namespace Typedown.Universal.Utilities
{
    public class Command<T> : ICommand
    {
        public event EventHandler CanExecuteChanged;

        private readonly Subject<T> executeSubject = new();

        private readonly BehaviorSubject<bool> canExecuteSubject;

        public Command(bool canExecute = true)
        {
            canExecuteSubject = new(canExecute);
            canExecuteSubject.DistinctUntilChanged().Subscribe(_ => CanExecuteChanged?.Invoke(this, EventArgs.Empty));
        }

        public void Execute(object parameter)
        {
            if (canExecuteSubject.Value)
                executeSubject.OnNext((T)parameter);
        }

        public bool CanExecute(object parameter = null) => canExecuteSubject.Value;

        public IObservable<T> OnExecute => executeSubject.AsObservable();

        public IObserver<bool> SetCanExecute => canExecuteSubject.AsObserver();
    }
}
