using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Components.Pages.Trade.Helpers;
using Poink.Sim.Models;
using Poink.Sim.Services.CTrader;
using Poink.Sim.Services.Symbols;

namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>Left sidebar of the live Trade page -- symbol search/category filters and the streaming watchlist. Owns its own filter/tab UI state; watch/unwatch and symbol category loading go through the injected session and symbol services directly.</summary>
	public partial class SymbolWatchlistPanel : ComponentBase, IDisposable
	{
		[Inject]
		private ICTraderSessionService Session { get; set; } = null!;

		[Inject]
		private ISymbolService SymbolService { get; set; } = null!;

		[Inject]
		private ISymbolMetadataCache MetadataCache { get; set; } = null!;

		[Parameter]
		public long? ActiveSymbolId { get; set; }

		[Parameter]
		public EventCallback<(long SymbolId, string SymbolName)> OnSymbolSelected { get; set; }

		[Parameter]
		public EventCallback<long> OnSymbolUnwatched { get; set; }

		[Parameter]
		public bool Collapsed { get; set; }

		[Parameter]
		public EventCallback<bool> CollapsedChanged { get; set; }

		private enum SidepanelTab
		{
			Symbols,
			Watchlist,
		}

		private string _symbolFilter = string.Empty;
		private SidepanelTab _sidepanelTab = SidepanelTab.Watchlist;

		private readonly Dictionary<long, SymbolCategoryModel> _symbolCategories = new();
		private bool _symbolCategoriesLoaded;
		private readonly SemaphoreSlim _categoriesGate = new(1, 1);
		private readonly HashSet<SymbolCategoryBucket> _selectedCategoryBuckets = new();
		private bool _showDisabledSymbols;
		private readonly HashSet<string> _selectedFxCurrencies = new();

		private static readonly (SymbolCategoryBucket Bucket, string Label, string FullName)[] CategoryToggleDefs =
		{
			(SymbolCategoryBucket.Index, "IX", "Indexes"),
			(SymbolCategoryBucket.Forex, "FX", "Forex"),
			(SymbolCategoryBucket.IndexFuture, "IXF", "Index Futures"),
			(SymbolCategoryBucket.CommodityFuture, "ComF", "Commodity Futures"),
			(SymbolCategoryBucket.Commodity, "Com", "Commodities"),
			(SymbolCategoryBucket.Crypto, "CX", "Cryptos"),
		};

		private static readonly string[] FxQuickCurrencies = { "USD", "GBP", "EUR", "JPY", "ZAR" };

		private bool IsConnected => Session.State == CTraderConnectionState.Connected;

		private IReadOnlyList<CTraderSymbolInfo> FilteredSymbols =>
			SymbolFilterHelper.Filter(
				Session.Symbols,
				_symbolFilter,
				_selectedCategoryBuckets,
				_showDisabledSymbols,
				_selectedFxCurrencies,
				_symbolCategories
			);

		private int TotalSymbolMatchCount =>
			SymbolFilterHelper.TotalMatchCount(
				Session.Symbols,
				_symbolFilter,
				_selectedCategoryBuckets,
				_showDisabledSymbols,
				_selectedFxCurrencies,
				_symbolCategories
			);

		protected override void OnInitialized()
		{
			Session.Changed += HandleSessionChanged;
			if (IsConnected)
				_ = EnsureSymbolCategoriesAsync();
		}

		private void HandleSessionChanged() =>
			InvokeAsync(async () =>
			{
				if (IsConnected)
					await EnsureSymbolCategoriesAsync();
				StateHasChanged();
			});

		// Categories are a tiny, broker-defined reference set, so this fetches once per connect
		// rather than requiring a manual DataPage refresh first.
		private async Task EnsureSymbolCategoriesAsync()
		{
			if (_symbolCategoriesLoaded)
				return;

			await _categoriesGate.WaitAsync();
			try
			{
				if (_symbolCategoriesLoaded)
					return;

				var categories = await SymbolService.ListCategoriesAsync();
				if (categories.Count == 0)
					categories = await SymbolService.RefreshCategoriesFromCTraderAsync();

				foreach (var category in categories)
					_symbolCategories[category.SymbolCategoryId] = category;
				_symbolCategoriesLoaded = true;
			}
			finally
			{
				_categoriesGate.Release();
			}
		}

		private Task ToggleCollapsed() => CollapsedChanged.InvokeAsync(!Collapsed);

		private void SelectTab(SidepanelTab tab) => _sidepanelTab = tab;

		// No toggle selected shows everything, matching today's name-only-filter behavior.
		private void ToggleCategoryBucket(SymbolCategoryBucket bucket)
		{
			if (_selectedCategoryBuckets.Remove(bucket))
			{
				if (bucket == SymbolCategoryBucket.Forex)
					_selectedFxCurrencies.Clear();
				return;
			}

			_selectedCategoryBuckets.Add(bucket);
		}

		private void ToggleShowDisabledSymbols() => _showDisabledSymbols = !_showDisabledSymbols;

		private void ToggleFxCurrency(string currency)
		{
			if (!_selectedFxCurrencies.Remove(currency))
				_selectedFxCurrencies.Add(currency);
		}

		private async Task WatchAsync(long symbolId)
		{
			await Session.WatchSymbolAsync(symbolId);
			await MetadataCache.EnsureAsync(symbolId);
		}

		private async Task UnwatchAsync(long symbolId)
		{
			await Session.UnwatchSymbolAsync(symbolId);
			await OnSymbolUnwatched.InvokeAsync(symbolId);
		}

		private Task SelectAsync(long symbolId, string symbolName) => OnSymbolSelected.InvokeAsync((symbolId, symbolName));

		// Quick-glance info from what's already loaded (no per-symbol metadata fetch).
		private string SymbolInfoTooltip(CTraderSymbolInfo symbol)
		{
			var categoryName = _symbolCategories.TryGetValue(symbol.SymbolCategoryId, out var category) ? category.Name : "Unknown category";
			var lines = new List<string> { symbol.SymbolName };
			if (!string.IsNullOrWhiteSpace(symbol.Description))
				lines.Add(symbol.Description);
			lines.Add(categoryName);
			lines.Add(symbol.Enabled ? "Enabled" : "Disabled");

			return string.Join("\n", lines);
		}

		private static string QuoteRowTitle(CTraderSymbolQuote quote, bool selected)
		{
			var state = selected ? "Viewing this symbol's chart" : "Select to view this symbol's chart";
			var updated = quote.UpdatedAt?.ToString("HH:mm:ss") ?? "waiting...";
			return $"{state} -- last update: {updated}";
		}

		private static string Price(double? value) => value?.ToString("N5") ?? "—";

		public void Dispose()
		{
			Session.Changed -= HandleSessionChanged;
			_categoriesGate.Dispose();
		}
	}
}
