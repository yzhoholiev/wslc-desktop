using WslcDesktop.Models;

namespace WslcDesktop.Services;

/// <summary>
///     The user's preferences and whether the app starts with Windows.
/// </summary>
public interface ISettingsStore
{
    AppSettings Current { get; }

    bool IsStartWithWindowsEnabled { get; set; }

    /// <summary>
    ///     The settings as the file holds them now, which the app's other instance may have changed, or null when it cannot be read.
    /// </summary>
    AppSettings? ReadSaved();

    void Save();
}
