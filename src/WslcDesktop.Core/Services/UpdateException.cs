namespace WslcDesktop.Services;

/// <summary>
///     A check, download or installation of an app update failed; the message is shown to the user.
/// </summary>
public sealed class UpdateException(string message, Exception? innerException = null) : Exception(message, innerException);
