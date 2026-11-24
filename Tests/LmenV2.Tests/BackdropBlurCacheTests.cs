using System.Windows.Media;
using System.Windows.Media.Imaging;
using LmenV2.Rendering;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LmenV2.Tests;

[TestClass]
public sealed class BackdropBlurCacheTests
{
    [TestMethod]
    public void FrozenArtworkReusesBlurredBackdropForSameQuality()
    {
        BitmapSource artwork = BitmapSource.Create(
            2,
            2,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            new byte[16],
            8);
        artwork.Freeze();

        BitmapSource? first = BackdropBlur.CreateBlurredBackdrop(artwork, 64, 4, 1);
        BitmapSource? second = BackdropBlur.CreateBlurredBackdrop(artwork, 64, 4, 1);

        Assert.IsNotNull(first);
        Assert.AreSame(first, second);
    }
}
