namespace WslcDesktop.ViewModels;

/// <summary>
///     One of the dashboard's top consumers; the share is its bar's width from 0 to 1.
/// </summary>
public sealed record UsageBar(string Name, string Value, double Share);
