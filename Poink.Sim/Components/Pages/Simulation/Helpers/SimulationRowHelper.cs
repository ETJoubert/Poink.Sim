using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Models;
using Poink.Sim.Repository;

namespace Poink.Sim.Components.Simulation
{
	/// <summary>Shapes a simulation's bars and ticks into the rows its charts and tick tables render.</summary>
	internal static class SimulationRowHelper
	{
		public sealed record TickRow(string Time, string Bid, string Ask, string Mid);

		public static List<CTraderBarRow> ToBarRows(IReadOnlyList<HlocAvdtBar> bars) =>
			bars.Select(b => new CTraderBarRow(
					b.OpenTime,
					b.Open,
					b.High,
					b.Low,
					b.Close,
					b.Volume,
					b.UpVolume,
					b.DownVolume,
					b.Distance
				))
				.ToList();

		/// <summary>Newest tick first, as the tick tables list them.</summary>
		public static List<object> ToTickRows(IReadOnlyList<Tick> ticks) =>
			ticks
				.Select(object (t) =>
					new TickRow(
						t.Timestamp.ToString("HH:mm:ss.fff"),
						t.Bid.ToString("F5"),
						t.Ask.ToString("F5"),
						((t.Bid + t.Ask) / 2).ToString("F5")
					)
				)
				.Reverse()
				.ToList();
	}
}
