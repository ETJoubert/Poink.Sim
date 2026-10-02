using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Poink.Sim.Helpers;
using Poink.Sim.Models;
using Poink.Sim.Services.MarketData;

namespace Poink.Sim.Services.Symbols
{
	/// <summary>Matches a tick table to its symbol by the live or historic table name, keeping the symbol list for the session.</summary>
	internal sealed class SymbolTableResolver : ISymbolTableResolver
	{
		private readonly ISymbolService _symbolService;
		private readonly ISymbolMetadataCache _metadataCache;
		private IReadOnlyList<SymbolModel>? _symbols;

		public SymbolTableResolver(ISymbolService symbolService, ISymbolMetadataCache metadataCache)
		{
			_symbolService = symbolService;
			_metadataCache = metadataCache;
		}

		public async Task<SymbolTableResolution> ResolveAsync(string table, CancellationToken cancellationToken = default)
		{
			_symbols ??= await _symbolService.ListAsync(cancellationToken);
			var symbol = _symbols.FirstOrDefault(s => IsTableOf(s, table));
			if (symbol is null)
				return new SymbolTableResolution(null, null, null);

			await _metadataCache.EnsureAsync(symbol.SymbolId);
			var metadata = _metadataCache.Get(symbol.SymbolId);
			return new SymbolTableResolution(symbol, metadata, metadata is null ? null : SymbolTradingLimits.FromMetadata(metadata));
		}

		private static bool IsTableOf(SymbolModel symbol, string table) =>
			string.Equals(TickTableNaming.ForSymbol(symbol.SymbolName, isLive: true), table, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(TickTableNaming.ForSymbol(symbol.SymbolName, isLive: false), table, StringComparison.OrdinalIgnoreCase);
	}
}
