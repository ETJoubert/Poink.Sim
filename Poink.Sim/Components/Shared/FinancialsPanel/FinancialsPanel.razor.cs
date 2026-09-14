using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Shared.FinancialsPanel
{
	/// <summary>
	/// Tabbed Positions/History/Statistics shell plus a Balance/Equity/Margin/Realized/Unrealized
	/// footer, shared by the live Trade page and the Simulation page. Carries no page-specific backend:
	/// each tab's actual content is projected in by the host via RenderFragment parameters, and the
	/// footer numbers are plain parameters the host computes however it already does.
	/// </summary>
	public partial class FinancialsPanel : ComponentBase
	{
		public enum FinancialsTab
		{
			Positions,
			History,
			Statistics,
			Equity,
		}

		[Parameter]
		public RenderFragment? PositionsContent { get; set; }

		[Parameter]
		public RenderFragment? HistoryContent { get; set; }

		[Parameter]
		public RenderFragment? StatisticsContent { get; set; }

		/// <summary>Equity-over-time chart -- the tab only appears once a host supplies this.</summary>
		[Parameter]
		public RenderFragment? EquityContent { get; set; }

		/// <summary>Realized P/L display -- a plain value, or a host-driven window picker (e.g. Trade's 1H/1D/1W/1M).</summary>
		[Parameter]
		public RenderFragment? RealizedContent { get; set; }

		[Parameter]
		public int PositionsCount { get; set; }

		[Parameter]
		public int HistoryCount { get; set; }

		[Parameter]
		public string HistoryTabTitle { get; set; } = "Closed trades";

		[Parameter]
		public string FinancialsSummaryTitle { get; set; } = "Account balance, margin, and realized/unrealized P&L";

		[Parameter]
		public EventCallback<FinancialsTab> OnTabSelected { get; set; }

		[Parameter]
		public double? Balance { get; set; }

		[Parameter]
		public double? Equity { get; set; }

		[Parameter]
		public double? MarginUsed { get; set; }

		[Parameter]
		public double? MarginLevel { get; set; }

		[Parameter]
		public double? FreeMargin { get; set; }

		[Parameter]
		public double? UnrealizedPnl { get; set; }

		private double? UnrealizedPnlPercentOfBalance =>
			Balance is { } balance and not 0 && UnrealizedPnl is { } pnl ? pnl / balance * 100 : null;

		private FinancialsTab _activeTab = FinancialsTab.Positions;
		private bool _tableCollapsed;

		private void SelectTab(FinancialsTab tab)
		{
			_activeTab = tab;
			_ = OnTabSelected.InvokeAsync(tab);
		}

		// Positions/History/Statistics can collapse to save vertical space; the financials summary below
		// (balance/equity/margin/P&L) always stays visible regardless.
		private void SelectTabAndExpand(FinancialsTab tab)
		{
			SelectTab(tab);
			_tableCollapsed = false;
		}

		private void ToggleTableCollapsed() => _tableCollapsed = !_tableCollapsed;

		// Public so host pages can format the numbers they put inside PositionsContent/HistoryContent/
		// RealizedContent consistently with the footer, without duplicating these one-liners per page.
		public static string Money(double? value) => value?.ToString("N2") ?? "—";

		public static string SignedMoney(double? value) => value is { } v ? $"{(v >= 0 ? "+" : "")}{v:N2}" : "—";

		public static string SignedPercent(double? value) => value is { } v ? $"{(v >= 0 ? "+" : "")}{v:N2}%" : "—";

		public static string PnlCssClass(double? value) =>
			value switch
			{
				> 0 => "fp-value-positive",
				< 0 => "fp-value-negative",
				_ => "",
			};
	}
}
