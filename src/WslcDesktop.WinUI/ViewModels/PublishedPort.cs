namespace WslcDesktop.ViewModels;

/// <summary>
///     One published port, parsed from the CLI's "127.0.0.1:8080->80/tcp" notation.
/// </summary>
public sealed record PublishedPort(string Address, int? HostPort, int ContainerPort, string Protocol)
{
    public string Label => HostPort is { } host ? $"{host} → {ContainerPort}" : $"{ContainerPort}/{Protocol}";

    public string Description => HostPort is { } host
        ? $"{Address}:{host} → {ContainerPort}/{Protocol}"
        : $"{ContainerPort}/{Protocol} (not published)";

    public Uri? Url => HostPort is { } host && Protocol == "tcp" && Uri.TryCreate($"http://{UrlHost(Address)}:{host}/", UriKind.Absolute, out Uri? url)
        ? url
        : null;

    private static string UrlHost(string address) => address switch
    {
        "" or "0.0.0.0" or "::" or "[::]" => "localhost",
        _ when address.Contains(':') && !address.StartsWith('[') => $"[{address}]",
        _ => address
    };

    public static PublishedPort[] ParseList(string ports) =>
        [.. ports.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(Parse).OfType<PublishedPort>()];

    private static PublishedPort? Parse(string text)
    {
        (string? binding, string? target) = text.Split("->") is [var left, var right] ? (left, right) : ("", text);
        string protocol = target.Contains('/') ? target[(target.IndexOf('/') + 1)..] : "tcp";
        if (!int.TryParse(target.Split('/')[0], out int containerPort))
        {
            return null;
        }

        if (binding.Length == 0)
        {
            return new PublishedPort("", null, containerPort, protocol);
        }

        int separator = binding.LastIndexOf(':');
        return int.TryParse(binding[(separator + 1)..], out int hostPort)
            ? new PublishedPort(separator > 0 ? binding[..separator] : "", hostPort, containerPort, protocol)
            : null;
    }
}
