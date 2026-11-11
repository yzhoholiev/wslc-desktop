using Microsoft.Win32;
using System.Security;
using System.Text.Json;
using System.Text.Json.Nodes;
using WslcDesktop.Models;

namespace WslcDesktop.Services;

/// <summary>
///     User preferences in a JSON file, since unpackaged apps have no ApplicationData store, and the start-with-Windows Run key entry.
/// </summary>
internal sealed class SettingsStore(string filePath) : ISettingsStore
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "WslcDesktop";

    /// <summary>
    ///     Scanners and the app's other instance hold the file only briefly.
    /// </summary>
    private const int ReadAttempts = 3;

    private static readonly TimeSpan ReadRetryDelay = TimeSpan.FromMilliseconds(100);

    private bool _isUnreadable;
    private JsonObject? _saved;

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
        if (_isUnreadable)
        {
            return;
        }

        string temporary = $"{filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            JsonObject current = Serialize(Current);
            File.WriteAllText(temporary, MergeWithFile(current).ToJsonString(AppSettingsJsonContext.Default.Options));
            File.Move(temporary, filePath, true);
            _saved = current;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CrashLog.Write(ex);
            TryDelete(temporary);
        }
    }

    /// <summary>
    ///     The settings to write: this instance's changes since its last save, over what the file holds now.
    /// </summary>
    private JsonObject MergeWithFile(JsonObject current)
    {
        if (_saved is not { } saved || ReadFile() is not { } onDisk)
        {
            return current;
        }

        var merged = new JsonObject();
        foreach ((string name, JsonNode? value) in current)
        {
            bool changedHere = !JsonNode.DeepEquals(value, saved[name]);
            JsonNode? kept = changedHere || !onDisk.ContainsKey(name) ? value : onDisk[name];
            merged[name] = kept?.DeepClone();
        }

        return merged;
    }

    private JsonObject? ReadFile()
    {
        try
        {
            return File.Exists(filePath) ? JsonNode.Parse(File.ReadAllText(filePath)) as JsonObject : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static JsonObject Serialize(AppSettings settings) =>
        JsonSerializer.SerializeToNode(settings, AppSettingsJsonContext.Default.AppSettings)!.AsObject();

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
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
        AppSettings settings = Read();
        _saved = Serialize(settings);
        return settings;
    }

    private AppSettings Read()
    {
        for (int attempt = 1; ; attempt++)
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
                if (attempt < ReadAttempts)
                {
                    Thread.Sleep(ReadRetryDelay);
                    continue;
                }

                // Saving the defaults used instead would replace settings that are intact but could not be read.
                CrashLog.Write(ex);
                _isUnreadable = true;
                return new AppSettings();
            }
        }
    }
}
