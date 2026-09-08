using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Poink.Sim.Models;

namespace Poink.Sim.Services.Symbols
{
	public interface ISymbolService
	{
		Task<IReadOnlyList<SymbolModel>> ListAsync(CancellationToken cancellationToken = default);

		/// <summary>Fetches the latest symbol list from the connected cTrader session and upserts it into the Symbol table. Requires a connected session.</summary>
		Task<IReadOnlyList<SymbolModel>> RefreshFromCTraderAsync(CancellationToken cancellationToken = default);
	}
}
