using System.Windows.Automation;
using static WslcDesktop.UiTests.AppSession;

namespace WslcDesktop.UiTests;

/// <summary>
///     Drives the published app through UI Automation; they take over the desktop, so they only run when asked to.
/// </summary>
[Collection(AppCollection.Name)]
public sealed class AppSmokeTests(AppSession app)
{
    private const string SkipReason = "Set WSLC_UI_TESTS=1 (with WSLC Desktop closed) to drive the published app.";

    private static readonly string[] NavigationItems = ["Dashboard", "Containers", "Images", "Volumes", "Networks", "WSL containers", "WSL", "Settings"];

    [Theory(Skip = SkipReason, SkipUnless = nameof(Enabled), SkipType = typeof(AppSession))]
    [InlineData("Dashboard", "Dashboard")]
    [InlineData("Containers", "Containers")]
    [InlineData("Images", "Images")]
    [InlineData("Volumes", "Volumes")]
    [InlineData("Networks", "Networks")]
    [InlineData("WSL containers", "The wslc session VM, its settings and sessions")]
    [InlineData("WSL", "Windows Subsystem for Linux")]
    [InlineData("Settings", "Settings")]
    public async Task Navigation_OpensEveryPage(string item, string title)
    {
        app.Navigate(item);

        Assert.NotNull(await WaitForAsync(() => Find(app.Window, title, ControlType.Text)));
        AssertNoCrash();
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(Enabled), SkipType = typeof(AppSession))]
    public async Task TitleBar_ShowsTheSampledRuntimeStatus()
    {
        string[] sampled = ["Runtime stopped", "Running", "wslc not installed", "wslc unavailable"];

        AutomationElement? status = await WaitForAsync(() => app.Window
            .FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text))
            .Cast<AutomationElement>()
            .FirstOrDefault(element => sampled.Any(text => element.Current.Name.StartsWith(text, StringComparison.Ordinal))));

        Assert.NotNull(status);
        AssertNoCrash();
    }

    [Theory(Skip = SkipReason, SkipUnless = nameof(Enabled), SkipType = typeof(AppSession))]
    [InlineData("Containers", "Reload logs")]
    [InlineData("Images", "Close details")]
    [InlineData("Volumes", "Close details")]
    [InlineData("Networks", "Close details")]
    public async Task SelectingARow_OpensItsDetails(string page, string detailsButton)
    {
        app.Navigate(page);
        AutomationElement? row = await WaitForAsync(FirstRow);
        if (row is null)
        {
            Assert.Skip($"The runtime has no {page.ToLowerInvariant()} to select.");
        }

        ((SelectionItemPattern)row.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();

        Assert.NotNull(await WaitForAsync(() => Find(app.Window, detailsButton, ControlType.Button)));
        AssertNoCrash();
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(Enabled), SkipType = typeof(AppSession))]
    public async Task Dashboard_ShowsAClickableCardPerResource()
    {
        app.Navigate("Dashboard");

        foreach (string card in new[] { "Open containers", "Open images", "Open volumes", "Open networks" })
        {
            Assert.NotNull(await WaitForAsync(() => Find(app.Window, card, ControlType.Button)));
        }

        Invoke(Find(app.Window, "Open images", ControlType.Button)!);
        Assert.NotNull(await WaitForAsync(() => Find(app.Window, "Images", ControlType.Text)));
        AssertNoCrash();
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(Enabled), SkipType = typeof(AppSession))]
    public async Task Containers_SortHeaderTogglesItsDirection()
    {
        app.Navigate("Containers");
        AutomationElement? header = await WaitForAsync(() =>
            Find(app.Window, "Sort by NAME", ControlType.Button) ?? Find(app.Window, "NAME, sorted ascending", ControlType.Button));

        Assert.NotNull(header);

        Invoke(header);
        AutomationElement? first = await WaitForAsync(() => SortHeader("NAME"));
        Assert.NotNull(first);
        string firstName = first.Current.Name;

        Invoke(first);
        string? secondName = await WaitForAsync(() => SortHeader("NAME")?.Current.Name is { } name && name != firstName ? name : null);

        Assert.Equal("NAME, sorted ascending", firstName);
        Assert.Equal("NAME, sorted descending", secondName);
        AssertNoCrash();
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(Enabled), SkipType = typeof(AppSession))]
    public async Task Settings_OffersTheWindowBackgrounds()
    {
        app.Navigate("Settings");

        foreach (string option in new[] { "Acrylic", "Mica", "Mica Alt" })
        {
            Assert.NotNull(await WaitForAsync(() => FindRadioStartingWith(option)));
        }

        AssertNoCrash();
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(Enabled), SkipType = typeof(AppSession))]
    public async Task ClosingTheWindow_KeepsTheAppInTheTrayUntilLaunchedAgain()
    {
        app.CloseWindow();
        Assert.True(await WaitForAsync(() => !app.IsWindowVisible()));
        Assert.False(app.Process!.HasExited);

        await LaunchAsync();

        Assert.True(await WaitForAsync(app.IsWindowVisible));
        Assert.NotNull(await WaitForAsync(() => Find(app.Window, "Dashboard", ControlType.ListItem)));
        AssertNoCrash();
    }

    private AutomationElement? FirstRow() =>
        app.Window.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem))
            .Cast<AutomationElement>()
            .FirstOrDefault(element => !NavigationItems.Contains(element.Current.Name));

    private AutomationElement? SortHeader(string column) =>
        app.Window.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button))
            .Cast<AutomationElement>()
            .FirstOrDefault(element => element.Current.Name.StartsWith($"{column}, sorted", StringComparison.Ordinal));

    private AutomationElement? FindRadioStartingWith(string label) =>
        app.Window.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.RadioButton))
            .Cast<AutomationElement>()
            .FirstOrDefault(element => element.Current.Name.StartsWith($"{label}:", StringComparison.Ordinal));

    private void AssertNoCrash() => Assert.False(app.CrashLogChanged(), app.CrashLogTail());
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AppCollection : ICollectionFixture<AppSession>, ICollectionFixture<RuntimeFixture>
{
    public const string Name = "Published app";
}
