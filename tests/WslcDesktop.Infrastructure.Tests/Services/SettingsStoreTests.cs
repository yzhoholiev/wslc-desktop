using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop.Tests.Services;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), "WslcDesktop.Tests", $"store-{Guid.NewGuid():N}", "settings.json");

    public void Dispose()
    {
        string folder = Path.GetDirectoryName(_file)!;
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Current_StartsWithDefaultsWhenNoFileExists()
    {
        AppSettings settings = new SettingsStore(_file).Current;

        Assert.True(settings.KeepRunningInTray);
        Assert.False(settings.StartRuntimeOnLaunch);
        Assert.False(settings.StopRuntimeOnExit);
        Assert.True(settings.ShowLogTimestamps);
        Assert.True(settings.IsNavigationPaneOpen);
        Assert.Equal(BackdropNames.Acrylic, settings.Backdrop);
        Assert.Empty(settings.SortOrders);
        Assert.Null(settings.Window);
    }

    [Fact]
    public void Save_RoundTripsEverySetting()
    {
        var store = new SettingsStore(_file);
        AppSettings settings = store.Current;
        settings.KeepRunningInTray = false;
        settings.TrayHintShown = true;
        settings.StartRuntimeOnLaunch = true;
        settings.StopRuntimeOnExit = true;
        settings.ShowLogTimestamps = false;
        settings.IsNavigationPaneOpen = false;
        settings.Backdrop = BackdropNames.MicaAlt;
        settings.SortOrders["containers"] = "cpu:desc";
        settings.Window = new WindowPlacement { X = 10, Y = 20, Width = 1200, Height = 800, IsMaximized = true };

        store.Save();
        AppSettings reloaded = new SettingsStore(_file).Current;

        Assert.NotSame(settings, reloaded);
        Assert.False(reloaded.KeepRunningInTray);
        Assert.True(reloaded.TrayHintShown);
        Assert.True(reloaded.StartRuntimeOnLaunch);
        Assert.True(reloaded.StopRuntimeOnExit);
        Assert.False(reloaded.ShowLogTimestamps);
        Assert.False(reloaded.IsNavigationPaneOpen);
        Assert.Equal(BackdropNames.MicaAlt, reloaded.Backdrop);
        Assert.Equal("cpu:desc", reloaded.SortOrders["containers"]);
        Assert.Equal((10, 20, 1200, 800, true),
            (reloaded.Window!.X, reloaded.Window.Y, reloaded.Window.Width, reloaded.Window.Height, reloaded.Window.IsMaximized));
    }

    [Fact]
    public void Current_FallsBackToDefaultsForACorruptFileAndKeepsACopyOfIt()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        File.WriteAllText(_file, "{ not json");
        var store = new SettingsStore(_file);

        Assert.True(store.Current.KeepRunningInTray);

        store.Save();

        Assert.Equal("{ not json", File.ReadAllText(_file + ".bak"));
    }

    [Fact]
    public void Current_ReplacesNullsInAHandEditedFileWithDefaults()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        File.WriteAllText(_file, """{ "SortOrders": null, "Backdrop": null }""");

        AppSettings settings = new SettingsStore(_file).Current;

        Assert.Empty(settings.SortOrders);
        Assert.Equal(BackdropNames.Acrylic, settings.Backdrop);
    }
}
