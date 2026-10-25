namespace WslcDesktop.Services;

/// <summary>
///     The processes that host wslc sessions, one per running session, listed without starting anything.
/// </summary>
public interface ISessionHosts
{
    int[] ListProcessIds();
}
