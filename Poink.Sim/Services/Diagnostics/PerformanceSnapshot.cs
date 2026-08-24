namespace Poink.Sim.Diagnostics;

public sealed record PerformanceSnapshot(
	double CpuPercent,
	double RamUsedGb,
	double RamTotalGb,
	double RamPercent,
	double? GpuPercent,
	double? GpuMemUsedGb
);
