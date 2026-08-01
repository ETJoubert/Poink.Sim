using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Training
{
    // Heatmap of which slices of the selected data range completed episodes have
    // actually sampled from -- one block per BlockSpan-length slice of [From, To),
    // shaded by how many episode windows overlap it. From/To/BlockSpan bind to
    // the Train page's own Data Range/bars-per-episode controls (not a run's
    // frozen parameters), so the block layout previews live as those are edited,
    // even before Start Training is clicked.
    public partial class EpisodeCoverageTimeline : ComponentBase
    {
        // Caps rendered blocks for a pathological combination (tiny BlockSpan
        // over a huge range) -- grouped blocks still sum their sub-block usage
        // counts, so shading stays meaningful, just coarser.
        private const int MaxRenderedBlocks = 1000;

        [Parameter] public DateTime From { get; set; }
        [Parameter] public DateTime To { get; set; }
        [Parameter] public TimeSpan BlockSpan { get; set; }
        [Parameter] public IReadOnlyList<RlEpisodeWindow> EpisodeWindows { get; set; } = Array.Empty<RlEpisodeWindow>();

        // Stacks blocks top-to-bottom (each sized by height%) instead of
        // left-to-right (width%) -- for narrow layouts like TrainingPreview's
        // Data column, where a horizontal bar wouldn't fit.
        [Parameter] public bool Vertical { get; set; }

        // Hides the DATA COVERAGE/date-range header -- for embedding
        // alongside text that already states the range (see TrainingPreview).
        [Parameter] public bool Compact { get; set; }

        // Optional single window to mark on top of the block grid -- doesn't
        // need to align to a block boundary (a randomly-picked episode
        // usually won't), so it's drawn as its own absolutely-positioned
        // overlay rather than folded into the per-block heatmap.
        [Parameter] public DateTime? HighlightStart { get; set; }
        [Parameter] public DateTime? HighlightEnd { get; set; }

        private sealed record Block(DateTime Start, DateTime End, double WidthPercent, int Count, double Intensity);

        // Empty (renders nothing) until a usable range/block size is set --
        // see EpisodeCoverageTimeline.razor.
        private bool HasRange => To > From && BlockSpan > TimeSpan.Zero;

        private bool HasHighlight => HasRange && HighlightStart is not null && HighlightEnd is not null && HighlightEnd > HighlightStart;

        // Percent offset/size along the track, clamped into [From, To] --
        // a highlight computed before a From/To edit could otherwise briefly
        // poke outside the track until TrainingPreview re-rolls it.
        private double HighlightOffsetPercent =>
            Math.Clamp((HighlightStart!.Value - From).TotalSeconds / (To - From).TotalSeconds * 100.0, 0, 100);

        private double HighlightSizePercent =>
            Math.Clamp((HighlightEnd!.Value - HighlightStart!.Value).TotalSeconds / (To - From).TotalSeconds * 100.0, 0, 100 - HighlightOffsetPercent);

        // One block per BlockSpan slice of [From, To) -- the final block is
        // clipped to To (and rendered narrower via WidthPercent) rather than
        // overhanging past the selected range's end.
        private List<Block> Blocks
        {
            get
            {
                if (!HasRange) return new List<Block>();

                double totalSeconds = (To - From).TotalSeconds;
                long naturalCount = (long)Math.Ceiling(totalSeconds / BlockSpan.TotalSeconds);
                int groupSize = naturalCount > MaxRenderedBlocks
                    ? (int)Math.Ceiling(naturalCount / (double)MaxRenderedBlocks)
                    : 1;
                var effectiveSpan = TimeSpan.FromSeconds(BlockSpan.TotalSeconds * groupSize);

                var blocks = new List<Block>();
                for (var start = From; start < To; start += effectiveSpan)
                {
                    var end = start + effectiveSpan > To ? To : start + effectiveSpan;
                    int count = EpisodeWindows.Count(w => w.Start < end && w.End > start);
                    blocks.Add(new Block(start, end, (end - start).TotalSeconds / totalSeconds * 100.0, count, 0));
                }

                int maxCount = blocks.Count == 0 ? 0 : blocks.Max(b => b.Count);
                return maxCount == 0 ? blocks : blocks.Select(b => b with { Intensity = b.Count / (double)maxCount }).ToList();
            }
        }

        // CSS width percentages/color-mix stops must use '.' as the decimal
        // separator regardless of host locale -- see RewardChart for the same
        // concern with SVG coordinates.
        private static string Pct(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
