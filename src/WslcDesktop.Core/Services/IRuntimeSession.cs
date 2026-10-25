namespace WslcDesktop.Services;

public enum RuntimeDataSource
{
    None,
    Engine,
    Cli
}

/// <summary>
///     The connection the reads hold to the session, which must be closed for the session VM to idle out.
/// </summary>
public interface IRuntimeSession
{
    /// <summary>
    ///     Whether the connection stays open between reads; when false it closes shortly after the last read.
    /// </summary>
    bool KeepConnected { get; set; }

    bool IsConnected { get; }

    /// <summary>
    ///     Why the connection was last given up for the fallback.
    /// </summary>
    string? LastConnectionError { get; }

    /// <summary>
    ///     Which reader answered the latest read, or none while disconnected.
    /// </summary>
    RuntimeDataSource Source { get; }

    /// <summary>
    ///     Closes the connection, so an idle or stopped runtime is not kept alive by this app.
    /// </summary>
    void Disconnect();
}
