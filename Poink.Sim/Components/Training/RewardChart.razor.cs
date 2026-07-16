using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Training
{
    // Pure Razor + inline SVG, no JS -- a single series needs no legend (per the
    // dataviz skill), so this is just a thin 2px line + a subtle zero-baseline
    // reference + a hover title per point (a native SVG <title>, no JS tooltip
    // needed). All coordinate math must format with InvariantCulture: SVG's
    // "points"/"x1" etc. attributes use '.' as the decimal separator and ','
    // as the coordinate separator, so a comma-decimal locale (this host's) would
    // silently corrupt the geometry if formatted with the current culture.
    public partial class RewardChart : ComponentBase
    {
        private const double ViewWidth = 600;
        private const double ViewHeight = 160;
        private const double PadLeft = 8;
        private const double PadRight = 8;
        private const double PadTop = 10;
        private const double PadBottom = 10;

        [Parameter] public IReadOnlyList<RlEpisodeMetric> Episodes { get; set; } = Array.Empty<RlEpisodeMetric>();
        [Parameter] public bool IsRunning { get; set; }

        private sealed record ChartPoint(string X, string Y, int Episode, double Reward, bool IsLatest);

        private static double PlotWidth => ViewWidth - PadLeft - PadRight;
        private static double PlotHeight => ViewHeight - PadTop - PadBottom;

        private (double Min, double Max) RewardRange
        {
            get
            {
                if (Episodes.Count == 0) return (-1, 1);
                double min = Episodes.Min(e => e.Reward);
                double max = Episodes.Max(e => e.Reward);
                if (Math.Abs(max - min) < 1e-6) { min -= 1; max += 1; }
                return (min, max);
            }
        }

        private double MapX(int index) =>
            Episodes.Count <= 1 ? PadLeft + PlotWidth / 2 : PadLeft + index * (PlotWidth / (Episodes.Count - 1));

        private double MapY(double reward)
        {
            var (min, max) = RewardRange;
            double t = (reward - min) / (max - min);
            return PadTop + (1 - t) * PlotHeight;
        }

        private static string FormatCoord(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        private string BaselineY => FormatCoord(MapY(0));

        private List<ChartPoint> Points =>
            Episodes.Select((e, i) => new ChartPoint(
                FormatCoord(MapX(i)), FormatCoord(MapY(e.Reward)), e.EpisodeNumber, e.Reward, i == Episodes.Count - 1))
                .ToList();

        private string PolylinePoints => string.Join(" ", Points.Select(p => $"{p.X},{p.Y}"));
    }
}
