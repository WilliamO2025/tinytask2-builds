using System;
using System.Collections.Generic;

namespace TinyTask;

// Compile once before starting the clock. Execution never changes recorded timing.
internal sealed class PlaybackTimeline
{
    private readonly double[] offsets;
    internal int Count => offsets.Length;
    internal double Duration { get; }

    internal PlaybackTimeline(IReadOnlyList<double> delays, double speed)
    {
        if (!double.IsFinite(speed) || speed < 0.01 || speed > 1000)
            throw new ArgumentException("Speed must be between 0.01 and 1000.");
        offsets = new double[delays.Count];
        double original = 0;
        for (int i = 0; i < delays.Count; i++)
        {
            if (!double.IsFinite(delays[i]) || delays[i] < 0 || delays[i] > 86400)
                throw new ArgumentException("Invalid recorded delay.");
            original += delays[i];
            offsets[i] = original / speed;
        }
        Duration = original / speed;
    }

    internal double Due(long loop, int index, double startDelay = 0)
    {
        if (loop < 0 || !double.IsFinite(startDelay) || startDelay < 0)
            throw new ArgumentOutOfRangeException();
        return startDelay + loop * Duration + offsets[index];
    }
}
