namespace Poink.Sim.Diagnostics;

// GpuPercent/GpuMemUsedGb are null when the host has no WDDM GPU perf-counter
// category available (e.g. no compatible driver) -- the UI shows "N/A" rather
// than a misleading 0%.
public sealed record PerformanceSnapshot(
    double CpuPercent,
    double RamUsedGb,
    double RamTotalGb,
    double RamPercent,
    double? GpuPercent,
    double? GpuMemUsedGb);
