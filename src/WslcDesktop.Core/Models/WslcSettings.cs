namespace WslcDesktop.Models;

/// <summary>
///     The values set in wslc's settings.yaml; null leaves wslc's built-in default.
/// </summary>
public sealed record WslcSettings(
    int? CpuCount = null,
    long? MemorySize = null,
    long? MaxStorageSize = null,
    string? StoragePath = null,
    string? DefaultBindingAddress = null,
    string? HostLoopback = null,
    int? IdleTimeoutSeconds = null,
    string? CredentialStore = null);
