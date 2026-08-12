using System;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Models;
using Poink.Sim.Simulation;

namespace Poink.Sim.Components.Shared.SimTimeline;


public partial class SimTimeline : ComponentBase, IDisposable
{
    [Inject] private ISimulationEngine Service { get; set; } = default!;

    protected override void OnInitialized() => Service.OnProgress += HandleProgress;

    private void HandleProgress() => _ = InvokeAsync(StateHasChanged);

    public void Dispose() => Service.OnProgress -= HandleProgress;

    private static double Percent(SimulationProgress p)
    {
        if (p.CurrentTimestampUtc is not { } cur)
            return 0;
        var totalTicks = (p.RangeToUtc - p.RangeFromUtc).Ticks;
        if (totalTicks <= 0)
            return 0;
        return Math.Clamp((cur - p.RangeFromUtc).Ticks * 100.0 / totalTicks, 0, 100);
    }


    private static string Pct(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}
