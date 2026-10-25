namespace WslcDesktop.Services;

public enum NotificationSeverity
{
    Success,
    Error
}

public sealed record Notification(NotificationSeverity Severity, string Title, string Message);

/// <summary>
///     The app's channel for operation results.
/// </summary>
public sealed class Notifier
{
    public event Action<Notification>? Notified;

    public void Success(string message) => Notified?.Invoke(new Notification(NotificationSeverity.Success, "Done", message));

    public void Error(string title, string message) => Notified?.Invoke(new Notification(NotificationSeverity.Error, title, message));

    /// <summary>
    ///     Runs a user-initiated operation, reports its outcome, and returns whether it succeeded.
    /// </summary>
    public async Task<bool> RunAsync(string description, Func<Task> operation, string? successMessage = null)
    {
        try
        {
            await operation();
            if (successMessage is not null)
            {
                Success(successMessage);
            }

            return true;
        }
        catch (CliException ex)
        {
            Error($"{description} failed", ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            // Callers often start operations without awaiting them, where an exception would end the app or go unseen.
            CrashLog.Write(ex);
            Error($"{description} failed", ex.Message);
            return false;
        }
    }
}
