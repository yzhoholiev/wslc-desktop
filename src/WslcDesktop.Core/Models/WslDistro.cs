namespace WslcDesktop.Models;

public sealed record WslDistro(string Name, string State, string Version, bool IsDefault)
{
    public bool IsRunning => State.Equals("Running", StringComparison.OrdinalIgnoreCase);
}
