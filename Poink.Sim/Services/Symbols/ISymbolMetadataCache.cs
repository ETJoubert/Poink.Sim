using System.Collections.Generic;
using System.Threading.Tasks;
using Poink.Sim.Models;
using Poink.Sim.Services.CTrader;

namespace Poink.Sim.Services.Symbols
{
	/// <summary>Per-session cache of symbol metadata (pip size, digits, etc.) used for pip/P&amp;L math across the Trade page's components -- checks the DB first, falling back to cTrader (and persisting the result) only the first time a symbol is needed.</summary>
	public interface ISymbolMetadataCache
	{
		SymbolMetadataModel? Get(long symbolId);

		IReadOnlyDictionary<long, SymbolMetadataModel> Snapshot { get; }

		Task EnsureAsync(long symbolId);

		Task EnsureForPositionsAsync(IEnumerable<CTraderPositionInfo> positions);
	}
}
