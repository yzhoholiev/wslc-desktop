namespace WslcDesktop.ViewModels;

/// <summary>
///     A running container that publishes ports, with the ports in the CLI's notation.
/// </summary>
public sealed record EndpointRow(string Id, string Name, string Ports);
