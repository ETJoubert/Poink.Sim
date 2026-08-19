using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;

namespace Poink.Sim.Diagnostics
{

    [SupportedOSPlatform("windows")]
    public sealed class SystemPerformanceMonitor : IDisposable
    {

        private static readonly TimeSpan GpuCounterRefreshInterval = TimeSpan.FromSeconds(10);

        private readonly Timer _timer;
        private readonly PerformanceCounter? _cpuCounter;
        private PerformanceCounter[] _gpuUtilCounters = Array.Empty<PerformanceCounter>();
        private PerformanceCounter[] _gpuMemCounters = Array.Empty<PerformanceCounter>();
        private DateTime _gpuCountersRefreshedAt = DateTime.MinValue;

        public event Action? OnChanged;

        public PerformanceSnapshot Current { get; private set; } = new(0, 0, 0, 0, null, null);

        public SystemPerformanceMonitor()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor Information", "% Processor Time", "_Total", readOnly: true);
            }
            catch
            {

                _cpuCounter = null;
            }

            _timer = new Timer(Sample, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        }

        private void Sample(object? state)
        {
            var cpuPercent = Math.Clamp(SafeNextValue(_cpuCounter), 0, 100);

            var (totalBytes, availBytes) = NativeMemory.GetPhysicalMemory();
            var totalGb = totalBytes / 1024.0 / 1024.0 / 1024.0;
            var usedGb = (totalBytes - availBytes) / 1024.0 / 1024.0 / 1024.0;
            var ramPercent = totalBytes == 0 ? 0 : Math.Clamp((totalBytes - availBytes) * 100.0 / totalBytes, 0, 100);

            var (gpuPercent, gpuMemGb) = SampleGpu();

            Current = new PerformanceSnapshot(cpuPercent, usedGb, totalGb, ramPercent, gpuPercent, gpuMemGb);
            OnChanged?.Invoke();
        }

        private (double? percent, double? memGb) SampleGpu()
        {
            try
            {
                RefreshGpuCountersIfNeeded();
                if (_gpuUtilCounters.Length == 0)
                    return (null, null);

                var percent = Math.Clamp(_gpuUtilCounters.Sum(SafeNextValue), 0, 100);
                var memGb = _gpuMemCounters.Sum(SafeNextValue) / 1024.0 / 1024.0 / 1024.0;
                return (percent, memGb);
            }
            catch
            {
                // No GPU perf-counter category on this host (e.g. no WDDM driver) -- GPU stats just won't show.
                return (null, null);
            }
        }

        private void RefreshGpuCountersIfNeeded()
        {
            if (DateTime.UtcNow - _gpuCountersRefreshedAt < GpuCounterRefreshInterval)
                return;
            _gpuCountersRefreshedAt = DateTime.UtcNow;

            // "engtype_3D" is the engine Task Manager's GPU graph tracks by default;
            // summing its per-process utilization approximates that same headline number.
            var newUtil = new PerformanceCounterCategory("GPU Engine").GetInstanceNames()
                .Where(n => n.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase))
                .Select(n => TryCreateCounter("GPU Engine", "Utilization Percentage", n))
                .Where(c => c is not null)
                .Select(c => c!)
                .ToArray();

            var newMem = new PerformanceCounterCategory("GPU Adapter Memory").GetInstanceNames()
                .Select(n => TryCreateCounter("GPU Adapter Memory", "Dedicated Usage", n))
                .Where(c => c is not null)
                .Select(c => c!)
                .ToArray();

            DisposeAll(_gpuUtilCounters);
            DisposeAll(_gpuMemCounters);
            _gpuUtilCounters = newUtil;
            _gpuMemCounters = newMem;
        }

        private static PerformanceCounter? TryCreateCounter(string category, string counter, string instance)
        {
            try
            { return new PerformanceCounter(category, counter, instance, readOnly: true); }
            catch { return null; }
        }

        private static double SafeNextValue(PerformanceCounter? counter)
        {
            try
            { return counter?.NextValue() ?? 0; }
            catch { return 0; }
        }

        private static void DisposeAll(PerformanceCounter[] counters)
        {
            foreach (var c in counters)
                c.Dispose();
        }

        public void Dispose()
        {
            _timer.Dispose();
            _cpuCounter?.Dispose();
            DisposeAll(_gpuUtilCounters);
            DisposeAll(_gpuMemCounters);
        }
    }
}
