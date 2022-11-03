using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Typedown.Universal.Utilities;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Typedown.Universal.Controls
{
    public sealed partial class FeedbackDialog : UserControl
    {
        public static readonly DependencyProperty RantingProperty = DependencyProperty.Register(nameof(Ranting), typeof(int), typeof(FeedbackDialog), new PropertyMetadata(-1));
        public int Ranting { get => (int)GetValue(RantingProperty); set => SetValue(RantingProperty, value); }

        public static readonly DependencyProperty FeedbackProperty = DependencyProperty.Register(nameof(Feedback), typeof(string), typeof(FeedbackDialog), new PropertyMetadata(""));
        public string Feedback { get => (string)GetValue(FeedbackProperty); set => SetValue(FeedbackProperty, value); }

        public static readonly DependencyProperty ContactProperty = DependencyProperty.Register(nameof(Contact), typeof(string), typeof(FeedbackDialog), new PropertyMetadata(""));
        public string Contact { get => (string)GetValue(ContactProperty); set => SetValue(ContactProperty, value); }

        public FeedbackDialog()
        {
            InitializeComponent();
        }

        public static async Task OpenFeedbackDialog(XamlRoot xamlRoot)
        {
            var content = new FeedbackDialog();
            var result = await AppContentDialog.Create(Localize.GetDialogString("FeedbackTitle"), content, Localize.GetDialogString("Cancel"), Localize.GetDialogString("Submit")).ShowAsync(xamlRoot);
            if (result == ContentDialogResult.None)
                return;
            string msg;
            if (string.IsNullOrEmpty(content.Feedback))
                msg = Localize.GetDialogString("ContentCanNotBeBlank");
            else
                try
                {
                    var res = await Common.Post("https://typedown.ownbox.cn/feedback", new
                    {
                        rating = content.Ranting,
                        feedback = content.Feedback,
                        contact = content.Contact,
                    });
                    if (res["code"].ToObject<int>() == 0)
                        msg = Localize.GetDialogString("SubmittedSuccessfully");
                    else
                        msg = res["msg"].ToString();
                }
                catch (Exception ex)
                {
                    msg = ex.Message;
                }
            await AppContentDialog.Create(Localize.GetDialogString("FeedbackTitle"), msg, Localize.GetDialogString("Ok")).ShowAsync(xamlRoot);
        }
    }
}
