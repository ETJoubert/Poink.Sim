using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Training
{

    public partial class EpisodeCoverageTimeline : ComponentBase
    {
        private const int MaxRenderedBlocks = 1000;

        [Parameter] public DateTime From { get; set; }
        [Parameter] public DateTime To { get; set; }
        [Parameter] public TimeSpan BlockSpan { get; set; }
        [Parameter] public IReadOnlyList<RlEpisodeWindow> EpisodeWindows { get; set; } = Array.Empty<RlEpisodeWindow>();


        [Parameter] public bool Vertical { get; set; }

        [Parameter] public bool Compact { get; set; }


        [Parameter] public DateTime? HighlightStart { get; set; }
        [Parameter] public DateTime? HighlightEnd { get; set; }

        private sealed record Block(DateTime Start, DateTime End, double WidthPercent, int Count, double Intensity);


        private bool HasRange => To > From && BlockSpan > TimeSpan.Zero;

        private bool HasHighlight => HasRange && HighlightStart is not null && HighlightEnd is not null && HighlightEnd > HighlightStart;


        private double HighlightOffsetPercent =>
            Math.Clamp((HighlightStart!.Value - From).TotalSeconds / (To - From).TotalSeconds * 100.0, 0, 100);

        private double HighlightSizePercent =>
            Math.Clamp((HighlightEnd!.Value - HighlightStart!.Value).TotalSeconds / (To - From).TotalSeconds * 100.0, 0, 100 - HighlightOffsetPercent);


        private List<Block> Blocks
        {
            get
            {
                if (!HasRange)
                    return new List<Block>();

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

        private static string Pct(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
