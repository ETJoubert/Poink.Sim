using System.Collections.Generic;
using Poink.Sim.Components.Charts;

namespace Poink.Sim.Components.Shared.Indicators
{
	/// <summary>Translates <see cref="IndicatorSettings"/> into the overlay settings each chart component already understands.</summary>
	public static class IndicatorsHelper
	{
		/// <summary>XGrid's own display settings with every level and time-line toggle taken from the indicators instead.</summary>
		public static XGridDisplaySettings ApplyTo(XGridDisplaySettings display, IndicatorSettings indicators) =>
			display with
			{
				OverlaysEnabled = indicators.Enabled,
				ShowPoc = indicators.ShowPoc,
				ShowVah = indicators.ShowVah,
				ShowVal = indicators.ShowVal,
				ShowVwap = indicators.ShowVwap,
				ShowAverage = indicators.ShowAverage,
				ShowMedian = indicators.ShowMedian,
				ShowRegressionLine = indicators.ShowRegression,
				ShowDayLines = indicators.ShowDayLines,
				ShowIntervalLines = indicators.ShowIntervalLines,
				IntervalHours = indicators.IntervalHours,
			};

		/// <summary>The TxFold level lines matching the visible indicators; TxFold has no day or interval lines.</summary>
		public static HashSet<TxFoldLevelKind> ToTxFoldLevels(IndicatorSettings indicators)
		{
			var levels = new HashSet<TxFoldLevelKind>();
			if (indicators.PocVisible) levels.Add(TxFoldLevelKind.Poc);
			if (indicators.VahVisible) levels.Add(TxFoldLevelKind.Vah);
			if (indicators.ValVisible) levels.Add(TxFoldLevelKind.Val);
			if (indicators.VwapVisible) levels.Add(TxFoldLevelKind.Vwap);
			if (indicators.AverageVisible) levels.Add(TxFoldLevelKind.Average);
			if (indicators.MedianVisible) levels.Add(TxFoldLevelKind.Median);
			if (indicators.RegressionVisible) levels.Add(TxFoldLevelKind.Regression);
			return levels;
		}
	}
}
