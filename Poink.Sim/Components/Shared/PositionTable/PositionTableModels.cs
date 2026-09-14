using System;
using System.Collections.Generic;

namespace Poink.Sim.Components.Shared.PositionTable
{
	/// <summary>
	/// Display-ready projection of one open position for PositionTable -- formatting and derived figures
	/// are computed once per row, not once per column per render. <typeparamref name="TPosition"/> is the
	/// host's own backend identity (e.g. a cTrader position, or a simulated Position), round-tripped
	/// through OnSelect/OnClose so PositionTable itself never depends on a specific backend.
	/// </summary>
	public sealed record PositionRowViewModel<TPosition>(
		TPosition Position,
		string SymbolName,
		bool IsLong,
		string Side,
		string Volume,
		string Entry,
		double? Pips,
		string PipsDisplay,
		double? Pnl,
		string PnlDisplay,
		string Opened,
		string StopLoss,
		string TakeProfit,
		double? ProjectedProfit,
		string ProjectedProfitDisplay,
		double? ProjectedLoss,
		string ProjectedLossDisplay,
		double? PercentToTakeProfit,
		string PercentToTakeProfitDisplay,
		double? PercentToStopLoss,
		string PercentToStopLossDisplay,
		double? RewardToRisk,
		string RewardToRiskDisplay,
		double? PercentNet,
		string PercentNetDisplay,
		string DurationDisplay
	);

	/// <summary>One PositionTable column: pairs its header with the cell(s) it renders, so a reorder or addition can't leave the header row out of sync with the body -- the bug this replaced.</summary>
	public sealed record PositionColumn<TPosition>(
		string Header,
		Func<PositionRowViewModel<TPosition>, string> Value,
		Func<PositionRowViewModel<TPosition>, string>? CssClass = null,
		Func<PositionRowViewModel<TPosition>, string>? Title = null
	);

	/// <summary>A named band of adjacent PositionTable columns, rendered as a spanning label above them.</summary>
	public sealed record PositionColumnGroup<TPosition>(string Name, IReadOnlyList<PositionColumn<TPosition>> Columns);
}
