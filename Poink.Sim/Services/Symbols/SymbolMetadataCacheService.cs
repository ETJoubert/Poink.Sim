using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Poink.Sim.Models;
using Poink.Sim.Services.CTrader;

namespace Poink.Sim.Services.Symbols
{
	/// <inheritdoc cref="ISymbolMetadataCache"/>
	public sealed class SymbolMetadataCacheService : ISymbolMetadataCache
	{
		private readonly ISymbolService _symbolService;
		private readonly Dictionary<long, SymbolMetadataModel> _metadata = new();

		// ISymbolService's DbContext is scoped and single-threaded, but multiple callers (watchlist
		// watch, position sync, order entry) can all request metadata for the same symbol at once.
		private readonly SemaphoreSlim _gate = new(1, 1);

		public SymbolMetadataCacheService(ISymbolService symbolService)
		{
			_symbolService = symbolService;
		}

		public SymbolMetadataModel? Get(long symbolId) => _metadata.GetValueOrDefault(symbolId);

		public IReadOnlyDictionary<long, SymbolMetadataModel> Snapshot => _metadata;

		public async Task EnsureAsync(long symbolId)
		{
			if (_metadata.ContainsKey(symbolId))
				return;

			await _gate.WaitAsync();
			try
			{
				if (_metadata.ContainsKey(symbolId))
					return;

				var metadata = await _symbolService.GetMetadataAsync(symbolId) ?? await _symbolService.RefreshMetadataFromCTraderAsync(symbolId);
				if (metadata is not null)
					_metadata[symbolId] = metadata;
			}
			finally
			{
				_gate.Release();
			}
		}

		public async Task EnsureForPositionsAsync(IEnumerable<CTraderPositionInfo> positions)
		{
			foreach (var symbolId in positions.Select(p => p.SymbolId).Distinct())
				await EnsureAsync(symbolId);
		}
	}
}
