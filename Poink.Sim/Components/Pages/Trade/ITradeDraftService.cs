using System;

namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>
	/// Scoped, so it's shared between TradePage (writer, on TradeConsole's behalf) and TradeChartPanel (reader) for
	/// one circuit without either being a parent of the other -- see ITradeCoordinator for the same
	/// pattern applied to the Trade page's other cross-panel state.
	/// </summary>
	internal interface ITradeDraftService
	{
		event Action? Changed;

		TradeDraft? Draft { get; }

		void Update(TradeDraft draft);

		void Clear();
	}
}
