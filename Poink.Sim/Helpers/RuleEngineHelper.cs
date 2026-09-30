using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Components.Charts;

namespace Poink.Sim.Helpers;

public enum RuleSignalGroup
{
    Baseline,
    XGrid,
    TxFold,
}

public readonly record struct RuleSignalRow(
    string RowLabel,
    string ColorVar,
    string? AboveKey,
    string InKey,
    string? BelowKey,
    bool Above,
    bool In,
    bool Below,
    RuleSignalGroup Group
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

// Baseline signals come from RangeStatsHelper alone; Compute optionally appends XGrid's rank/delta rows on top when a grid is supplied.
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
        public const string RankTotalIn = "rank-total-in";
        public const string RankUpIn = "rank-up-in";
        public const string RankDownIn = "rank-down-in";
        public const string DeltaUpIn = "delta-up-in";
        public const string DeltaDownIn = "delta-down-in";
    }

    // Per-timeframe TxFold rows are keyed dynamically since which timeframes exist depends on selection.
    public static class TxFoldKeys
    {
        public static string Above(int seconds) => $"txfold-{seconds}-above";

        public static string In(int seconds) => $"txfold-{seconds}-in";

        public static string Below(int seconds) => $"txfold-{seconds}-below";
    }

    public static RuleEngineResult Compute<TSample, TBar>(
        IReadOnlyList<TSample> volumeSamples,
        Func<TSample, double> samplePrice,
        Func<TSample, double> sampleWeight,
        IReadOnlyList<TBar> vwapBars,
        Func<TBar, double> barPrice,
        Func<TBar, double> barWeight,
        Func<TBar, double> barClose,
        double currentPrice,
        XGridResult? grid = null,
        double xGridPercent = 10
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

        var rows = new List<RuleSignalRow>
        {
            Row("POC", "--color-primary", Keys.PocAbove, Keys.PocIn, Keys.PocBelow, poc, currentPrice),
            Row("VAH", "--color-highlight", Keys.VahAbove, Keys.VahIn, Keys.VahBelow, vah, currentPrice),
            Row("VAL", "--color-highlight", Keys.ValAbove, Keys.ValIn, Keys.ValBelow, val, currentPrice),
            Row("VWAP", "--color-tertiary", Keys.VwapAbove, Keys.VwapIn, Keys.VwapBelow, vwap, currentPrice),
            Row("MEDIAN", "--color-badge-2", Keys.MedianAbove, Keys.MedianIn, Keys.MedianBelow, median, currentPrice),
            Row("REG", "--color-badge-1", Keys.RegressionAbove, Keys.RegressionIn, Keys.RegressionBelow, regression, currentPrice),
        };

        if (grid is { } g)
            rows.AddRange(XGridRows(g, xGridPercent));

        return new RuleEngineResult(poc, vah, val, vwap, median, regression, currentPrice, rows);
    }

    // Above/Below/In (inclusive) each bar's mean +/- one-stddev band -- true high/low would always
    // contain currentPrice, so bars must come from BarAggregationKind.MeanStdDev (see RefreshTxFoldIfNeeded).
    public static IReadOnlyList<RuleSignalRow> TxFoldRows(IReadOnlyList<TxFoldBar> bars, double currentPrice) =>
        bars.OrderBy(b => b.LookbackSeconds).Select(b => TxFoldRow(b, currentPrice)).ToList();

    private static RuleSignalRow TxFoldRow(TxFoldBar bar, double currentPrice)
    {
        var label = SnapshotTimeframeOptions.All.First(o => o.Seconds == bar.LookbackSeconds).Label.ToUpperInvariant();
        var upperBand = bar.Bar.High;
        var lowerBand = bar.Bar.Low;
        return new RuleSignalRow(
            label,
            "--color-secondary",
            TxFoldKeys.Above(bar.LookbackSeconds),
            TxFoldKeys.In(bar.LookbackSeconds),
            TxFoldKeys.Below(bar.LookbackSeconds),
            currentPrice > upperBand,
            currentPrice >= lowerBand && currentPrice <= upperBand,
            currentPrice < lowerBand,
            RuleSignalGroup.TxFold
        );
    }

    // Rank-style (single In state, no Above/Below) -- same predicates XGridSignalCatalog's Rank group
    // uses, called directly rather than re-derived so the two stay in lockstep.
    private static IEnumerable<RuleSignalRow> XGridRows(XGridResult grid, double percent)
    {
        yield return RankRow(
            "TOP % TOTAL",
            "--color-secondary",
            Keys.RankTotalIn,
            XGridStats.IsCurrentInTopPercent(grid, XGridSpectrumMetric.Total, percent)
        );
        yield return RankRow(
            "TOP % UP",
            "--color-success",
            Keys.RankUpIn,
            XGridStats.IsCurrentInTopPercent(grid, XGridSpectrumMetric.Up, percent)
        );
        yield return RankRow(
            "TOP % DOWN",
            "--color-error",
            Keys.RankDownIn,
            XGridStats.IsCurrentInTopPercent(grid, XGridSpectrumMetric.Down, percent)
        );
        yield return RankRow(
            "DELTA UP",
            "--color-badge-1",
            Keys.DeltaUpIn,
            XGridStats.IsCurrentInDeltaPercent(grid, percent, positive: true)
        );
        yield return RankRow(
            "DELTA DOWN",
            "--color-badge-3",
            Keys.DeltaDownIn,
            XGridStats.IsCurrentInDeltaPercent(grid, percent, positive: false)
        );
    }

    private static RuleSignalRow RankRow(string label, string colorVar, string inKey, bool value) =>
        new(label, colorVar, null, inKey, null, false, value, false, RuleSignalGroup.XGrid);

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
            level is { } l3 && currentPrice < l3,
            RuleSignalGroup.Baseline
        );
}
