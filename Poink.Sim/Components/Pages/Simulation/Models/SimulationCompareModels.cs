using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Simulation
{
	/// <summary>What the Simulation compare chart measures each symbol's % change from.</summary>
	public enum SimulationCompareMode
	{
		/// <summary>Each symbol's first bar in view, re-basing as the chart pans.</summary>
		PercentChange,

		/// <summary>Each symbol's bar at the Lookback Window edge picked by <see cref="SimulationCompareAnchor"/>.</summary>
		WindowAnchored,
	}

	/// <summary>Which Lookback Window edge <see cref="SimulationCompareMode.WindowAnchored"/> measures % change from.</summary>
	public enum SimulationCompareAnchor
	{
		/// <summary>The window's left edge, so lines fan out from 0% across the window.</summary>
		WindowStart,

		/// <summary>The window's right edge, so lines converge on 0% there.</summary>
		WindowEnd,
	}

	/// <summary>One symbol on the Simulation compare chart, its bars oldest first; the traded symbol comes first.</summary>
	public sealed record SimulationCompareSymbol(string Table, string ColorVar, IReadOnlyList<CTraderBarRow> Bars);
}
