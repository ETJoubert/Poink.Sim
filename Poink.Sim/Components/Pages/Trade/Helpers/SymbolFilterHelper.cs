using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Models;
using Poink.Sim.Services.CTrader;

namespace Poink.Sim.Components.Pages.Trade.Helpers
{
	/// <summary>Name/category/enabled/FX-currency filtering for the Trade page's symbol search, shared by SymbolWatchlistPanel's list and match-count display.</summary>
	public static class SymbolFilterHelper
	{
		private const int MaxResults = 100;

		public static IReadOnlyList<CTraderSymbolInfo> Filter(
			IReadOnlyList<CTraderSymbolInfo> symbols,
			string nameFilter,
			IReadOnlySet<SymbolCategoryBucket> selectedCategoryBuckets,
			bool showDisabledSymbols,
			IReadOnlySet<string> selectedFxCurrencies,
			IReadOnlyDictionary<long, SymbolCategoryModel> categories
		) =>
			Matching(symbols, nameFilter, selectedCategoryBuckets, showDisabledSymbols, selectedFxCurrencies, categories)
				.Take(MaxResults)
				.ToList();

		public static int TotalMatchCount(
			IReadOnlyList<CTraderSymbolInfo> symbols,
			string nameFilter,
			IReadOnlySet<SymbolCategoryBucket> selectedCategoryBuckets,
			bool showDisabledSymbols,
			IReadOnlySet<string> selectedFxCurrencies,
			IReadOnlyDictionary<long, SymbolCategoryModel> categories
		) => Matching(symbols, nameFilter, selectedCategoryBuckets, showDisabledSymbols, selectedFxCurrencies, categories).Count();

		private static IEnumerable<CTraderSymbolInfo> Matching(
			IReadOnlyList<CTraderSymbolInfo> symbols,
			string nameFilter,
			IReadOnlySet<SymbolCategoryBucket> selectedCategoryBuckets,
			bool showDisabledSymbols,
			IReadOnlySet<string> selectedFxCurrencies,
			IReadOnlyDictionary<long, SymbolCategoryModel> categories
		)
		{
			var matches = string.IsNullOrWhiteSpace(nameFilter)
				? symbols
				: symbols.Where(s => s.SymbolName.Contains(nameFilter, StringComparison.OrdinalIgnoreCase));

			return matches
				.Where(s => MatchesCategoryFilter(s, selectedCategoryBuckets, categories))
				.Where(s => MatchesEnabledFilter(s, showDisabledSymbols))
				.Where(s => MatchesFxCurrencyFilter(s, selectedFxCurrencies, categories));
		}

		// No toggle selected shows everything, matching the name-only-filter default.
		public static bool MatchesCategoryFilter(
			CTraderSymbolInfo symbol,
			IReadOnlySet<SymbolCategoryBucket> selectedCategoryBuckets,
			IReadOnlyDictionary<long, SymbolCategoryModel> categories
		) =>
			selectedCategoryBuckets.Count == 0
			|| (categories.TryGetValue(symbol.SymbolCategoryId, out var category) && selectedCategoryBuckets.Contains(category.Bucket));

		// Disabled symbols are hidden by default -- the toggle opts back into seeing them.
		public static bool MatchesEnabledFilter(CTraderSymbolInfo symbol, bool showDisabledSymbols) =>
			showDisabledSymbols || symbol.Enabled;

		// Currency quick-filters only narrow the Forex bucket, leaving other selected categories untouched.
		public static bool MatchesFxCurrencyFilter(
			CTraderSymbolInfo symbol,
			IReadOnlySet<string> selectedFxCurrencies,
			IReadOnlyDictionary<long, SymbolCategoryModel> categories
		)
		{
			if (selectedFxCurrencies.Count == 0)
				return true;
			if (!categories.TryGetValue(symbol.SymbolCategoryId, out var category) || category.Bucket != SymbolCategoryBucket.Forex)
				return true;

			return selectedFxCurrencies.Any(currency => symbol.SymbolName.Contains(currency, StringComparison.OrdinalIgnoreCase));
		}
	}
}
