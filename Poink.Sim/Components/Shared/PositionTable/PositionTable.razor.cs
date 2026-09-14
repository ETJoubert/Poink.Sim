using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Shared.PositionTable
{
	/// <summary>
	/// Read-only, tight-row table of open positions -- no card view or column picker, unlike the generic
	/// DataTable. Shared by the live Trade page and the Simulation page: it renders whatever rows the host
	/// hands it and round-trips <typeparamref name="TPosition"/> back through OnSelect/OnClose, with no
	/// backend of its own.
	/// </summary>
	public partial class PositionTable<TPosition> : ComponentBase
	{
		[Parameter, EditorRequired]
		public IReadOnlyList<PositionRowViewModel<TPosition>> Rows { get; set; } = System.Array.Empty<PositionRowViewModel<TPosition>>();

		[Parameter]
		public EventCallback<TPosition> OnSelect { get; set; }

		[Parameter]
		public EventCallback<TPosition> OnClose { get; set; }

		// Single source of truth for the table shape -- add a column here and it appears in both the
		// header and every row in the same place, so header and cells can never drift out of alignment.
		private static readonly List<PositionColumnGroup<TPosition>> ColumnGroups = new()
		{
			new(
				"Position",
				new List<PositionColumn<TPosition>>
				{
					new("Symbol", row => row.SymbolName, _ => "pt-cell-symbol"),
					new("Opened", row => row.Opened),
					new("Entry", row => row.Entry),
				}
			),
			new(
				"Risk",
				new List<PositionColumn<TPosition>>
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
				new List<PositionColumn<TPosition>>
				{
					new(
						"Proj. Profit",
						row => row.ProjectedProfitDisplay,
						row => PositionRowFormat.SignCssClass(row.ProjectedProfit),
						_ => "Money gained if the take-profit is hit, for this position's volume from the entry price"
					),
					new(
						"% to TP",
						row => row.PercentToTakeProfitDisplay,
						row => PositionRowFormat.SignCssClass(row.PercentToTakeProfit),
						_ => "Progress from entry toward the take-profit, as a percent of the entry-to-TP distance"
					),
					new(
						"Proj. Loss",
						row => row.ProjectedLossDisplay,
						row => PositionRowFormat.SignCssClass(-(row.ProjectedLoss ?? 0)),
						_ => "Money lost if the stop-loss is hit, for this position's volume from the entry price"
					),
					new(
						"% to SL",
						row => row.PercentToStopLossDisplay,
						row => PositionRowFormat.SignCssClass(-(row.PercentToStopLoss ?? 0)),
						_ => "Progress from entry toward the stop-loss, as a percent of the entry-to-SL distance"
					),
				}
			),
			new(
				"Time",
				new List<PositionColumn<TPosition>>
				{
					new("Duration", row => row.DurationDisplay, Title: _ => "Time since the position was opened"),
				}
			),
			new(
				"Net",
				new List<PositionColumn<TPosition>>
				{
					new(
						"Pips",
						row => row.PipsDisplay,
						row => PositionRowFormat.SignCssClass(row.Pips),
						_ => "Entry-to-market distance in pips, positive when the position is in profit"
					),
					new(
						"P/L",
						row => row.PnlDisplay,
						row => PositionRowFormat.SignCssClass(row.Pnl),
						_ => "Unrealized profit/loss: pips x volume"
					),
					new(
						"% Net",
						row => row.PercentNetDisplay,
						row => PositionRowFormat.SignCssClass(row.PercentNet),
						_ => "Unrealized P/L as a percent of account balance"
					),
				}
			),
		};

		private static readonly List<PositionColumn<TPosition>> Columns = ColumnGroups.SelectMany(g => g.Columns).ToList();

		private static readonly Dictionary<string, string> GroupSlugByHeader = ColumnGroups
			.SelectMany(g => g.Columns.Select(c => (c.Header, Slug: Slug(g.Name))))
			.ToDictionary(x => x.Header, x => x.Slug);

		private static readonly HashSet<string> GroupStartHeaders = ColumnGroups
			.Where(g => g.Columns.Count > 0)
			.Select(g => g.Columns[0].Header)
			.ToHashSet();

		private Task SelectAsync(PositionRowViewModel<TPosition> row) => OnSelect.InvokeAsync(row.Position);

		private Task CloseAsync(PositionRowViewModel<TPosition> row) => OnClose.InvokeAsync(row.Position);

		private static string Slug(string name) => name.ToLowerInvariant().Replace(" ", "-").Replace("/", "-");

		private static string CombineClasses(params string?[] classes) =>
			string.Join(" ", classes.Where(c => !string.IsNullOrEmpty(c)));

		private static string GroupCellClass(PositionColumn<TPosition> column) =>
			CombineClasses(
				$"pt-group-{GroupSlugByHeader[column.Header]}",
				GroupStartHeaders.Contains(column.Header) ? "pt-group-start" : null
			);

		private static string HeaderCellClass(PositionColumn<TPosition> column) => GroupCellClass(column);

		private static string BodyCellClass(PositionColumn<TPosition> column, PositionRowViewModel<TPosition> row) =>
			CombineClasses(GroupCellClass(column), column.CssClass?.Invoke(row));
	}
}
