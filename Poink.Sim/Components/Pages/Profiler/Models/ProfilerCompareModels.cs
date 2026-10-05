namespace Poink.Sim.Components.Profiler
{
	/// <summary>What the Profiler's compare chart measures each symbol's % change from.</summary>
	public enum ProfilerCompareMode
	{
		/// <summary>Each symbol's first bar in view, re-basing as the chart pans.</summary>
		PercentChange,

		/// <summary>Each symbol's bar at the Lookback Window's start, or its first loaded bar while no window is set.</summary>
		WindowAnchored,
	}
}
