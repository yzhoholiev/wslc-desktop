using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop.ViewModels;

public sealed partial class WslViewModel : PageViewModel
{
    private static readonly Dictionary<string, SortColumn<WslDistro>> DistroColumns = new()
    {
        ["name"] = new SortColumn<WslDistro>(distro => distro.Name),
        ["state"] = new SortColumn<WslDistro>(distro => distro.IsRunning ? 0 : 1),
        ["version"] = new SortColumn<WslDistro>(distro => distro.Version)
    };

    private const int DistroRefreshEveryUpdates = 3;

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

    public void OpenDistroTerminal(WslDistro distro) =>
        Services.Shell.OpenDistroShell(distro.Name);

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
