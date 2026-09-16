using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Components.Charts;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Training
{
	public partial class FeaturesSection : ComponentBase
	{
		[Inject]
		private ITrainingService Service { get; set; } = default!;

		[Parameter]
		public BrainBuilderState State { get; set; } = default!;

		[Parameter]
		public EventCallback OnChanged { get; set; }

		[Parameter]
		public int TimeSeriesInputsFeatureCount { get; set; }

		[Parameter]
		public int BarWindowFeatureCount { get; set; }

		[Parameter]
		public int VolumeProfileFeatureCount { get; set; }

		[Parameter]
		public int TimeProfileFeatureCount { get; set; }

		[Parameter]
		public int SessionClockFeatureCount { get; set; }

		[Parameter]
		public int SpreadVolatilityFeatureCount { get; set; }

		[Parameter]
		public int TraderStateFeatureCount { get; set; }

		private Task NotifyChanged() => OnChanged.InvokeAsync();

		private bool _timeSeriesInputsHidden = true;

		private Task OnBarWindowNormalizationChanged(ChangeEventArgs e)
		{
			State.BarWindowNormalization = System.Enum.Parse<NormalizationKind>(
				e.Value?.ToString() ?? nameof(NormalizationKind.Normalized)
			);
			return NotifyChanged();
		}

		private Task OnVolumeProfileNormalizationChanged(ChangeEventArgs e)
		{
			State.VolumeProfileNormalization = System.Enum.Parse<NormalizationKind>(
				e.Value?.ToString() ?? nameof(NormalizationKind.Normalized)
			);
			return NotifyChanged();
		}

		private Task OnTimeProfileNormalizationChanged(ChangeEventArgs e)
		{
			State.TimeProfileNormalization = System.Enum.Parse<NormalizationKind>(
				e.Value?.ToString() ?? nameof(NormalizationKind.Normalized)
			);
			return NotifyChanged();
		}

		private string VolumeProfileBucketsTooltip =>
			(State.VolumeProfileNormalization == NormalizationKind.Raw, State.VolumeProfileSplitUpDown) switch
			{
				(true, true) =>
					"How many histogram buckets Volume Profile rebins its merged bars into -- each bucket feeds price, up-volume, down-volume, and their diff under Raw+Split, so its input count is this times 4 plus 3 summary values",
				(true, false) =>
					"How many histogram buckets Volume Profile rebins its merged bars into -- each bucket feeds price and volume under Raw, so its input count is this times 2 plus 3 summary values",
				(false, true) =>
					"How many histogram buckets Volume Profile rebins its merged bars into -- each bucket feeds up-volume, down-volume, and their diff under Split, so its input count is this times 3 plus 3 summary values",
				(false, false) =>
					"How many histogram buckets Volume Profile rebins its merged bars into -- its input count is this plus 3 summary values",
			};

		private string TimeProfileBucketsTooltip =>
			(State.TimeProfileNormalization == NormalizationKind.Raw, State.TimeProfileSplitUpDown) switch
			{
				(true, true) =>
					"How many histogram buckets Time Profile rebins its merged bars into -- each bucket feeds price, up-seconds, down-seconds, and their diff under Raw+Split, so its input count is this times 4 plus 3 summary values",
				(true, false) =>
					"How many histogram buckets Time Profile rebins its merged bars into -- each bucket feeds price and seconds under Raw, so its input count is this times 2 plus 3 summary values",
				(false, true) =>
					"How many histogram buckets Time Profile rebins its merged bars into -- each bucket feeds up-seconds, down-seconds, and their diff under Split, so its input count is this times 3 plus 3 summary values",
				(false, false) =>
					"How many histogram buckets Time Profile rebins its merged bars into -- its input count is this plus 3 summary values",
			};

		private sealed record TraderStateFieldRow(string Label, string Tooltip, System.Func<bool> Get, System.Action<bool> Set);

		// Drives the Trader State checkbox row from one list instead of 12 near-duplicate
		// blocks; follows the same fresh-collection-per-access convention as XGridSignalRows.
		private IReadOnlyList<TraderStateFieldRow> TraderStateFieldRows =>
			new List<TraderStateFieldRow>
			{
				new(
					"Position",
					"Direction of the open position: 1 long, -1 short, 0 flat",
					() => State.TraderStateIncludePosition,
					v => State.TraderStateIncludePosition = v
				),
				new(
					"Position Price",
					"Entry price of the open position, 0 if flat",
					() => State.TraderStateIncludePositionPrice,
					v => State.TraderStateIncludePositionPrice = v
				),
				new(
					"Position Ticks",
					"Ticks the open position has been held, 0 if flat",
					() => State.TraderStateIncludePositionTicks,
					v => State.TraderStateIncludePositionTicks = v
				),
				new(
					"Position Distance",
					"Cumulative price distance traveled since the position opened, 0 if flat",
					() => State.TraderStateIncludePositionDistance,
					v => State.TraderStateIncludePositionDistance = v
				),
				new(
					"Current Price",
					"Current market price",
					() => State.TraderStateIncludeCurrentPrice,
					v => State.TraderStateIncludeCurrentPrice = v
				),
				new("Cash", "Available cash balance", () => State.TraderStateIncludeCash, v => State.TraderStateIncludeCash = v),
				new(
					"Realized",
					"Realized profit/loss booked so far",
					() => State.TraderStateIncludeRealized,
					v => State.TraderStateIncludeRealized = v
				),
				new(
					"Unrealized",
					"Unrealized profit/loss on the open position",
					() => State.TraderStateIncludeUnrealized,
					v => State.TraderStateIncludeUnrealized = v
				),
				new(
					"Equity",
					"Cash plus unrealized profit/loss",
					() => State.TraderStateIncludeEquity,
					v => State.TraderStateIncludeEquity = v
				),
				new(
					"Margin",
					"Margin currently used by the open position",
					() => State.TraderStateIncludeMargin,
					v => State.TraderStateIncludeMargin = v
				),
				new(
					"Margin Level",
					"Equity as a percent of margin used",
					() => State.TraderStateIncludeMarginLevel,
					v => State.TraderStateIncludeMarginLevel = v
				),
				new(
					"Position Count",
					"1 if a position is open, 0 if flat",
					() => State.TraderStateIncludePositionCount,
					v => State.TraderStateIncludePositionCount = v
				),
			};

		private Task OnTraderStateFieldRowChanged(TraderStateFieldRow row, ChangeEventArgs e)
		{
			row.Set(e.Value is bool b && b);
			return NotifyChanged();
		}

		private const int TotalTraderStateFieldCount = 12;

		private int SelectedTraderStateFieldCount => TraderStateFieldRows.Count(r => r.Get());

		private bool AllTraderStateBaseFieldsSelected
		{
			get => SelectedTraderStateFieldCount == TotalTraderStateFieldCount;
			set
			{
				foreach (var row in TraderStateFieldRows)
					row.Set(value);
			}
		}

		private bool AllTraderStateListsSelected
		{
			get => State.IncludeTraderStateOutcomes && State.IncludeTraderStatePositionLevel && State.IncludeTraderStateDirection;
			set
			{
				State.IncludeTraderStateOutcomes = value;
				State.IncludeTraderStatePositionLevel = value;
				State.IncludeTraderStateDirection = value;
			}
		}

		private int TotalXGridSignalCount => XGridSignalCatalog.All.Count;

		private int SelectedXGridSignalCount => State.XGridEnabledSignals.Count;

		private bool AnyXGridSignalSelected => SelectedXGridSignalCount > 0;

		private bool AllXGridSignalsSelected
		{
			get => SelectedXGridSignalCount == TotalXGridSignalCount;
			set =>
				State.XGridEnabledSignals = value
					? new HashSet<string>(XGridSignalCatalog.All.Select(d => d.Key))
					: new HashSet<string>();
		}
	}
}
