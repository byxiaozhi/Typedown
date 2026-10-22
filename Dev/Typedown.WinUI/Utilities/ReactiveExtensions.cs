using System;
using System.ComponentModel;
using System.Reactive;
using System.Reactive.Linq;

namespace Typedown.WinUI.Utilities
{
    // Trimmed port of Typedown.Core\Utilities\ReactiveExtensions.cs — only the two members
    // SettingsViewModel actually needs. SubscribeWeak, GetCollectionObservable, DisposeOnUnloaded,
    // and the DependencyObject-based BindTwoWay helper are UWP-XAML-coupled and deferred to
    // milestone #3+ when real XAML controls come back.
    public static class ReactiveExtensions
    {
        public static IObservable<EventPattern<PropertyChangedEventArgs>> GetPropertyObservable(this INotifyPropertyChanged obj)
        {
            return Observable.FromEventPattern<PropertyChangedEventArgs>(obj, nameof(obj.PropertyChanged));
        }

        public static IObservable<object> WhenPropertyChanged<T>(this T source, string propertyName) where T : INotifyPropertyChanged
        {
            var property = source.GetType().GetProperty(propertyName);
            return source.GetPropertyObservable().Where(x => x.EventArgs.PropertyName == propertyName).Select(_ => property.GetValue(source));
        }
    }
}
