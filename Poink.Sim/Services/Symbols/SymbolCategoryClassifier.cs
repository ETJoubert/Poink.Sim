using System;
using Poink.Sim.Models;

namespace Poink.Sim.Services.Symbols
{
	/// <summary>
	/// Maps a cTrader symbol category's name (plus its asset class's name) to one of the Trade page's
	/// filter buckets. Broker category names aren't standardized, so these are a first-draft guess --
	/// check them against SymbolService.RefreshCategoriesFromCTraderAsync's log output and adjust here.
	/// </summary>
	public static class SymbolCategoryClassifier
	{
		// Order matters: compound rules (index/commodity + future) must be checked before the plain
		// index/commodity rules they'd otherwise be swallowed by.
		private static readonly (Func<string, bool> IsMatch, SymbolCategoryBucket Bucket)[] Rules =
		{
			(t => Has(t, "crypto"), SymbolCategoryBucket.Crypto),
			(t => IsIndex(t) && Has(t, "future"), SymbolCategoryBucket.IndexFuture),
			(t => IsCommodity(t) && Has(t, "future"), SymbolCategoryBucket.CommodityFuture),
			(IsIndex, SymbolCategoryBucket.Index),
			(IsCommodity, SymbolCategoryBucket.Commodity),
			(t => Has(t, "forex") || Has(t, "currenc") || Has(t, "major") || Has(t, "minor") || Has(t, "exotic"), SymbolCategoryBucket.Forex),
		};

		public static SymbolCategoryBucket Classify(string categoryName, string assetClassName)
		{
			var text = $"{categoryName} {assetClassName}".ToLowerInvariant();

			foreach (var (isMatch, bucket) in Rules)
			{
				if (isMatch(text))
					return bucket;
			}

			return SymbolCategoryBucket.Unclassified;
		}

		private static bool IsIndex(string text) => Has(text, "index") || Has(text, "indices");

		private static bool IsCommodity(string text) => Has(text, "commodit") || Has(text, "metal") || Has(text, "energy");

		private static bool Has(string text, string keyword) => text.Contains(keyword, StringComparison.Ordinal);
	}
}
