using WslcDesktop.Models;

namespace WslcDesktop.ViewModels;

public sealed record NetworkItem(string Id, string ShortId, string Name, string Driver, bool IsInternal, string Created, int ContainerCount)
{
    public bool IsBuiltIn => NetworkDto.IsBuiltInName(Name);

    public bool CanRemove => !IsBuiltIn;

    public string Kind => IsBuiltIn ? "Built-in" : IsInternal ? "Internal" : "User-defined";
}
