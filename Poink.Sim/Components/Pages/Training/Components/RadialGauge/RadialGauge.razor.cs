using System;
using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Training
{
    // Fixed 270-degree arc meter (90-degree gap at the bottom) for a single
    // ratio against a limit -- e.g. run progress or win rate on the Train
    // dashboard. Pure inline SVG, no JS, same InvariantCulture-coordinate and
    // native-<title>-tooltip conventions as RewardChart/TrendLineChart. Only
    // Value is ever animated (CSS transition on stroke-dasharray) -- the arc
    // geometry itself is a compile-time constant, never recomputed.
    public partial class RadialGauge : ComponentBase
    {
        private const double Radius = 44;
        private const double ArcFraction = 0.75; // 270 of 360 degrees
        private static readonly double ArcLength = 2 * Math.PI * Radius * ArcFraction;

        // The 270-degree arc from bottom-left (135 deg either side of straight
        // down) through the top to bottom-right, centered on (60,60) -- see
        // RadialGauge's plan notes for the trig behind these two literals.
        private const string ArcPath = "M 28.89 91.11 A 44 44 0 1 1 91.11 91.11";

        [Parameter] public double Value { get; set; }
        [Parameter] public double Min { get; set; }
        [Parameter] public double Max { get; set; } = 100;
        [Parameter] public string Label { get; set; } = "";
        [Parameter] public string ValueFormat { get; set; } = "N0";
        [Parameter] public string ValueSuffix { get; set; } = "";
        [Parameter] public string ColorVariant { get; set; } = "primary";
        [Parameter] public string Title { get; set; } = "";

        private double Fraction => Max > Min ? Math.Clamp((Value - Min) / (Max - Min), 0, 1) : 0;

        private static string FormatCoord(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        private string FillDash => $"{FormatCoord(Fraction * ArcLength)} {FormatCoord(ArcLength)}";

        private string FormattedValue => Value.ToString(ValueFormat, CultureInfo.InvariantCulture) + ValueSuffix;
    }
}
