using WslcDesktop.Services;

namespace WslcDesktop.ViewModels;

/// <summary>
///     Loads the text of a details pane, such as logs or inspect output.
/// </summary>
internal static class PaneText
{
    /// <summary>
    ///     Shows what the read returns, or wslc's error in its place.
    /// </summary>
    /// <param name="read">Reads the text.</param>
    /// <param name="isCurrent">Whether the pane still shows what this read is for.</param>
    /// <param name="show">Puts the text, or the error message, in the pane.</param>
    public static async Task LoadAsync(Func<Task<string>> read, Func<bool> isCurrent, Action<string> show)
    {
        try
        {
            string text = await read();
            if (isCurrent())
            {
                show(text);
            }
        }
        catch (CliException ex)
        {
            if (isCurrent())
            {
                show(ex.Message);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
