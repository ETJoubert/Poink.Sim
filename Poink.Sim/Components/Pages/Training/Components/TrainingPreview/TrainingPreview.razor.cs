using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Simulation;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Training
{
    // Read-only summary of the brain that Start/Step will train, derived
    // straight from its BrainRecipe rather than any panel's in-progress
    // fields -- so it works whether the recipe came from BrainBuilder or was
    // loaded from a saved brain. Sits on TrainPage above the Start button,
    // independent of Brain Builder's own collapsible panel. DataSelector and
    // Training config summaries are a planned follow-up, not this component's
    // concern yet.
    public partial class TrainingPreview : ComponentBase
    {
        // Matches RlEpisodeRunner's own PriceBucketWidth -- keeps the fine-
        // grained per-bar histogram this preview merges/rebins for the
        // Volume/Time Profile mini charts consistent with what a real
        // training run would build.
        private const double PriceBucketWidth = 0.01;

        private const double ChartViewWidth = 120;
        private const double ChartViewHeight = 140;
        private const double ChartPad = 4;

        // Caps how many rebinned rows the Volume/Time Profile mini charts
        // draw, regardless of the recipe's own configured bucket count --
        // keeps them readable in the Episode column's narrow width.
        private const int ProfileMaxRows = 20;

        // Action Decoder diagram geometry -- one funnel per decoded group,
        // flowing left to right: circles (raw N-way logits) on the left
        // converge through a decode badge into a stem that points at the
        // group's action-set list rendered in HTML to the right of the svg
        // (see BuildActionDecoderGroup). Fixed viewBox -- only the circles'
        // vertical spread depends on a group's own way-count.
        private const string DecoderViewBox = "0 0 94 56";
        private const double DecoderViewBoxHeight = 56;
        private const double DecoderCircleX = 8;
        private const double DecoderCirclePadY = 8;
        private const double DecoderCenterY = DecoderViewBoxHeight / 2;
        private const double DecoderBadgeX = 34;
        private const double DecoderBadgeWidth = 50;
        private const double DecoderBadgeHeight = 13;
        private const double DecoderStemLength = 10;

        [Inject] private ITickRepository TickRepository { get; set; } = default!;

        [Parameter] public BrainRecipe? Recipe { get; set; }
        [Parameter] public IReadOnlyList<string> DataItems { get; set; } = [];
        [Parameter] public IReadOnlyList<string> TrainingItems { get; set; } = [];

        // Feeds the vertical Data-coverage timeline -- same From/To/BlockSpan/
        // EpisodeWindows EpisodeCoverageTimeline already takes elsewhere on
        // TrainPage, so the slice boundaries here match that one exactly.
        [Parameter] public DateTime From { get; set; }
        [Parameter] public DateTime To { get; set; }
        [Parameter] public TimeSpan BlockSpan { get; set; }
        [Parameter] public IReadOnlyList<RlEpisodeWindow> EpisodeWindows { get; set; } = [];

        // Table + BlockSpan (bars/episode * bar width) together pick the
        // random episode window the Episode column charts -- same table and
        // span an actual training run would sample from.
        [Parameter] public string Table { get; set; } = "";
        [Parameter] public int BarWidthMinutes { get; set; }

        // Feeds BuildTraderStateValues -- same starting cash a live run's
        // Trader begins each episode with (see TrainPage's Trader card).
        [Parameter] public double StartingCash { get; set; }

        // Random ticks_* windows routinely land on a gap (FX markets close
        // over weekends -- see SessionClockEncoder), so one empty attempt
        // doesn't mean the table has no data at all -- worth a few retries
        // before giving up and showing "no data".
        private const int MaxRandomEpisodeAttempts = 8;

        private string? _lastEpisodeFetchKey;
        private bool _loadingEpisode;
        private bool _collapsed = false;
        private string? _episodeError;
        private List<HlocAvdtBar> _episodeBars = [];

        // Index-aligned with _episodeBars -- each bar's own per-price-level
        // tick histogram, needed (alongside the bars themselves for MergeTime's
        // dwell-time math) to build the Volume/Time Profile mini charts.
        private List<BarPriceHistogram> _episodeHistograms = [];

        // The picked window's own [Start, End) -- feeds the Data column's
        // vertical timeline so it can highlight exactly where this Episode
        // chart's sample came from (see TrainingPreview.razor).
        private DateTime? _episodeWindowStart;
        private DateTime? _episodeWindowEnd;

        private IReadOnlyList<TrainingPreviewSourceRow> SourceRows => BuildSourceRows(Recipe, _episodeBars, _episodeHistograms, StartingCash);
        private IReadOnlyList<string> NeuralNetworkItems => BuildNeuralNetworkItems(Recipe);
        private List<StackSegment> InputBarSegments => BuildInputBarSegments(SourceRows);
        private IReadOnlyList<string> HiddenLayerItems => BuildHiddenLayerItems(Recipe);
        private List<HiddenLayerBar> HiddenLayerBars => BuildHiddenLayerBars(HiddenLayerItems);
        private List<StackSegment> OutputBarSegments => BuildOutputBarSegments(Recipe);
        private List<ActionDecoderGroup> ActionDecoderGroups => BuildActionDecoderGroups(Recipe);
        private List<EpisodeBarShape> EpisodeBarShapes => BuildEpisodeBarShapes(_episodeBars);

        private List<ProfileBarRow> VolumeProfileRows => BuildVolumeProfileRows(Recipe, _episodeHistograms);
        private List<ProfileBarRow> TimeProfileRows => BuildTimeProfileRows(Recipe, _episodeBars, _episodeHistograms);

        public void ToggleCollapsed()
        {
            _collapsed = !_collapsed;
        }
        // Which CSS modifier colors a source's card -- one badge color per
        // optional input set (see .tp-source-badge-* in TrainingPreview.razor.css),
        // Trader State gets its own neutral color since it's always-on rather
        // than one of the selectable sources.
        private static string SourceColorClass(string source) => source switch
        {
            "Bar Window" => "tp-source-badge-1",
            "Volume Profile" => "tp-source-badge-2",
            "Time Profile" => "tp-source-badge-3",
            "Session Clock" => "tp-source-badge-4",
            "Spread + Volatility" => "tp-source-badge-5",
            _ => "tp-source-badge-neutral",
        };

        // Re-rolls a random episode only when the inputs that actually change
        // its window (table/range/span/bar size) change -- TrainPage
        // re-renders this component constantly while training runs (see
        // Service.OnChanged), and re-querying Postgres on every one of those
        // frames would be wasteful and would make the chart flicker.
        protected override void OnParametersSet()
        {
            var key = $"{Table}|{From:o}|{To:o}|{BlockSpan}|{BarWidthMinutes}";
            if (key == _lastEpisodeFetchKey)
                return;
            _lastEpisodeFetchKey = key;

            _episodeBars = [];
            _episodeWindowStart = null;
            _episodeWindowEnd = null;
            if (!string.IsNullOrEmpty(Table) && To > From && BlockSpan > TimeSpan.Zero && BarWidthMinutes > 0)
                _ = LoadRandomEpisode(Table, From, To, BlockSpan, TimeSpan.FromMinutes(BarWidthMinutes));
        }

        private async Task LoadRandomEpisode(string table, DateTime from, DateTime to, TimeSpan span, TimeSpan barSize)
        {
            _loadingEpisode = true;
            _episodeError = null;
            StateHasChanged();
            try
            {
                var result = await Task.Run(() => FetchRandomEpisode(table, from, to, span, barSize));
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

        // Histograms is index-aligned 1:1 with Bars -- see _episodeHistograms.
        private sealed record RandomEpisodeResult(List<HlocAvdtBar> Bars, List<BarPriceHistogram> Histograms, DateTime Start, DateTime End);

        // Picks a random BlockSpan-length window inside [from, to) and
        // aggregates its ticks into bars, the same tick-by-tick aggregation
        // RlEpisodeRunner uses live during training -- so this preview is
        // built from one plausible real episode, not synthetic data. Retries
        // a few times if a window comes back with zero ticks (see
        // MaxRandomEpisodeAttempts) before giving up.
        private RandomEpisodeResult? FetchRandomEpisode(string table, DateTime from, DateTime to, TimeSpan span, TimeSpan barSize)
        {
            if (to - from < span)
                return null;

            for (int attempt = 0; attempt < MaxRandomEpisodeAttempts; attempt++)
            {
                var result = FetchEpisode(table, from, to, span, barSize);
                if (result.Bars.Count > 0)
                    return result;
            }
            return null;
        }

        // Aggregates one randomly-picked window's ticks into bars, including
        // the trailing partial bar (if any) so a window that doesn't divide
        // evenly into whole bars still shows its last bit of data.
        private RandomEpisodeResult FetchEpisode(string table, DateTime from, DateTime to, TimeSpan span, TimeSpan barSize)
        {
            double maxOffsetSeconds = (to - from - span).TotalSeconds;
            var start = from.AddSeconds(Random.Shared.NextDouble() * maxOffsetSeconds);
            var end = start + span;

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

        // Vertical High/Low range bar per aggregated bar -- a simplified OHLC
        // bar chart (range only, no open/close ticks), colored by whether
        // that bar closed up or down.
        private sealed record EpisodeBarShape(string X, string Y, string Width, string Height, bool IsUp, string Tooltip);

        private static List<EpisodeBarShape> BuildEpisodeBarShapes(List<HlocAvdtBar> bars)
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

        // SVG geometry attributes always need '.' regardless of host locale --
        // same reasoning as RewardChart/TrendLineChart.
        private static string FormatCoord(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        // One rebinned price bucket's horizontal bar -- WidthPercent is
        // relative to the tallest bucket (matches TickVolumeProfileChart's
        // own max-relative scaling), rows are built high-price-first so the
        // caller can render top-to-bottom.
        private sealed record ProfileBarRow(string WidthPercent, string Tooltip);

        // Volume Profile's own encoder merges the trailing VolumeProfileLookbackBars
        // bars' histograms and rebins to VolumeProfileBucketCount buckets (see
        // VolumeProfileEncoder) -- reused here verbatim so the mini chart matches
        // exactly what the network would be fed, just capped to ProfileMaxRows
        // rows so it stays readable in the Episode column.
        private static List<ProfileBarRow> BuildVolumeProfileRows(BrainRecipe? recipe, List<BarPriceHistogram> episodeHistograms)
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

        // Time Profile counterpart to BuildVolumeProfileRows -- same merge/
        // rebin recipe as TimeProfileEncoder, weighted by seconds spent at
        // each price level instead of tick count (see BarPriceHistogram.MergeTime).
        private static List<ProfileBarRow> BuildTimeProfileRows(
            BrainRecipe? recipe, List<HlocAvdtBar> episodeBars, List<BarPriceHistogram> episodeHistograms)
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

        // Volume Profile's own encoder's Raw mode -- literal tick count per
        // rebinned bucket, ascending by price (the same order the network's
        // input vector itself is in, see VolumeProfileAnalysis.BuildRawHistogram)
        // -- feeds the Source column's card, uncapped (unlike
        // BuildVolumeProfileRows' chart, which caps rows for readability).
        private static IReadOnlyList<string> BuildVolumeProfileRawValues(BrainRecipe? recipe, List<BarPriceHistogram> episodeHistograms)
        {
            if (recipe is null || episodeHistograms.Count == 0)
                return [];

            var sizes = recipe.EncoderSizes ?? new EncoderSizeOptions();
            var merged = BarPriceHistogram.Merge(episodeHistograms, sizes.VolumeProfileLookbackBars);
            if (merged.Count == 0)
                return [];

            var (counts, _) = VolumeProfileAnalysis.BuildRawHistogram(merged, sizes.VolumeProfileBucketCount);
            return counts.Select(c => c.ToString("0", CultureInfo.InvariantCulture)).ToList();
        }

        // Time Profile counterpart to BuildVolumeProfileRawValues -- literal
        // seconds spent per rebinned bucket instead of tick count.
        private static IReadOnlyList<string> BuildTimeProfileRawValues(
            BrainRecipe? recipe, List<HlocAvdtBar> episodeBars, List<BarPriceHistogram> episodeHistograms)
        {
            if (recipe is null || episodeHistograms.Count == 0)
                return [];

            var sizes = recipe.EncoderSizes ?? new EncoderSizeOptions();
            var merged = BarPriceHistogram.MergeTime(episodeBars, episodeHistograms, sizes.TimeProfileLookbackBars);
            if (merged.Count == 0)
                return [];

            var (seconds, _) = TimeProfileAnalysis.BuildRawHistogram(merged, sizes.TimeProfileBucketCount);
            return seconds.Select(s => s.ToString("0.#", CultureInfo.InvariantCulture)).ToList();
        }

        // Same trailing buckets, but transformed the way VolumeProfileEncoder.Encode
        // itself would for the configured Normalization -- Raw leaves them the
        // literal tick counts (same as BuildVolumeProfileRawValues); Normalized
        // reports each bucket's fraction-of-total volume; Local/GlobalZScore
        // z-scores that same fraction array across buckets (see
        // VolumeProfileEncoder's own ApplyZScore). Feeds the Normalization
        // column's Volume Profile card.
        private static IReadOnlyList<string> BuildVolumeProfileNormalizedValues(BrainRecipe? recipe, List<BarPriceHistogram> episodeHistograms)
        {
            if (recipe is null || episodeHistograms.Count == 0)
                return [];

            var sizes = recipe.EncoderSizes ?? new EncoderSizeOptions();
            var merged = BarPriceHistogram.Merge(episodeHistograms, sizes.VolumeProfileLookbackBars);
            if (merged.Count == 0)
                return [];

            if (sizes.VolumeProfileNormalization == NormalizationKind.Raw)
            {
                var (counts, _) = VolumeProfileAnalysis.BuildRawHistogram(merged, sizes.VolumeProfileBucketCount);
                return counts.Select(c => c.ToString("0", CultureInfo.InvariantCulture)).ToList();
            }

            var fractions = VolumeProfileAnalysis.BuildRelativeHistogram(merged, sizes.VolumeProfileBucketCount);
            if (sizes.VolumeProfileNormalization is NormalizationKind.LocalZScore or NormalizationKind.GlobalZScore)
                return ApplyZScore(fractions, sizes.VolumeProfileNormalization, sizes.VolumeProfileZScoreSubWindowSize).Select(FormatZScore).ToList();
            return fractions.Select(FormatFraction).ToList();
        }

        // Time Profile counterpart to BuildVolumeProfileNormalizedValues -- Raw
        // leaves the literal seconds-spent (same as BuildTimeProfileRawValues);
        // every other mode reports each bucket's fraction-of-total seconds
        // (TimeProfileEncoder doesn't offer Local/GlobalZScore -- see
        // BrainBuilder's input table -- so no z-score branch here).
        private static IReadOnlyList<string> BuildTimeProfileNormalizedValues(
            BrainRecipe? recipe, List<HlocAvdtBar> episodeBars, List<BarPriceHistogram> episodeHistograms)
        {
            if (recipe is null || episodeHistograms.Count == 0)
                return [];

            var sizes = recipe.EncoderSizes ?? new EncoderSizeOptions();
            var merged = BarPriceHistogram.MergeTime(episodeBars, episodeHistograms, sizes.TimeProfileLookbackBars);
            if (merged.Count == 0)
                return [];

            if (sizes.TimeProfileNormalization == NormalizationKind.Raw)
            {
                var (seconds, _) = TimeProfileAnalysis.BuildRawHistogram(merged, sizes.TimeProfileBucketCount);
                return seconds.Select(s => s.ToString("0.#", CultureInfo.InvariantCulture)).ToList();
            }

            return TimeProfileAnalysis.BuildRelativeHistogram(merged, sizes.TimeProfileBucketCount).Select(FormatFraction).ToList();
        }

        // Feeds BuildVolumeProfileNormalizedValues' LocalZScore/GlobalZScore
        // branch -- mirrors ZScoreNormalization's own Global/Local split.
        private static float[] ApplyZScore(float[] values, NormalizationKind kind, int subWindowSize) =>
            kind == NormalizationKind.GlobalZScore
                ? ZScoreNormalization.Global(Array.ConvertAll(values, v => (double)v))
                : ZScoreNormalization.Local(Array.ConvertAll(values, v => (double)v), subWindowSize);

        private static string FormatFraction(float f) => (f * 100).ToString("0.00", CultureInfo.InvariantCulture) + "%";

        // Bar Window's own encoder feeds the trailing BarWindowSize bars'
        // Close prices, most-recent-last -- reuses the same random episode
        // the Episode column already sampled. Always the literal prices,
        // regardless of Normalization -- feeds the Source column's card (see
        // TrainingPreview.razor), which shows the original data the encoder
        // starts from either way.
        private static IReadOnlyList<string> BuildBarWindowRawValues(List<HlocAvdtBar> episodeBars, int barWindowSize)
        {
            if (episodeBars.Count == 0)
                return [];
            return episodeBars.TakeLast(barWindowSize).Select(b => b.Close.ToString("0.####", CultureInfo.InvariantCulture)).ToList();
        }

        // Same trailing prices, but transformed the way BarWindowEncoder.Encode
        // itself would for the configured Normalization -- Raw mode leaves them
        // unchanged (same as BuildBarWindowRawValues); every other mode applies
        // percent-offset from the most recent price, or a Local/Global Z-Score
        // against the window's own prices (see ZScoreNormalization). Feeds the
        // Normalization column's own Bar Window card, so what the network would
        // actually see is visible alongside the raw values in Source.
        private static IReadOnlyList<string> BuildBarWindowNormalizedValues(
            List<HlocAvdtBar> episodeBars, NormalizationKind normalization, int barWindowSize, int zScoreSubWindowSize)
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
                _ => BuildPercentOffsets(prices),
            };
        }

        // Normalized mode's percent-change-from-current-price -- the anchor
        // is the window's own most recent (last) price, the same
        // observation.CurrentPrice a live decision would use at that instant.
        private static List<string> BuildPercentOffsets(List<double> prices)
        {
            double currentPrice = prices[^1];
            return prices
                .Select(p => currentPrice == 0 ? 0 : (p - currentPrice) / currentPrice)
                .Select(offset => (offset * 100).ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture) + "%")
                .ToList();
        }

        private static string FormatZScore(float z) => z.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture);

        // One value per TraderStateEncoder feature, from a flat trader sitting
        // at this episode's own last close with StartingCash on hand and no
        // realized/unrealized P&L yet -- the same state a live run's Trader
        // begins each episode from, so the Source column's Trader State card
        // shows real numbers instead of "--". Tooltip is that feature's own
        // name (see TraderStateEncoder.Features) since values like "FLAT" or
        // "10000.00" aren't self-explanatory the way Bar Window's prices are.
        // Feeds both RawValues and NormalizedValues identically -- every
        // TraderStateEncoder normalizer is a passthrough (see that class's
        // doc comment), so there's nothing separate to show "normalized".
        private static IReadOnlyList<(string Value, string Tooltip)> BuildTraderStateValues(List<HlocAvdtBar> episodeBars, double startingCash)
        {
            if (episodeBars.Count == 0)
                return [];

            double currentPrice = episodeBars[^1].Close;
            var capital = new CapitalState(startingCash, RealizedPnl: 0, UnrealizedPnl: 0, MarginUsed: 0, MarginLevel: null);
            var observation = new SimulationObservation(
                episodeBars[^1].OpenTime, currentPrice, [], [], [], capital, Position: null, currentPrice, currentPrice);

            var encoder = new TraderStateEncoder();
            var raw = encoder.Encode(observation);
            var features = encoder.Features;

            var values = new List<(string, string)>(features.Count);
            for (int i = 0; i < features.Count; i++)
                values.Add((features[i].Normalizer.Format(raw[i]), features[i].Label));
            return values;
        }

        private static string NormalizationLabel(NormalizationKind kind) => kind switch
        {
            NormalizationKind.Raw => "Raw",
            NormalizationKind.LocalZScore => "Local Z-Score",
            NormalizationKind.GlobalZScore => "Global Z-Score",
            _ => "Normalized",
        };

        // One row per included input set, in the same order as Brain
        // Builder's own input table, always ending with the always-on
        // Trader State.
        private static IReadOnlyList<TrainingPreviewSourceRow> BuildSourceRows(
            BrainRecipe? recipe, List<HlocAvdtBar> episodeBars, List<BarPriceHistogram> episodeHistograms, double startingCash)
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
                rows.Add(new("Volume Profile", count.ToString(), normalization,
                    $"{sizes.VolumeProfileLookbackBars} bars", sizes.VolumeProfileBucketCount.ToString(),
                    RawValues: BuildVolumeProfileRawValues(recipe, episodeHistograms),
                    NormalizedValues: BuildVolumeProfileNormalizedValues(recipe, episodeHistograms)));
            }
            if (recipe.EncoderKinds.Contains(EncoderKind.TimeProfile))
            {
                var count = BrainCatalog.CreateEncoder(EncoderKind.TimeProfile, sizes).FeatureCount;
                var normalization = NormalizationLabel(sizes.TimeProfileNormalization) + (sizes.TimeProfileSplitUpDown ? " (split)" : "");
                rows.Add(new("Time Profile", count.ToString(), normalization,
                    $"{sizes.TimeProfileLookbackBars} bars", sizes.TimeProfileBucketCount.ToString(),
                    RawValues: BuildTimeProfileRawValues(recipe, episodeBars, episodeHistograms),
                    NormalizedValues: BuildTimeProfileNormalizedValues(recipe, episodeBars, episodeHistograms)));
            }
            if (recipe.EncoderKinds.Contains(EncoderKind.SessionClock))
            {
                var count = BrainCatalog.CreateEncoder(EncoderKind.SessionClock, sizes).FeatureCount;
                rows.Add(new("Session Clock", count.ToString(), "—", "—", "—"));
            }
            if (recipe.EncoderKinds.Contains(EncoderKind.SpreadVolatility))
            {
                var count = BrainCatalog.CreateEncoder(EncoderKind.SpreadVolatility, sizes).FeatureCount;
                rows.Add(new("Spread + Volatility", count.ToString(), "—", $"{sizes.SpreadVolatilityAtrPeriod} bars", "—"));
            }
            var traderStateValues = BuildTraderStateValues(episodeBars, startingCash);
            rows.Add(new("Trader State", new TraderStateEncoder().FeatureCount.ToString(), "Raw", "—", "—",
                RawValues: traderStateValues.Select(v => v.Value).ToList(),
                NormalizedValues: traderStateValues.Select(v => v.Value).ToList(),
                ValueTooltips: traderStateValues.Select(v => v.Tooltip).ToList()));
            return rows;
        }

        // Architecture identity only (no layer sizes -- see BuildHiddenLayerItems
        // for those) -- MLP/LSTM need nothing beyond their own name; CNN also
        // names which selected input set its kernel sweeps over.
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
                _ => ["MLP"],
            };
        }

        // The network's hidden shape -- mirrors BrainCatalog.CreateArchitecture's
        // own reading of HiddenSizes: MLP's variable-length hidden-layer sizes,
        // LSTM's hidden-state size + stacked-layer count, or CNN's kernel size
        // + per-layer channel counts.
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
                    items.Add($"Kernel: {(hiddenSizes.Count > 0 ? hiddenSizes[0] : 3)}");
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

        // One bar per HiddenLayerItems entry -- unlike Input's single stacked
        // bar (parts of one whole), these are independent quantities (each
        // layer/channel/kernel its own separate bar), so HeightPercent is
        // relative to whichever entry is largest rather than summing to 100%.
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

        // One segment of a single stacked bar -- HeightPercent is this
        // segment's share of the bar's total (all segments sum to 100%),
        // shared shape for both Input (per source) and Output (per decoded
        // action group) stacked bars, each with their own distinct color per
        // segment (see SourceColorClass/OutputColorClass).
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

        // Which CSS modifier colors an Output bar segment -- one badge color
        // per decoded action group, reusing the same tp-source-badge-*
        // palette Source/Input already use (a distinct, unrelated set of
        // labels, so no collision even though the classes are shared).
        private static string OutputColorClass(string label) => label switch
        {
            "Direction" => "tp-source-badge-1",
            "Volume" => "tp-source-badge-2",
            "Stop Loss" => "tp-source-badge-3",
            "Take Profit" => "tp-source-badge-4",
            _ => "tp-source-badge-neutral",
        };

        // Stacked-bar counterpart to BuildTotalOutputWidth's plain sum --
        // one segment per decoded action group (Direction always present,
        // Volume/Stop Loss/Take Profit only when enabled), each segment's
        // share of the total output width.
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

        // One collapse funnel per independently-decoded group, mirroring
        // Brain Builder's own Action Decoding rows but as a diagram: each
        // group's raw N-way logits (Circles) converge through a single
        // decode badge (softmax samples weighted by probability, argmax
        // always picks the top logit -- see DecisionDecodingKind) down a
        // stem pointing at that group's action-set list (ActionLabels),
        // whose first entry is the one visually connected.
        private static List<ActionDecoderGroup> BuildActionDecoderGroups(BrainRecipe? recipe)
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
            var directionLabels = new List<string>();
            if (directions.Contains(SimulationAction.Hold))
                directionLabels.Add("Hold");
            if (directions.Contains(SimulationAction.EnterLong))
                directionLabels.Add("Enter Long");
            if (directions.Contains(SimulationAction.EnterShort))
                directionLabels.Add("Enter Short");
            if (directions.Contains(SimulationAction.Close))
                directionLabels.Add("Close");
            if (directionLabels.Count > 0)
                groups.Add(BuildActionDecoderGroup("Direction", OutputColorClass("Direction"), decodeLabel, decodeTooltip, directionLabels));

            var actionKinds = recipe.ActionOutputKinds ?? [];
            if (actionKinds.Contains(ActionOutputKind.Volume))
                groups.Add(BuildActionDecoderGroup("Volume", OutputColorClass("Volume"), decodeLabel, decodeTooltip,
                    DiscretizedLevelsActionHead.GetLevelLabels(ActionOutputKind.Volume)));
            if (actionKinds.Contains(ActionOutputKind.StopLoss))
                groups.Add(BuildActionDecoderGroup("Stop Loss", OutputColorClass("Stop Loss"), decodeLabel, decodeTooltip,
                    DiscretizedLevelsActionHead.GetLevelLabels(ActionOutputKind.StopLoss)));
            if (actionKinds.Contains(ActionOutputKind.TakeProfit))
                groups.Add(BuildActionDecoderGroup("Take Profit", OutputColorClass("Take Profit"), decodeLabel, decodeTooltip,
                    DiscretizedLevelsActionHead.GetLevelLabels(ActionOutputKind.TakeProfit)));

            return groups;
        }

        // Lays out one group's funnel left to right: Circles spread evenly
        // down the diagram's left edge (one per action-set option), each
        // with a FunnelLine converging on the decode badge's left-center,
        // and a StemLine from the badge's right-center pointing at the
        // action-set list rendered in HTML to the right of the svg (see
        // TrainingPreview.razor).
        private static ActionDecoderGroup BuildActionDecoderGroup(
            string label, string colorClass, string decodeLabel, string decodeTooltip, IReadOnlyList<string> actionLabels)
        {
            int ways = actionLabels.Count;

            var circles = new List<ActionDecoderCircle>(ways);
            var funnelLines = new List<ActionDecoderLine>(ways);
            for (int i = 0; i < ways; i++)
            {
                double y = ways <= 1 ? DecoderCenterY : DecoderCirclePadY + i * (DecoderViewBoxHeight - 2 * DecoderCirclePadY) / (ways - 1);
                circles.Add(new ActionDecoderCircle(FormatCoord(DecoderCircleX), FormatCoord(y)));
                funnelLines.Add(new ActionDecoderLine(FormatCoord(DecoderCircleX), FormatCoord(y), FormatCoord(DecoderBadgeX), FormatCoord(DecoderCenterY)));
            }

            double badgeY = DecoderCenterY - DecoderBadgeHeight / 2;
            double badgeRightX = DecoderBadgeX + DecoderBadgeWidth;
            double stemEndX = badgeRightX + DecoderStemLength;
            var stemLine = new ActionDecoderLine(FormatCoord(badgeRightX), FormatCoord(DecoderCenterY), FormatCoord(stemEndX), FormatCoord(DecoderCenterY));

            return new ActionDecoderGroup(
                label, colorClass, decodeLabel, decodeTooltip, circles, funnelLines,
                FormatCoord(DecoderBadgeX), FormatCoord(badgeY), FormatCoord(DecoderBadgeWidth), FormatCoord(DecoderBadgeHeight),
                FormatCoord(DecoderBadgeX + DecoderBadgeWidth / 2), FormatCoord(DecoderCenterY),
                stemLine, actionLabels);
        }
    }

    // One point in an Action Decoder funnel's left edge -- one Circle per
    // action-set option (see TrainingPreview.BuildActionDecoderGroup).
    public sealed record ActionDecoderCircle(string Cx, string Cy);

    // One straight connector in an Action Decoder funnel, either a circle-
    // to-badge FunnelLine or the badge-to-actions StemLine.
    public sealed record ActionDecoderLine(string X1, string Y1, string X2, string Y2);

    // One decoded group's full collapse funnel, left to right -- Circles/
    // FunnelLines/Badge*/StemLine are svg geometry within the fixed
    // TrainingPreview.DecoderViewBox; ActionLabels is the group's action set
    // in order, with ActionLabels[0] the one StemLine visually points at.
    public sealed record ActionDecoderGroup(
        string Label, string ColorClass, string DecodeLabel, string DecodeTooltip,
        IReadOnlyList<ActionDecoderCircle> Circles, IReadOnlyList<ActionDecoderLine> FunnelLines,
        string BadgeX, string BadgeY, string BadgeWidth, string BadgeHeight, string BadgeCx, string BadgeCy,
        ActionDecoderLine StemLine, IReadOnlyList<string> ActionLabels);

    // One row of the Source/Inputs/Normalization/Window/Bucket columns --
    // Normalization/Window/Bucket are "--" where a source has no such concept
    // (e.g. Session Clock has no window, Bar Window has no bucketing).
    // RawValues/NormalizedValues are populated for Bar Window, the profiles,
    // and Trader State (see the matching Build*Values methods) -- RawValues
    // feeds a card under Source (always the literal prices), NormalizedValues
    // feeds a matching card under Normalization (raw or transformed, to match
    // this row's own Normalization), so both what the encoder starts from and
    // what the network actually sees are visible side by side. ValueTooltips,
    // when non-empty, is index-aligned with both -- each value's own field
    // name (see BuildTraderStateValues), for sources whose values aren't
    // self-explanatory the way Bar Window's prices are.
    public sealed record TrainingPreviewSourceRow(
        string Source, string Inputs, string Normalization, string Window, string Bucket,
        IReadOnlyList<string>? RawValues = null, IReadOnlyList<string>? NormalizedValues = null,
        IReadOnlyList<string>? ValueTooltips = null)
    {
        public IReadOnlyList<string> RawValues { get; init; } = RawValues ?? [];
        public IReadOnlyList<string> NormalizedValues { get; init; } = NormalizedValues ?? [];
        public IReadOnlyList<string> ValueTooltips { get; init; } = ValueTooltips ?? [];
    }


}
