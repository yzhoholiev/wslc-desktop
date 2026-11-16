using System.Globalization;
using WslcDesktop.Services;

namespace WslcDesktop.ViewModels;

public sealed record VolumeItem(string Name, string DisplayName, string Driver, string Mountpoint, bool IsAnonymous, int ContainerCount, long? SizeBytes)
{
    public string Kind => IsAnonymous ? "Anonymous" : "Named";

    public bool IsUnused => ContainerCount == 0;

    public string Size => SizeBytes is { } bytes ? ByteSize.Format(bytes) : "—";

    public string? SizeInBytes => SizeBytes is { } bytes ? $"{bytes.ToString("N0", CultureInfo.CurrentCulture)} {(bytes == 1 ? "byte" : "bytes")}" : null;
}
