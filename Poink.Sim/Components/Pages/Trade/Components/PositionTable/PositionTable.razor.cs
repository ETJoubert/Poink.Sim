using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Components.Pages.Trade.Helpers;
using Poink.Sim.Components.Pages.Trade.Models;
using Poink.Sim.Models;
using Poink.Sim.Services.CTrader;

namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>Read-only, tight-row table of open positions for the live Trade page -- no card view or column picker, unlike the generic DataTable.</summary>
	public partial class PositionTable : ComponentBase
	{
		[Parameter, EditorRequired]
		public IReadOnlyList<CTraderPositionInfo> Positions { get; set; } = Array.Empty<CTraderPositionInfo>();

		[Parameter]
		public IReadOnlyList<CTraderSymbolQuote> Quotes { get; set; } = Array.Empty<CTraderSymbolQuote>();

		[Parameter]
		public IReadOnlyDictionary<long, SymbolMetadataModel> SymbolMetadata { get; set; } =
			new Dictionary<long, SymbolMetadataModel>();

		[Parameter]
		public EventCallback<CTraderPositionInfo> OnSelect { get; set; }

		[Parameter]
		public EventCallback<CTraderPositionInfo> OnClose { get; set; }

		// Single source of truth for the table shape -- add a column here and it appears in both the
		// header and every row in the same place, so header and cells can never drift out of alignment.
		private static readonly List<PositionColumn> Columns = new()
		{
			new("Symbol", row => row.SymbolName, _ => "pt-cell-symbol"),
			new("Opened", row => row.Opened),
			new("Entry", row => row.Entry),
			new("TP", row => row.TakeProfit),
			new("SL", row => row.StopLoss),
			new("Volume", row => row.Volume),
			new("Side", row => row.Side, row => row.IsLong ? "pt-side-long" : "pt-side-short"),
			new(
				"Pips",
				row => row.PipsDisplay,
				row => SignCssClass(row.Pips),
				_ => "Entry-to-market distance in pips, positive when the position is in profit"
			),
			new(
				"P/L",
				row => row.PnlDisplay,
				row => SignCssClass(row.Pnl),
				_ => "Unrealized profit/loss: pips x volume"
			),
		};

		private IReadOnlyList<PositionRowViewModel> Rows => Positions.Select(ToRow).ToList();

		private Task SelectAsync(PositionRowViewModel row) => OnSelect.InvokeAsync(row.Position);

		private Task CloseAsync(PositionRowViewModel row) => OnClose.InvokeAsync(row.Position);

		private PositionRowViewModel ToRow(CTraderPositionInfo position)
		{
			var quote = Quotes.FirstOrDefault(q => q.SymbolId == position.SymbolId);
			SymbolMetadata.TryGetValue(position.SymbolId, out var metadata);
			var pips = PositionPnlHelper.PipDifference(position, quote, metadata);
			var pnl = PositionPnlHelper.UnrealizedPnl(position, quote, metadata);

			return new PositionRowViewModel(
				position,
				position.SymbolName,
				position.IsLong,
				position.IsLong ? "LONG" : "SHORT",
				position.Volume.ToString("N2"),
				position.EntryPrice.ToString("N5"),
				pips,
				PipsLabel(pips),
				pnl,
				PnlLabel(pnl),
				position.OpenTimeUtc.ToLocalTime().ToString("HH:mm:ss"),
				position.StopLossPrice?.ToString("N5") ?? "—",
				position.TakeProfitPrice?.ToString("N5") ?? "—"
			);
		}

		private static string PipsLabel(double? pips) =>
			pips is { } value ? $"{(value >= 0 ? "+" : "")}{value:N1}" : "—";

		private static string PnlLabel(double? pnl) => pnl is { } value ? $"{(value >= 0 ? "+" : "")}{value:N2}" : "—";

		private static string SignCssClass(double? value) =>
			value switch
			{
				> 0 => "pt-value-positive",
				< 0 => "pt-value-negative",
				_ => "pt-value-neutral",
			};
	}
}

