using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media.Animation;

namespace Typedown.Universal.Utilities
{
    public static class StoryboardExtensions
    {
        public static Task BeginAsync(this Storyboard storyboard)
        {
            TaskCompletionSource<bool> tcs = new();
            if (storyboard == null)
                tcs.SetException(new ArgumentNullException());
            else
            {
                void onComplete(object s, object e)
                {
                    storyboard.Completed -= onComplete;
                    tcs.SetResult(true);
                }
                storyboard.Completed += onComplete;
                storyboard.Begin();
            }
            return tcs.Task;
        }
    }
}
