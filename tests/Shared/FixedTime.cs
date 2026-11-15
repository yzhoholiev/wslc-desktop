namespace WslcDesktop.Tests;

/// <summary>
///     A clock that stands still, so a test can name the time something happened.
/// </summary>
internal sealed class FixedTime : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
}
