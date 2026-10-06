using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Pages.Trade.Models
{
	/// <summary>The Trade symbol search's active filters; empty sets and false flags each mean "don't narrow".</summary>
	public sealed record SymbolFilterCriteria(
		string NameFilter,
		IReadOnlySet<SymbolCategoryBucket> CategoryBuckets,
		bool ShowDisabled,
		IReadOnlySet<string> FxCurrencies,
		bool OnlyWithData
	);
}
