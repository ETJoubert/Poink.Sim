using System;

namespace Poink.Sim.Models
{
	public sealed record SymbolModel(
		Guid Id,
		long SymbolId,
		string SymbolName,
		bool Enabled,
		long BaseAssetId,
		long QuoteAssetId,
		long SymbolCategoryId,
		string Description,
		double SortingNumber,
		DateTime CreatedAt,
		DateTime? ChangedAt
	);

	/// <summary>Trade page symbol-search filter bucket, resolved from a cTrader symbol category's name via <see cref="Services.Symbols.SymbolCategoryClassifier"/>.</summary>
	public enum SymbolCategoryBucket
	{
		Unclassified,
		Index,
		Forex,
		IndexFuture,
		CommodityFuture,
		Commodity,
		Crypto,
	}

	public sealed record SymbolCategoryModel(
		Guid Id,
		long SymbolCategoryId,
		string Name,
		long AssetClassId,
		string AssetClassName,
		SymbolCategoryBucket Bucket,
		DateTime CreatedAt,
		DateTime? ChangedAt
	);
}
