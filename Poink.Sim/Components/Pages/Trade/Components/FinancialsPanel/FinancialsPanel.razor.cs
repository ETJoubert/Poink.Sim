using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Components.Pages.Trade.Helpers;
using Poink.Sim.Models;
using Poink.Sim.Services.CTrader;

namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>Open positions plus account financials for the live Trade page -- owns the derived figures (equity, margin, P/L); CTraderSessionService only supplies Positions and Balance.</summary>
	public partial class FinancialsPanel : ComponentBase
	{
		private static readonly TimeSpan HistoryLookback = TimeSpan.FromDays(30);

		private enum FinancialsTab
		{
			Positions,
			History,
			Statistics,
		}

		private enum RealizedWindow
		{
			Hour,
			Day,
			Week,
			Month,
		}

		[Inject]
		private ITradeHistoryService History { get; set; } = null!;

		[Parameter, EditorRequired]
		public IReadOnlyList<CTraderPositionInfo> Positions { get; set; } = System.Array.Empty<CTraderPositionInfo>();

		[Parameter]
		public double? Balance { get; set; }

		[Parameter]
		public IReadOnlyList<CTraderSymbolQuote> Quotes { get; set; } = System.Array.Empty<CTraderSymbolQuote>();

		[Parameter]
		public IReadOnlyDictionary<long, SymbolMetadataModel> SymbolMetadata { get; set; } =
			new Dictionary<long, SymbolMetadataModel>();

		[Parameter]
		public EventCallback<CTraderPositionInfo> OnSelectPosition { get; set; }

		[Parameter]
		public EventCallback<CTraderPositionInfo> OnClosePosition { get; set; }

		private RealizedWindow _realizedWindow = RealizedWindow.Day;
		private double? _realizedPnl;
		private bool _realizedLoading;
		private bool _realizedPickerOpen;
		private HashSet<long> _knownPositionIds = new();

		private static TimeSpan ToTimeSpan(RealizedWindow window) =>
			window switch
			{
				RealizedWindow.Hour => TimeSpan.FromHours(1),
				RealizedWindow.Week => TimeSpan.FromDays(7),
				RealizedWindow.Month => TimeSpan.FromDays(30),
				_ => TimeSpan.FromDays(1),
			};

		private static string RealizedWindowLabel(RealizedWindow window) =>
			window switch
			{
				RealizedWindow.Hour => "1H",
				RealizedWindow.Week => "1W",
				RealizedWindow.Month => "1M",
				_ => "1D",
			};

		private void ToggleRealizedPicker() => _realizedPickerOpen = !_realizedPickerOpen;

		private async Task SelectRealizedWindow(RealizedWindow window)
		{
			_realizedWindow = window;
			_realizedPickerOpen = false;
			await LoadRealizedPnlAsync();
		}

		private async Task LoadRealizedPnlAsync()
		{
			_realizedLoading = true;
			StateHasChanged();
			try
			{
				var toUtc = DateTime.UtcNow;
				var deals = await History.GetClosedDealsAsync(toUtc - ToTimeSpan(_realizedWindow), toUtc);
				_realizedPnl = deals.Sum(d => (d.RealizedPnl ?? 0) + (d.Commission ?? 0) + (d.Swap ?? 0));
			}
			finally
			{
				_realizedLoading = false;
			}
		}

		// Every open position's symbol is auto-subscribed independently of the watchlist, so this excludes
		// a position only in the brief window before its subscription/metadata/first quote lands.
		private double? UnrealizedPnl
		{
			get
			{
				var known = Positions
					.Select(p => PositionPnlHelper.UnrealizedPnl(p, QuoteFor(p.SymbolId), SymbolMetadata.GetValueOrDefault(p.SymbolId)))
					.Where(pnl => pnl is not null)
					.Select(pnl => pnl!.Value)
					.ToList();
				return known.Count == 0 ? null : known.Sum();
			}
		}

		private CTraderSymbolQuote? QuoteFor(long symbolId) => Quotes.FirstOrDefault(q => q.SymbolId == symbolId);

		private double? UnrealizedPnlPercentOfBalance =>
			Balance is { } balance and not 0 && UnrealizedPnl is { } pnl ? pnl / balance * 100 : null;

		private double? RealizedPnlPercentOfBalance =>
			Balance is { } balance and not 0 && _realizedPnl is { } pnl ? pnl / balance * 100 : null;

		private double? Equity => Balance is { } balance && UnrealizedPnl is { } pnl ? balance + pnl : null;

		private double? MarginUsed => Positions.Count == 0 ? null : Positions.Sum(p => p.UsedMargin);

		private double? MarginLevel =>
			Equity is { } equity && MarginUsed is { } used && used != 0 ? equity / used * 100 : null;

		private double? FreeMargin => Equity is { } equity && MarginUsed is { } used ? equity - used : null;

		private FinancialsTab _activeTab = FinancialsTab.Positions;
		private IReadOnlyList<CTraderDealInfo> _closedDeals = Array.Empty<CTraderDealInfo>();
		private bool _historyLoading;
		private string? _historyError;
		private bool _tableCollapsed;

		protected override void OnInitialized()
		{
			_knownPositionIds = Positions.Select(p => p.PositionId).ToHashSet();
			_ = LoadRealizedPnlAsync();
		}

		// A position vanishing from the live list (rather than appearing) means it closed, so the
		// realized figure -- otherwise only refreshed on init or window change -- is stale until reloaded.
		protected override void OnParametersSet()
		{
			var currentPositionIds = Positions.Select(p => p.PositionId).ToHashSet();
			if (_knownPositionIds.Any(id => !currentPositionIds.Contains(id)))
				_ = LoadRealizedPnlAsync();
			_knownPositionIds = currentPositionIds;
		}

		private void SelectTab(FinancialsTab tab)
		{
			_activeTab = tab;
			if (tab is FinancialsTab.History or FinancialsTab.Statistics)
				_ = LoadHistoryAsync();
		}

		// Statistics is derived from the same closed-deal set History shows, rather than a separate fetch.
		private IReadOnlyList<StatGroup> StatGroups => LiveTradeStatisticsHelper.From(_closedDeals);

		// Positions/History can collapse to save vertical space; the financials summary below
		// (balance/equity/margin/P&L) always stays visible regardless.
		private void SelectTabAndExpand(FinancialsTab tab)
		{
			SelectTab(tab);
			_tableCollapsed = false;
		}

		private void ToggleTableCollapsed() => _tableCollapsed = !_tableCollapsed;

		private async Task LoadHistoryAsync()
		{
			_historyLoading = true;
			_historyError = null;
			StateHasChanged();
			try
			{
				var toUtc = DateTime.UtcNow;
				_closedDeals = await History.GetClosedDealsAsync(toUtc - HistoryLookback, toUtc);
			}
			catch (Exception ex)
			{
				_closedDeals = Array.Empty<CTraderDealInfo>();
				_historyError = ex.Message;
			}
			finally
			{
				_historyLoading = false;
			}
		}

		private static string Money(double? value) => value?.ToString("N2") ?? "—";

		private static string SignedMoney(double? value) => value is { } v ? $"{(v >= 0 ? "+" : "")}{v:N2}" : "—";

		private static string SignedPercent(double? value) => value is { } v ? $"{(v >= 0 ? "+" : "")}{v:N2}%" : "—";

		private static string PnlCssClass(double? value) =>
			value switch
			{
				> 0 => "fp-value-positive",
				< 0 => "fp-value-negative",
				_ => "",
			};
	}
}
