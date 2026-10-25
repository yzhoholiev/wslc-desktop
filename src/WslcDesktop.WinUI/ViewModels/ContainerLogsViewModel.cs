using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WslcDesktop.Services;

namespace WslcDesktop.ViewModels;

/// <summary>
///     The logs pane of the selected container.
/// </summary>
public sealed partial class ContainerLogsViewModel : ObservableObject
{
    private const int DefaultTail = 500;

    private readonly IRuntimeDetails _details;
    private readonly ISettingsStore _settings;
    private ContainerItem? _container;
    private CancellationToken _ct;
    private bool _isLoading;
    private int _request;

    internal ContainerLogsViewModel(IRuntimeDetails details, ISettingsStore settings)
    {
        _details = details;
        _settings = settings;
        Text = "";
        Tail = DefaultTail;
        ShowTimestamps = _settings.Current.ShowLogTimestamps;
    }

    [ObservableProperty]
    public partial string Text { get; set; }

    [ObservableProperty]
    public partial double Tail { get; set; }

    [ObservableProperty]
    public partial bool Follow { get; set; }

    [ObservableProperty]
    public partial bool ShowTimestamps { get; set; }

    /// <summary>
    ///     Switches the pane to another container, or to none, and clears it until its logs load.
    /// </summary>
    /// <param name="container">The selected container, if any.</param>
    /// <param name="ct">Cancelled when the selection changes again.</param>
    internal void Show(ContainerItem? container, CancellationToken ct)
    {
        _container = container;
        _ct = ct;
        Text = "";
    }

    /// <summary>
    ///     Reloads the logs when following them, unless a load is still running.
    /// </summary>
    internal void OnContainersUpdated()
    {
        if (Follow && _container is not null && !_isLoading)
        {
            _ = LoadAsync(_container);
        }
    }

    internal async Task LoadAsync(ContainerItem container)
    {
        // Overlapping fetches (follow ticks, manual reloads) can finish out of order; only the newest may write.
        int request = ++_request;
        int tail = double.IsNaN(Tail) || Tail < 1 ? DefaultTail : (int)Tail;

        _isLoading = true;
        try
        {
            await PaneText.LoadAsync(() => _details.GetLogsAsync(container.Id, tail, _ct), IsCurrent, logs => Text = logs.Length == 0 ? "(no output)" : logs);
        }
        finally
        {
            if (request == _request)
            {
                _isLoading = false;
            }
        }

        return;

        bool IsCurrent()
        {
            return request == _request && _container == container;
        }
    }

    [RelayCommand]
    private Task RefreshAsync() => _container is null ? Task.CompletedTask : LoadAsync(_container);

    partial void OnShowTimestampsChanged(bool value)
    {
        _settings.Current.ShowLogTimestamps = value;
        _settings.Save();
    }
}
