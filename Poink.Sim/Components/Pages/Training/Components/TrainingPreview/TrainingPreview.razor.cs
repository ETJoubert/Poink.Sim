using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Components.Charts;
using Poink.Sim.Helpers;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Simulation;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Training
{

    public partial class TrainingPreview : ComponentBase
    {

        private const double PriceBucketWidth = 0.01;

        private const double ChartViewWidth = 120;
        private const double ChartViewHeight = 140;
        private const double ChartPad = 4;


        private const int ProfileMaxRows = 20;

        [Inject] private ITickRepository TickRepository { get; set; } = default!;

        [Parameter] public BrainRecipe? Recipe { get; set; }
        [Parameter] public IReadOnlyList<string> DataItems { get; set; } = [];
        [Parameter] public IReadOnlyList<string> TrainingItems { get; set; } = [];

        [Parameter] public DateTime From { get; set; }
        [Parameter] public DateTime To { get; set; }
        [Parameter] public TimeSpan BlockSpan { get; set; }
        [Parameter] public IReadOnlyList<RlEpisodeWindow> EpisodeWindows { get; set; } = [];


        [Parameter] public string Table { get; set; } = "";
        [Parameter] public int BarWidthSeconds { get; set; }


        [Parameter] public EpisodeTimeConstraint TimeConstraint { get; set; } = EpisodeTimeConstraint.None;


        [Parameter] public double StartingCash { get; set; }


        [Inject] private ITrainingService Service { get; set; } = default!;


        private const int MaxRandomEpisodeAttempts = 8;

        private string? _lastEpisodeFetchKey;
        private bool _loadingEpisode;
        private bool _collapsed = false;
        private string? _episodeError;
        private List<HlocAvdtBar> _episodeBars = [];


        private List<BarPriceHistogram> _episodeHistograms = [];

        private DateTime? _episodeWindowStart;
        private DateTime? _episodeWindowEnd;


        private DateTime? DataHighlightStart => EpisodeWindows.Count > 0 ? EpisodeWindows[^1].Start : _episodeWindowStart;
        private DateTime? DataHighlightEnd => EpisodeWindows.Count > 0 ? EpisodeWindows[^1].End : _episodeWindowEnd;


        private IReadOnlyList<HlocAvdtBar> EffectiveEpisodeBars =>
            Service.LatestEpisodeBars.Count > 0 ? Service.LatestEpisodeBars : _episodeBars;
        private IReadOnlyList<BarPriceHistogram> EffectiveEpisodeHistograms =>
            Service.LatestEpisodeBars.Count > 0 ? Service.LatestEpisodeBarHistograms : _episodeHistograms;

        private IReadOnlyList<TrainingPreviewSourceRow> SourceRows =>
            BuildSourceRows(Recipe, EffectiveEpisodeBars, EffectiveEpisodeHistograms, StartingCash, Service.LatestInputs);

        private int TotalSourceInputCount => SourceRows.Sum(r => int.TryParse(r.Inputs, out var n) ? n : 0);
        private IReadOnlyList<string> NeuralNetworkItems => BuildNeuralNetworkItems(Recipe);
        private List<StackSegment> InputBarSegments => BuildInputBarSegments(SourceRows);
        private IReadOnlyList<string> HiddenLayerItems => BuildHiddenLayerItems(Recipe);
        private List<HiddenLayerBar> HiddenLayerBars => BuildHiddenLayerBars(HiddenLayerItems);
        private List<StackSegment> OutputBarSegments => BuildOutputBarSegments(Recipe);
        private List<ActionDecoderGroup> ActionDecoderGroups => BuildActionDecoderGroups(Recipe, Service.LatestOutputs);
        private List<EpisodeBarShape> EpisodeBarShapes => BuildEpisodeBarShapes(EffectiveEpisodeBars);

        private List<ProfileBarRow> VolumeProfileRows => BuildVolumeProfileRows(Recipe, EffectiveEpisodeHistograms);
        private List<ProfileBarRow> TimeProfileRows => BuildTimeProfileRows(Recipe, EffectiveEpisodeBars, EffectiveEpisodeHistograms);


        private IReadOnlyList<string> EpisodeSummaryItems
        {
            get
            {
                var bars = EffectiveEpisodeBars;
                if (bars.Count == 0)
                    return [];
                long tickCount = bars.Sum(b => (long)b.Volume);
                return
                [
                    $"Start: {bars[0].OpenTime:yyyy-MM-dd HH:mm}",
                    $"End: {bars[^1].CloseTime:yyyy-MM-dd HH:mm}",
                    $"Bars: {bars.Count}",
                    $"Bar width: {BarSizeOptions.Label(BarWidthSeconds)}",
                    $"Ticks: {tickCount:N0}",
                ];
            }
        }


        private IReadOnlyList<RlEpisodeMetric> ResultsEpisodes => Service.RecentEpisodes;
        private bool ResultsIsRunning => Service.IsRunning;

        public void ToggleCollapsed()
        {
            _collapsed = !_collapsed;
        }

        private static string SourceColorClass(string source) => source switch
        {
            "Bar Window" => "tp-source-badge-1",
            "Volume Profile" => "tp-source-badge-2",
            "Time Profile" => "tp-source-badge-3",
            "Session Clock" => "tp-source-badge-4",
            "Spread + Volatility" => "tp-source-badge-5",
            _ => "tp-source-badge-neutral",
        };


        private static string ValueKindClass(string? kind) => kind switch
        {
            "price" => "tp-value-price",
            "up" => "tp-value-up",
            "down" => "tp-value-down",
            "diff" => "tp-value-diff",
            "summary" => "tp-value-summary",
            "on" => "tp-value-on",
            "off" => "tp-value-off",
            _ => "",
        };


        protected override void OnParametersSet()
        {
            var weekdays = string.Join(",", TimeConstraint.AllowedWeekdays.OrderBy(d => d));
            var key = $"{Table}|{From:o}|{To:o}|{BlockSpan}|{BarWidthSeconds}|{TimeConstraint.Enabled}|{weekdays}|{TimeConstraint.SessionStart}|{TimeConstraint.SessionEnd}";
            if (key == _lastEpisodeFetchKey)
                return;
            _lastEpisodeFetchKey = key;


            _episodeBars = [];
            _episodeHistograms = [];
            _episodeWindowStart = null;
            _episodeWindowEnd = null;
            if (!string.IsNullOrEmpty(Table) && To > From && BlockSpan > TimeSpan.Zero && BarWidthSeconds > 0)
                _ = LoadRandomEpisode(Table, From, To, BlockSpan, TimeSpan.FromSeconds(BarWidthSeconds), TimeConstraint);
        }

        private async Task LoadRandomEpisode(
            string table, DateTime from, DateTime to, TimeSpan span, TimeSpan barSize, EpisodeTimeConstraint constraint)
        {
            _loadingEpisode = true;
            _episodeError = null;
            StateHasChanged();
            try
            {
                var result = await Task.Run(() => FetchRandomEpisode(table, from, to, span, barSize, constraint));
                _episodeBars = result?.Bars ?? [];
                _episodeHistograms = result?.Histograms ?? [];
                _episodeWindowStart = result?.Start;
                _episodeWindowEnd = result?.End;
            }
            catch (Exception ex)
            {
                _episodeBars = [];
                _episodeHistograms = [];
                _episodeWindowStart = null;
                _episodeWindowEnd = null;
                _episodeError = ex.Message;
            }
            finally
            {
                _loadingEpisode = false;
                StateHasChanged();
            }
        }


        private sealed record RandomEpisodeResult(List<HlocAvdtBar> Bars, List<BarPriceHistogram> Histograms, DateTime Start, DateTime End);


        private RandomEpisodeResult? FetchRandomEpisode(
            string table, DateTime from, DateTime to, TimeSpan span, TimeSpan barSize, EpisodeTimeConstraint constraint)
        {
            if (constraint.Enabled)
                return FetchConstrainedRandomEpisode(table, from, to, span, barSize, constraint);

            if (to - from < span)
                return null;

            for (int attempt = 0; attempt < MaxRandomEpisodeAttempts; attempt++)
            {
                double maxOffsetSeconds = (to - from - span).TotalSeconds;
                var start = from.AddSeconds(Random.Shared.NextDouble() * maxOffsetSeconds);
                var result = FetchEpisode(table, start, start + span, barSize);
                if (result.Bars.Count > 0)
                    return result;
            }
            return null;
        }


        private RandomEpisodeResult? FetchConstrainedRandomEpisode(
            string table, DateTime from, DateTime to, TimeSpan span, TimeSpan barSize, EpisodeTimeConstraint constraint)
        {
            var eligibleDays = EpisodeWindowSampler.EligibleDays(from, to, constraint).ToList();
            if (eligibleDays.Count == 0)
                return null;

            for (int attempt = 0; attempt < MaxRandomEpisodeAttempts; attempt++)
            {
                var day = eligibleDays[Random.Shared.Next(eligibleDays.Count)];
                var start = day + constraint.SessionStart;
                var result = FetchEpisode(table, start, start + span, barSize);
                if (result.Bars.Count > 0)
                    return result;
            }
            return null;
        }


        private RandomEpisodeResult FetchEpisode(string table, DateTime start, DateTime end, TimeSpan barSize)
        {
            var aggregator = new SimulationBarAggregator(barSize, PriceBucketWidth);
            var bars = new List<HlocAvdtBar>();
            var histograms = new List<BarPriceHistogram>();
            foreach (var tick in TickRepository.ReadTicks(table, start, end))
            {
                if (aggregator.Feed(tick) is { } completed)
                {
                    bars.Add(completed.Bar);
                    histograms.Add(completed.Histogram);
                }
            }
            if (aggregator.CurrentPartialBar is { } partial && aggregator.CurrentPartialHistogram is { } partialHistogram)
            {
                bars.Add(partial);
                histograms.Add(partialHistogram);
            }
            return new RandomEpisodeResult(bars, histograms, start, end);
        }


        private sealed record EpisodeBarShape(string X, string Y, string Width, string Height, bool IsUp, string Tooltip);

        private static List<EpisodeBarShape> BuildEpisodeBarShapes(IReadOnlyList<HlocAvdtBar> bars)
        {
            if (bars.Count == 0)
                return [];

            double min = bars.Min(b => b.Low);
            double max = bars.Max(b => b.High);
            if (max - min < 1e-9)
            { min -= 0.01; max += 0.01; }

            double plotWidth = ChartViewWidth - ChartPad * 2;
            double plotHeight = ChartViewHeight - ChartPad * 2;
            double barSlot = plotWidth / bars.Count;
            double barWidth = Math.Max(1, barSlot * 0.6);

            double MapY(double price) => ChartPad + (1 - (price - min) / (max - min)) * plotHeight;

            var shapes = new List<EpisodeBarShape>(bars.Count);
            for (int i = 0; i < bars.Count; i++)
            {
                var bar = bars[i];
                double x = ChartPad + i * barSlot + (barSlot - barWidth) / 2;
                double yHigh = MapY(bar.High);
                double yLow = MapY(bar.Low);
                shapes.Add(new EpisodeBarShape(
                    FormatCoord(x), FormatCoord(yHigh), FormatCoord(barWidth), FormatCoord(Math.Max(1, yLow - yHigh)),
                    bar.Close >= bar.Open,
                    $"{bar.OpenTime:HH:mm} O:{bar.Open:0.####} H:{bar.High:0.####} L:{bar.Low:0.####} C:{bar.Close:0.####}"));
            }
            return shapes;
        }


        private static string FormatCoord(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);


        private sealed record ProfileBarRow(string WidthPercent, string Tooltip);


        private static List<ProfileBarRow> BuildVolumeProfileRows(BrainRecipe? recipe, IReadOnlyList<BarPriceHistogram> episodeHistograms)
        {
            if (recipe is null || !recipe.EncoderKinds.Contains(EncoderKind.VolumeProfile) || episodeHistograms.Count == 0)
                return [];

            var sizes = recipe.EncoderSizes ?? new EncoderSizeOptions();
            var merged = BarPriceHistogram.Merge(episodeHistograms, sizes.VolumeProfileLookbackBars);
            if (merged.Count == 0)
                return [];

            int bucketCount = Math.Clamp(sizes.VolumeProfileBucketCount, 1, ProfileMaxRows);
            var (counts, prices) = VolumeProfileAnalysis.BuildRawHistogram(merged, bucketCount);
            double maxCount = counts.Length == 0 ? 0 : counts.Max();
            if (maxCount <= 0)
                return [];

            var rows = new List<ProfileBarRow>(bucketCount);
            for (int i = bucketCount - 1; i >= 0; i--)
            {
                double pct = counts[i] / maxCount * 100.0;
                rows.Add(new ProfileBarRow(FormatCoord(pct), $"{prices[i]:0.####}: {counts[i]:0} ticks"));
            }
            return rows;
        }


        private static List<ProfileBarRow> BuildTimeProfileRows(
            BrainRecipe? recipe, IReadOnlyList<HlocAvdtBar> episodeBars, IReadOnlyList<BarPriceHistogram> episodeHistograms)
        {
            if (recipe is null || !recipe.EncoderKinds.Contains(EncoderKind.TimeProfile) || episodeHistograms.Count == 0)
                return [];

            var sizes = recipe.EncoderSizes ?? new EncoderSizeOptions();
            var merged = BarPriceHistogram.MergeTime(episodeBars, episodeHistograms, sizes.TimeProfileLookbackBars);
            if (merged.Count == 0)
                return [];

            int bucketCount = Math.Clamp(sizes.TimeProfileBucketCount, 1, ProfileMaxRows);
            var (seconds, prices) = TimeProfileAnalysis.BuildRawHistogram(merged, bucketCount);
            double maxSeconds = seconds.Length == 0 ? 0 : seconds.Max();
            if (maxSeconds <= 0)
                return [];

            var rows = new List<ProfileBarRow>(bucketCount);
            for (int i = bucketCount - 1; i >= 0; i--)
            {
                double pct = seconds[i] / maxSeconds * 100.0;
                rows.Add(new ProfileBarRow(FormatCoord(pct), $"{prices[i]:0.####}: {seconds[i]:0.#}s"));
            }
            return rows;
        }


        private sealed record ProfileValue(string Text, string Kind, string Tooltip);

        private static string FormatCount(float v) => v.ToString("0", CultureInfo.InvariantCulture);
        private static string FormatSeconds(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);
        private static string FormatPrice(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);


        private static string FormatOffsetPercent(double price, double currentPrice) =>
            ((currentPrice == 0 ? 0 : (price - currentPrice) / currentPrice) * 100)
                .ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture) + "%";


        private static List<ProfileValue> BuildVolumeProfileRawValues(BrainRecipe? recipe, IReadOnlyList<BarPriceHistogram> episodeHistograms)
        {
            var values = new List<ProfileValue>();
            if (recipe is null || episodeHistograms.Count == 0)
                return values;

            var sizes = recipe.EncoderSizes ?? new EncoderSizeOptions();
            var merged = BarPriceHistogram.Merge(episodeHistograms, sizes.VolumeProfileLookbackBars);
            if (merged.Count == 0)
                return values;

            int bucketCount = sizes.VolumeProfileBucketCount;
            if (sizes.VolumeProfileSplitUpDown)
            {
                var (up, down, diff, _) = VolumeProfileAnalysis.BuildRawSplitHistogram(merged, bucketCount);
                for (int i = 0; i < bucketCount; i++)
                {
                    values.Add(new(FormatCount(up[i]), "up", $"Bucket {i} up ticks"));
                    values.Add(new(FormatCount(down[i]), "down", $"Bucket {i} down ticks"));
                    values.Add(new(FormatCount(diff[i]), "diff", $"Bucket {i} up-down diff"));
                }
            }
            else
            {
                var (counts, _) = VolumeProfileAnalysis.BuildRawHistogram(merged, bucketCount);
                for (int i = 0; i < bucketCount; i++)
                    values.Add(new(FormatCount(counts[i]), "volume", $"Bucket {i} ticks"));
            }

            if (VolumeProfileAnalysis.ComputeSummary(merged, episodeHistograms[^1].BucketWidth) is { } summary)
            {
                values.Add(new(FormatPrice(summary.PointOfControl), "summary", "Point of Control"));
                values.Add(new(FormatPrice(summary.ValueAreaHigh), "summary", "Value Area High"));
                values.Add(new(FormatPrice(summary.ValueAreaLow), "summary", "Value Area Low"));
            }
            return values;
        }


        private static List<ProfileValue> BuildTimeProfileRawValues(
            BrainRecipe? recipe, IReadOnlyList<HlocAvdtBar> episodeBars, IReadOnlyList<BarPriceHistogram> episodeHistograms)
        {
            var values = new List<ProfileValue>();
            if (recipe is null || episodeHistograms.Count == 0)
                return values;

            var sizes = recipe.EncoderSizes ?? new EncoderSizeOptions();
            var merged = BarPriceHistogram.MergeTime(episodeBars, episodeHistograms, sizes.TimeProfileLookbackBars);
            if (merged.Count == 0)
                return values;

            int bucketCount = sizes.TimeProfileBucketCount;
            if (sizes.TimeProfileSplitUpDown)
            {
                var (up, down, diff, _) = TimeProfileAnalysis.BuildRawSplitHistogram(merged, bucketCount);
                for (int i = 0; i < bucketCount; i++)
                {
                    values.Add(new(FormatSeconds(up[i]), "up", $"Bucket {i} up seconds"));
                    values.Add(new(FormatSeconds(down[i]), "down", $"Bucket {i} down seconds"));
                    values.Add(new(FormatSeconds(diff[i]), "diff", $"Bucket {i} up-down diff"));
                }
            }
            else
            {
                var (seconds, _) = TimeProfileAnalysis.BuildRawHistogram(merged, bucketCount);
                for (int i = 0; i < bucketCount; i++)
                    values.Add(new(FormatSeconds(seconds[i]), "seconds", $"Bucket {i} seconds"));
            }

            if (TimeProfileAnalysis.ComputeSummary(merged, episodeHistograms[^1].BucketWidth) is { } summary)
            {
                values.Add(new(FormatPrice(summary.PointOfControl), "summary", "Point of Control"));
                values.Add(new(FormatPrice(summary.ValueAreaHigh), "summary", "Value Area High"));
                values.Add(new(FormatPrice(summary.ValueAreaLow), "summary", "Value Area Low"));
            }
            return values;
        }

        private static List<ProfileValue> BuildVolumeProfileNormalizedValues(
           BrainRecipe? recipe, IReadOnlyList<HlocAvdtBar> episodeBars, IReadOnlyList<BarPriceHistogram> episodeHistograms)
        {
            var values = new List<ProfileValue>();
            if (recipe is null || episodeHistograms.Count == 0 || episodeBars.Count == 0)
                return values;

            var sizes = recipe.EncoderSizes ?? new EncoderSizeOptions();
            var merged = BarPriceHistogram.Merge(episodeHistograms, sizes.VolumeProfileLookbackBars);
            if (merged.Count == 0)
                return values;

            int bucketCount = sizes.VolumeProfileBucketCount;
            bool isRaw = sizes.VolumeProfileNormalization == NormalizationKind.Raw;
            double currentPrice = episodeBars[^1].Close;

            if (isRaw && sizes.VolumeProfileSplitUpDown)
            {
                var (up, down, diff, prices) = VolumeProfileAnalysis.BuildRawSplitHistogram(merged, bucketCount);
                for (int i = 0; i < bucketCount; i++)
                {
                    values.Add(new(FormatPrice(prices[i]), "price", $"Bucket {i} price"));
                    values.Add(new(FormatCount(up[i]), "up", $"Bucket {i} up ticks"));
                    values.Add(new(FormatCount(down[i]), "down", $"Bucket {i} down ticks"));
                    values.Add(new(FormatCount(diff[i]), "diff", $"Bucket {i} up-down diff"));
                }
            }
            else if (isRaw)
            {
                var (counts, prices) = VolumeProfileAnalysis.BuildRawHistogram(merged, bucketCount);
                for (int i = 0; i < bucketCount; i++)
                {
                    values.Add(new(FormatPrice(prices[i]), "price", $"Bucket {i} price"));
                    values.Add(new(FormatCount(counts[i]), "volume", $"Bucket {i} ticks"));
                }
            }
            else if (sizes.VolumeProfileSplitUpDown)
            {
                var (up, down, diff) = VolumeProfileAnalysis.BuildRelativeSplitHistogram(merged, bucketCount);
                if (sizes.VolumeProfileNormalization is NormalizationKind.LocalZScore or NormalizationKind.GlobalZScore)
                {
                    up = ApplyZScore(up, sizes.VolumeProfileNormalization, sizes.VolumeProfileZScoreSubWindowSize);
                    down = ApplyZScore(down, sizes.VolumeProfileNormalization, sizes.VolumeProfileZScoreSubWindowSize);
                    diff = ApplyZScore(diff, sizes.VolumeProfileNormalization, sizes.VolumeProfileZScoreSubWindowSize);
                    for (int i = 0; i < bucketCount; i++)
                    {
                        values.Add(new(FormatZScore(up[i]), "up", $"Bucket {i} up"));
                        values.Add(new(FormatZScore(down[i]), "down", $"Bucket {i} down"));
                        values.Add(new(FormatZScore(diff[i]), "diff", $"Bucket {i} diff"));
                    }
                }
                else if (sizes.VolumeProfileNormalization == NormalizationKind.Binary)
                {
                    var upB = BinaryNormalization.Compute(Array.ConvertAll(up, v => (double)v));
                    var downB = BinaryNormalization.Compute(Array.ConvertAll(down, v => (double)v));
                    var diffB = BinaryNormalization.Compute(Array.ConvertAll(diff, v => (double)v));
                    for (int i = 0; i < bucketCount; i++)
                    {
                        values.Add(new(FormatBinary(upB[i]), "up", $"Bucket {i} up"));
                        values.Add(new(FormatBinary(downB[i]), "down", $"Bucket {i} down"));
                        values.Add(new(FormatBinary(diffB[i]), "diff", $"Bucket {i} diff"));
                    }
                }
                else
                {
                    for (int i = 0; i < bucketCount; i++)
                    {
                        values.Add(new(FormatFraction(up[i]), "up", $"Bucket {i} up"));
                        values.Add(new(FormatFraction(down[i]), "down", $"Bucket {i} down"));
                        values.Add(new(FormatFraction(diff[i]), "diff", $"Bucket {i} diff"));
                    }
                }
            }
            else
            {
                var fractions = VolumeProfileAnalysis.BuildRelativeHistogram(merged, bucketCount);
                if (sizes.VolumeProfileNormalization is NormalizationKind.LocalZScore or NormalizationKind.GlobalZScore)
                {
                    var z = ApplyZScore(fractions, sizes.VolumeProfileNormalization, sizes.VolumeProfileZScoreSubWindowSize);
                    for (int i = 0; i < bucketCount; i++)
                        values.Add(new(FormatZScore(z[i]), "volume", $"Bucket {i}"));
                }
                else if (sizes.VolumeProfileNormalization == NormalizationKind.Binary)
                {
                    var b = BinaryNormalization.Compute(Array.ConvertAll(fractions, v => (double)v));
                    for (int i = 0; i < bucketCount; i++)
                        values.Add(new(FormatBinary(b[i]), "volume", $"Bucket {i}"));
                }
                else
                {
                    for (int i = 0; i < bucketCount; i++)
                        values.Add(new(FormatFraction(fractions[i]), "volume", $"Bucket {i}"));
                }
            }

            if (VolumeProfileAnalysis.ComputeSummary(merged, episodeHistograms[^1].BucketWidth) is { } summary)
            {
                string Fmt(double p) => isRaw ? FormatPrice(p) : FormatOffsetPercent(p, currentPrice);
                values.Add(new(Fmt(summary.PointOfControl), "summary", "Point of Control"));
                values.Add(new(Fmt(summary.ValueAreaHigh), "summary", "Value Area High"));
                values.Add(new(Fmt(summary.ValueAreaLow), "summary", "Value Area Low"));
            }
            return values;
        }


        private static List<ProfileValue> BuildTimeProfileNormalizedValues(
            BrainRecipe? recipe, IReadOnlyList<HlocAvdtBar> episodeBars, IReadOnlyList<BarPriceHistogram> episodeHistograms)
        {
            var values = new List<ProfileValue>();
            if (recipe is null || episodeHistograms.Count == 0 || episodeBars.Count == 0)
                return values;

            var sizes = recipe.EncoderSizes ?? new EncoderSizeOptions();
            var merged = BarPriceHistogram.MergeTime(episodeBars, episodeHistograms, sizes.TimeProfileLookbackBars);
            if (merged.Count == 0)
                return values;

            int bucketCount = sizes.TimeProfileBucketCount;
            bool isRaw = sizes.TimeProfileNormalization == NormalizationKind.Raw;
            double currentPrice = episodeBars[^1].Close;

            if (isRaw && sizes.TimeProfileSplitUpDown)
            {
                var (up, down, diff, prices) = TimeProfileAnalysis.BuildRawSplitHistogram(merged, bucketCount);
                for (int i = 0; i < bucketCount; i++)
                {
                    values.Add(new(FormatPrice(prices[i]), "price", $"Bucket {i} price"));
                    values.Add(new(FormatSeconds(up[i]), "up", $"Bucket {i} up seconds"));
                    values.Add(new(FormatSeconds(down[i]), "down", $"Bucket {i} down seconds"));
                    values.Add(new(FormatSeconds(diff[i]), "diff", $"Bucket {i} up-down diff"));
                }
            }
            else if (isRaw)
            {
                var (seconds, prices) = TimeProfileAnalysis.BuildRawHistogram(merged, bucketCount);
                for (int i = 0; i < bucketCount; i++)
                {
                    values.Add(new(FormatPrice(prices[i]), "price", $"Bucket {i} price"));
                    values.Add(new(FormatSeconds(seconds[i]), "seconds", $"Bucket {i} seconds"));
                }
            }
            else if (sizes.TimeProfileSplitUpDown)
            {
                var (up, down, diff) = TimeProfileAnalysis.BuildRelativeSplitHistogram(merged, bucketCount);
                for (int i = 0; i < bucketCount; i++)
                {
                    values.Add(new(FormatFraction(up[i]), "up", $"Bucket {i} up"));
                    values.Add(new(FormatFraction(down[i]), "down", $"Bucket {i} down"));
                    values.Add(new(FormatFraction(diff[i]), "diff", $"Bucket {i} diff"));
                }
            }
            else
            {
                var fractions = TimeProfileAnalysis.BuildRelativeHistogram(merged, bucketCount);
                for (int i = 0; i < bucketCount; i++)
                    values.Add(new(FormatFraction(fractions[i]), "seconds", $"Bucket {i}"));
            }

            if (TimeProfileAnalysis.ComputeSummary(merged, episodeHistograms[^1].BucketWidth) is { } summary)
            {
                string Fmt(double p) => isRaw ? FormatPrice(p) : FormatOffsetPercent(p, currentPrice);
                values.Add(new(Fmt(summary.PointOfControl), "summary", "Point of Control"));
                values.Add(new(Fmt(summary.ValueAreaHigh), "summary", "Value Area High"));
                values.Add(new(Fmt(summary.ValueAreaLow), "summary", "Value Area Low"));
            }
            return values;
        }


        private static float[] ApplyZScore(float[] values, NormalizationKind kind, int subWindowSize) =>
            kind == NormalizationKind.GlobalZScore
                ? ZScoreNormalization.Global(Array.ConvertAll(values, v => (double)v))
                : ZScoreNormalization.Local(Array.ConvertAll(values, v => (double)v), subWindowSize);

        private static string FormatFraction(float f) => (f * 100).ToString("0.00", CultureInfo.InvariantCulture) + "%";

        private static IReadOnlyList<string> BuildBarWindowRawValues(IReadOnlyList<HlocAvdtBar> episodeBars, int barWindowSize)
        {
            if (episodeBars.Count == 0)
                return [];
            return episodeBars.TakeLast(barWindowSize).Select(b => b.Close.ToString("0.####", CultureInfo.InvariantCulture)).ToList();
        }


        private static IReadOnlyList<string> BuildBarWindowNormalizedValues(
            IReadOnlyList<HlocAvdtBar> episodeBars, NormalizationKind normalization, int barWindowSize, int zScoreSubWindowSize)
        {
            if (episodeBars.Count == 0)
                return [];

            var prices = episodeBars.TakeLast(barWindowSize).Select(b => b.Close).ToList();
            if (prices.Count == 0)
                return [];

            return normalization switch
            {
                NormalizationKind.Raw =>
                    prices.Select(p => p.ToString("0.####", CultureInfo.InvariantCulture)).ToList(),
                NormalizationKind.LocalZScore =>
                    ZScoreNormalization.Local(prices, zScoreSubWindowSize).Select(FormatZScore).ToList(),
                NormalizationKind.GlobalZScore =>
                    ZScoreNormalization.Global(prices).Select(FormatZScore).ToList(),
                NormalizationKind.Binary =>
                    BinaryNormalization.Compute(prices).Select(FormatBinary).ToList(),
                _ => BuildPercentOffsets(prices),
            };
        }


        private static List<string> BuildPercentOffsets(List<double> prices)
        {
            double currentPrice = prices[^1];
            return prices
                .Select(p => currentPrice == 0 ? 0 : (p - currentPrice) / currentPrice)
                .Select(offset => (offset * 100).ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture) + "%")
                .ToList();
        }

        private static string FormatZScore(float z) => z.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture);

        private static string FormatBinary(float b) => b.ToString("0", CultureInfo.InvariantCulture);


        private static IReadOnlyList<(string Value, string Tooltip)> BuildTraderStateValues(
            IReadOnlyList<HlocAvdtBar> episodeBars, double startingCash, EncoderSizeOptions sizes, IReadOnlyList<RlInputFeature>? liveInputs)
        {
            var encoder = (TraderStateEncoder)BrainCatalog.CreateEncoder(EncoderKind.TraderState, sizes);
            if (TryBuildLiveValues(encoder.Features, liveInputs, out var liveValues))
                return liveValues;

            if (episodeBars.Count == 0)
                return [];

            double currentPrice = episodeBars[^1].Close;
            var capital = new CapitalState(startingCash, RealizedPnl: 0, UnrealizedPnl: 0, MarginUsed: 0, MarginLevel: null);
            var observation = new SimulationObservation(
                episodeBars[^1].OpenTime, currentPrice, [], [], [], capital, Position: null, RecentTrades: [], currentPrice, currentPrice);

            return FormatEncoded(encoder.Features, encoder.Encode(observation));
        }


        private static IReadOnlyList<(string Value, string Tooltip)> BuildSessionClockValues(
            IReadOnlyList<HlocAvdtBar> episodeBars, IReadOnlyList<RlInputFeature>? liveInputs)
        {
            var encoder = new SessionClockEncoder();
            if (TryBuildLiveValues(encoder.Features, liveInputs, out var liveValues))
                return liveValues;

            if (episodeBars.Count == 0)
                return [];

            double currentPrice = episodeBars[^1].Close;
            var observation = new SimulationObservation(
                episodeBars[^1].OpenTime, currentPrice, [], [], [],
                new CapitalState(0, 0, 0, 0, null), Position: null, RecentTrades: [], currentPrice, currentPrice);

            return FormatEncoded(encoder.Features, encoder.Encode(observation));
        }

        private static IReadOnlyList<(string Value, string Tooltip)> BuildSpreadVolatilityValues(
            IReadOnlyList<HlocAvdtBar> episodeBars, int atrPeriod, IReadOnlyList<RlInputFeature>? liveInputs)
        {
            var encoder = new SpreadVolatilityEncoder(atrPeriod);
            if (TryBuildLiveValues(encoder.Features, liveInputs, out var liveValues))
                return liveValues;

            if (episodeBars.Count == 0)
                return [];

            double currentPrice = episodeBars[^1].Close;
            var observation = new SimulationObservation(
                episodeBars[^1].OpenTime, currentPrice, episodeBars, [], [],
                new CapitalState(0, 0, 0, 0, null), Position: null, RecentTrades: [], currentPrice, currentPrice);

            return FormatEncoded(encoder.Features, encoder.Encode(observation));
        }

        // Unlike the other Build*Values helpers, each XGrid feature is a differently-named
        // signal rather than an interchangeable bucket/scalar -- so instead of a bare "0"/"1"
        // (meaningless without hovering 15 identical-looking chips), the visible text is the
        // signal's own short name and its Kind ("on"/"off") drives the chip's styling.
        private static List<ProfileValue> BuildXGridSignalValues(
            IReadOnlyList<HlocAvdtBar> episodeBars, IReadOnlyList<BarPriceHistogram> episodeHistograms,
            EncoderSizeOptions sizes, IReadOnlyList<RlInputFeature>? liveInputs)
        {
            var signals = BrainCatalog.ResolveXGridSignals(sizes);
            if (signals.Count == 0)
                return [];

            var encoder = BrainCatalog.CreateEncoder(EncoderKind.XGrid, sizes);
            if (TryBuildLiveValues(encoder.Features, liveInputs, out var liveValues))
                return XGridProfileValues(signals, i => i < liveValues.Count && liveValues[i].Value == "1");

            if (episodeBars.Count == 0)
                return [];

            double currentPrice = episodeBars[^1].Close;
            var observation = new SimulationObservation(
                episodeBars[^1].OpenTime, currentPrice, episodeBars, [], episodeHistograms,
                new CapitalState(0, 0, 0, 0, null), Position: null, RecentTrades: [], currentPrice, currentPrice);
            var raw = encoder.Encode(observation);

            return XGridProfileValues(signals, i => i < raw.Length && raw[i] != 0);
        }

        private static List<ProfileValue> XGridProfileValues(IReadOnlyList<string> signals, Func<int, bool> isOn)
        {
            var values = new List<ProfileValue>(signals.Count);
            for (int i = 0; i < signals.Count; i++)
            {
                string label = XGridSignalCatalog.Get(signals[i]).Label;
                bool on = isOn(i);
                values.Add(new ProfileValue(label, on ? "on" : "off", $"{label}: {(on ? "true" : "false")}"));
            }
            return values;
        }

        private static bool TryBuildLiveValues(
            IReadOnlyList<FeatureDescriptor> features, IReadOnlyList<RlInputFeature>? liveInputs,
            out List<(string Value, string Tooltip)> values)
        {
            values = new List<(string, string)>(features.Count);
            if (liveInputs is null)
                return false;

            bool anyMatch = false;
            foreach (var f in features)
            {
                var match = liveInputs.FirstOrDefault(i => i.Label == f.Label);
                anyMatch |= match is not null;
                values.Add((match is not null ? match.Normalizer.Format(match.Value) : "—", f.Label));
            }
            return anyMatch;
        }

        private static List<(string Value, string Tooltip)> FormatEncoded(IReadOnlyList<FeatureDescriptor> features, float[] raw)
        {
            var values = new List<(string, string)>(features.Count);
            for (int i = 0; i < features.Count; i++)
                values.Add((features[i].Normalizer.Format(raw[i]), features[i].Label));
            return values;
        }


        private static string TraderStateListLengthsLabel(EncoderSizeOptions sizes)
        {
            var parts = new List<string>();
            if (sizes.TraderStateTradeOutcomeListLength > 0)
                parts.Add($"outcomes:{sizes.TraderStateTradeOutcomeListLength}");
            if (sizes.TraderStatePositionLevelListLength > 0)
                parts.Add($"level:{sizes.TraderStatePositionLevelListLength}");
            if (sizes.TraderStateDirectionListLength > 0)
                parts.Add($"direction:{sizes.TraderStateDirectionListLength}");
            return parts.Count > 0 ? string.Join(" ", parts) : "—";
        }

        private static string NormalizationLabel(NormalizationKind kind) => kind switch
        {
            NormalizationKind.Raw => "Raw",
            NormalizationKind.LocalZScore => "Local Z-Score",
            NormalizationKind.GlobalZScore => "Global Z-Score",
            NormalizationKind.Binary => "Binary",
            _ => "Normalized",
        };


        private static IReadOnlyList<TrainingPreviewSourceRow> BuildSourceRows(
            BrainRecipe? recipe, IReadOnlyList<HlocAvdtBar> episodeBars, IReadOnlyList<BarPriceHistogram> episodeHistograms,
            double startingCash, IReadOnlyList<RlInputFeature>? liveInputs)
        {
            var rows = new List<TrainingPreviewSourceRow>();
            if (recipe is null)
                return rows;

            var sizes = recipe.EncoderSizes ?? new EncoderSizeOptions();
            if (recipe.EncoderKinds.Contains(EncoderKind.BarWindow))
            {
                var count = BrainCatalog.CreateEncoder(EncoderKind.BarWindow, sizes).FeatureCount;
                rows.Add(new("Bar Window", count.ToString(), NormalizationLabel(sizes.BarWindowNormalization),
                    $"{sizes.BarWindowSize} bars", "—",
                    RawValues: BuildBarWindowRawValues(episodeBars, sizes.BarWindowSize),
                    NormalizedValues: BuildBarWindowNormalizedValues(
                        episodeBars, sizes.BarWindowNormalization, sizes.BarWindowSize, sizes.BarWindowZScoreSubWindowSize)));
            }
            if (recipe.EncoderKinds.Contains(EncoderKind.VolumeProfile))
            {
                var count = BrainCatalog.CreateEncoder(EncoderKind.VolumeProfile, sizes).FeatureCount;
                var normalization = NormalizationLabel(sizes.VolumeProfileNormalization) + (sizes.VolumeProfileSplitUpDown ? " (split)" : "");
                var raw = BuildVolumeProfileRawValues(recipe, episodeHistograms);
                var normalized = BuildVolumeProfileNormalizedValues(recipe, episodeBars, episodeHistograms);
                rows.Add(new("Volume Profile", count.ToString(), normalization,
                    $"{sizes.VolumeProfileLookbackBars} bars", sizes.VolumeProfileBucketCount.ToString(),
                    RawValues: raw.Select(v => v.Text).ToList(),
                    NormalizedValues: normalized.Select(v => v.Text).ToList(),
                    RawValueKinds: raw.Select(v => v.Kind).ToList(),
                    NormalizedValueKinds: normalized.Select(v => v.Kind).ToList(),
                    RawValueTooltips: raw.Select(v => v.Tooltip).ToList(),
                    NormalizedValueTooltips: normalized.Select(v => v.Tooltip).ToList()));
            }
            if (recipe.EncoderKinds.Contains(EncoderKind.TimeProfile))
            {
                var count = BrainCatalog.CreateEncoder(EncoderKind.TimeProfile, sizes).FeatureCount;
                var normalization = NormalizationLabel(sizes.TimeProfileNormalization) + (sizes.TimeProfileSplitUpDown ? " (split)" : "");
                var raw = BuildTimeProfileRawValues(recipe, episodeBars, episodeHistograms);
                var normalized = BuildTimeProfileNormalizedValues(recipe, episodeBars, episodeHistograms);
                rows.Add(new("Time Profile", count.ToString(), normalization,
                    $"{sizes.TimeProfileLookbackBars} bars", sizes.TimeProfileBucketCount.ToString(),
                    RawValues: raw.Select(v => v.Text).ToList(),
                    NormalizedValues: normalized.Select(v => v.Text).ToList(),
                    RawValueKinds: raw.Select(v => v.Kind).ToList(),
                    NormalizedValueKinds: normalized.Select(v => v.Kind).ToList(),
                    RawValueTooltips: raw.Select(v => v.Tooltip).ToList(),
                    NormalizedValueTooltips: normalized.Select(v => v.Tooltip).ToList()));
            }
            if (recipe.EncoderKinds.Contains(EncoderKind.SessionClock))
            {
                var count = BrainCatalog.CreateEncoder(EncoderKind.SessionClock, sizes).FeatureCount;
                var sessionClockValues = BuildSessionClockValues(episodeBars, liveInputs);
                rows.Add(new("Session Clock", count.ToString(), "Raw", "—", "—",
                    RawValues: sessionClockValues.Select(v => v.Value).ToList(),
                    NormalizedValues: sessionClockValues.Select(v => v.Value).ToList(),
                    RawValueTooltips: sessionClockValues.Select(v => v.Tooltip).ToList(),
                    NormalizedValueTooltips: sessionClockValues.Select(v => v.Tooltip).ToList()));
            }
            if (recipe.EncoderKinds.Contains(EncoderKind.SpreadVolatility))
            {
                var count = BrainCatalog.CreateEncoder(EncoderKind.SpreadVolatility, sizes).FeatureCount;
                var spreadVolatilityValues = BuildSpreadVolatilityValues(episodeBars, sizes.SpreadVolatilityAtrPeriod, liveInputs);
                rows.Add(new("Spread + Volatility", count.ToString(), "Raw", $"{sizes.SpreadVolatilityAtrPeriod} bars", "—",
                    RawValues: spreadVolatilityValues.Select(v => v.Value).ToList(),
                    NormalizedValues: spreadVolatilityValues.Select(v => v.Value).ToList(),
                    RawValueTooltips: spreadVolatilityValues.Select(v => v.Tooltip).ToList(),
                    NormalizedValueTooltips: spreadVolatilityValues.Select(v => v.Tooltip).ToList()));
            }
            if (recipe.EncoderKinds.Contains(EncoderKind.TraderState))
            {
                var count = BrainCatalog.CreateEncoder(EncoderKind.TraderState, sizes).FeatureCount;
                var traderStateValues = BuildTraderStateValues(episodeBars, startingCash, sizes, liveInputs);
                rows.Add(new("Trader State", count.ToString(), "Raw", TraderStateListLengthsLabel(sizes), "—",
                    RawValues: traderStateValues.Select(v => v.Value).ToList(),
                    NormalizedValues: traderStateValues.Select(v => v.Value).ToList(),
                    RawValueTooltips: traderStateValues.Select(v => v.Tooltip).ToList(),
                    NormalizedValueTooltips: traderStateValues.Select(v => v.Tooltip).ToList()));
            }
            if (recipe.EncoderKinds.Contains(EncoderKind.XGrid))
            {
                var count = BrainCatalog.CreateEncoder(EncoderKind.XGrid, sizes).FeatureCount;
                var xgridValues = BuildXGridSignalValues(episodeBars, episodeHistograms, sizes, liveInputs);
                rows.Add(new("XGrid Signals", count.ToString(), "Binary", $"{sizes.XGridWindowBars} bars",
                    $"{(sizes.XGridRowCount * sizes.XGridPipSize):0.#####}/row x {sizes.XGridColCount}col",
                    RawValues: xgridValues.Select(v => v.Text).ToList(),
                    NormalizedValues: xgridValues.Select(v => v.Text).ToList(),
                    RawValueKinds: xgridValues.Select(v => v.Kind).ToList(),
                    NormalizedValueKinds: xgridValues.Select(v => v.Kind).ToList(),
                    RawValueTooltips: xgridValues.Select(v => v.Tooltip).ToList(),
                    NormalizedValueTooltips: xgridValues.Select(v => v.Tooltip).ToList()));
            }
            return rows;
        }


        private static IReadOnlyList<string> BuildNeuralNetworkItems(BrainRecipe? recipe)
        {
            if (recipe is null)
                return [];

            return recipe.ArchitectureKind switch
            {
                ArchitectureKind.Lstm => ["LSTM"],
                ArchitectureKind.Cnn =>
                [
                    "CNN",
                    $"Sweeps: {(recipe.CnnKernelSweepSource == CnnKernelSweepSource.VolumeProfile ? "Volume Profile" : "Bar Window")}",
                ],
                ArchitectureKind.Neat => ["NEAT"],
                _ => ["MLP"],
            };
        }


        private static IReadOnlyList<string> BuildHiddenLayerItems(BrainRecipe? recipe)
        {
            if (recipe is null)
                return [];

            var items = new List<string>();
            var hiddenSizes = recipe.HiddenSizes;
            switch (recipe.ArchitectureKind)
            {
                case ArchitectureKind.Lstm:
                    items.Add($"Hidden: {(hiddenSizes.Count > 0 ? hiddenSizes[0] : 64)}");
                    items.Add($"Layers: {(hiddenSizes.Count > 1 ? hiddenSizes[1] : 2)}");
                    break;
                case ArchitectureKind.Cnn:
                    items.Add($"K: {(hiddenSizes.Count > 0 ? hiddenSizes[0] : 3)}");
                    var channels = hiddenSizes.Count > 1 ? hiddenSizes.Skip(1).ToList() : [16, 32];
                    for (int i = 0; i < channels.Count; i++)
                        items.Add($"C{i + 1}: {channels[i]}");
                    break;
                default:
                    for (int i = 0; i < hiddenSizes.Count; i++)
                        items.Add($"L{i + 1}: {hiddenSizes[i]}");
                    break;
            }
            return items;
        }

        private sealed record HiddenLayerBar(string Label, int Size, string HeightPercent);

        private static List<HiddenLayerBar> BuildHiddenLayerBars(IReadOnlyList<string> hiddenLayerItems)
        {
            var parsed = new List<(string Label, int Size)>();
            foreach (var item in hiddenLayerItems)
            {
                var parts = item.Split(':', 2);
                if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out var size))
                    parsed.Add((parts[0].Trim(), size));
            }
            if (parsed.Count == 0)
                return [];

            int max = parsed.Max(p => p.Size);
            if (max <= 0)
                return [];

            return parsed.Select(p => new HiddenLayerBar(p.Label, p.Size, FormatCoord(p.Size / (double)max * 100.0))).ToList();
        }

        private sealed record StackSegment(string Label, int Value, string ColorClass, string HeightPercent);

        private static List<StackSegment> BuildInputBarSegments(IReadOnlyList<TrainingPreviewSourceRow> sourceRows)
        {
            if (sourceRows.Count == 0)
                return [];

            var counts = sourceRows.Select(r => int.TryParse(r.Inputs, out var n) ? n : 0).ToList();
            int total = counts.Sum();
            if (total <= 0)
                return [];

            var segments = new List<StackSegment>(sourceRows.Count);
            for (int i = 0; i < sourceRows.Count; i++)
            {
                if (counts[i] <= 0)
                    continue;
                double pct = counts[i] / (double)total * 100.0;
                segments.Add(new StackSegment(sourceRows[i].Source, counts[i], SourceColorClass(sourceRows[i].Source), FormatCoord(pct)));
            }
            return segments;
        }

        private static string OutputColorClass(string label) => label switch
        {
            "Direction" => "tp-source-badge-1",
            "Volume" => "tp-source-badge-2",
            "Stop Loss" => "tp-source-badge-3",
            "Take Profit" => "tp-source-badge-4",
            _ => "tp-source-badge-neutral",
        };


        private static List<StackSegment> BuildOutputBarSegments(BrainRecipe? recipe)
        {
            if (recipe is null)
                return [];

            var directions = recipe.EnabledDirections ?? ComposedPolicyBrain.AllDirections;
            var actionKinds = recipe.ActionOutputKinds ?? [];

            var parts = new List<(string Label, int Value)> { ("Direction", directions.Count) };
            if (actionKinds.Contains(ActionOutputKind.Volume))
                parts.Add(("Volume", BrainCatalog.CreateActionOutputHead(ActionOutputKind.Volume, recipe.ActionOutputEncodingKind).OutputWidth));
            if (actionKinds.Contains(ActionOutputKind.StopLoss))
                parts.Add(("Stop Loss", BrainCatalog.CreateActionOutputHead(ActionOutputKind.StopLoss, recipe.ActionOutputEncodingKind).OutputWidth));
            if (actionKinds.Contains(ActionOutputKind.TakeProfit))
                parts.Add(("Take Profit", BrainCatalog.CreateActionOutputHead(ActionOutputKind.TakeProfit, recipe.ActionOutputEncodingKind).OutputWidth));

            int total = parts.Sum(p => p.Value);
            if (total <= 0)
                return [];

            return parts.Where(p => p.Value > 0)
                .Select(p => new StackSegment(p.Label, p.Value, OutputColorClass(p.Label), FormatCoord(p.Value / (double)total * 100.0)))
                .ToList();
        }


        private static List<ActionDecoderGroup> BuildActionDecoderGroups(BrainRecipe? recipe, IReadOnlyList<RlOutputFeature>? liveOutputs)
        {
            if (recipe is null)
                return [];

            var decodeLabel = recipe.DecisionDecoding == DecisionDecodingKind.HardMax ? "argmax" : "softmax";
            var decodeTooltip = recipe.DecisionDecoding == DecisionDecodingKind.HardMax
                ? "HardMax decoding -- always picks the highest-logit option"
                : recipe.DecisionTemperature != 1.0
                    ? $"Softmax decoding @ T={recipe.DecisionTemperature:0.##} -- samples weighted by probability"
                    : "Softmax decoding -- samples weighted by probability";

            var groups = new List<ActionDecoderGroup>();

            var directions = recipe.EnabledDirections ?? ComposedPolicyBrain.AllDirections;
            var directionOptions = new List<(string RawKey, string Display)>();
            if (directions.Contains(SimulationAction.Hold))
                directionOptions.Add((nameof(SimulationAction.Hold), "Hold"));
            if (directions.Contains(SimulationAction.EnterLong))
                directionOptions.Add((nameof(SimulationAction.EnterLong), "Enter Long"));
            if (directions.Contains(SimulationAction.EnterShort))
                directionOptions.Add((nameof(SimulationAction.EnterShort), "Enter Short"));
            if (directions.Contains(SimulationAction.Close))
                directionOptions.Add((nameof(SimulationAction.Close), "Close"));
            if (directionOptions.Count > 0)
                groups.Add(BuildActionDecoderGroup(
                    "Direction", OutputColorClass("Direction"), decodeLabel, decodeTooltip,
                    "Direction", directionOptions, liveOutputs));

            var actionKinds = recipe.ActionOutputKinds ?? [];
            if (actionKinds.Contains(ActionOutputKind.Volume))
                groups.Add(BuildActionDecoderGroup(
                    "Volume", OutputColorClass("Volume"), decodeLabel, decodeTooltip,
                    nameof(ActionOutputKind.Volume), LevelOptions(ActionOutputKind.Volume), liveOutputs));
            if (actionKinds.Contains(ActionOutputKind.StopLoss))
                groups.Add(BuildActionDecoderGroup(
                    "Stop Loss", OutputColorClass("Stop Loss"), decodeLabel, decodeTooltip,
                    nameof(ActionOutputKind.StopLoss), LevelOptions(ActionOutputKind.StopLoss), liveOutputs));
            if (actionKinds.Contains(ActionOutputKind.TakeProfit))
                groups.Add(BuildActionDecoderGroup(
                    "Take Profit", OutputColorClass("Take Profit"), decodeLabel, decodeTooltip,
                    nameof(ActionOutputKind.TakeProfit), LevelOptions(ActionOutputKind.TakeProfit), liveOutputs));

            return groups;
        }


        private static List<(string RawKey, string Display)> LevelOptions(ActionOutputKind kind) =>
            DiscretizedLevelsActionHead.GetLevelLabels(kind).Select(l => (l, l)).ToList();


        private static ActionDecoderGroup BuildActionDecoderGroup(
            string label, string colorClass, string decodeLabel, string decodeTooltip,
            string outputGroupKey, IReadOnlyList<(string RawKey, string Display)> options,
            IReadOnlyList<RlOutputFeature>? liveOutputs)
        {
            var groupOutputs = liveOutputs?.Where(o => o.Group == outputGroupKey).ToList();
            bool hasLiveData = groupOutputs is { Count: > 0 };

            var decoderOptions = new List<ActionDecoderOption>(options.Count);
            for (int i = 0; i < options.Count; i++)
            {
                var match = groupOutputs?.FirstOrDefault(o => o.Label == options[i].RawKey);
                bool isChosen = hasLiveData ? match?.IsChosen ?? false : i == 0;
                string rawValue = match is not null ? FormatLogit(match.Logit) : "";
                string percentage = match is not null ? FormatDecoderPercent(match.Probability) : "";
                string widthPercent = match is not null ? FormatCoord(match.Probability * 100) : "0";
                string display = options[i].Display;

                string tooltip = display
                    + (rawValue.Length == 0 ? "" : $": {rawValue} raw logit")
                    + (percentage.Length == 0 ? "" : $", {percentage} chance")
                    + (isChosen ? $" -- where {label}'s {decodeLabel} collapsed to" : "");

                decoderOptions.Add(new ActionDecoderOption(rawValue, display, percentage, widthPercent, isChosen, tooltip));
            }

            return new ActionDecoderGroup(label, colorClass, decodeLabel, decodeTooltip, decoderOptions);
        }
        private static string FormatLogit(double logit) => logit.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture);


        private static string FormatDecoderPercent(double probability) => (probability * 100).ToString("N0", CultureInfo.InvariantCulture) + "%";
    }


    public sealed record ActionDecoderOption(
        string RawValue, string Label, string Percentage, string WidthPercent, bool IsChosen, string Tooltip);


    public sealed record ActionDecoderGroup(
        string Label, string ColorClass, string DecodeLabel, string DecodeTooltip,
        IReadOnlyList<ActionDecoderOption> Options);


    public sealed record TrainingPreviewSourceRow(
        string Source, string Inputs, string Normalization, string Window, string Bucket,
        IReadOnlyList<string>? RawValues = null, IReadOnlyList<string>? NormalizedValues = null,
        IReadOnlyList<string>? RawValueKinds = null, IReadOnlyList<string>? NormalizedValueKinds = null,
        IReadOnlyList<string>? RawValueTooltips = null, IReadOnlyList<string>? NormalizedValueTooltips = null)
    {
        public IReadOnlyList<string> RawValues { get; init; } = RawValues ?? [];
        public IReadOnlyList<string> NormalizedValues { get; init; } = NormalizedValues ?? [];
        public IReadOnlyList<string> RawValueKinds { get; init; } = RawValueKinds ?? [];
        public IReadOnlyList<string> NormalizedValueKinds { get; init; } = NormalizedValueKinds ?? [];
        public IReadOnlyList<string> RawValueTooltips { get; init; } = RawValueTooltips ?? [];
        public IReadOnlyList<string> NormalizedValueTooltips { get; init; } = NormalizedValueTooltips ?? [];
    }


}
