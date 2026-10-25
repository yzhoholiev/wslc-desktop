namespace WslcDesktop.ViewModels;

public sealed record VolumeItem(string Name, string DisplayName, string Driver, string Mountpoint, bool IsAnonymous, int ContainerCount)
{
    public string Kind => IsAnonymous ? "Anonymous" : "Named";

    public bool IsUnused => ContainerCount == 0;
}
