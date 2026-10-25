using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.ComponentModel;
using WinRT.Interop;
using WslcDesktop.Controls;
using WslcDesktop.Interop;
using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop;

public sealed partial class MainWindow
{
    private static readonly TimeSpan SuccessDisplayTime = TimeSpan.FromSeconds(5);

    private readonly nint _hwnd;
    private readonly WindowMessageHook _launchMessages;
    private readonly DispatcherTimer _notificationTimer = new() { Interval = SuccessDisplayTime };

    private readonly OverlappedPresenter _presenter = OverlappedPresenter.Create();
    private readonly TrayIcon _tray;
    private readonly TrayMenuHost _trayMenu = new();

    public MainWindow(AppServices services)
    {
        Services = services;
        InitializeComponent();

        _hwnd = WindowNative.GetWindowHandle(this);
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");

        // Under Native AOT, AppWindow.Presenter cannot be downcast to OverlappedPresenter, so own a typed instance.
        _presenter.PreferredMinimumWidth = 900;
        _presenter.PreferredMinimumHeight = 600;
        AppWindow.SetPresenter(_presenter);

        ApplyBackdrop(Services.Settings.Current.Backdrop);
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(iconPath);
        RestorePlacement();

        if (Elevation.IsElevated)
        {
            Title = AppIdentity.Name;
            AdministratorBadge.Visibility = Visibility.Visible;
        }

        _launchMessages = new WindowMessageHook(_hwnd, Program.ActivateMessage, ex => Services.Notifier.Error("Launch command failed", ex.Message));
        _launchMessages.Received += argument => Execute((LaunchCommand)argument);

        _tray = new TrayIcon(_hwnd, iconPath, AppIdentity.Name, ex => Services.Notifier.Error("Tray action failed", ex.Message));
        _tray.Activated += ShowAndActivate;
        AppIdentity.SetRelaunchTarget(_hwnd);
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => AppIdentity.SetJumpListTasks());
        _tray.ContextMenuRequested += OnTrayMenuRequested;

        _notificationTimer.Tick += (_, _) =>
        {
            _notificationTimer.Stop();
            NotificationBar.IsOpen = false;
        };

        AppWindow.Closing += OnClosing;
        Services.Notifier.Notified += OnNotified;
        Services.Monitor.Updated += OnRuntimeUpdated;
        Services.Monitor.PropertyChanged += OnMonitorPropertyChanged;
        Closed += (_, _) =>
        {
            AppIdentity.ClearRelaunchTarget(_hwnd);
            Services.Notifier.Notified -= OnNotified;
            Services.Monitor.Updated -= OnRuntimeUpdated;
            Services.Monitor.PropertyChanged -= OnMonitorPropertyChanged;
            _launchMessages.Dispose();
            _tray.Dispose();
            _trayMenu.Close();
        };

        LiveToggle.IsOn = Services.Monitor.IsLive;
        Nav.IsPaneOpen = Services.Settings.Current.IsNavigationPaneOpen;
        Nav.SelectedItem = Nav.MenuItems[0];
        OnRuntimeUpdated();
    }

    private AppServices Services { get; }

    private void OnLiveToggled(object sender, RoutedEventArgs e) => SetLive(LiveToggle.IsOn);

    private void SetLive(bool isLive)
    {
        if (Services.Monitor.IsLive == isLive)
        {
            return;
        }

        Services.Monitor.IsLive = isLive;
        if (isLive)
        {
            _ = Services.Monitor.RefreshAsync();
        }
    }

    /// <summary>
    ///     Keeps the live toggle in step with the monitor, since pausing stops the samples that refresh the rest of the title bar.
    /// </summary>
    private void OnMonitorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RuntimeMonitor.IsLive))
        {
            DispatcherQueue.TryEnqueue(() => LiveToggle.IsOn = Services.Monitor.IsLive);
        }
    }

    private void OnRuntimeUpdated()
    {
        RuntimeSnapshot snapshot = Services.Monitor.Latest;
        (string text, string dotStyle) = snapshot switch
        {
            { IsWslcMissing: true } => ("wslc not installed", "CriticalDotStyle"),
            { Error: not null } => ("wslc unavailable", "CriticalDotStyle"),
            { IsRunning: true, Containers.Count: 0 } => ("Running · no containers", "SuccessDotStyle"),
            { IsRunning: true } => (
                $"Running · {snapshot.RunningContainers} of {Humanize.Count(snapshot.Containers.Count, "container")} up",
                "SuccessDotStyle"),
            { Info: not null } => ("Runtime stopped", "NeutralDotStyle"),
            _ => ("Connecting…", "NeutralDotStyle")
        };

        RuntimeText.Text = text;
        RuntimeDot.Style = (Style)Application.Current.Resources[dotStyle];
        ShowCount(ContainersBadge, snapshot.IsRunning ? snapshot.Containers.Count : 0);
        ShowCount(ImagesBadge, snapshot.IsRunning ? snapshot.Inventory?.Images ?? 0 : 0);
        ShowCount(VolumesBadge, snapshot.IsRunning ? snapshot.Inventory?.Volumes ?? 0 : 0);
        ShowCount(NetworksBadge, snapshot.IsRunning ? snapshot.Inventory?.Networks ?? 0 : 0);
        _tray.SetTooltip($"{AppIdentity.Name} · {text}");
    }

    public void ApplyBackdrop(string name) => SystemBackdrop = name switch
    {
        BackdropNames.Mica => new MicaBackdrop(),
        BackdropNames.MicaAlt => new MicaBackdrop { Kind = MicaKind.BaseAlt },
        _ => new DesktopAcrylicBackdrop()
    };

    private static void ShowCount(InfoBadge badge, int count)
    {
        badge.Value = count;
        badge.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnNotified(Notification notification)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            // An InfoBar in a hidden or minimized window would go unseen.
            if (!AppWindow.IsVisible || _presenter.State == OverlappedPresenterState.Minimized)
            {
                _tray.ShowBalloon(notification.Title, notification.Message, notification.Severity == NotificationSeverity.Error);
                return;
            }

            NotificationBar.Severity = NotificationSeverities.ToInfoBar(notification.Severity);
            NotificationBar.Title = notification.Title;
            NotificationBar.Message = notification.Message;
            NotificationBar.IsOpen = true;

            _notificationTimer.Stop();
            if (notification.Severity != NotificationSeverity.Error)
            {
                _notificationTimer.Start();
            }
        });
    }
}
