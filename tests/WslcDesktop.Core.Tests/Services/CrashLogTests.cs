using System.Collections.Concurrent;
using WslcDesktop.Services;

namespace WslcDesktop.Tests.Services;

public sealed class CrashLogTests
{
    [Fact]
    public void Write_ForwardsToTheSink()
    {
        // Tests running alongside may log their own failures while this sink is installed.
        ConcurrentQueue<Exception> written = [];
        var failure = new InvalidOperationException("boom");

        CrashLog.UseSink(written.Enqueue);
        try
        {
            CrashLog.Write(failure);
        }
        finally
        {
            CrashLog.UseSink(null);
        }

        Assert.Single(written, exception => ReferenceEquals(exception, failure));
    }
}
