using System.Collections.Generic;

namespace Poink.Sim.Components.Charts;

internal readonly record struct SnapshotTimeframe(int Seconds, string Label);

internal static class SnapshotTimeframeOptions
{
	public static readonly IReadOnlyList<SnapshotTimeframe> All = new[]
	{
		new SnapshotTimeframe(1, "1s"),
		new SnapshotTimeframe(2, "2s"),
		new SnapshotTimeframe(4, "4s"),
		new SnapshotTimeframe(8, "8s"),
		new SnapshotTimeframe(15, "15s"),
		new SnapshotTimeframe(30, "30s"),
		new SnapshotTimeframe(60, "1m"),
		new SnapshotTimeframe(120, "2m"),
		new SnapshotTimeframe(240, "4m"),
		new SnapshotTimeframe(480, "8m"),
		new SnapshotTimeframe(900, "15m"),
		new SnapshotTimeframe(1800, "30m"),
		new SnapshotTimeframe(3600, "1h"),
		new SnapshotTimeframe(7200, "2h"),
		new SnapshotTimeframe(14400, "4h"),
		new SnapshotTimeframe(28800, "8h"),
		new SnapshotTimeframe(57600, "16h"),
		new SnapshotTimeframe(86400, "1d"),
		new SnapshotTimeframe(172800, "2d"),
		new SnapshotTimeframe(345600, "4d"),
		new SnapshotTimeframe(604800, "7d"),
	};

	// Seconds values that start a new unit (minutes/hours/days) -- the header inserts a divider before these.
	public static readonly IReadOnlySet<int> UnitStarts = new HashSet<int> { 60, 3600, 86400 };
}
