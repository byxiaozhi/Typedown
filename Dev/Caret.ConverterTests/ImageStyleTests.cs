using Typedown.WinUI.Utilities;
using Xunit;

namespace Caret.ConverterTests
{
    // The size menu of the image toolbar puts a check mark on the size the picture has: that is read from its style.
    public class ImageStyleTests
    {
        [Theory]
        [InlineData("zoom:50%;", 50.0)]
        [InlineData("width: 10px; zoom: 33%", 33.0)]
        [InlineData("ZOOM:150%;color:red;", 150.0)]
        [InlineData("zoom:0.5", 50.0)]
        [InlineData("zoom: 67.5%;", 67.5)]
        public void The_zoom_is_read_from_the_style(string style, double expected) =>
            Assert.Equal(expected, ImageStyle.Zoom(style));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("width:10px;")]
        [InlineData("zoom;")]
        [InlineData("zoom:big;")]
        [InlineData("mozoom:50%;")]
        public void No_zoom_gives_null(string style) => Assert.Null(ImageStyle.Zoom(style));

        [Theory]
        [InlineData("zoom:50%;", "50%", true)]
        [InlineData("zoom:50%;", "67%", false)]
        [InlineData("", "100%", true)]            // a picture with no zoom is at 100%
        [InlineData(null, "100%", true)]
        [InlineData("width:10px;", "100%", true)]
        [InlineData("width:10px;", "50%", false)]
        [InlineData("zoom:0.5", "50%", true)]
        [InlineData("zoom:75%;", "80%", false)]   // a size that is not in the menu checks nothing
        public void The_size_the_picture_has_is_the_one_that_is_checked(string style, string size, bool expected) =>
            Assert.Equal(expected, ImageStyle.IsZoom(style, size));

        [Theory]
        [InlineData("", "50%", "zoom:50%;")]
        [InlineData(null, "50%", "zoom:50%;")]
        [InlineData("zoom:25%;", "100%", "zoom:100%;")]
        [InlineData("width:10px;zoom:25%;", "50%", "width:10px;zoom:50%;")]
        [InlineData("ZOOM:25%; color:red", "50%", " color:red;zoom:50%;")]
        public void A_new_size_replaces_the_old_one_and_keeps_the_rest(string style, string size, string expected) =>
            Assert.Equal(expected, ImageStyle.WithZoom(style, size));
    }
}
