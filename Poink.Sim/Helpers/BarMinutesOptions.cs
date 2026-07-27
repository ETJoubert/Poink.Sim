using System.Collections.Generic;

namespace Poink.Sim.Helpers
{
    // Shared "Bar minutes" dropdown choices for ProfilerPage/SimulationPage -- doubles
    // from 1 minute up to 1024, then adds the 1-day (1440) cap on top since it's a
    // meaningful bar size (a full trading day) a pure power-of-two wouldn't reach.
    public static class BarMinutesOptions
    {
        public static readonly IReadOnlyList<int> Values = new[] { 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 1440 };

        // "64m (~1h)" / "1440m (1d)" -- approx conversion only shown once the bar is an
        // hour or more, where the raw minute count stops being immediately readable.
        public static string Label(int minutes)
        {
            if (minutes < 60) return $"{minutes}m";
            if (minutes == 1440) return $"{minutes}m (1d)";
            return $"{minutes}m (~{minutes / 60.0:0.#}h)";
        }
    }
}
