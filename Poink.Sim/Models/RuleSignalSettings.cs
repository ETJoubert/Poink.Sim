namespace Poink.Sim.Models
{
	/// <summary>
	/// Thresholds and lookbacks for the event, zone, regime, session and position rule-engine signals, shared by every
	/// rule set and the Rule Engine tab. Defaults are starting points to tune, not proven values.
	/// </summary>
	public sealed record RuleSignalSettings
	{
		/// <summary>Completed bars looked back over for crosses, reclaims and value-area re-entries.</summary>
		public int EventLookbackBars { get; init; } = 5;

		/// <summary>Completed bars that must not have touched VAH/VAL for a touch now to count as the first.</summary>
		public int FirstTouchLookbackBars { get; init; } = 20;

		/// <summary>Bars averaged into the average true range (ATR).</summary>
		public int AtrPeriod { get; init; } = 14;

		/// <summary>How many ATRs from VWAP or POC count as stretched.</summary>
		public double AtrDistanceMultiple { get; init; } = 2;

		/// <summary>Bar closes the efficiency ratio is measured over.</summary>
		public int EfficiencyPeriod { get; init; } = 10;

		/// <summary>Efficiency ratio at or above which the market counts as trending; a random walk sits near 0.3.</summary>
		public double EfficiencyTrendThreshold { get; init; } = 0.5;

		/// <summary>Efficiency ratio at or below which the market counts as choppy.</summary>
		public double EfficiencyChopThreshold { get; init; } = 0.2;

		/// <summary>Regression rise over the window, in ATRs, beyond which the slope counts as up or down rather than flat.</summary>
		public double SlopeAtrThreshold { get; init; } = 0.5;

		/// <summary>Regression R² at or above which the trend line fits cleanly.</summary>
		public double RegressionFitHigh { get; init; } = 0.6;

		/// <summary>Regression R² at or below which the trend line fits poorly.</summary>
		public double RegressionFitLow { get; init; } = 0.3;

		/// <summary>ATR percentile at or above which volatility counts as extreme.</summary>
		public double AtrPercentileHigh { get; init; } = 80;

		/// <summary>ATR percentile at or below which volatility counts as quiet.</summary>
		public double AtrPercentileLow { get; init; } = 20;

		/// <summary>Up-minus-down tick volume over the window, as a fraction of the total, beyond which buying or selling dominates.</summary>
		public double VolumeImbalanceThreshold { get; init; } = 0.05;

		/// <summary>UTC hour trading may start, inclusive.</summary>
		public int SessionStartHourUtc { get; init; } = 7;

		/// <summary>UTC hour trading stops, exclusive; earlier than the start wraps past midnight.</summary>
		public int SessionEndHourUtc { get; init; } = 20;

		/// <summary>How many times the typical spread the current spread may be and still count as normal.</summary>
		public double SpreadMultiple { get; init; } = 1.5;

		/// <summary>Bars held at or beyond which a position counts as overdue (a time stop).</summary>
		public int MaxBarsHeld { get; init; } = 20;

		/// <summary>Open profit, in R (multiples of the initial stop distance), at or above which a position counts as a winner.</summary>
		public double ProfitTargetR { get; init; } = 1.5;

		/// <summary>Open loss, in R, at or beyond which a position counts as a loser; entered as a positive number.</summary>
		public double LossLimitR { get; init; } = 0.5;

		/// <summary>Share of a position's peak open profit, in percent, that counts as given back.</summary>
		public double GivebackPercent { get; init; } = 50;

		/// <summary>Smallest peak open profit, in R, before giveback is judged, so a trade that barely went green is not cut.</summary>
		public double GivebackMinPeakR { get; init; } = 0.5;
	}
}
