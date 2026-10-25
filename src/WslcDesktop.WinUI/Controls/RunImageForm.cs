using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop.Controls;

/// <summary>
///     The run dialog's inputs, with ports, volumes and variables up front and the rest under More options.
/// </summary>
internal sealed class RunImageForm
{
    private const string ImageDefault = "Image default";

    /// <summary>
    ///     The dialog's title, button row and margins around its content, in view pixels.
    /// </summary>
    private const double DialogChromeHeight = 300;

    private const double MinScrollHeight = 200;

    private readonly CheckBox _allGpus = new() { Content = "Give the container every GPU" };
    private readonly TextBox _command;
    private readonly TextBox _cpus = FormParts.Field(new TextBox { PlaceholderText = "e.g. 0.5 or 2" }, "CPUs", RunImageHelp.Cpus);
    private readonly RunImageDefaults _defaults;
    private readonly TextBox _entrypoint;
    private readonly PairRows _environment = new("Add variable", "Name", "=", "Value");

    private readonly TextBox _environmentFile =
        FormParts.Field(new TextBox { PlaceholderText = "A file of KEY=value lines" }, "Environment file", RunImageHelp.EnvironmentFile);

    private readonly InfoBar _error = new() { Severity = InfoBarSeverity.Error, Title = "Could not create the container", IsClosable = false };
    private readonly TextBox _hostname = FormParts.Field(new TextBox { PlaceholderText = "The container ID" }, "Hostname", RunImageHelp.Hostname);
    private readonly CheckBox _interactive;
    private readonly TextBox _memory = FormParts.Field(new TextBox { PlaceholderText = "e.g. 512M or 1G" }, "Memory limit", RunImageHelp.Memory);
    private readonly TextBox _name = FormParts.Field(new TextBox { PlaceholderText = "Leave blank to generate one" }, "Container name", RunImageHelp.Name);
    private readonly ComboBox _network;
    private readonly PairRows _ports = new("Add port", "Host port", "→", "Container port, e.g. 80/tcp");
    private readonly ProgressBar _progress = new() { IsIndeterminate = true, Visibility = Visibility.Collapsed };
    private readonly CheckBox _publishAll = new() { Content = "Publish every exposed port to a random host port" };
    private readonly CheckBox _removeWhenStopped = new() { Content = "Remove the container when it stops" };
    private readonly ScrollViewer _scroller;
    private readonly CheckBox _tty;
    private readonly TextBox _user;
    private readonly PairRows _volumes = new("Add volume", "Host folder or volume name", "→", "Path in the container", FormParts.PickFolderAsync);
    private readonly TextBox _workingDirectory;
    private XamlRoot? _window;

