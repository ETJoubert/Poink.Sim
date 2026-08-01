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
        // Matches RlEpisodeRunner's own PriceBucketWidth -- irrelevant here
        // (the per-bar histogram this produces is discarded, see
        // FetchRandomEpisodeBars), just kept consistent with the app's other
        // SimulationBarAggregator call sites rather than an arbitrary value.
        private const double PriceBucketWidth = 0.01;

        private const double ChartViewWidth = 120;
        private const double ChartViewHeight = 140;
        private const double ChartPad = 4;

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

        // Random ticks_* windows routinely land on a gap (FX markets close
        // over weekends -- see SessionClockEncoder), so one empty attempt
        // doesn't mean the table has no data at all -- worth a few retries
        // before giving up and showing "no data".
        private const int MaxRandomEpisodeAttempts = 8;

        private string? _lastEpisodeFetchKey;
        private bool _loadingEpisode;
        private string? _episodeError;
        private List<HlocAvdtBar> _episodeBars = [];

        // The picked window's own [Start, End) -- feeds the Data column's
        // vertical timeline so it can highlight exactly where this Episode
        // chart's sample came from (see TrainingPreview.razor).
        private DateTime? _episodeWindowStart;
        private DateTime? _episodeWindowEnd;

        private IReadOnlyList<TrainingPreviewSourceRow> SourceRows => BuildSourceRows(Recipe, _episodeBars);
        private IReadOnlyList<string> StructureItems => BuildStructureItems(Recipe);
        private IReadOnlyList<string> ActionItems => BuildActionItems(Recipe);
        private IReadOnlyList<string> DecodedActionItems => BuildDecodedActionItems(Recipe);
        private List<EpisodeBarShape> EpisodeBarShapes => BuildEpisodeBarShapes(_episodeBars);

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
            if (key == _lastEpisodeFetchKey) return;
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
                _episodeWindowStart = result?.Start;
                _episodeWindowEnd = result?.End;
            }
            catch (Exception ex)
            {
                _episodeBars = [];
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

        private sealed record RandomEpisodeResult(List<HlocAvdtBar> Bars, DateTime Start, DateTime End);

        // Picks a random BlockSpan-length window inside [from, to) and
        // aggregates its ticks into bars, the same tick-by-tick aggregation
        // RlEpisodeRunner uses live during training -- so this preview is
        // built from one plausible real episode, not synthetic data. Retries
        // a few times if a window comes back with zero ticks (see
        // MaxRandomEpisodeAttempts) before giving up.
        private RandomEpisodeResult? FetchRandomEpisode(string table, DateTime from, DateTime to, TimeSpan span, TimeSpan barSize)
        {
            if (to - from < span) return null;

            for (int attempt = 0; attempt < MaxRandomEpisodeAttempts; attempt++)
            {
                var result = FetchEpisode(table, from, to, span, barSize);
                if (result.Bars.Count > 0) return result;
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
            foreach (var tick in TickRepository.ReadTicks(table, start, end))
            {
                if (aggregator.Feed(tick) is { } completed) bars.Add(completed.Bar);
            }
            if (aggregator.CurrentPartialBar is { } partial) bars.Add(partial);
            return new RandomEpisodeResult(bars, start, end);
        }

        // Vertical High/Low range bar per aggregated bar -- a simplified OHLC
        // bar chart (range only, no open/close ticks), colored by whether
        // that bar closed up or down.
        private sealed record EpisodeBarShape(string X, string Y, string Width, string Height, bool IsUp, string Tooltip);

        private static List<EpisodeBarShape> BuildEpisodeBarShapes(List<HlocAvdtBar> bars)
        {
            if (bars.Count == 0) return [];

            double min = bars.Min(b => b.Low);
            double max = bars.Max(b => b.High);
            if (max - min < 1e-9) { min -= 0.01; max += 0.01; }

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

        // Bar Window's own encoder feeds the trailing BarWindowSize bars'
        // Close prices, most-recent-last -- reuses the same random episode
        // the Episode column already sampled. Always the literal prices,
        // regardless of Normalization -- feeds the Source column's card (see
        // TrainingPreview.razor), which shows the original data the encoder
        // starts from either way.
        private static IReadOnlyList<string> BuildBarWindowRawPrices(List<HlocAvdtBar> episodeBars, int barWindowSize)
        {
            if (episodeBars.Count == 0) return [];
            return episodeBars.TakeLast(barWindowSize).Select(b => b.Close.ToString("0.####", CultureInfo.InvariantCulture)).ToList();
        }

        // Same trailing prices, but transformed the way BarWindowEncoder.Encode
        // itself would for the configured Normalization -- Raw mode leaves them
        // unchanged (same as BuildBarWindowRawPrices); every other mode applies
        // percent-offset from the most recent price, or a Local/Global Z-Score
        // against the window's own prices (see ZScoreNormalization). Feeds the
        // Normalization column's own Bar Window card, so what the network would
        // actually see is visible alongside the raw values in Source.
        private static IReadOnlyList<string> BuildBarWindowNormalizedValues(
            List<HlocAvdtBar> episodeBars, NormalizationKind normalization, int barWindowSize, int zScoreSubWindowSize)
        {
            if (episodeBars.Count == 0) return [];

            var prices = episodeBars.TakeLast(barWindowSize).Select(b => b.Close).ToList();
            if (prices.Count == 0) return [];

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
        private static IReadOnlyList<TrainingPreviewSourceRow> BuildSourceRows(BrainRecipe? recipe, List<HlocAvdtBar> episodeBars)
        {
            var rows = new List<TrainingPreviewSourceRow>();
            if (recipe is null) return rows;

            var sizes = recipe.EncoderSizes ?? new EncoderSizeOptions();
            if (recipe.EncoderKinds.Contains(EncoderKind.BarWindow))
            {
                var count = BrainCatalog.CreateEncoder(EncoderKind.BarWindow, sizes).FeatureCount;
                rows.Add(new("Bar Window", count.ToString(), NormalizationLabel(sizes.BarWindowNormalization),
                    $"{sizes.BarWindowSize} bars", "—",
                    RawPrices: BuildBarWindowRawPrices(episodeBars, sizes.BarWindowSize),
                    NormalizedValues: BuildBarWindowNormalizedValues(
                        episodeBars, sizes.BarWindowNormalization, sizes.BarWindowSize, sizes.BarWindowZScoreSubWindowSize)));
            }
            if (recipe.EncoderKinds.Contains(EncoderKind.VolumeProfile))
            {
                var count = BrainCatalog.CreateEncoder(EncoderKind.VolumeProfile, sizes).FeatureCount;
                var normalization = NormalizationLabel(sizes.VolumeProfileNormalization) + (sizes.VolumeProfileSplitUpDown ? " (split)" : "");
                rows.Add(new("Volume Profile", count.ToString(), normalization,
                    $"{sizes.VolumeProfileLookbackBars} bars", sizes.VolumeProfileBucketCount.ToString()));
            }
            if (recipe.EncoderKinds.Contains(EncoderKind.TimeProfile))
            {
                var count = BrainCatalog.CreateEncoder(EncoderKind.TimeProfile, sizes).FeatureCount;
                var normalization = NormalizationLabel(sizes.TimeProfileNormalization) + (sizes.TimeProfileSplitUpDown ? " (split)" : "");
                rows.Add(new("Time Profile", count.ToString(), normalization,
                    $"{sizes.TimeProfileLookbackBars} bars", sizes.TimeProfileBucketCount.ToString()));
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
            rows.Add(new("Trader State", new TraderStateEncoder().FeatureCount.ToString(), "Raw", "—", "—"));
            return rows;
        }

        // Architecture kind followed by its shape (hidden layers, LSTM
        // size/depth, or CNN kernel/sweep/channels) -- mirrors
        // BrainCatalog.CreateArchitecture's own reading of HiddenSizes.
        private static IReadOnlyList<string> BuildStructureItems(BrainRecipe? recipe)
        {
            if (recipe is null) return [];

            var items = new List<string>();
            var hiddenSizes = recipe.HiddenSizes;
            switch (recipe.ArchitectureKind)
            {
                case ArchitectureKind.Lstm:
                    items.Add("LSTM");
                    items.Add($"Hidden: {(hiddenSizes.Count > 0 ? hiddenSizes[0] : 64)}");
                    items.Add($"Layers: {(hiddenSizes.Count > 1 ? hiddenSizes[1] : 2)}");
                    break;
                case ArchitectureKind.Cnn:
                    items.Add("CNN");
                    items.Add($"Sweeps: {(recipe.CnnKernelSweepSource == CnnKernelSweepSource.VolumeProfile ? "Volume Profile" : "Bar Window")}");
                    items.Add($"Kernel: {(hiddenSizes.Count > 0 ? hiddenSizes[0] : 3)}");
                    var channels = hiddenSizes.Count > 1 ? hiddenSizes.Skip(1).ToList() : [16, 32];
                    for (int i = 0; i < channels.Count; i++)
                        items.Add($"C{i + 1}: {channels[i]}");
                    break;
                default:
                    items.Add("MLP");
                    for (int i = 0; i < hiddenSizes.Count; i++)
                        items.Add($"L{i + 1}: {hiddenSizes[i]}");
                    break;
            }
            return items;
        }

        // Every enabled direction plus optional Volume/Stop Loss/Take Profit
        // outputs.
        private static IReadOnlyList<string> BuildActionItems(BrainRecipe? recipe)
        {
            if (recipe is null) return [];

            var directions = recipe.EnabledDirections ?? ComposedPolicyBrain.AllDirections;
            var items = new List<string>();
            if (directions.Contains(SimulationAction.Hold)) items.Add("Hold");
            if (directions.Contains(SimulationAction.EnterLong)) items.Add("Enter Long");
            if (directions.Contains(SimulationAction.EnterShort)) items.Add("Enter Short");
            if (directions.Contains(SimulationAction.Close)) items.Add("Close");

            var actionKinds = recipe.ActionOutputKinds ?? [];
            if (actionKinds.Contains(ActionOutputKind.Volume)) items.Add("Volume");
            if (actionKinds.Contains(ActionOutputKind.StopLoss)) items.Add("Stop Loss");
            if (actionKinds.Contains(ActionOutputKind.TakeProfit)) items.Add("Take Profit");
            return items;
        }

        // One line per independently-decoded softmax group, mirroring Brain
        // Builder's own Action Decoding rows.
        private static IReadOnlyList<string> BuildDecodedActionItems(BrainRecipe? recipe)
        {
            if (recipe is null) return [];

            var directions = recipe.EnabledDirections ?? ComposedPolicyBrain.AllDirections;
            var decodingLabel = recipe.DecisionDecoding switch
            {
                DecisionDecodingKind.HardMax => "hardmax",
                _ when recipe.DecisionTemperature != 1.0 => $"softmax @ T={recipe.DecisionTemperature:0.##}",
                _ => "softmax",
            };

            var items = new List<string> { $"Direction: {directions.Count}-way {decodingLabel}" };
            var actionKinds = recipe.ActionOutputKinds ?? [];
            if (actionKinds.Contains(ActionOutputKind.Volume))
                items.Add($"Volume: {BrainCatalog.CreateActionOutputHead(ActionOutputKind.Volume, recipe.ActionOutputEncodingKind).OutputWidth}-way {decodingLabel}");
            if (actionKinds.Contains(ActionOutputKind.StopLoss))
                items.Add($"Stop Loss: {BrainCatalog.CreateActionOutputHead(ActionOutputKind.StopLoss, recipe.ActionOutputEncodingKind).OutputWidth}-way {decodingLabel}");
            if (actionKinds.Contains(ActionOutputKind.TakeProfit))
                items.Add($"Take Profit: {BrainCatalog.CreateActionOutputHead(ActionOutputKind.TakeProfit, recipe.ActionOutputEncodingKind).OutputWidth}-way {decodingLabel}");
            return items;
        }
    }

    // One row of the Source/Inputs/Normalization/Window/Bucket columns --
    // Normalization/Window/Bucket are "--" where a source has no such concept
    // (e.g. Session Clock has no window, Bar Window has no bucketing).
    // RawPrices/NormalizedValues are only populated for Bar Window (see
    // BuildBarWindowRawPrices/BuildBarWindowNormalizedValues) -- RawPrices
    // feeds a card under Source (always the literal prices), NormalizedValues
    // feeds a matching card under Normalization (raw or transformed, to match
    // this row's own Normalization), so both what the encoder starts from and
    // what the network actually sees are visible side by side.
    public sealed record TrainingPreviewSourceRow(
        string Source, string Inputs, string Normalization, string Window, string Bucket,
        IReadOnlyList<string>? RawPrices = null, IReadOnlyList<string>? NormalizedValues = null)
    {
        public IReadOnlyList<string> RawPrices { get; init; } = RawPrices ?? [];
        public IReadOnlyList<string> NormalizedValues { get; init; } = NormalizedValues ?? [];
    }
}
