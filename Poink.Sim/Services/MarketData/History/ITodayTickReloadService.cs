using System.Threading;
using System.Threading.Tasks;

namespace Poink.Sim.Services.MarketData.History
{
	/// <summary>The outcome of a today reload: the table written and the rows replaced, or why nothing was written.</summary>
	internal sealed record TodayTickReloadResult(string Table, long Deleted, long Inserted, string? Error)
	{
		public bool Succeeded => Error is null;
	}

	/// <summary>
	/// Overrides today's stored ticks for a symbol with a fresh cTrader fetch, e.g. after recorded ticks drifted or a
	/// switch left the wrong ticks behind. Writes the connected environment's table, the same one today's ticks live in.
	/// </summary>
	internal interface ITodayTickReloadService
	{
		Task<TodayTickReloadResult> ReloadAsync(long symbolId, string symbolName, CancellationToken cancellationToken = default);

		/// <summary>The table a reload for this symbol would replace today's ticks in.</summary>
		string TableFor(string symbolName);
	}
}