    public RunImageForm(RunImageDefaults defaults)
    {
        _defaults = defaults;
        ImageConfigDto config = defaults.Config;
        foreach (string port in config.ExposedPorts?.Keys ?? Enumerable.Empty<string>())
        {
            _ports.Add("", port);
        }

        foreach (string target in config.Volumes?.Keys ?? Enumerable.Empty<string>())
        {
            _volumes.Add("", target);
        }

        _command = FormParts.Field(new TextBox { PlaceholderText = DefaultOf(config.Cmd) }, "Command", RunImageHelp.Command);
        _entrypoint = FormParts.Field(new TextBox { PlaceholderText = DefaultOf(config.Entrypoint) }, "Entrypoint", RunImageHelp.Entrypoint);
        _workingDirectory = FormParts.Field(new TextBox { PlaceholderText = DefaultOf(config.WorkingDir) }, "Working directory", RunImageHelp.WorkingDirectory);
        _user = FormParts.Field(new TextBox { PlaceholderText = DefaultOf(config.User) }, "User", RunImageHelp.User);
        _network = CreateNetworkChoice(defaults.Networks);
        _tty = new CheckBox { Content = "Allocate a terminal (TTY)", IsChecked = config.StartsShell };
        _interactive = new CheckBox { Content = "Keep standard input open", IsChecked = config.StartsShell };

        var content = new StackPanel
        {
            Spacing = 12,
            MinWidth = 560,
            Padding = new Thickness(0, 0, 16, 0),
            Children =
            {
                _error, _progress, _name,
                FormParts.Section("Ports", RunImageHelp.Ports, "Leave the host port blank to keep a port unpublished.", _ports.Panel),
                FormParts.Section("Volumes", RunImageHelp.Volumes, null, _volumes.Panel),
                FormParts.Section("Environment variables", RunImageHelp.Environment, null, _environment.Panel),
                CreateMoreOptions()
            }
        };

        _scroller = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    public UIElement Content => _scroller;

    public ContainerRunOptions Collect()
    {
        return new ContainerRunOptions(_defaults.Image)
        {
            Name = _name.Text,
            Ports = [.. _ports.Values.Select(pair => new PortMapping(pair.First, pair.Second))],
            PublishAllPorts = _publishAll.IsChecked == true,
            Volumes = [.. _volumes.Values.Select(pair => new VolumeMapping(pair.First, pair.Second))],
            Environment = [.. _environment.Values.Select(pair => new KeyValueEntry(pair.First, pair.Second))],
            EnvironmentFile = _environmentFile.Text,
            Command = _command.Text,
            Entrypoint = _entrypoint.Text,
            WorkingDirectory = _workingDirectory.Text,
            User = _user.Text,
            Network = _network.SelectedIndex > 0 ? (string)_network.SelectedItem : "",
            Hostname = _hostname.Text,
            Memory = _memory.Text,
            Cpus = _cpus.Text,
            Tty = _tty.IsChecked == true,
            Interactive = _interactive.IsChecked == true,
            RemoveWhenStopped = _removeWhenStopped.IsChecked == true,
            AllGpus = _allGpus.IsChecked == true
        };
    }

    public void ShowError(string message)
    {
        _error.Message = message;
        _error.IsOpen = true;
        _scroller.ChangeView(null, 0, null);
    }

    public void ShowProgress(bool visible) => _progress.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    ///     Keeps the content within the window's height until <see cref="StopTrackingWindow" />.
    /// </summary>
    public void TrackWindow(XamlRoot window)
    {
        _window = window;
        FitToWindow();
        window.Changed += OnWindowChanged;
    }

    public void StopTrackingWindow()
    {
        if (_window is not null)
        {
            _window.Changed -= OnWindowChanged;
        }
    }

    private static string DefaultOf(string[]? words) => words is { Length: > 0 } ? $"{ImageDefault}: {ShellWords.Join(words)}" : ImageDefault;

    private static string DefaultOf(string? value) => string.IsNullOrEmpty(value) ? ImageDefault : $"{ImageDefault}: {value}";

    private static ComboBox CreateNetworkChoice(IEnumerable<string> networks)
    {
        ComboBox network = FormParts.Field(new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch }, "Network", RunImageHelp.Network);
        network.Items.Add("Default");
        foreach (string name in networks)
        {
            network.Items.Add(name);
        }

        network.SelectedIndex = 0;
        return network;
    }

    private Expander CreateMoreOptions()
    {
        var options = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                _command, _entrypoint, FormParts.Pair(_workingDirectory, _user), FormParts.Pair(_network, _hostname), FormParts.Pair(_memory, _cpus),
                FormParts.WithBrowse(_environmentFile, FormParts.PickFileAsync),
                FormParts.Label(_publishAll, RunImageHelp.PublishAll), FormParts.Label(_tty, RunImageHelp.Tty),
                FormParts.Label(_interactive, RunImageHelp.Interactive), FormParts.Label(_removeWhenStopped, RunImageHelp.RemoveWhenStopped),
                FormParts.Label(_allGpus, RunImageHelp.AllGpus)
            }
        };

        var expander = new Expander
        {
            Header = "More options",
            Content = options,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };

        ScrollToWhenExpanded(expander, options);
        return expander;
    }

    private static void ScrollToWhenExpanded(Expander expander, FrameworkElement options)
    {
        // Expanding fires before the options are laid out, so they are scrolled to on the first layout that includes them.
        bool scrollToOptions = false;
        expander.Expanding += (_, _) => scrollToOptions = true;
        options.LayoutUpdated += (_, _) =>
        {
            if (!scrollToOptions || options.ActualHeight == 0)
            {
                return;
            }

            scrollToOptions = false;
            expander.StartBringIntoView(new BringIntoViewOptions { VerticalAlignmentRatio = 0, AnimationDesired = true });
        };
    }

    private void OnWindowChanged(XamlRoot sender, XamlRootChangedEventArgs args) => FitToWindow();

    private void FitToWindow()
    {
        // ContentDialog measures its content at full height and clips it, so the scroller needs a height that fits the window.
        _scroller.MaxHeight = Math.Max(MinScrollHeight, _window!.Size.Height - DialogChromeHeight);
    }
}
