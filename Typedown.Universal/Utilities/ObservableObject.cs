using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Typedown.Universal.Utilities
{
    public class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private readonly Dictionary<string, object> container = new();

        public void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new(propertyName));
        }

        public T GetValue<T>(T defaultValue = default, [CallerMemberName] string propertyName = null)
        {
            return (T)(container.TryGetValue(propertyName, out var value) ? value : defaultValue);
        }

        public bool HasValue(string propertyName = null)
        {
            return container.ContainsKey(propertyName);
        }

        public void SetValue<T>(T value = default, Action action = null, [CallerMemberName] string propertyName = null)
        {
            if (!container.TryGetValue(propertyName, out var oldValue) || !oldValue.Equals(value))
            {
                container[propertyName] = value;
                RaisePropertyChanged(propertyName);
                action?.Invoke();
            }
        }
    }
}
