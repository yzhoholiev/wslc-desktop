using WslcDesktop.Models;

namespace WslcDesktop.Services;

/// <summary>
///     Recent CPU and memory use of the session VM.
/// </summary>
public sealed class MetricsHistory
{
    /// <summary>
    ///     How many samples each series keeps.
    /// </summary>
    public const int Length = 60;

    private readonly Queue<double> _cpu = new();
    private readonly Queue<double> _memory = new();

    public double[] Cpu { get; private set; } = [];

    public double[] Memory { get; private set; } = [];

    /// <summary>
    ///     Adds the snapshot's measurements, or starts over once the runtime has stopped.
    /// </summary>
    public void Record(RuntimeSnapshot snapshot)
    {
        if (snapshot.Vm is { } vm)
        {
            Cpu = Append(_cpu, vm.CpuPercent);
            Memory = Append(_memory, vm.MemoryPercent);
        }
        else if (!snapshot.IsRunning)
        {
            _cpu.Clear();
            _memory.Clear();
            Cpu = [];
            Memory = [];
        }
    }

    private static double[] Append(Queue<double> history, double value)
    {
        history.Enqueue(value);
        while (history.Count > Length)
        {
            history.Dequeue();
        }

        return [.. history];
    }
}
