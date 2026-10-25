using System.Diagnostics.CodeAnalysis;

namespace WslcDesktop.Models;

public sealed class AppSettings
{
    public bool KeepRunningInTray { get; set; } = true;

    public bool TrayHintShown { get; set; }

    public bool StartRuntimeOnLaunch { get; set; }

    public bool StopRuntimeOnExit { get; set; }

    [AllowNull]
    public string Backdrop { get; set => field = value ?? BackdropNames.Acrylic; } = BackdropNames.Acrylic;

    /// <summary>
    ///     Sort order per list page, as "column:asc" or "column:desc".
    /// </summary>
    [AllowNull]
    public Dictionary<string, string> SortOrders { get; set => field = value ?? []; } = [];

    public bool ShowLogTimestamps { get; set; } = true;

    public bool IsNavigationPaneOpen { get; set; } = true;

    /// <summary>
    ///     What the last cleanup removed, offered again next time.
    /// </summary>
    public CleanupOptions? LastCleanup { get; set; }

    public WindowPlacement? Window { get; set; }
}

public static class BackdropNames
{
    public const string Acrylic = "Acrylic";
    public const string Mica = "Mica";
    public const string MicaAlt = "MicaAlt";
}

/// <summary>
///     Restored (non-maximized) window bounds in physical pixels, plus whether the window was maximized.
/// </summary>
public sealed class WindowPlacement
{
    public int X { get; set; }

    public int Y { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public bool IsMaximized { get; set; }
}
