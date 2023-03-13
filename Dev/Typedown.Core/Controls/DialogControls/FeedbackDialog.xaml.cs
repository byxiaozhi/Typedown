using System;
using System.Threading.Tasks;
using Typedown.Core.Utilities;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Controls
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
            var result = await AppContentDialog.Create(Locale.GetDialogString("FeedbackTitle"), content, Locale.GetDialogString("Cancel"), Locale.GetDialogString("Submit")).ShowAsync(xamlRoot);
            if (result == ContentDialogResult.None)
                return;
            string msg;
            if (string.IsNullOrEmpty(content.Feedback))
                msg = Locale.GetDialogString("ContentCanNotBeBlank");
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
                        msg = Locale.GetDialogString("SubmittedSuccessfully");
                    else
                        msg = res["msg"].ToString();
                }
                catch (Exception ex)
                {
                    msg = ex.Message;
                }
            await AppContentDialog.Create(Locale.GetDialogString("FeedbackTitle"), msg, Locale.GetDialogString("Ok")).ShowAsync(xamlRoot);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
             Bindings?.StopTracking();
        }
    }
}
