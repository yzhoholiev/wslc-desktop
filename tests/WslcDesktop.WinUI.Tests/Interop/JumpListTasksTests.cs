using WslcDesktop.Interop;
using WslcDesktop.Services;

namespace WslcDesktop.Tests.Interop;

public sealed class JumpListTasksTests
{
    private static readonly JumpListTask[] Tasks = [.. JumpListTasks.All.OfType<JumpListTask>()];

    [Fact]
    public void JumpListTasks_StartTheLauncherWithEveryCommandInOrder()
    {
        JumpListItem[] items = JumpListTasks.All;
        int separator = Array.FindIndex(items, item => item is JumpListSeparator);

        Assert.All(Tasks, task => Assert.Equal(Launcher.Executable, task.ProgramPath));
        Assert.Equal(Enum.GetValues<LaunchCommand>().Where(command => command != LaunchCommand.None),
            Tasks.Select(task => LaunchCommands.Parse([task.Arguments])));
        Assert.Equal(4, separator);
        Assert.Equal(LaunchCommand.ShowWsl, LaunchCommands.Parse([Assert.IsType<JumpListTask>(items[separator - 1]).Arguments]));
        Assert.Equal(LaunchCommand.StartRuntime, LaunchCommands.Parse([Assert.IsType<JumpListTask>(items[separator + 1]).Arguments]));
        Assert.Single(items, item => item is JumpListSeparator);
    }

    [Fact]
    public void JumpListTasks_HaveTheirIconsInTheApp()
    {
        string assets = Path.Combine(AppContext.BaseDirectory, "Assets", "JumpList");

        Assert.All(Tasks,
            task => Assert.True(Path.GetDirectoryName(task.IconPath) == assets && File.Exists(task.IconPath), $"{task.Title} has no icon in {assets}"));
    }
}
