using System.Collections.Generic;

namespace Poink.Sim.Helpers
{

    public static class BarSizeOptions
    {
        public static readonly IReadOnlyList<int> Values = new[]
        {
            1, 2, 4, 8, 16, 32,
            60, 120, 240, 480, 960, 1920, 3840, 7680, 15360, 30720, 61440, 86400,
        };

        public static string Label(int seconds)
        {
            if (seconds < 60)
                return $"{seconds}s";
            int minutes = seconds / 60;
            if (seconds == 86400)
                return $"{minutes}m (1d)";
            if (minutes < 60)
                return $"{minutes}m";
            return $"{minutes}m (~{minutes / 60.0:0.#}h)";
        }
    }
}
