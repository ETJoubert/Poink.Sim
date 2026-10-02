using System.Threading;
using System.Threading.Tasks;
using Poink.Sim.Helpers;
using Poink.Sim.Models;

namespace Poink.Sim.Services.Symbols
{
	/// <summary>A tick table's symbol, its trading metadata, and the trading limits (pip size included) derived from it; any may be unknown.</summary>
	public sealed record SymbolTableResolution(SymbolModel? Symbol, SymbolMetadataModel? Metadata, SymbolTradingLimits? Limits);

	/// <summary>Resolves a tick table to the symbol it holds and that symbol's trading metadata.</summary>
	public interface ISymbolTableResolver
	{
		Task<SymbolTableResolution> ResolveAsync(string table, CancellationToken cancellationToken = default);
	}
}
