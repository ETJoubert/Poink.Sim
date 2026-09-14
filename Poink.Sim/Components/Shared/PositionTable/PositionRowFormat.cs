using System;

namespace Poink.Sim.Components.Shared.PositionTable
{
	/// <summary>Pure formatting for PositionTable row figures -- shared so every host's own row-mapping (Trade's cTrader positions, Simulation's simulated one) renders identically.</summary>
	public static class PositionRowFormat
	{
		public static string PipsLabel(double? pips) => pips is { } value ? $"{(value >= 0 ? "+" : "")}{value:N1}" : "—";

		public static string PnlLabel(double? pnl) =>
			global::Poink.Sim.Components.Shared.FinancialsPanel.FinancialsPanel.SignedMoney(pnl);

		public static string MoneyLabel(double? value) =>
			global::Poink.Sim.Components.Shared.FinancialsPanel.FinancialsPanel.SignedMoney(value);

		public static string PercentLabel(double? percent) =>
			percent is { } value ? $"{(value >= 0 ? "+" : "")}{value:N1}%" : "—";

		public static string RewardToRiskLabel(double? ratio) => ratio is { } value ? $"{value:N2} : 1" : "—";

		public static string DurationLabel(TimeSpan duration) =>
			duration.TotalDays >= 1
				? $"{(int)duration.TotalDays}d {duration:hh\\:mm}"
				: $"{duration:hh\\:mm\\:ss}";

		public static string SignCssClass(double? value) =>
			value switch
			{
				> 0 => "pt-value-positive",
				< 0 => "pt-value-negative",
				_ => "pt-value-neutral",
			};
	}
}
