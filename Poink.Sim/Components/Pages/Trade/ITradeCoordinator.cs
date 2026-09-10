using System;
using System.Threading.Tasks;

namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>
	/// The Trade page's cross-panel UI state -- which symbol's chart is open, both side panels'
	/// collapsed state, and settings persistence -- none of which any driver owns itself.
	/// </summary>
	internal interface ITradeCoordinator
	{
		/// <summary>Fires when the underlying session reconnects or its positions change, so a subscriber can re-render.</summary>
		event Action? Changed;

		bool IsConnected { get; }

		int BarSizeSeconds { get; set; }

		bool SidepanelCollapsed { get; set; }

		bool RightPanelCollapsed { get; }

		long? ChartSymbolId { get; }

		string? ChartSymbolName { get; }

		ChartPanelSettings? PendingChartSettings { get; }

		void ToggleRightPanel();

		void OpenChart(long symbolId, string symbolName);

		void HandleSymbolSelected((long SymbolId, string SymbolName) selection);

		void HandleSymbolUnwatched(long symbolId);

		Task LoadPersistedSettingsAsync();

		Task PersistSettingsIfChangedAsync(ChartPanelSettings? chartSettings);
	}
}
