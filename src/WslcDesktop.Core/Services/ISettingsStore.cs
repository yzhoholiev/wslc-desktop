using WslcDesktop.Models;

namespace WslcDesktop.Services;

/// <summary>
///     The user's preferences and whether the app starts with Windows.
/// </summary>
public interface ISettingsStore
{
    AppSettings Current { get; }

    bool IsStartWithWindowsEnabled { get; set; }

    void Save();
}
