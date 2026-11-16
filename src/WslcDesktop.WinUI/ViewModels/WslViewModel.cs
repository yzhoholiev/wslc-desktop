using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop.ViewModels;

public sealed partial class WslViewModel : PageViewModel
{
    private const int DistroRefreshEveryUpdates = 3;

    private static readonly Dictionary<string, SortColumn<WslDistro>> DistroColumns = new()
    {
        ["name"] = new SortColumn<WslDistro>(distro => distro.Name),
        ["state"] = new SortColumn<WslDistro>(distro => distro.IsRunning ? 0 : 1),
        ["version"] = new SortColumn<WslDistro>(distro => distro.Version)
    };

    private List<WslDistro> _distros = [];
    private bool _isRefreshingDistros;
    private int _updatesUntilDistroRefresh;

    public WslViewModel(AppServices services)
        : base(services)
    {
        IsSettingsAppInstalled = services.Wsl.IsSettingsAppInstalled;
        WslConfigText = "";
        DistroSort = ListSort.For(services.Settings, "wsl-distros", "name", DistroColumns);
        DistroSort.Changed += (_, _) => ShowDistros(_distros);
    }

    public ObservableCollection<WslDistro> Distros { get; } = [];

    public ObservableCollection<KeyValueEntry> Versions { get; } = [];

    public ListSort DistroSort { get; }

    [ObservableProperty]
    public partial string WslConfigText { get; set; }

    [ObservableProperty]
    public partial bool HasNoDistros { get; set; }

    [ObservableProperty]
    public partial bool UsePreRelease { get; set; }

    public bool IsSettingsAppInstalled { get; }

    [RelayCommand]
    private Task RefreshAsync() => WithBusyAsync(ReloadAsync);

    public async Task LoadAsync()
    {
        await WithBusyAsync(async () =>
        {
            try
            {
                Task<List<WslDistro>> distros = Services.Wsl.ListDistrosAsync();
                Task<List<KeyValueEntry>> versions = Services.Wsl.GetVersionInfoAsync();

                ShowDistros(await distros);
                Versions.ReplaceWith(await versions);
            }
            catch (CliException ex)
            {
                Services.Notifier.Error("Could not query WSL", ex.Message);
            }
        });

        WslConfigText = Services.WslConfig.ReadText() is { Length: > 0 } text ? text.TrimEnd() : "No .wslconfig file — WSL uses its defaults.";
    }

    /// <summary>
    ///     Follows a monitor update: updates the busy state and, every few updates, the distribution states.
    /// </summary>
    public async Task OnRuntimeUpdatedAsync()
    {
        UpdateBusy();
        if (_updatesUntilDistroRefresh-- <= 0 && await RefreshDistrosAsync())
        {
            _updatesUntilDistroRefresh = DistroRefreshEveryUpdates - 1;
        }
    }

    /// <summary>
    ///     Re-reads distribution states, which change outside the app whenever a terminal starts or exits a distro.
    /// </summary>
    private async Task<bool> RefreshDistrosAsync()
    {
        if (_isRefreshingDistros || IsWorking)
        {
            return false;
        }

        _isRefreshingDistros = true;
        try
        {
            ShowDistros(await Services.Wsl.ListDistrosAsync());
        }
        catch (CliException)
        {
        }
        finally
        {
            _isRefreshingDistros = false;
        }

        return true;
    }

    private void ShowDistros(List<WslDistro> distros)
    {
        _distros = distros;
        Distros.SyncTo([.. DistroSort.Apply(distros, DistroColumns, distro => distro.Name)], distro => distro.Name);
        HasNoDistros = Distros.Count == 0;
    }

    public Task ShutdownAsync() => RunAndReloadAsync("Shutting down WSL", Services.Actions.ShutdownWslAsync, "WSL has been shut down.");

    public Task TerminateAsync(WslDistro distro) =>
        RunAndReloadAsync($"Terminating {distro.Name}", () => Services.Wsl.TerminateAsync(distro.Name), $"{distro.Name} terminated.");

    public Task SetDefaultAsync(WslDistro distro) =>
        RunAndReloadAsync($"Setting {distro.Name} as default", () => Services.Wsl.SetDefaultAsync(distro.Name),
            $"{distro.Name} is now the default distribution.");

    /// <summary>
    ///     Unregisters the distribution, never alongside a WSL shutdown or update.
    /// </summary>
    public Task RemoveAsync(WslDistro distro) =>
        RunAndReloadAsync($"Removing {distro.Name}", () => Services.Wsl.UnregisterAsync(distro.Name), $"{distro.Name} removed.", true);

    public void OpenDistroTerminal(WslDistro distro) =>
        Services.Shell.OpenDistroShell(distro.Name);

    /// <summary>
    ///     Lists what WSL offers to install, marking what is installed already.
    /// </summary>
    /// <param name="ct">Stops the download of the list.</param>
    /// <exception cref="CliException">WSL could not get the list.</exception>
    public async Task<AvailableDistroItem[]> ListAvailableAsync(CancellationToken ct)
    {
        Task<List<WslDistro>> current = Services.Wsl.ListDistrosAsync(ct);
        Task<List<AvailableDistro>> offered = Services.Wsl.ListAvailableDistrosAsync(ct);
        await Task.WhenAll(current, offered);

        // The page's own list is refreshed only every few updates, and the fresh one is empty when wsl.exe fails.
        var installed = _distros.Concat(current.Result).Select(distro => distro.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return [.. offered.Result.Select(distro => new AvailableDistroItem(distro.Name, distro.FriendlyName, installed.Contains(distro.Name)))];
    }

    public void Install(AvailableDistroItem distro) => Services.Shell.InstallDistro(distro.Name);

    [RelayCommand]
    private Task UpdateAsync() => RunOperationAsync(
        "Updating WSL",
        async () =>
        {
            await Services.Actions.UpdateWslAsync(UsePreRelease);
            await LoadAsync();
        });

    [RelayCommand]
    private void OpenWslConfig() => Services.Shell.OpenWslConfig();

    [RelayCommand]
    private void OpenSettingsApp()
    {
        if (IsSettingsAppInstalled)
        {
            Services.Shell.OpenWslSettings();
        }
    }

    protected override async Task ReloadAsync()
    {
        await Services.Monitor.RefreshAsync();
        await LoadAsync();
    }
}
