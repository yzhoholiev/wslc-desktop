using System.Text.RegularExpressions;

namespace WslcDesktop.Tests;

/// <summary>
///     The rules every list follows, checked over the app's XAML so a new list cannot handle overflow its own way.
/// </summary>
public sealed partial class ListRulesTests
{
    private static readonly string AppFolder = Path.Combine(FindRepositoryRoot(), "src", "WslcDesktop.WinUI");

    public static TheoryData<string> XamlFiles => new(
        Directory.EnumerateFiles(AppFolder, "*.xaml", SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(file))
            .Select(file => Path.GetRelativePath(AppFolder, file)));

    [Theory]
    [MemberData(nameof(XamlFiles))]
    public void Text_ShortensThroughOverflow_NotItsOwnTrimming(string file)
    {
        string xaml = File.ReadAllText(Path.Combine(AppFolder, file));

        Assert.DoesNotContain("TextTrimming=\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("TextWrapping=\"NoWrap\"", xaml, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(XamlFiles))]
    public void Text_TakesItsTooltipFromOverflow(string file)
    {
        string xaml = File.ReadAllText(Path.Combine(AppFolder, file));

        Assert.DoesNotContain(TextBlockTag().Matches(xaml), tag => tag.Value.Contains("ToolTipService.ToolTip", StringComparison.Ordinal));
    }

    [Fact]
    public void XamlFiles_AreFound() => Assert.Contains(Path.Combine("Views", "ContainersPage.xaml"), XamlFiles.Select(row => row.Data));

    [GeneratedRegex(@"<TextBlock\b[^<>]*>", RegexOptions.Singleline)]
    private static partial Regex TextBlockTag();

    private static bool IsBuildOutput(string file)
    {
        string[] parts = Path.GetRelativePath(AppFolder, file).Split(Path.DirectorySeparatorChar);
        return parts.Contains("obj") || parts.Contains("bin");
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WslcDesktop.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("WslcDesktop.slnx was not found above the test output.");
    }
}
