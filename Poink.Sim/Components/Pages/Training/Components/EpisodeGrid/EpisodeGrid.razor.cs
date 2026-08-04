using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Training
{

    public partial class EpisodeGrid : ComponentBase
    {
        [Parameter] public IReadOnlyList<RlEpisodeMetric> Episodes { get; set; } = Array.Empty<RlEpisodeMetric>();
        [Parameter] public bool IsRunning { get; set; }

        private sealed record GridBlock(int Episode, double Reward, double FinalEquity, string Glyph, string ColorCss, bool IsLatest);

        private List<GridBlock> Blocks
        {
            get
            {
                if (Episodes.Count == 0)
                    return new List<GridBlock>();

                double maxAbs = Episodes.Max(e => Math.Abs(e.Reward));
                var blocks = new List<GridBlock>(Episodes.Count);
                for (int i = 0; i < Episodes.Count; i++)
                {
                    var e = Episodes[i];
                    double intensity = maxAbs > 0 ? Math.Clamp(Math.Abs(e.Reward) / maxAbs, 0.2, 1.0) : 0.3;
                    string glyph = e.Reward switch { > 0 => "+", < 0 => "−", _ => "·" };
                    string colorVar = e.Reward switch { > 0 => "--color-success", < 0 => "--color-error", _ => "--color-muted" };
                    string colorCss = $"color-mix(in srgb, var({colorVar}) {(int)Math.Round(intensity * 100)}%, transparent)";
                    blocks.Add(new GridBlock(e.EpisodeNumber, e.Reward, e.FinalEquity, glyph, colorCss, i == Episodes.Count - 1));
                }
                return blocks;
            }
        }
    }
}
