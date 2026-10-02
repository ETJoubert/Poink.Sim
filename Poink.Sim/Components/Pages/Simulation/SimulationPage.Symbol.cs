using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Poink.Sim.Helpers;
using Poink.Sim.Models;
using Poink.Sim.Services.MarketData;
using Poink.Sim.Services.Symbols;

namespace Poink.Sim.Components.Pages
{
	/// <summary>
	/// Resolves the selected data table to its symbol and trading metadata, so the setup tab can show the
	/// symbol's trading parameters and the pip size comes from the symbol instead of being typed in.
	/// </summary>
	public partial class SimulationPage
	{
		[Inject]
		private ISymbolService SymbolService { get; set; } = default!;

		[Inject]
		private ISymbolMetadataCache SymbolMetadataCache { get; set; } = default!;

		private IReadOnlyList<SymbolModel>? _symbols;
		private SymbolModel? _symbol;
		private SymbolMetadataModel? _symbolMetadata;
		private SymbolTradingLimits? _symbolLimits;
		private bool _loadingSymbolMetadata;
		private string? _metadataTable;

		private async Task EnsureSymbolMetadataAsync()
		{
			if (string.IsNullOrWhiteSpace(_table) || (_metadataTable == _table && _symbolMetadata is not null))
				return;

			var table = _table;
			_metadataTable = table;
			_loadingSymbolMetadata = true;
			_symbol = null;
			_symbolMetadata = null;
			_symbolLimits = null;
			try
			{
				_symbols ??= await SymbolService.ListAsync();
				_symbol = _symbols.FirstOrDefault(s => IsTableOf(s, table));
				if (_symbol is not null)
				{
					await SymbolMetadataCache.EnsureAsync(_symbol.SymbolId);
					_symbolMetadata = SymbolMetadataCache.Get(_symbol.SymbolId);
				}
				if (_symbolMetadata is { } metadata)
				{
					_symbolLimits = SymbolTradingLimits.FromMetadata(metadata);
					_pipSize = _symbolLimits.PipSize;
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, "Could not resolve symbol metadata for table {Table}", table);
			}
			finally
			{
				_loadingSymbolMetadata = false;
			}
		}

		private async Task RefreshSymbolMetadataIfTableChangedAsync()
		{
			if (_table == _metadataTable)
				return;
			await EnsureSymbolMetadataAsync();
			StateHasChanged();
		}

		private static bool IsTableOf(SymbolModel symbol, string table) =>
			string.Equals(TickTableNaming.ForSymbol(symbol.SymbolName, isLive: true), table, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(TickTableNaming.ForSymbol(symbol.SymbolName, isLive: false), table, StringComparison.OrdinalIgnoreCase);
	}
}
