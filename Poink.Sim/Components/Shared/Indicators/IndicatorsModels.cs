namespace Poink.Sim.Components.Shared.Indicators
{
	/// <summary>
	/// Which Lookback-Window-derived levels and time lines are overlaid on every chart the Indicators component drives.
	/// Immutable so a change always flows back through the component's SettingsChanged callback.
	/// </summary>
	public sealed record IndicatorSettings
	{
		/// <summary>Master switch; turning it off hides every overlay without clearing the individual toggles.</summary>
		public bool Enabled { get; init; } = true;

		public bool ShowPoc { get; init; }
		public bool ShowVah { get; init; }
		public bool ShowVal { get; init; }
		public bool ShowVwap { get; init; }
		public bool ShowAverage { get; init; }
		public bool ShowMedian { get; init; }
		public bool ShowRegression { get; init; }
		public bool ShowDayLines { get; init; }
		public bool ShowIntervalLines { get; init; }
		public int IntervalHours { get; init; } = 4;

		public bool PocVisible => Enabled && ShowPoc;
		public bool VahVisible => Enabled && ShowVah;
		public bool ValVisible => Enabled && ShowVal;
		public bool VwapVisible => Enabled && ShowVwap;
		public bool AverageVisible => Enabled && ShowAverage;
		public bool MedianVisible => Enabled && ShowMedian;
		public bool RegressionVisible => Enabled && ShowRegression;
		public bool DayLinesVisible => Enabled && ShowDayLines;
		public bool IntervalLinesVisible => Enabled && ShowIntervalLines && IntervalHours > 0;
	}
}
