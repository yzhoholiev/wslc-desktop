using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop.ViewModels;

public sealed partial class WslcViewModel : PageViewModel
{
    private static readonly Dictionary<string, SortColumn<WslcSessionItem>> SessionColumns = new()
    {
        ["id"] = new SortColumn<WslcSessionItem>(session => session.Id),
        ["name"] = new SortColumn<WslcSessionItem>(session => session.Name),
        ["pid"] = new SortColumn<WslcSessionItem>(session => session.CreatorPid)
    };

    private List<WslcSessionItem> _sessions = [];

    public WslcViewModel(AppServices services)
        : base(services)
    {
        SettingsFilePath = services.WslcSettings.FilePath;
        SessionSort = ListSort.For(services.Settings, "wslc-sessions", "id", SessionColumns);
        SessionSort.Changed += (_, _) => ShowSessions(_sessions);
    }

    public ObservableCollection<KeyValueEntry> SessionSettings { get; } = [];

    public ObservableCollection<KeyValueEntry> Versions { get; } = [];

    public ObservableCollection<WslcSessionItem> Sessions { get; } = [];

    public ListSort SessionSort { get; }

    public string SettingsFilePath { get; }

    [ObservableProperty]
    public partial string SettingsText { get; set; } = "";

    [ObservableProperty]
    public partial string VmText { get; set; } = "";

    [ObservableProperty]
    public partial bool HasNoSessions { get; set; } = true;

    public void Load()
    {
        string text = Services.WslcSettings.ReadText();
        SettingsText = text.Trim().Length > 0 ? text.TrimEnd() : "No settings.yaml — wslc uses its built-in defaults.";

        long hostMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        SessionSettings.ReplaceWith(WslcSettingsParser.Describe(WslcSettingsParser.Parse(text), Environment.ProcessorCount, hostMemory));
        Apply(Services.Monitor.Latest);
    }

    public void Apply(RuntimeSnapshot snapshot)
    {
        UpdateBusy();
        VmText = snapshot is { IsRunning: true, Vm: { } vm }
            ? $"{vm.VirtualCpus} vCPUs · {ByteSize.Format(vm.MemoryTotal)} memory · {ByteSize.Format(vm.SwapTotal)} swap"
            : "Not running — the next start uses the settings above.";

        ShowSessions(snapshot.Info?.Server.Sessions
            .Select(session => new WslcSessionItem(session.Id, session.Name, session.CreatorPid))
            .ToList() ?? []);

        List<KeyValueEntry> versions = [];
        if (snapshot.Info is { } info)
        {
            versions.Add(new KeyValueEntry("wslc", info.Client.Version));
            versions.Add(new KeyValueEntry("Session manager", info.Server.SessionManagerVersion));
        }

        versions.Add(new KeyValueEntry("Data source", Services.Session.Source switch
        {
            RuntimeDataSource.Engine => "Docker Engine API (session shell)",
            RuntimeDataSource.Cli when Services.Session.LastConnectionError is not null => "wslc CLI (engine API unavailable)",
            RuntimeDataSource.Cli => "wslc CLI",
            _ => "–"
        }));

        Versions.ReplaceWith(versions);
    }

    public Task TerminateSessionAsync(WslcSessionItem session) =>
        RunAndReloadAsync(
            $"Terminating session {session.Name}",
            () => Services.Commands.TerminateSessionAsync(session.Name),
            $"Session {session.Name} terminated.",
            true);

    public void OpenSessionShell(WslcSessionItem session) => Services.Shell.OpenSessionShell(session.Name);

    [RelayCommand]
    private Task RefreshAsync() => WithBusyAsync(ReloadAsync);

    [RelayCommand]
    private Task OpenSettingsAsync() => RunOperationAsync("Opening wslc settings", Services.Commands.OpenSettingsAsync);

    protected override async Task ReloadAsync()
    {
        await Services.Monitor.RefreshAsync();
        Load();
    }

    private void ShowSessions(List<WslcSessionItem> sessions)
    {
        _sessions = sessions;
        Sessions.SyncTo([.. SessionSort.Apply(sessions, SessionColumns, session => session.Name)], session => session.Id);
        HasNoSessions = Sessions.Count == 0;
    }
}
