using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Linq;

namespace Typedown.Universal.Utilities
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
    }
}
