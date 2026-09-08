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
}
