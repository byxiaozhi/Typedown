using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Metadata;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Typedown.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Controls;

using Avalonia.VisualTree;
using Avalonia;

namespace Typedown.Core.Utilities
{
    public static class InjectionExtensions
    {
        public static T GetService<T>(this Control element)
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(element);
                if (topLevel?.DataContext is AppViewModel model)
                    return model.ServiceProvider.GetService<T>();
                
                // Fallback to Application current if not connected to tree
                if (Application.Current?.DataContext is AppViewModel appModel)
                    return appModel.ServiceProvider.GetService<T>();

                return default;
            }
            catch
            {
                return default;
            }
        }

        public static Lazy<T> GetServiceLazy<T>(this Control element)
        {
            return new(() => GetService<T>(element));
        }

        public static T GetAncestor<T>(this Control element, string name = null) where T : Control
        {
            var obj = element.GetVisualParent();
            while (obj != null)
            {
                if (obj is T res && (string.IsNullOrEmpty(name) || res.Name == name))
                    return res;
                obj = obj.GetVisualParent();
            }
            return default;
        }
    }
}
