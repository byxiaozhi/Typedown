using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace Typedown.Core.Utilities
{
    public static class ReactiveExtensions
    {
        public static IDisposable SubscribeWeak<T>(this IObservable<T> observable, Action<T> onNext)
        {
            var targetRef = new WeakReference(onNext.Target);
            var method = onNext.Method;
            var param = Expression.Parameter(typeof(T));
            IDisposable d = null;
            d = observable.Subscribe(x =>
            {
                if (targetRef.Target is object target)
                {
                    var body = Expression.Call(Expression.Constant(target), method, param);
                    var func = Expression.Lambda<Action<T>>(body, param).Compile();
                    func(x);
                }
                else
                {
                    d.Dispose();
                }
            });
            return d;
        }

        public static IObservable<EventPattern<NotifyCollectionChangedEventArgs>> GetCollectionObservable(this INotifyCollectionChanged collection)
        {
            return Observable.FromEventPattern<NotifyCollectionChangedEventArgs>(collection, nameof(collection.CollectionChanged));
        }

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
