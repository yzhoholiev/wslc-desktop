using WslcDesktop.Services;

namespace WslcDesktop.Tests.Services;

public sealed class ImageReferenceTests
{
    [Theory]
    [InlineData("nginx")]
    [InlineData("nginx:alpine")]
    [InlineData("ghcr.io/owner/app:1.0")]
    [InlineData("registry.example.com:5000/team/app:1.2-rc_1")]
    [InlineData("nginx@sha256:0123abcd")]
    [InlineData("76090a34b858")]
    public void ImageReference_AcceptsReferencesAndIds(string reference) => Assert.True(ImageReference.IsValid(reference));

    [Theory]
    [InlineData("")]
    [InlineData("--help")]
    [InlineData("-q")]
    [InlineData(".hidden")]
    [InlineData("nginx alpine")]
    [InlineData("nginx;calc")]
    [InlineData("nginx'; rm -rf / #")]
    [InlineData("nginx$(reboot)")]
    [InlineData("nginx`id`")]
    [InlineData("nginx\"quoted\"")]
    [InlineData("nginx\nalpine")]
    public void ImageReference_RejectsOptionsAndShellCharacters(string reference) => Assert.False(ImageReference.IsValid(reference));
}
