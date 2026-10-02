using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Pages.Analize
{
	/// <summary>
	/// A barrier map as a stop-loss × take-profit heatmap for one side and time limit, coloured by the chosen metric:
	/// diverging green/red for average pips and edge, a single colour scaled to the largest value for the rest.
	/// </summary>
	public partial class BarrierMapView : ComponentBase
	{
		private const int MaxTintPercent = 65;

		private static readonly BarrierSide[] Sides = { BarrierSide.Long, BarrierSide.Short, BarrierSide.Both };

		private static readonly BarrierMetric[] Metrics =
		{
			BarrierMetric.MeanPips,
			BarrierMetric.WinRate,
			BarrierMetric.Edge,
			BarrierMetric.MeanMinutes,
			BarrierMetric.Trades,
		};

		[Parameter, EditorRequired]
		public BarrierMapResult Result { get; set; } = default!;

		[Parameter, EditorRequired]
		public BarrierMapSelection Selection { get; set; } = default!;

		[Parameter]
		public EventCallback<BarrierMapSelection> SelectionChanged { get; set; }

		private BarrierMapGrid? Grid => Result.Grid(Selection.Side, Selection.TimeLimitMinutes);

		private Task Select(BarrierMapSelection selection) => SelectionChanged.InvokeAsync(selection);

		private double ScaleMax =>
			Grid is not { } grid || grid.Cells.Count == 0
				? 1
				: Math.Max(
					double.Epsilon,
					grid.Cells.Max(cell => Selection.Metric is BarrierMetric.MeanPips or BarrierMetric.Edge ? Math.Abs(Value(cell) ?? 0) : Value(cell) ?? 0)
				);

		private double? Value(BarrierCell cell) =>
			Selection.Metric switch
			{
				BarrierMetric.MeanPips => cell.Trades > 0 ? cell.MeanPips : null,
				BarrierMetric.WinRate => cell.WinRate,
				BarrierMetric.Edge => cell.Edge,
				BarrierMetric.MeanMinutes => cell.Trades > 0 ? cell.MeanMinutes : null,
				_ => cell.Trades,
			};

		private string CellStyle(BarrierCell cell)
		{
			if (Value(cell) is not { } value)
				return string.Empty;
			bool diverging = Selection.Metric is BarrierMetric.MeanPips or BarrierMetric.Edge;
			string colour = !diverging ? "var(--color-primary)" : value >= 0 ? "var(--color-success)" : "var(--color-error)";
			double share = Math.Clamp(Math.Abs(value) / ScaleMax, 0, 1);
			string percent = (share * MaxTintPercent).ToString("0", CultureInfo.InvariantCulture);
			return $"background: color-mix(in srgb, {colour} {percent}%, transparent);";
		}

		private string MetricText(BarrierCell cell) =>
			Selection.Metric switch
			{
				BarrierMetric.MeanPips => cell.Trades > 0 ? Signed(cell.MeanPips, "0.0") : "—",
				BarrierMetric.WinRate => cell.WinRate?.ToString("P0") ?? "—",
				BarrierMetric.Edge => cell.Edge is { } edge ? Signed(edge * 100, "0.0") + "%" : "—",
				BarrierMetric.MeanMinutes => cell.Trades > 0 ? cell.MeanMinutes.ToString("0") : "—",
				_ => cell.Trades.ToString("N0"),
			};

		private static string CellTitle(BarrierCell cell) =>
			$"SL {cell.StopPips:0.#} / TP {cell.TargetPips:0.#} pips: {cell.Trades:N0} trades, "
			+ $"{cell.Wins:N0} won, {cell.Losses:N0} stopped, {cell.Timeouts:N0} timed out -- "
			+ $"average {Signed(cell.MeanPips, "0.00")} pips{(cell.CiPips is { } ci ? $" ±{ci:0.00}" : "")}, "
			+ $"win rate {cell.WinRate?.ToString("P1") ?? "—"} vs {cell.BenchmarkWinRate?.ToString("P1") ?? "—"} on the scrambled price, "
			+ $"{cell.MeanMinutes:0} min on average. Click for the distribution";

		private static string Signed(double value, string format) => (value > 0 ? "+" : "") + value.ToString(format);

		private static string LimitLabel(int minutes) => minutes % 60 == 0 ? $"{minutes / 60}h" : $"{minutes}m";

		private static string SideTitle(BarrierSide side) =>
			side switch
			{
				BarrierSide.Long => "Buy at the ask, sell at the bid",
				BarrierSide.Short => "Sell at the bid, buy back at the ask",
				_ => "Long and short trades together",
			};

		private static string MetricLabel(BarrierMetric metric) =>
			metric switch
			{
				BarrierMetric.MeanPips => "AVG PIPS",
				BarrierMetric.WinRate => "WIN RATE",
				BarrierMetric.Edge => "EDGE",
				BarrierMetric.MeanMinutes => "MINUTES",
				_ => "TRADES",
			};

		private static string MetricTitle(BarrierMetric metric) =>
			metric switch
			{
				BarrierMetric.MeanPips => "Average result per trade in pips, after the spread",
				BarrierMetric.WinRate => "Share of all trades that hit the take profit",
				BarrierMetric.Edge => "Win rate minus the win rate of the same trades on a scrambled copy of the price (same timing, spreads and move sizes, random direction) -- green means the target was reached more often than chance",
				BarrierMetric.MeanMinutes => "Average minutes a trade stayed open",
				_ => "Trades in the cell -- fewer at the edges where data ran out",
			};
	}
}
