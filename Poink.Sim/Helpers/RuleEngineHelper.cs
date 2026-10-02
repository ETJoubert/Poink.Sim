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

    public static string? SelectedKey(RuleSignalRow row, HashSet<string> enabledKeys) =>
        row.AboveKey is { } above && enabledKeys.Contains(above) ? above
        : enabledKeys.Contains(row.InKey) ? row.InKey
        : row.BelowKey is { } below && enabledKeys.Contains(below) ? below
        : null;

    public static bool LiveValue(RuleSignalRow row, string key) =>
        key == row.AboveKey ? row.Above
        : key == row.InKey ? row.In
        : key == row.BelowKey && row.Below;

    /// <summary>
    /// Whether enough selected rows hold their live value: all of them when <paramref name="minMatches"/> is null or
    /// not positive, otherwise at least that many (capped at the number selected). Keys of a deactivated group have
    /// no row, so a mask left with no active selection never matches.
    /// </summary>
    public static bool Matches(IReadOnlyList<RuleSignalRow> rows, HashSet<string> enabledKeys, int? minMatches = null)
    {
        int selected = 0,
            matched = 0;
        foreach (var row in rows)
        {
            if (SelectedKey(row, enabledKeys) is not { } key)
                continue;
            selected++;
            if (LiveValue(row, key))
                matched++;
        }
        return selected > 0 && matched >= RequiredMatches(selected, minMatches);
    }

    public static int RequiredMatches(int selected, int? minMatches) =>
        minMatches is > 0 ? Math.Min(minMatches.Value, selected) : selected;

    // Which signal group a key belongs to -- keys are named after their group, so this needs no row to look it up.
    public static RuleSignalGroup GroupOf(string key) =>
        key.StartsWith("txfold-", StringComparison.Ordinal) ? RuleSignalGroup.TxFold
        : key.StartsWith("rank-", StringComparison.Ordinal) || key.StartsWith("delta-", StringComparison.Ordinal) ? RuleSignalGroup.XGrid
        : RuleSignalGroup.Baseline;

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
        double xGridPercent = 10,
        bool includeBaseline = true,
        double inTolerance = 0
    )
    {
        var priceStats = includeBaseline ? RangeStatsHelper.Compute(volumeSamples, samplePrice, sampleWeight) : null;
        var vwapStats = includeBaseline ? RangeStatsHelper.Compute(vwapBars, barPrice, barWeight) : null;
        var regressionStats = includeBaseline ? RangeStatsHelper.Compute(vwapBars, barClose) : null;

        var poc = priceStats?.Poc;
        var vah = priceStats?.Vah;
        var val = priceStats?.Val;
        var vwap = vwapStats?.Vwap;
        var median = priceStats?.Median;
        var regression = regressionStats?.Regression;

        var rows = includeBaseline
            ? BaselineRows(poc, vah, val, vwap, median, regression, currentPrice, Math.Max(0, inTolerance))
            : new List<RuleSignalRow>();

        if (grid is { } g)
            rows.AddRange(XGridRows(g, xGridPercent));

        return new RuleEngineResult(poc, vah, val, vwap, median, regression, currentPrice, rows);
    }

    private static List<RuleSignalRow> BaselineRows(
        double? poc,
        double? vah,
        double? val,
        double? vwap,
        double? median,
        double? regression,
        double currentPrice,
        double inTolerance
    ) =>
        new()
        {
            Row("POC", "--color-primary", Keys.PocAbove, Keys.PocIn, Keys.PocBelow, poc, currentPrice, inTolerance),
            Row("VAH", "--color-highlight", Keys.VahAbove, Keys.VahIn, Keys.VahBelow, vah, currentPrice, inTolerance),
            Row("VAL", "--color-highlight", Keys.ValAbove, Keys.ValIn, Keys.ValBelow, val, currentPrice, inTolerance),
            Row("VWAP", "--color-tertiary", Keys.VwapAbove, Keys.VwapIn, Keys.VwapBelow, vwap, currentPrice, inTolerance),
            Row("MEDIAN", "--color-badge-2", Keys.MedianAbove, Keys.MedianIn, Keys.MedianBelow, median, currentPrice, inTolerance),
            Row("REG", "--color-badge-1", Keys.RegressionAbove, Keys.RegressionIn, Keys.RegressionBelow, regression, currentPrice, inTolerance),
        };

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

    // In is a band of +/- inTolerance around the level, and Above/Below only start beyond it, so exactly one state holds.
    private static RuleSignalRow Row(
        string label,
        string colorVar,
        string aboveKey,
        string inKey,
        string belowKey,
        double? level,
        double currentPrice,
        double inTolerance
    ) =>
        new(
            label,
            colorVar,
            aboveKey,
            inKey,
            belowKey,
            level is { } l && currentPrice > l + inTolerance,
            level is { } l2 && Math.Abs(currentPrice - l2) <= inTolerance,
            level is { } l3 && currentPrice < l3 - inTolerance,
            RuleSignalGroup.Baseline
        );
}
