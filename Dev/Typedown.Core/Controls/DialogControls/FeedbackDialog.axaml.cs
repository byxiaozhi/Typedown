using System;
using System.Collections.ObjectModel;
using Avalonia.Input;
using Avalonia.Metadata;
using Avalonia.Data.Converters;
using System.Threading.Tasks;
using Typedown.Core.Utilities;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Typedown.Core.Controls
{
    public sealed partial class FeedbackDialog : UserControl
    {
        public static readonly StyledProperty RantingProperty = AvaloniaProperty.Register<FeedbackDialog, int>(nameof(Ranting), new PropertyMetadata(-1));
        public int Ranting { get => GetValue(RantingProperty); set => SetValue(RantingProperty, value); }

        public static readonly StyledProperty FeedbackProperty = AvaloniaProperty.Register<FeedbackDialog, string>(nameof(Feedback), new PropertyMetadata(""));
        public string Feedback { get => GetValue(FeedbackProperty); set => SetValue(FeedbackProperty, value); }

        public static readonly StyledProperty ContactProperty = AvaloniaProperty.Register<FeedbackDialog, string>(nameof(Contact), new PropertyMetadata(""));
        public string Contact { get => GetValue(ContactProperty); set => SetValue(ContactProperty, value); }

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
