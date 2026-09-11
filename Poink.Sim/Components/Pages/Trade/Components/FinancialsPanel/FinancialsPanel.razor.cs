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
		}

		[Inject]
		private ITradeHistoryAccess History { get; set; } = null!;

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

		private double? RealizedPnl => null;

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

		private double? Equity => Balance is { } balance && UnrealizedPnl is { } pnl ? balance + pnl : null;

		private double? MarginUsed => Positions.Count == 0 ? null : Positions.Sum(p => p.UsedMargin);

		private double? MarginLevel =>
			Equity is { } equity && MarginUsed is { } used && used != 0 ? equity / used * 100 : null;

		private double? FreeMargin => Equity is { } equity && MarginUsed is { } used ? equity - used : null;

		private FinancialsTab _activeTab = FinancialsTab.Positions;
		private IReadOnlyList<CTraderDealInfo> _closedDeals = Array.Empty<CTraderDealInfo>();
		private bool _historyLoading;
		private string? _historyError;

		private void SelectTab(FinancialsTab tab)
		{
			_activeTab = tab;
			if (tab == FinancialsTab.History)
				_ = LoadHistoryAsync();
		}

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

		private static string PnlCssClass(double? value) =>
			value switch
			{
				> 0 => "fp-value-positive",
				< 0 => "fp-value-negative",
				_ => "",
			};
	}
}
