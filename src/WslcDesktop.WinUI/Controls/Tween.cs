using Microsoft.UI.Dispatching;
using System.Diagnostics;
using Windows.UI.ViewManagement;

namespace WslcDesktop.Controls;

/// <summary>
///     Eases a number towards a target on the UI thread, jumping straight there when Windows animations are off.
/// </summary>
internal sealed class Tween(Action<double> apply)
{
    private const double Epsilon = 1e-9;
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(450);
    private static readonly UISettings Settings = new();
    private double _from;
    private long _started;

    private DispatcherQueueTimer? _timer;
    private double _to;

    public double Current { get; private set; } = double.NaN;

    public void To(double target)
    {
        if (Math.Abs(target - _to) < Epsilon && !double.IsNaN(Current))
        {
            return;
        }

        _to = target;
        if (double.IsNaN(Current) || double.IsNaN(target) || !Settings.AnimationsEnabled)
        {
            _timer?.Stop();
            Set(target);
            return;
        }

        _from = Current;
        _started = Stopwatch.GetTimestamp();
        if (_timer is null)
        {
            _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(16);
            _timer.Tick += OnTick;
        }

        _timer.Start();
    }

    private void OnTick(DispatcherQueueTimer timer, object args)
    {
        double progress = Math.Min(1, Stopwatch.GetElapsedTime(_started) / Duration);
        double eased = 1 - Math.Pow(1 - progress, 3);
        Set(_from + (_to - _from) * eased);
        if (progress >= 1)
        {
            timer.Stop();
        }
    }

    private void Set(double value)
    {
        Current = value;
        apply(value);
    }
}
