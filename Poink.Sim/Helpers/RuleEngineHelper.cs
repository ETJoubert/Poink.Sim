using System;
using System.Collections.Generic;

namespace Poink.Sim.Helpers;

public readonly record struct RuleSignalRow(
    string RowLabel,
    string ColorVar,
    string AboveKey,
    string InKey,
    string BelowKey,
    bool Above,
    bool In,
    bool Below
);

public readonly record struct RuleEngineResult(
    double? Poc,
    double? Vah,
    double? Val,
    double? Vwap,
    double? Median,
    double? Regression,
    double CurrentPrice,
    IReadOnlyList<RuleSignalRow> Rows
);

// Baseline signals -- current price vs POC/VAH/VAL/VWAP/Median/Regression from RangeStatsHelper, independent of any XGrid row/column grid.
internal static class RuleEngineHelper
{
    public static class Keys
    {
        public const string PocAbove = "poc-above";
        public const string PocIn = "poc-in";
        public const string PocBelow = "poc-below";
        public const string VahAbove = "vah-above";
        public const string VahIn = "vah-in";
        public const string VahBelow = "vah-below";
        public const string ValAbove = "val-above";
        public const string ValIn = "val-in";
        public const string ValBelow = "val-below";
        public const string VwapAbove = "vwap-above";
        public const string VwapIn = "vwap-in";
        public const string VwapBelow = "vwap-below";
        public const string MedianAbove = "median-above";
        public const string MedianIn = "median-in";
        public const string MedianBelow = "median-below";
        public const string RegressionAbove = "regression-above";
        public const string RegressionIn = "regression-in";
        public const string RegressionBelow = "regression-below";
    }

    public static RuleEngineResult Compute<TSample, TBar>(
        IReadOnlyList<TSample> volumeSamples,
        Func<TSample, double> samplePrice,
        Func<TSample, double> sampleWeight,
        IReadOnlyList<TBar> vwapBars,
        Func<TBar, double> barPrice,
        Func<TBar, double> barWeight,
        Func<TBar, double> barClose,
        double currentPrice
    )
    {
        var priceStats = RangeStatsHelper.Compute(volumeSamples, samplePrice, sampleWeight);
        var vwapStats = RangeStatsHelper.Compute(vwapBars, barPrice, barWeight);
        var regressionStats = RangeStatsHelper.Compute(vwapBars, barClose);

        var poc = priceStats?.Poc;
        var vah = priceStats?.Vah;
        var val = priceStats?.Val;
        var vwap = vwapStats?.Vwap;
        var median = priceStats?.Median;
        var regression = regressionStats?.Regression;

        var rows = new[]
        {
            Row("POC", "--color-primary", Keys.PocAbove, Keys.PocIn, Keys.PocBelow, poc, currentPrice),
            Row("VAH", "--color-highlight", Keys.VahAbove, Keys.VahIn, Keys.VahBelow, vah, currentPrice),
            Row("VAL", "--color-highlight", Keys.ValAbove, Keys.ValIn, Keys.ValBelow, val, currentPrice),
            Row("VWAP", "--color-tertiary", Keys.VwapAbove, Keys.VwapIn, Keys.VwapBelow, vwap, currentPrice),
            Row("MEDIAN", "--color-badge-2", Keys.MedianAbove, Keys.MedianIn, Keys.MedianBelow, median, currentPrice),
            Row("REG", "--color-badge-1", Keys.RegressionAbove, Keys.RegressionIn, Keys.RegressionBelow, regression, currentPrice),
        };

        return new RuleEngineResult(poc, vah, val, vwap, median, regression, currentPrice, rows);
    }

    private static RuleSignalRow Row(
        string label,
        string colorVar,
        string aboveKey,
        string inKey,
        string belowKey,
        double? level,
        double currentPrice
    ) =>
        new(
            label,
            colorVar,
            aboveKey,
            inKey,
            belowKey,
            level is { } l && currentPrice > l,
            level is { } l2 && currentPrice == l2,
            level is { } l3 && currentPrice < l3
        );
}
