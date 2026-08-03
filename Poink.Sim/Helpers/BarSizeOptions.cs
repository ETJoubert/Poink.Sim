using System.Collections.Generic;

namespace Poink.Sim.Helpers
{
    // Shared "Bar size" dropdown choices for ProfilerPage/SimulationPage/TrainPage/
    // TransformerPage -- values are in seconds so sub-minute bar sizes are
    // representable. Doubles from 1 second up through 32 seconds, jumps to the
    // 1-minute mark and keeps doubling from there up to 1024 minutes, then adds the
    // 1-day (1440-minute) cap on top since it's a meaningful bar size a pure
    // power-of-two wouldn't reach.
    public static class BarSizeOptions
    {
        public static readonly IReadOnlyList<int> Values = new[]
        {
            1, 2, 4, 8, 16, 32,
            60, 120, 240, 480, 960, 1920, 3840, 7680, 15360, 30720, 61440, 86400,
        };

        // "4s" / "64m (~1h)" / "1440m (1d)" -- seconds below a minute, minutes from
        // there (approx hour conversion only shown once the bar is an hour or more,
        // where the raw minute count stops being immediately readable).
        public static string Label(int seconds)
        {
            if (seconds < 60) return $"{seconds}s";
            int minutes = seconds / 60;
            if (seconds == 86400) return $"{minutes}m (1d)";
            if (minutes < 60) return $"{minutes}m";
            return $"{minutes}m (~{minutes / 60.0:0.#}h)";
        }
    }
}
