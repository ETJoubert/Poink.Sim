using System;
using System.Collections.Generic;
using Poink.Sim.Components.Charts;
using Poink.Sim.Components.Shared.Indicators;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Simulation
{
	/// <summary>
	/// Everything one watched symbol's chart set renders, already computed for the page's Lookback Window and Indicators:
	/// its chart bars and indicator lines, XGrid, volume/time profile samples and TxFold anchor.
	/// </summary>
	public sealed record SimulationSymbolPanelData(
		string Table,
		string ColorVar,
		double PipSize,
		DateTime? CurrentTimestampUtc,
		double? CurrentBid,
		double? CurrentAsk,
		List<CTraderBarRow> Bars,
		List<object> TickRows,
		IReadOnlyList<ChartHorizontalLine> HorizontalLines,
		IReadOnlyList<ChartVerticalLine> VerticalLines,
		IReadOnlyList<ChartDiagonalLine> DiagonalLines,
		IReadOnlyList<ChartLinePoint> VwapLine,
		DateTime? WindowStartUtc,
		DateTime? WindowEndUtc,
		int WindowBarCount,
		List<WeightedPriceSample> Samples,
		List<WeightedTimeSample> TimeSamples,
		XGridResult? XGrid,
		DateTime? TxFoldAnchorEnd,
		double? TxFoldLatestPrice
	)
	{
		public double? CurrentPrice => CurrentBid is { } bid && CurrentAsk is { } ask ? (bid + ask) / 2.0 : null;
	}

	/// <summary>The page's panel toggles and indicator state, applied identically to every watched symbol's chart set.</summary>
	public sealed record SimulationChartSetOptions(
		bool Aggregate,
		bool ShowMainChart,
		bool ShowXGrid,
		bool ShowVolumeProfile,
		bool ShowTimeProfile,
		bool ShowTxFold,
		IndicatorSettings Indicators,
		DateTime RangeStart
	);
}
