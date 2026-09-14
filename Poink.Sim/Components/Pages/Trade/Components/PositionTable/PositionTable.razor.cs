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
		public double? Balance { get; set; }

		[Parameter]
		public EventCallback<CTraderPositionInfo> OnSelect { get; set; }

		[Parameter]
		public EventCallback<CTraderPositionInfo> OnClose { get; set; }

		// Single source of truth for the table shape -- add a column here and it appears in both the
		// header and every row in the same place, so header and cells can never drift out of alignment.
		private static readonly List<PositionColumnGroup> ColumnGroups = new()
		{
			new(
				"Position",
				new List<PositionColumn>
				{
					new("Symbol", row => row.SymbolName, _ => "pt-cell-symbol"),
					new("Opened", row => row.Opened),
					new("Entry", row => row.Entry),
				}
			),
			new(
				"Risk",
				new List<PositionColumn>
				{
					new("TP", row => row.TakeProfit, Title: _ => "Take-profit price"),
					new("SL", row => row.StopLoss, Title: _ => "Stop-loss price"),
					new(
						"RR",
						row => row.RewardToRiskDisplay,
						Title: _ => "Reward-to-risk ratio between the projected profit and projected loss"
					),
					new("Volume", row => row.Volume),
					new("Side", row => row.Side, row => row.IsLong ? "pt-side-long" : "pt-side-short"),
				}
			),
			new(
				"Projections",
				new List<PositionColumn>
				{
					new(
						"Proj. Profit",
						row => row.ProjectedProfitDisplay,
						row => SignCssClass(row.ProjectedProfit),
						_ => "Money gained if the take-profit is hit, for this position's volume from the entry price"
					),
					new(
						"% to TP",
						row => row.PercentToTakeProfitDisplay,
						row => SignCssClass(row.PercentToTakeProfit),
						_ => "Progress from entry toward the take-profit, as a percent of the entry-to-TP distance"
					),
					new(
						"Proj. Loss",
						row => row.ProjectedLossDisplay,
						row => SignCssClass(-(row.ProjectedLoss ?? 0)),
						_ => "Money lost if the stop-loss is hit, for this position's volume from the entry price"
					),
					new(
						"% to SL",
						row => row.PercentToStopLossDisplay,
						row => SignCssClass(-(row.PercentToStopLoss ?? 0)),
						_ => "Progress from entry toward the stop-loss, as a percent of the entry-to-SL distance"
					),
				}
			),
			new(
				"Time",
				new List<PositionColumn>
				{
					new("Duration", row => row.DurationDisplay, Title: _ => "Time since the position was opened"),
				}
			),
			new(
				"Net",
				new List<PositionColumn>
				{
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
					new(
						"% Net",
						row => row.PercentNetDisplay,
						row => SignCssClass(row.PercentNet),
						_ => "Unrealized P/L as a percent of account balance"
					),
				}
			),
		};

		private static readonly List<PositionColumn> Columns = ColumnGroups.SelectMany(g => g.Columns).ToList();

		private static readonly Dictionary<string, string> GroupSlugByHeader = ColumnGroups
			.SelectMany(g => g.Columns.Select(c => (c.Header, Slug: Slug(g.Name))))
			.ToDictionary(x => x.Header, x => x.Slug);

		private static readonly HashSet<string> GroupStartHeaders = ColumnGroups
			.Where(g => g.Columns.Count > 0)
			.Select(g => g.Columns[0].Header)
			.ToHashSet();

		private IReadOnlyList<PositionRowViewModel> Rows => Positions.Select(ToRow).ToList();

		private Task SelectAsync(PositionRowViewModel row) => OnSelect.InvokeAsync(row.Position);

		private Task CloseAsync(PositionRowViewModel row) => OnClose.InvokeAsync(row.Position);

		private static string Slug(string name) => name.ToLowerInvariant().Replace(" ", "-").Replace("/", "-");

		private static string CombineClasses(params string?[] classes) =>
			string.Join(" ", classes.Where(c => !string.IsNullOrEmpty(c)));

		private static string GroupCellClass(PositionColumn column) =>
			CombineClasses(
				$"pt-group-{GroupSlugByHeader[column.Header]}",
				GroupStartHeaders.Contains(column.Header) ? "pt-group-start" : null
			);

		private static string HeaderCellClass(PositionColumn column) => GroupCellClass(column);

		private static string BodyCellClass(PositionColumn column, PositionRowViewModel row) =>
			CombineClasses(GroupCellClass(column), column.CssClass?.Invoke(row));

		private PositionRowViewModel ToRow(CTraderPositionInfo position)
		{
			var quote = Quotes.FirstOrDefault(q => q.SymbolId == position.SymbolId);
			SymbolMetadata.TryGetValue(position.SymbolId, out var metadata);
			var pips = PositionPnlHelper.PipDifference(position, quote, metadata);
			var pnl = PositionPnlHelper.UnrealizedPnl(position, quote, metadata);
			var projectedProfit = PositionRiskHelper.ProjectedProfit(position, metadata);
			var projectedLoss = PositionRiskHelper.ProjectedLoss(position, metadata);
			var percentToTakeProfit = PositionRiskHelper.PercentToTakeProfit(position, quote, metadata);
			var percentToStopLoss = PositionRiskHelper.PercentToStopLoss(position, quote, metadata);
			var rewardToRisk = PositionRiskHelper.RewardToRisk(position, metadata);
			var percentNet = PositionRiskHelper.PercentOfBalance(pnl, Balance);
			var duration = DateTime.UtcNow - position.OpenTimeUtc;

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
				position.TakeProfitPrice?.ToString("N5") ?? "—",
				projectedProfit,
				MoneyLabel(projectedProfit),
				projectedLoss,
				MoneyLabel(-projectedLoss),
				percentToTakeProfit,
				PercentLabel(percentToTakeProfit),
				percentToStopLoss,
				PercentLabel(percentToStopLoss),
				rewardToRisk,
				RewardToRiskLabel(rewardToRisk),
				percentNet,
				PercentLabel(percentNet),
				DurationLabel(duration)
			);
		}

		private static string PipsLabel(double? pips) =>
			pips is { } value ? $"{(value >= 0 ? "+" : "")}{value:N1}" : "—";

		private static string PnlLabel(double? pnl) => pnl is { } value ? $"{(value >= 0 ? "+" : "")}{value:N2}" : "—";

		private static string MoneyLabel(double? value) => value is { } v ? $"{(v >= 0 ? "+" : "")}{v:N2}" : "—";

		private static string PercentLabel(double? percent) =>
			percent is { } value ? $"{(value >= 0 ? "+" : "")}{value:N1}%" : "—";

		private static string RewardToRiskLabel(double? ratio) => ratio is { } value ? $"{value:N2} : 1" : "—";

		private static string DurationLabel(TimeSpan duration) =>
			duration.TotalDays >= 1
				? $"{(int)duration.TotalDays}d {duration:hh\\:mm}"
				: $"{duration:hh\\:mm\\:ss}";

		private static string SignCssClass(double? value) =>
			value switch
			{
				> 0 => "pt-value-positive",
				< 0 => "pt-value-negative",
				_ => "pt-value-neutral",
			};
	}
}

