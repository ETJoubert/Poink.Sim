using System;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Models;
using Poink.Sim.Simulation;

namespace Poink.Sim.Components.Shared.SimTimeline;

// Self-contained: subscribes to SimulationEngine.OnProgress directly rather
// than taking Progress as a [Parameter], so its StateHasChanged calls only
// re-render this component -- not SimulationPage's whole tree. That's what
// lets an uncapped Max run's rapid progress-only publishes (see
// SimulationEngine.PublishProgress) keep this rail live without forcing the
// chart/log/stats to re-render on every slice.
public partial class SimTimeline : ComponentBase, IDisposable
{
    [Inject] private SimulationEngine Service { get; set; } = default!;

    protected override void OnInitialized() => Service.OnProgress += HandleProgress;

    private void HandleProgress() => _ = InvokeAsync(StateHasChanged);

    public void Dispose() => Service.OnProgress -= HandleProgress;

    private static double Percent(SimulationProgress p)
    {
        if (p.CurrentTimestampUtc is not { } cur) return 0;
        var totalTicks = (p.RangeToUtc - p.RangeFromUtc).Ticks;
        if (totalTicks <= 0) return 0;
        return Math.Clamp((cur - p.RangeFromUtc).Ticks * 100.0 / totalTicks, 0, 100);
    }

    // CSS percentages must use '.' regardless of host locale -- see
    // DateRangeSlider/TickVolumeProfileChart's Pct for the same concern.
    private static string Pct(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}
