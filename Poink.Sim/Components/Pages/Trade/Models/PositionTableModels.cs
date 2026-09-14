using System;
using System.Collections.Generic;
using Poink.Sim.Services.CTrader;

namespace Poink.Sim.Components.Pages.Trade.Models
{
	/// <summary>Display-ready projection of one open position for PositionTable -- formatting and derived figures are computed once per row, not once per column per render.</summary>
	public sealed record PositionRowViewModel(
		CTraderPositionInfo Position,
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
	public sealed record PositionColumn(
		string Header,
		Func<PositionRowViewModel, string> Value,
		Func<PositionRowViewModel, string>? CssClass = null,
		Func<PositionRowViewModel, string>? Title = null
	);

	/// <summary>A named band of adjacent PositionTable columns, rendered as a spanning label above them.</summary>
	public sealed record PositionColumnGroup(string Name, IReadOnlyList<PositionColumn> Columns);
}
