namespace WslcDesktop.Models;

public sealed record WslDistro(string Name, string State, string Version, bool IsDefault)
{
    public bool IsRunning => State.Equals("Running", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
///     A distribution WSL can install, with the name its install command takes.
/// </summary>
public sealed record AvailableDistro(string Name, string FriendlyName);
