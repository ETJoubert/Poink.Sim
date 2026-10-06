using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Components.Pages.Trade.Models;
using Poink.Sim.Models;
using Poink.Sim.Services.CTrader;

namespace Poink.Sim.Components.Pages.Trade.Helpers
{
	/// <summary>Name/category/enabled/FX-currency/stored-data filtering for the Trade page's symbol search, shared by SymbolWatchlistPanel's list and match-count display.</summary>
	public static class SymbolFilterHelper
	{
		private const int MaxResults = 100;

		public static IReadOnlyList<CTraderSymbolInfo> Filter(
			IReadOnlyList<CTraderSymbolInfo> symbols,
			SymbolFilterCriteria criteria,
			IReadOnlyDictionary<long, SymbolCategoryModel> categories,
			IReadOnlyDictionary<string, SymbolTickCoverage> coverage
		) => Matching(symbols, criteria, categories, coverage).Take(MaxResults).ToList();

		public static int TotalMatchCount(
			IReadOnlyList<CTraderSymbolInfo> symbols,
			SymbolFilterCriteria criteria,
			IReadOnlyDictionary<long, SymbolCategoryModel> categories,
			IReadOnlyDictionary<string, SymbolTickCoverage> coverage
		) => Matching(symbols, criteria, categories, coverage).Count();

		private static IEnumerable<CTraderSymbolInfo> Matching(
			IReadOnlyList<CTraderSymbolInfo> symbols,
			SymbolFilterCriteria criteria,
			IReadOnlyDictionary<long, SymbolCategoryModel> categories,
			IReadOnlyDictionary<string, SymbolTickCoverage> coverage
		)
		{
			var matches = string.IsNullOrWhiteSpace(criteria.NameFilter)
				? symbols
				: symbols.Where(s => s.SymbolName.Contains(criteria.NameFilter, StringComparison.OrdinalIgnoreCase));

			return matches
				.Where(s => MatchesCategoryFilter(s, criteria.CategoryBuckets, categories))
				.Where(s => MatchesEnabledFilter(s, criteria.ShowDisabled))
				.Where(s => MatchesFxCurrencyFilter(s, criteria.FxCurrencies, categories))
				.Where(s => MatchesDataFilter(s, criteria.OnlyWithData, coverage));
		}

		public static bool MatchesDataFilter(
			CTraderSymbolInfo symbol,
			bool onlyWithData,
			IReadOnlyDictionary<string, SymbolTickCoverage> coverage
		) => !onlyWithData || SymbolCoverageHelper.For(symbol.SymbolName, coverage).HasData;

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
