using Microsoft.Win32;
using System.Security;
using System.Text.Json;
using WslcDesktop.Models;

namespace WslcDesktop.Services;

/// <summary>
///     User preferences in a JSON file, since unpackaged apps have no ApplicationData store, and the start-with-Windows Run key entry.
/// </summary>
internal sealed class SettingsStore(string filePath) : ISettingsStore
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "WslcDesktop";

    public static string DefaultFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WslcDesktop", "settings.json");

    public AppSettings Current => field ??= Load();

    public bool IsStartWithWindowsEnabled
    {
        get
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
                return key?.GetValue(RunValueName) is string command
                    && command.Contains(Launcher.Executable, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
            {
                return false;
            }
        }
        set
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (value)
            {
                key.SetValue(RunValueName, $"\"{Launcher.Executable}\" {LaunchCommands.TrayArgument}");
            }
            else
            {
                key.DeleteValue(RunValueName, false);
            }
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            string temporary = filePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(Current, AppSettingsJsonContext.Default.AppSettings));
            File.Move(temporary, filePath, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CrashLog.Write(ex);
        }
    }

    /// <summary>
    ///     Copies an unreadable settings file aside, since the next save replaces it with defaults.
    /// </summary>
    private void KeepCorruptCopy()
    {
        try
        {
            File.Copy(filePath, filePath + ".bak", true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CrashLog.Write(ex);
        }
    }

    private AppSettings Load()
    {
        try
        {
            return File.Exists(filePath)
                ? JsonSerializer.Deserialize(File.ReadAllText(filePath), AppSettingsJsonContext.Default.AppSettings) ?? new AppSettings()
                : new AppSettings();
        }
        catch (JsonException)
        {
            KeepCorruptCopy();
            return new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }
}
