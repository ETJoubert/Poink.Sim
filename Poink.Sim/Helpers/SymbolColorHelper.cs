using System.Collections.Generic;
using System.Linq;

namespace Poink.Sim.Helpers
{
	/// <summary>
	/// Assigns each selected symbol a theme colour by its selection order, so a page's symbols panel works as the legend
	/// for every chart set and the compare chart -- shared by the Profiler and Simulation pages so both colour alike.
	/// </summary>
	internal static class SymbolColorHelper
	{
		/// <summary>For a symbol still charted but no longer selected, e.g. one removed from the selection until the next load.</summary>
		public const string FallbackColorVar = "--color-neutral";

		private static readonly string[] Palette =
		[
			"--color-primary",
			"--color-badge-1",
			"--color-badge-3",
			"--color-badge-4",
			"--color-badge-5",
			"--color-info",
			"--color-accent",
			"--color-badge-2",
		];

		public static IReadOnlyDictionary<string, string> Assign(IEnumerable<string> tables) =>
			tables.Distinct().Select((table, index) => (table, index)).ToDictionary(t => t.table, t => Palette[t.index % Palette.Length]);

		public static string ColorVarFor(IReadOnlyDictionary<string, string> colors, string table) =>
			colors.TryGetValue(table, out var colorVar) ? colorVar : FallbackColorVar;
	}
}
