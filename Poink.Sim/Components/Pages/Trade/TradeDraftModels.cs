namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>A not-yet-submitted order being edited in TradeControlsPanel, previewed on the chart/XGrid.</summary>
	internal readonly record struct TradeDraft(
		long SymbolId,
		bool IsLong,
		double EntryPrice,
		double Volume,
		double? StopLossPrice,
		double? TakeProfitPrice
	);
}
