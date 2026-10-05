using System.Collections.Generic;
using System.Linq;

namespace Poink.Sim.Components.Profiler
{
	/// <summary>
	/// Assigns each selected Profiler symbol a theme colour by its selection order, so the symbols panel works as the
	/// legend for every chart set and the compare chart.
	/// </summary>
	internal static class ProfilerSymbolColorHelper
	{
		/// <summary>For a symbol still charted but no longer selected, e.g. a removed primary kept until the next LOAD.</summary>
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
