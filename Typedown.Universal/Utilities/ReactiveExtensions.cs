using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

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

        private class ValueObject : DependencyObject
        {
            public static DependencyProperty ValueProperty = DependencyProperty.Register(
                nameof(Value), typeof(object), typeof(ValueObject),
                new(null, (d, e) => (d as ValueObject).ValueChanged?.Invoke(d, e)));

            public object Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

            public event EventHandler<DependencyPropertyChangedEventArgs> ValueChanged;

            public static ValueObject CreateBindingObject(object source, PropertyPath path)
            {
                var valueObject = new ValueObject();
                BindingOperations.SetBinding(valueObject, ValueProperty, new Binding() { Source = source, Path = path });
                return valueObject;
            }
        }

        private static readonly ConditionalWeakTable<object, HashSet<ValueObject>> valueObjectTable = new();

        public static IObservable<object> Binding<T>(this T source, PropertyPath path) where T : class
        {
            if (source is not DependencyObject && source is not INotifyPropertyChanged)
                throw new ArgumentException();
            return Observable.Create<object>(o =>
            {
                var valueObject = ValueObject.CreateBindingObject(source, path);
                void OnChanged(object sender, DependencyPropertyChangedEventArgs e) => o.OnNext(e.NewValue);
                valueObject.ValueChanged += OnChanged;
                if (valueObjectTable.TryGetValue(source, out var valueObjects))
                    valueObjects.Add(valueObject);
                else
                    valueObjectTable.Add(source, new() { valueObject });
                var sourceRef = new WeakReference<T>(source);
                var valueObjectRef = new WeakReference<ValueObject>(valueObject);
                return () =>
                {
                    if (valueObjectRef.TryGetTarget(out var v))
                    {
                        v.ValueChanged -= OnChanged;
                        if (sourceRef.TryGetTarget(out var s) && valueObjectTable.TryGetValue(s, out var vs))
                            vs.Remove(v);
                    }
                };
            });
        }

        public static IObservable<object> WhenPropertyChanged<T>(this T source, string propertyName) where T : INotifyPropertyChanged
        {
            var property = source.GetType().GetProperty(propertyName);
            return source.GetPropertyObservable().Where(x => x.EventArgs.PropertyName == propertyName).Select(_ => property.GetValue(source));
        }
    }
}
