using WslcDesktop.Models;
using WslcDesktop.ViewModels;
using static WslcDesktop.Tests.Snapshots;

namespace WslcDesktop.Tests.ViewModels;

public sealed class WslcViewModelTests
{
    private readonly TestServices _test = new();

    [Fact]
    public void Load_DescribesTheDefaultsWithoutASettingsFile()
    {
        var viewModel = new WslcViewModel(_test.Services);

        viewModel.Load();

        Assert.Equal("No settings.yaml — wslc uses its built-in defaults.", viewModel.SettingsText);
        Assert.Equal($"{Environment.ProcessorCount} (default: all logical processors)", viewModel.SessionSettings[0].Value);
        Assert.Equal(_test.WslcSettings.FilePath, viewModel.SettingsFilePath);
    }

    [Fact]
    public void Load_ShowsTheSettingsTheFileSets()
    {
        _test.WslcSettings.Text = "session:\n  cpuCount: 4\n  memorySize: 8GB\n\n";
        var viewModel = new WslcViewModel(_test.Services);

        viewModel.Load();

        Assert.Equal("session:\n  cpuCount: 4\n  memorySize: 8GB", viewModel.SettingsText);
        Assert.Equal([new KeyValueEntry("CPUs", "4"), new KeyValueEntry("Memory", "8 GiB")], viewModel.SessionSettings.Take(2));
    }

    [Fact]
    public void Apply_ShowsWhatTheRunningVmReports()
    {
        var viewModel = new WslcViewModel(_test.Services);
        RuntimeSnapshot running = Running() with { Vm = new VmMetrics(12, 3.7, 31L << 30, 27L << 30, 8L << 30, 8L << 30, "0.23", TimeSpan.FromHours(1)) };

        viewModel.Apply(running);

        Assert.Equal("12 vCPUs · 31 GiB memory · 8 GiB swap", viewModel.VmText);
    }

    [Fact]
    public void Apply_SaysWhenTheVmIsNotRunning()
    {
        var viewModel = new WslcViewModel(_test.Services);

        viewModel.Apply(Stopped());

        Assert.Equal("Not running — the next start uses the settings above.", viewModel.VmText);
    }

    [Fact]
    public void Apply_ListsTheWslcVersions()
    {
        var viewModel = new WslcViewModel(_test.Services);

        viewModel.Apply(Stopped());

        Assert.Equal(["wslc", "Session manager", "Data source"], viewModel.Versions.Select(entry => entry.Key));
        Assert.Equal(["3.0.3.0", "3.0.3"], viewModel.Versions.Take(2).Select(entry => entry.Value));
    }

    [Fact]
    public void Apply_ListsSessionsById()
    {
        var viewModel = new WslcViewModel(_test.Services);

        viewModel.Apply(RuntimeSnapshot.Empty with { Info = Info("wslc-cli-b", "wslc-cli-a") });

        Assert.Equal([new WslcSessionItem(1, "wslc-cli-b", 100), new WslcSessionItem(2, "wslc-cli-a", 101)], viewModel.Sessions);
        Assert.False(viewModel.HasNoSessions);
    }

    [Fact]
    public void Apply_FollowsTheChosenSessionSort()
    {
        var viewModel = new WslcViewModel(_test.Services);
        viewModel.Apply(RuntimeSnapshot.Empty with { Info = Info("wslc-cli-b", "wslc-cli-a") });

        viewModel.SessionSort.ChangeCommand.Execute("name");

        Assert.Equal(["wslc-cli-a", "wslc-cli-b"], viewModel.Sessions.Select(session => session.Name));
    }

    [Fact]
    public void Apply_ReportsNoSessionsWithoutInfo()
    {
        var viewModel = new WslcViewModel(_test.Services);

        viewModel.Apply(RuntimeSnapshot.Empty);

        Assert.Empty(viewModel.Sessions);
        Assert.True(viewModel.HasNoSessions);
    }
}
