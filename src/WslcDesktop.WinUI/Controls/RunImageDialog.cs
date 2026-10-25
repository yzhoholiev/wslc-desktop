using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop.Controls;

/// <summary>
///     Asks for the options of a new container from an image and creates it.
/// </summary>
public static class RunImageDialog
{
    /// <summary>
    ///     Collects the options and creates the container.
    /// </summary>
    /// <param name="xamlRoot">The root the dialog shows over.</param>
    /// <param name="defaults">The image's own defaults, shown as placeholders and pre-filled rows.</param>
    /// <param name="create">Creates the container, and starts it when asked; returns its ID.</param>
    /// <returns>The new container's ID and whether it was started, or null when cancelled.</returns>
    public static async Task<(string Id, bool Started)?> ShowAsync(XamlRoot xamlRoot, RunImageDefaults defaults,
        Func<ContainerRunOptions, bool, Task<string>> create)
    {
        var form = new RunImageForm(defaults);
        ContentDialog dialog = Dialogs.Create(xamlRoot, $"Run {defaults.Reference}", form.Content, "Run");
        dialog.SecondaryButtonText = "Create";
        dialog.DefaultButton = ContentDialogButton.Primary;
        dialog.Resources["ContentDialogMaxWidth"] = 720.0;

        var submission = new Submission(dialog, form, create);
        dialog.PrimaryButtonClick += (_, args) => submission.Submit(args, true);
        dialog.SecondaryButtonClick += (_, args) => submission.Submit(args, false);

        form.TrackWindow(xamlRoot);
        try
        {
            return await Dialogs.ShowAsync(dialog) == ContentDialogResult.None ? null : submission.Created;
        }
        finally
        {
            form.StopTrackingWindow();
        }
    }

    /// <summary>
    ///     Creates the container when Run or Create is clicked, holding the dialog open until wslc answers.
    /// </summary>
    private sealed class Submission(ContentDialog dialog, RunImageForm form, Func<ContainerRunOptions, bool, Task<string>> create)
    {
        public (string Id, bool Started)? Created { get; private set; }

        public void Submit(ContentDialogButtonClickEventArgs args, bool start)
        {
            ContainerRunOptions options = form.Collect();
            try
            {
                WslcCli.CreateArguments(options, start);
            }
            catch (FormatException ex)
            {
                form.ShowError($"The command cannot be read: {ex.Message}");
                args.Cancel = true;
                return;
            }

            _ = CreateAsync(options, start, args, args.GetDeferral());
        }

        private async Task CreateAsync(ContainerRunOptions options, bool start, ContentDialogButtonClickEventArgs args,
            ContentDialogButtonClickDeferral deferral)
        {
            SetBusy(true);
            try
            {
                Created = (await create(options, start), start);
            }
            catch (CliException ex)
            {
                form.ShowError(ex.Message);
                args.Cancel = true;
            }
            catch (Exception ex)
            {
                CrashLog.Write(ex);
                form.ShowError(ex.Message);
                args.Cancel = true;
            }
            finally
            {
                SetBusy(false);
                deferral.Complete();
            }
        }

        private void SetBusy(bool busy)
        {
            dialog.IsPrimaryButtonEnabled = !busy;
            dialog.IsSecondaryButtonEnabled = !busy;
            form.ShowProgress(busy);
        }
    }
}
