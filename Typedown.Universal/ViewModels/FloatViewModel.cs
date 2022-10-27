using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Services;
using Typedown.Universal.Utilities;

namespace Typedown.Universal.ViewModels
{
    public class FloatViewModel : ObservableObject
    {
        public IServiceProvider ServiceProvider { get; }

        public bool FrontMenuOpen { get; set; }
        public JToken FrontMenuArg { get; set; }
        public bool FormatPickerOpen { get; set; }
        public JToken FormatPickerArg { get; set; }
        public bool ImageToolbarOpen { get; set; }
        public JToken ImageToolbarArg { get; set; }
        public bool ImageSelectorOpen { get; set; }
        public JToken ImageSelectorOpenArg { get; set; }
        public bool TableToolsOpen { get; set; }
        public JToken TableToolsArg { get; set; }
        public string TableToolsBarType { get; set; }
        public int SearchOpen { get; set; }
        public bool ToolTipOpen { get; set; }
        public JToken ToolTipArg { get; set; }
        public AppViewModel ViewModel => ServiceProvider.GetService<AppViewModel>();

        public EventCenter EventCenter => ServiceProvider.GetService<EventCenter>();

        public IMarkdownEditor Transport => ServiceProvider.GetService<IMarkdownEditor>();

        public FloatViewModel(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public void OnSearchOpenChange(int open)
        {
            Transport?.PostMessage("SearchOpenChange", new { open });
        }

        public void OnOpenImageToolbar(JToken arg)
        {
            if (!ImageToolbarOpen)
            {
                ImageToolbarArg = arg;
                ImageToolbarOpen = true;
            }
        }

        public void OnOpenFrontMenu(JToken arg)
        {
            FrontMenuArg = arg;
            FrontMenuOpen = true;
        }

        public void OnOpenFormatPicker(JToken arg)
        {
            FormatPickerArg = arg;
            FormatPickerOpen = true;
        }

        public void OnOpenImageSelector(JToken arg)
        {
            ImageSelectorOpenArg = arg;
            ImageSelectorOpen = true;
        }

        public void OnOpenTableTools(JToken arg)
        {
            TableToolsArg = arg;
            TableToolsOpen = true;
            TableToolsBarType = arg["tableInfo"]["barType"].ToString();
        }

        public void OnOpenToolTip(JToken arg)
        {
            ToolTipArg = arg;
            ToolTipOpen = arg["open"].ToObject<bool>();
        }

        public void CloseAll()
        {
            if (FormatPickerOpen)
                FormatPickerOpen = false;
            if (ImageToolbarOpen)
                ImageToolbarOpen = false;
            if (FrontMenuOpen)
                FrontMenuOpen = false;
            if (ImageSelectorOpen)
                ImageSelectorOpen = false;
            if (TableToolsOpen)
                TableToolsOpen = false;
        }
    }
}
