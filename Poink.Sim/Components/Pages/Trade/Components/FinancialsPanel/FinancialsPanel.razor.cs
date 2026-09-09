using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Components.Pages.Trade.Helpers;
using Poink.Sim.Models;
using Poink.Sim.Services.CTrader;

namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>Open positions plus account financials for the live Trade page -- owns the derived figures (equity, margin, P/L); CTraderSessionService only supplies Positions and Balance.</summary>
	public partial class FinancialsPanel : ComponentBase
	{
		[Parameter, EditorRequired]
		public IReadOnlyList<CTraderPositionInfo> Positions { get; set; } = System.Array.Empty<CTraderPositionInfo>();

		[Parameter]
		public double? Balance { get; set; }

		[Parameter]
		public IReadOnlyList<CTraderSymbolQuote> Watchlist { get; set; } = System.Array.Empty<CTraderSymbolQuote>();

		[Parameter]
		public IReadOnlyDictionary<long, SymbolMetadataModel> SymbolMetadata { get; set; } =
			new Dictionary<long, SymbolMetadataModel>();

		[Parameter]
		public EventCallback<CTraderPositionInfo> OnSelectPosition { get; set; }

		private double? RealizedPnl => null;

		// Excludes any position whose symbol isn't watched yet (no live quote/metadata to price it against),
		// so this is a partial sum until every open position's symbol has been watched at least once.
		private double? UnrealizedPnl
		{
			get
			{
				var known = Positions
					.Select(p => PositionPnlHelper.UnrealizedPnl(p, QuoteFor(p.SymbolId), SymbolMetadata.GetValueOrDefault(p.SymbolId)))
					.Where(pnl => pnl is not null)
					.Select(pnl => pnl!.Value)
					.ToList();
				return known.Count == 0 ? null : known.Sum();
			}
		}

		private CTraderSymbolQuote? QuoteFor(long symbolId) => Watchlist.FirstOrDefault(q => q.SymbolId == symbolId);

		private double? Equity => Balance is { } balance && UnrealizedPnl is { } pnl ? balance + pnl : null;

		private double? MarginUsed => Positions.Count == 0 ? null : Positions.Sum(p => p.UsedMargin);

		private double? MarginLevel =>
			Equity is { } equity && MarginUsed is { } used && used != 0 ? equity / used * 100 : null;

		private double? FreeMargin => Equity is { } equity && MarginUsed is { } used ? equity - used : null;

		private static string Money(double? value) => value?.ToString("N2") ?? "—";
	}
}
