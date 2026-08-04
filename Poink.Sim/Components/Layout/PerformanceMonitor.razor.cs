using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Diagnostics;

namespace Poink.Sim.Components.Layout
{

    [SupportedOSPlatform("windows")]
    public partial class PerformanceMonitor : ComponentBase, System.IDisposable
    {
        [Inject] private SystemPerformanceMonitor Monitor { get; set; } = default!;

        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        private PerformanceSnapshot Snap => Monitor.Current;

        protected override void OnInitialized() => Monitor.OnChanged += HandleChanged;

        private void HandleChanged() => _ = InvokeAsync(StateHasChanged);


        private static string Pct(double v) => v.ToString("0.#", Culture);

        private static string LevelClass(double percent) => percent switch
        {
            >= 85 => "perf-level-high",
            >= 60 => "perf-level-mid",
            _ => "perf-level-low",
        };

        public void Dispose() => Monitor.OnChanged -= HandleChanged;
    }
}
