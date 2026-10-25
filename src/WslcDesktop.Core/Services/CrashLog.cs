namespace WslcDesktop.Services;

/// <summary>
///     Records unexpected failures from anywhere, including native callbacks.
/// </summary>
public static class CrashLog
{
    private static Action<Exception>? _sink;

    public static void UseSink(Action<Exception>? sink) => Volatile.Write(ref _sink, sink);

    public static void Write(Exception exception) => Volatile.Read(ref _sink)?.Invoke(exception);
}
