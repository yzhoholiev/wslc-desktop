namespace WslcDesktop.Services;

/// <summary>
///     A reader that holds a connection to the session, which must be closed for the session VM to idle out.
/// </summary>
internal interface IRuntimeConnection : IRuntimeReader, IDisposable
{
    bool IsConnected { get; }
}
