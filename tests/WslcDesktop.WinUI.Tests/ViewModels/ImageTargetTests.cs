using WslcDesktop.ViewModels;

namespace WslcDesktop.Tests.ViewModels;

public sealed class ImageTargetTests
{
    [Theory]
    [InlineData("busybox", "1.36", "busybox:1.36")]
    [InlineData("busybox", "<none>", "0123456789ab")]
    [InlineData("busybox", "", "0123456789ab")]
    [InlineData("<none>", "<none>", "0123456789ab")]
    public void ImageTarget_NamesTheImageByTagOrElseById(string repository, string tag, string expected)
    {
        string reference = tag is "" or "<none>" ? repository : $"{repository}:{tag}";
        var image = new ImageItem("sha256:0123456789ab", reference, repository, tag, "0123456789ab", "1 MiB", 1, "", "", 0);

        Assert.Equal(expected, ImagesViewModel.ImageTarget(image));
    }
}
