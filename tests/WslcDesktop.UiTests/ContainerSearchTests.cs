using System.Windows.Automation;
using static WslcDesktop.UiTests.AppSession;

namespace WslcDesktop.UiTests;

/// <summary>
///     The containers list's search against a running runtime that has a container to hide.
/// </summary>
[Collection(AppCollection.Name)]
public sealed class ContainerSearchTests(AppSession app) : IAsyncLifetime
{
    private const string SkipReason = "Set WSLC_E2E=1 (with WSLC Desktop closed) to search containers in the published app; it starts the runtime.";

    private readonly string _container = RuntimeFixture.NewName("search");

    public async ValueTask InitializeAsync()
    {
        if (RuntimeFixture.Enabled)
        {
            await WslcCli.RunAsync("create", "--name", _container, RuntimeFixture.ShellImage);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (RuntimeFixture.Enabled)
        {
            await WslcCli.TryAsync("remove", "--force", _container);
        }
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(RuntimeFixture.Enabled), SkipType = typeof(RuntimeFixture))]
    public async Task SearchWithoutMatches_OffersToClearIt()
    {
        app.Navigate("Containers");
        Assert.NotNull(await WaitForAsync(() => Find(app.Window, _container), TimeSpan.FromSeconds(30)));
        AutomationElement? search = await WaitForAsync(() =>
            app.Window.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit)));

        Assert.NotNull(search);

        ((ValuePattern)search.GetCurrentPattern(ValuePattern.Pattern)).SetValue("zzz-no-such-container");
        AutomationElement? clear = await WaitForAsync(() => Find(app.Window, "Clear search", ControlType.Button));
        Assert.NotNull(clear);
        Invoke(clear);

        Assert.True(await WaitForAsync(() => ((ValuePattern)search.GetCurrentPattern(ValuePattern.Pattern)).Current.Value.Length == 0));
        Assert.NotNull(await WaitForAsync(() => Find(app.Window, _container)));
        Assert.False(app.CrashLogChanged(), app.CrashLogTail());
    }
}
