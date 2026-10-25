namespace WslcDesktop.Services;

/// <summary>
///     A wslc or wsl.exe command failed; its message is what the tool reported.
/// </summary>
public class CliException(string message) : Exception(message);

/// <summary>
///     The executable a command needs is not installed.
/// </summary>
public sealed class ToolNotFoundException(string message) : CliException(message);

/// <summary>
///     Raised when the session behind the session shell is gone, as opposed to a single command failing.
/// </summary>
public sealed class SessionEndedException(string message) : CliException(message);
