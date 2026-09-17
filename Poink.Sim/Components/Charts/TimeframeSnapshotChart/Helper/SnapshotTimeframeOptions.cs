using System.Collections.Generic;

namespace Poink.Sim.Components.Charts;

internal readonly record struct SnapshotTimeframe(int Seconds, string Label);

internal static class SnapshotTimeframeOptions
{
	public static readonly IReadOnlyList<SnapshotTimeframe> All = new[]
	{
		new SnapshotTimeframe(60, "1m"),
		new SnapshotTimeframe(240, "4m"),
		new SnapshotTimeframe(960, "16m"),
		new SnapshotTimeframe(3600, "1h"),
	};
}
