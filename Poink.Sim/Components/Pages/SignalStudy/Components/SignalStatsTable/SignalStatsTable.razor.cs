using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Models;

namespace Poink.Sim.Components.SignalStudy
{
	/// <summary>
	/// Signal states (or pairs) with what followed their entries, at one forward horizon. Rows whose better side has a
	/// t-statistic of 2 or more and whose two halves agree are marked as worth a closer look.
	/// </summary>
	public partial class SignalStatsTable : ComponentBase
	{
		private const double NotableTStat = 2;

		[Parameter, EditorRequired]
		public IReadOnlyList<SignalStateStats> Rows { get; set; } = Array.Empty<SignalStateStats>();

		/// <summary>The every-point row the others are compared with, shown first when given.</summary>
		[Parameter]
		public SignalStateStats? Baseline { get; set; }

		[Parameter]
		public int HorizonIndex { get; set; }

		[Parameter]
		public string HorizonLabel { get; set; } = "";

		private double? At(IReadOnlyList<double?> values) => HorizonIndex < values.Count ? values[HorizonIndex] : null;

		private static string RowClass(SignalStateStats row) =>
			row.BestTStat >= NotableTStat && row.HalvesAgree ? "stats-notable" : "";

		private static string Signed(double? value, string format) =>
			value is { } v ? (v > 0 ? "+" : "") + v.ToString(format) : "—";

		private static string Percent(double? value) => value?.ToString("P0") ?? "—";

		private static string CiLabel(double? halfWidth) => halfWidth is { } h ? $" ±{h:0.000}" : "";

		private static string SignClass(double? value) =>
			value switch
			{
				> 0 => "stats-pos",
				< 0 => "stats-neg",
				_ => "",
			};
	}
}
