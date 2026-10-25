using System.Windows.Automation;
using static WslcDesktop.UiTests.AppSession;

namespace WslcDesktop.UiTests;

/// <summary>
///     Drives the run-image dialog through UI Automation, finding each input by the name the form gives it.
/// </summary>
internal sealed class RunDialog
{
    private readonly AppSession _app;

    private RunDialog(AppSession app, string image)
    {
        _app = app;
        Title = $"Run {image}";
    }

    public string Title { get; }

    public bool IsOpen => Find(_app.Window, Title) is not null;

    public bool HasError => Find(_app.Window, "Could not create the container") is not null;

    /// <summary>
    ///     Opens the dialog from the image's row on the Images page.
    /// </summary>
    public static async Task<RunDialog> OpenAsync(AppSession app, string image)
    {
        await CloseAnyAsync(app);
        app.Navigate("Images");
        var dialog = new RunDialog(app, image);
        for (int attempt = 0; attempt < 6 && !dialog.IsOpen; attempt++)
        {
            AutomationElement? run = await WaitForAsync(() => RunButtonOfRow(app.Window, image), TimeSpan.FromSeconds(60));
            Assert.NotNull(run);
            Invoke(run);

            // The app ignores a click while the previous dialog is still closing, and the first open reads the image's defaults.
            await WaitForAsync(() => dialog.IsOpen, TimeSpan.FromSeconds(10));
        }

        Assert.True(dialog.IsOpen, $"The '{dialog.Title}' dialog did not open.");
        return dialog;
    }

    /// <summary>
    ///     Cancels a dialog an earlier test left open, since WinUI shows only one at a time.
    /// </summary>
    public static async Task CloseAnyAsync(AppSession app)
    {
        if (Find(app.Window, "Cancel", ControlType.Button) is { } cancel)
        {
            Invoke(cancel);
            await WaitForAsync(() => Find(app.Window, "Cancel", ControlType.Button) is null);
        }
    }

    public void SetText(string field, string value) => Value(Input(field, ControlType.Edit)).SetValue(value);

    public string GetText(string field) => Value(Input(field, ControlType.Edit)).Current.Value;

    public void ExpandMoreOptions()
    {
        AutomationElement expander = Wait(() => Find(_app.Window, "More options")) ?? throw new InvalidOperationException("No 'More options' expander.");
        ((ExpandCollapsePattern)expander.GetCurrentPattern(ExpandCollapsePattern.Pattern)).Expand();
    }

    public bool IsChecked(string checkBox) =>
        ((TogglePattern)Input(checkBox, ControlType.CheckBox).GetCurrentPattern(TogglePattern.Pattern)).Current.ToggleState == ToggleState.On;

    public void SetChecked(string checkBox, bool isChecked)
    {
        var toggle = (TogglePattern)Input(checkBox, ControlType.CheckBox).GetCurrentPattern(TogglePattern.Pattern);
        while (toggle.Current.ToggleState == ToggleState.On != isChecked)
        {
            toggle.Toggle();
        }
    }

    /// <summary>
    ///     Adds a row to a ports, volumes or variables list and fills both of its boxes.
    /// </summary>
    public async Task AddRowAsync(string addLink, string firstField, string first, string secondField, string second)
    {
        int count = All(firstField, ControlType.Edit).Length;
        Invoke(Input(addLink, ControlType.Hyperlink));
        Assert.True(await WaitForAsync(() => All(firstField, ControlType.Edit).Length > count), $"'{addLink}' added no row.");

        Value(All(firstField, ControlType.Edit)[^1]).SetValue(first);
        Value(All(secondField, ControlType.Edit)[^1]).SetValue(second);
    }

    public async Task SelectNetworkAsync(string network)
    {
        var choice = (ExpandCollapsePattern)Input("Network", ControlType.ComboBox).GetCurrentPattern(ExpandCollapsePattern.Pattern);
        choice.Expand();
        AutomationElement? item = await WaitForAsync(() => Find(_app.Window, network, ControlType.ListItem));
        Assert.NotNull(item);
        ((SelectionItemPattern)item.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        choice.Collapse();
    }

    /// <summary>
    ///     Clicks Run, Create or Cancel.
    /// </summary>
    public void Click(string button) => Invoke(Input(button, ControlType.Button));

    public async Task<bool> WaitClosedAsync() => await WaitForAsync(() => !IsOpen, TimeSpan.FromSeconds(90));

    /// <summary>
    ///     The text of the error the dialog shows, once it shows one that starts as expected.
    /// </summary>
    public async Task<string?> WaitForErrorAsync(string startsWith) =>
        await WaitForAsync(() => _app.Window.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text))
            .Cast<AutomationElement>()
            .Select(element => element.Current.Name)
            .FirstOrDefault(text => text.StartsWith(startsWith, StringComparison.Ordinal)), TimeSpan.FromSeconds(60));

    private static AutomationElement? RunButtonOfRow(AutomationElement window, string image)
    {
        string repository = image[..image.IndexOf(':')];
        string tag = image[(image.IndexOf(':') + 1)..];

        // A row's own Run button is the only one inside the smallest ancestor of its repository name that holds any;
        // the tag tells rows apart whose repository name also appears as another row's tag (nginx:alpine and alpine:3).
        foreach (AutomationElement label in window.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, repository))
                     .Cast<AutomationElement>())
        {
            for (AutomationElement? ancestor = TreeWalker.ControlViewWalker.GetParent(label);
                 ancestor is not null;
                 ancestor = TreeWalker.ControlViewWalker.GetParent(ancestor))
            {
                AutomationElementCollection buttons = ancestor.FindAll(TreeScope.Descendants, new AndCondition(
                    new PropertyCondition(AutomationElement.NameProperty, "Run a container"),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)));

                if (buttons.Count == 1)
                {
                    if (Find(ancestor, tag) is not null)
                    {
                        return buttons[0];
                    }

                    break;
                }

                if (buttons.Count > 1)
                {
                    break;
                }
            }
        }

        return null;
    }

    private static ValuePattern Value(AutomationElement input) => (ValuePattern)input.GetCurrentPattern(ValuePattern.Pattern);

    private AutomationElement Input(string name, ControlType type) =>
        Wait(() => Find(_app.Window, name, type)) ?? throw new InvalidOperationException($"No {type.ProgrammaticName} named '{name}' in the dialog.");

    /// <summary>
    ///     Waits for an element, since the automation tree catches up with the UI a moment later, as after a dropdown closes.
    /// </summary>
    private static AutomationElement? Wait(Func<AutomationElement?> find)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (true)
        {
            try
            {
                if (find() is { } element)
                {
                    return element;
                }
            }
            catch (ElementNotAvailableException)
            {
            }

            if (DateTime.UtcNow > deadline)
            {
                return null;
            }

            Thread.Sleep(200);
        }
    }

    private AutomationElement[] All(string name, ControlType type) =>
    [
        .. _app.Window.FindAll(TreeScope.Descendants, new AndCondition(
                new PropertyCondition(AutomationElement.NameProperty, name),
                new PropertyCondition(AutomationElement.ControlTypeProperty, type)))
            .Cast<AutomationElement>()
    ];
}
