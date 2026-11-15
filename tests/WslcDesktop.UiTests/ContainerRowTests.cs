using System.Windows.Automation;
using static WslcDesktop.UiTests.AppSession;
using Rect = System.Windows.Rect;

namespace WslcDesktop.UiTests;

/// <summary>
///     A containers row whose name does not fit its column, in the smallest window the app allows.
/// </summary>
[Collection(AppCollection.Name)]
public sealed class ContainerRowTests(AppSession app) : IAsyncLifetime
{
    private const string SkipReason = "Set WSLC_E2E=1 (with WSLC Desktop closed) to show a container in the published app; it starts the runtime.";

    private readonly string _container = RuntimeFixture.NewName("a-name-far-too-long-for-the-column-it-is-shown-in");

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
    public async Task Row_KeepsEveryTextOnOneLineAndInsideIt_AtTheMinimumWindowSize()
    {
        app.Navigate("Containers");
        Assert.NotNull(await WaitForAsync(() => Find(app.Window, _container, ControlType.Text), TimeSpan.FromSeconds(30)));

        await app.AtMinimumSizeAsync(() =>
        {
            AutomationElement name = Find(app.Window, _container, ControlType.Text)!;
            AutomationElement row = RowOf(name);
            Rect bounds = row.Current.BoundingRectangle;
            double line = name.Current.BoundingRectangle.Height;

            foreach (AutomationElement text in row.FindAll(TreeScope.Descendants,
                         new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text)).Cast<AutomationElement>())
            {
                Rect box = text.Current.BoundingRectangle;
                if (box.IsEmpty)
                {
                    continue;
                }

                Assert.True(box.Height <= line + 2, $"'{text.Current.Name}' takes more than one line.");
                Assert.True(box.Right <= bounds.Right + 1, $"'{text.Current.Name}' runs past its row.");
            }
        });

        Assert.False(app.CrashLogChanged(), app.CrashLogTail());
    }

    private static AutomationElement RowOf(AutomationElement element)
    {
        AutomationElement? current = element;
        while (current is not null && !Equals(current.Current.ControlType, ControlType.ListItem))
        {
            current = TreeWalker.ControlViewWalker.GetParent(current);
        }

        return current ?? throw new InvalidOperationException("The text is not in a list row.");
    }
}
