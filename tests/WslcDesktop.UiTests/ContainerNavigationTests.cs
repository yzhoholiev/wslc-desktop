using System.Windows.Automation;
using static WslcDesktop.UiTests.AppSession;

namespace WslcDesktop.UiTests;

/// <summary>
///     Running a container from the run dialog opens it in the containers list with its row in view, also when the list is longer than the window.
/// </summary>
[Collection(AppCollection.Name)]
public sealed class ContainerNavigationTests(AppSession app) : IAsyncLifetime
{
    private const string SkipReason = "Set WSLC_E2E=1 (with WSLC Desktop closed) to create containers through the published app; it starts the runtime.";

    /// <summary>
    ///     More rows than the containers list shows at the app's default window size.
    /// </summary>
    private const int Fillers = 25;

    private readonly List<string> _containers = [];

    public async ValueTask InitializeAsync()
    {
        if (!RuntimeFixture.Enabled)
        {
            return;
        }

        for (int i = 0; i < Fillers; i++)
        {
            string name = $"{RuntimeFixture.Prefix}nav-a{i:00}";
            _containers.Add(name);
            await WslcCli.RunAsync("run", "--detach", "--name", name, RuntimeFixture.ShellImage, "sleep", "300");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await RunDialog.CloseAnyAsync(app);
        foreach (string name in _containers)
        {
            await WslcCli.TryAsync("remove", "--force", name);
        }
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(RuntimeFixture.Enabled), SkipType = typeof(RuntimeFixture))]
    public async Task StartedContainer_IsScrolledIntoView()
    {
        // Running containers sort by name, so this one comes after every running filler, below the visible rows.
        string name = $"{RuntimeFixture.Prefix}nav-zz";
        _containers.Add(name);

        RunDialog dialog = await RunDialog.OpenAsync(app, RuntimeFixture.ShellImage);
        dialog.SetText("Container name", name);
        dialog.ExpandMoreOptions();
        dialog.SetText("Command", "sleep 300");
        dialog.Click("Run");
        Assert.True(await dialog.WaitClosedAsync(), "The dialog stayed open.");

        AutomationElement? row =
            await WaitForAsync(() => Find(ContainerList(), name) is { Current.IsOffscreen: false } element ? element : null, TimeSpan.FromSeconds(30));

        Assert.True(row is not null, DescribeRows(name));
        Assert.False(app.CrashLogChanged(), app.CrashLogTail());
    }

    /// <summary>
    ///     The containers list itself; the details pane repeats the selected container's name.
    /// </summary>
    private AutomationElement ContainerList() =>
        app.Window.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "ContainerList"))
        ?? throw new InvalidOperationException("The containers list is not shown.");

    /// <summary>
    ///     Which container rows the list has realized and whether they are on screen, for a failure message.
    /// </summary>
    private string DescribeRows(string name)
    {
        AutomationElement? target = Find(ContainerList(), name);
        IEnumerable<string> rows = ContainerList()
            .FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text))
            .Cast<AutomationElement>()
            .Where(element => element.Current.Name.StartsWith($"{RuntimeFixture.Prefix}nav-", StringComparison.Ordinal))
            .Select(element => $"{element.Current.Name[RuntimeFixture.Prefix.Length..]}{(element.Current.IsOffscreen ? " (offscreen)" : "")}");

        return
            $"{name} is {(target is null ? "not in the tree" : target.Current.IsOffscreen ? "off screen" : "visible")}; realized rows: {string.Join(", ", rows)}";
    }
}
